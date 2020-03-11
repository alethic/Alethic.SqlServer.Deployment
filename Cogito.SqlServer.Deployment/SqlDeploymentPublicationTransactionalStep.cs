using System;
using System.IO;
using System.Net;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Step to configure a transactional publication.
    /// </summary>
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

        /// <summary>
        /// Gets the configuration of the log reader agent.
        /// </summary>
        public SqlDeploymentLogReaderAgentConfig LogReaderAgent { get; set; }

        /// <summary>
        /// Gets the configuration of the snapshot agent.
        /// </summary>
        public SqlDeploymentSnapshotAgentConfig SnapshotAgent { get; set; }

        public override Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var publish = await OpenConnectionAsync(cancellationToken);

            // switch to publisher database
            publish.ChangeDatabase(DatabaseName);

            // ensure replication is enabled on the database
            await publish.ExecuteSpSetReplicationDbOptionAsync(DatabaseName, "publish", "true");

            // configure log reader agent
            var logReaderAgent = await publish.ExecuteSpHelpLogReaderAgentAsync();
            if (logReaderAgent?.JobId == null)
                await publish.ExecuteSpAddLogReaderAgentAsync(
                    LogReaderAgent?.ProcessCredentials?.UserName,
                    LogReaderAgent?.ProcessCredentials?.Password,
                    LogReaderAgent?.ConnectCredentials == null ? 1 : 0,
                    LogReaderAgent?.ConnectCredentials?.UserId,
                    LogReaderAgent?.ConnectCredentials?.Password != null ? new NetworkCredential("", LogReaderAgent.ConnectCredentials.Password).Password : null);

            // add publication if it does not exist
            var existingPublication1 = await publish.LoadDataTableAsync($"SELECT * from syspublications WHERE name = {Name}");
            if (existingPublication1.Rows.Count == 0)
                await publish.ExecuteSpAddPublicationAsync(Name, "active", true, true, true);

            // add publication snapshot if it does not exist
            var existingPublication2 = await publish.LoadDataTableAsync($"SELECT * from syspublications WHERE name = {Name} AND snapshot_jobid IS NOT NULL");
            if (existingPublication2.Rows.Count == 0)
                await publish.ExecuteSpAddPublicationSnapshotAsync(
                    Name,
                    SnapshotAgent?.ProcessCredentials?.UserName,
                    SnapshotAgent?.ProcessCredentials?.Password,
                    SnapshotAgent?.ConnectCredentials == null ? 1 : 0,
                    SnapshotAgent?.ConnectCredentials?.UserId,
                    SnapshotAgent?.ConnectCredentials?.Password != null ? new NetworkCredential("", SnapshotAgent.ConnectCredentials.Password).Password : null);

            // update the snapshot directory ACLs
            await UpdateSnapshotDirectoryAcl(publish, Name);

            // change publication options
            await publish.ExecuteSpChangePublicationAsync(Name, "allow_anonymous", "false", 1);
            await publish.ExecuteSpChangePublicationAsync(Name, "immediate_sync", "false", 1);
            await publish.ExecuteSpChangePublicationAsync(Name, "sync_method", "database snapshot", 1);
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
