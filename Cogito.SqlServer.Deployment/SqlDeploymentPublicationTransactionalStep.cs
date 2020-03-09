using System;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

using Cogito.Collections;
using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationTransactionalStep : SqlDeploymentPublicationStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="publicationName"></param>
        /// <param name="databaseName"></param>
        public SqlDeploymentPublicationTransactionalStep(string instanceName, string publicationName, string databaseName) :
            base(instanceName, publicationName, databaseName)
        {

        }

        public override Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using (var distributor = new SqlConnection(DistributorName))
            using (var publisher = await OpenConnectionAsync(cancellationToken))
            {
                await distributor.OpenAsync(cancellationToken);

                // configure the publisher to accept connections from the distributor
                await ConfigurePublisher(distributor, publisher);

                // switch to publisher database
                publisher.ChangeDatabase(DatabaseName);

                // ensure replication is enabled on the database
                await publisher.ExecuteSpSetReplicationDbOptionAsync(DatabaseName, "publish", "true");

                // configure log reader agent
                var logReaderAgent = await publisher.ExecuteSpHelpLogReaderAgentAsync();
                if (logReaderAgent?.JobId == null)
                {
                    if (config.LogReaderAgentCredential != null)
                        await publisher.ExecuteSpAddLogReaderAgentAsync(config.LogReaderAgentCredential.UserName, config.LogReaderAgentCredential.Password, 1);
                    else
                        await publisher.ExecuteSpAddLogReaderAgentAsync(null, null, null);
                }

                // add publication if it does not exist
                var existingPublications1 = await publisher.LoadDataTableAsync($"SELECT * from syspublications WHERE name = {Name}");
                if (existingPublications1.Rows.Count == 0)
                    await publisher.ExecuteSpAddPublicationAsync(Name, "active", true, true, true);

                // add publication snapshot if it does not exist
                var existingPublication2 = await publisher.LoadDataTableAsync($"SELECT * from syspublications WHERE name = {Name} AND snapshot_jobid IS NOT NULL");
                if (existingPublication2.Rows.Count == 0)
                    if (config.SnapshotAgentCredential != null)
                        await publisher.ExecuteSpAddPublicationSnapshotAsync(Name, config.SnapshotAgentCredential.UserName, config.SnapshotAgentCredential.Password, 1);
                    else
                        await publisher.ExecuteSpAddPublicationSnapshotAsync(Name, null, null, null);

                // update the snapshot directory ACLs
                await UpdateSnapshotDirectoryAcl(publisher, Name);

                // change publication options
                await publisher.ExecuteSpChangePublicationAsync(Name, "allow_anonymous", "false", 1);
                await publisher.ExecuteSpChangePublicationAsync(Name, "immediate_sync", "false", 1);
                await publisher.ExecuteSpChangePublicationAsync(Name, "sync_method", "database snapshot", 1);

                var tables = (await publisher.LoadDataTableAsync($@"
                            SELECT      s.schema_id         AS schema_id,
                                        NULLIF(s.name, '')  AS schema_name,
                                        t.object_id         AS object_id,
                                        NULLIF(t.name, '')  AS object_name,
                                        p.pubid             AS publication_id,
                                        NULLIF(p.name, '')  AS publication_name,
                                        a.artid             AS article_id,
                                        NULLIF(a.name, '')  AS article_name
                            FROM        sys.tables t
                            INNER JOIN  syspublications p
                                ON      1 = 1
                            INNER JOIN  sys.schemas s
                                ON      s.schema_id = t.schema_id
                            LEFT JOIN   sysarticles a
                                ON      a.objid = t.object_id
                                AND     a.pubid = p.pubid
                            WHERE       p.name = {Name}"))
                    .Rows.Cast<DataRow>()
                    .Select(i => new
                    {
                        SchemaId = (int)i["schema_id"],
                        SchemaName = (string)i["schema_name"],
                        ObjectId = (int)i["object_id"],
                        ObjectName = (string)i["object_name"],
                        PublicationId = (int)i["publication_id"],
                        PublicationName = (string)i["publication_name"],
                        ArticleId = i["article_id"] != DBNull.Value ? (int?)i["article_id"] : null,
                        ArticleName = i["article_name"] != DBNull.Value ? (string)i["article_name"] : null,
                    })
                    .ToDictionary(i => $"[{i.SchemaName}].[{i.ObjectName}]", i => i, StringComparer.OrdinalIgnoreCase);

                foreach (var tableDefinition in definition.Articles.OfType<SqlTableArticleDefinition>())
                {
                    var table = tables.GetOrDefault(tableDefinition.Name);
                    if (table == null)
                        throw new InvalidOperationException($"Missing table '{tableDefinition.Name}'.");

                    if (table.ArticleId == null)
                        await publisher.ExecuteSpAddArticleAsync(
                            publication: table.PublicationName,
                            article: table.ObjectName,
                            sourceOwner: table.SchemaName,
                            sourceObject: table.ObjectName,
                            destinationTable: table.ObjectName,
                            destinationOwner: table.SchemaName,
                            status: 24,
                            forceInvalidateSnapshot: true);
                }

                // start publication
                try
                {
                    await publisher.ExecuteSpStartPublicationSnapshotAsync(Name);
                }
                catch (SqlException)
                {
                    // ignore
                }
            }
        }

        /// <summary>
        /// Updates the ACLs of the snapshot directory.
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="publication"></param>
        /// <returns></returns>
        async Task UpdateSnapshotDirectoryAcl(SqlConnection cnn, string publication)
        {
            if (cnn == null)
                throw new ArgumentNullException(nameof(cnn));
            if (publication == null)
                throw new ArgumentNullException(nameof(publication));

            // find login of publiation
            var publicationInfo = await cnn.ExecuteSpHelpPublicationSnapshotAsync(publication);
            var logReaderInfo = await cnn.ExecuteSpHelpLogReaderAgentAsync();

            // nothing to update
            if (publicationInfo?.JobLogin == null &&
                logReaderInfo?.JobLogin == null)
                return;

            var d = new DirectoryInfo(await GetSnapshotFolder(cnn, publication));
            if (d.Exists == false)
            {
                // attempt to determine domain name of SQL instance and use to append to path
                var u = new Uri(d.FullName);
                if (u.IsUnc && u.Host.Contains(".") == false)
                {
                    var n = await GetDomainName(cnn);
                    if (string.IsNullOrWhiteSpace(n) == false)
                    {
                        var b = new UriBuilder(u);
                        b.Host += "." + n;
                        d = new DirectoryInfo(b.Uri.LocalPath);
                    }
                }

                if (d.Exists == false)
                {
                    return;
                }
            }

            await Task.Run(() =>
            {
                try
                {
                    // grant permissions to directory to snapshot agent account
                    var acl = d.GetAccessControl();

                    if (publicationInfo?.JobLogin != null)
                        acl.AddAccessRule(new FileSystemAccessRule(
                            publicationInfo.JobLogin,
                            FileSystemRights.ReadAndExecute | FileSystemRights.Write,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.InheritOnly,
                            AccessControlType.Allow));

                    if (logReaderInfo?.JobLogin != null)
                        acl.AddAccessRule(new FileSystemAccessRule(
                            logReaderInfo.JobLogin,
                            FileSystemRights.ReadAndExecute,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.InheritOnly,
                            AccessControlType.Allow));

                    d.SetAccessControl(acl);
                }
                catch (Exception e)
                {
                    //logger.Error(e, "Unable to update snapshot directory ACLs.");
                }
            });
        }

        /// <summary>
        /// Gets the snapshot folder for the publication.
        /// </summary>
        /// <param name="cnn"></param>
        /// <returns></returns>
        async Task<string> GetSnapshotFolder(SqlConnection cnn, string publication)
        {
            var publicationInfo = await cnn.ExecuteSpHelpPublicationAsync(publication);

            // uses default distributor folder
            if (publicationInfo?.SnapshotInDefaultFolder == true)
            {
                // fix permissions on replication directory
                var distributorInfo = await cnn.ExecuteSpHelpDistributorAsync();
                if (distributorInfo.Directory != null)
                    return distributorInfo.Directory;
            }

            // specified directly on publication
            if (publicationInfo?.AltSnapshotFolder != null)
                return publicationInfo.AltSnapshotFolder;

            return null;
        }

        /// <summary>
        /// Gets the fully qualified name of the connected server.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        async Task<string> GetDomainName(SqlConnection connection)
        {
            var d = (string)await connection.ExecuteScalarAsync($@"
                DECLARE @DomainName nvarchar(256)
                EXEC    master.dbo.xp_regread 'HKEY_LOCAL_MACHINE', 'SYSTEM\CurrentControlSet\Services\Tcpip\Parameters', N'Domain', @DomainName OUTPUT
                SELECT  @DomainName");

            return d;
        }

    }

}
