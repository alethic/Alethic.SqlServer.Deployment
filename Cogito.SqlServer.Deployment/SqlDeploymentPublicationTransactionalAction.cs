using System;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Step to configure a transactional publication.
    /// </summary>
    public class SqlDeploymentPublicationTransactionalAction : SqlDeploymentPublicationAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="publicationName"></param>
        /// <param name="databaseName"></param>
        public SqlDeploymentPublicationTransactionalAction(string instanceName, string publicationName, string databaseName) :
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

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var publish = await OpenConnectionAsync(cancellationToken);

            // switch to publisher database
            publish.ChangeDatabase(DatabaseName);

            // ensure replication is enabled on the database
            await publish.ExecuteSpSetReplicationDbOptionAsync(DatabaseName, "publish", "true");

            // configure log reader agent
            var logReaderAgent = await publish.ExecuteSpHelpLogReaderAgentAsync(cancellationToken);
            if (logReaderAgent?.JobId == null)
                await publish.ExecuteSpAddLogReaderAgentAsync(
                    LogReaderAgent?.ProcessCredentials?.UserName,
                    LogReaderAgent?.ProcessCredentials?.Password,
                    LogReaderAgent?.ConnectCredentials == null ? 1 : 0,
                    LogReaderAgent?.ConnectCredentials?.UserId,
                    LogReaderAgent?.ConnectCredentials?.Password != null ? new NetworkCredential("", LogReaderAgent.ConnectCredentials.Password).Password : null,
                    cancellationToken);

            // add publication if it does not exist
            var existingPublication1 = await publish.LoadDataTableAsync($"SELECT * from syspublications WHERE name = {Name}", cancellationToken: cancellationToken);
            if (existingPublication1.Rows.Count == 0)
                await publish.ExecuteSpAddPublicationAsync(Name, "active", true, true, true, cancellationToken);

            // add publication snapshot if it does not exist
            var existingPublication2 = await publish.LoadDataTableAsync($"SELECT * from syspublications WHERE name = {Name} AND snapshot_jobid IS NOT NULL", cancellationToken: cancellationToken);
            if (existingPublication2.Rows.Count == 0)
                await publish.ExecuteSpAddPublicationSnapshotAsync(
                    Name,
                    SnapshotAgent?.ProcessCredentials?.UserName,
                    SnapshotAgent?.ProcessCredentials?.Password,
                    SnapshotAgent?.ConnectCredentials == null ? 1 : 0,
                    SnapshotAgent?.ConnectCredentials?.UserId,
                    SnapshotAgent?.ConnectCredentials?.Password != null ? new NetworkCredential("", SnapshotAgent.ConnectCredentials.Password).Password : null,
                    cancellationToken);

            // update the snapshot directory ACLs
            await UpdateSnapshotDirectoryAcl(context, publish, Name, cancellationToken);

            // change publication options
            await publish.ExecuteSpChangePublicationAsync(Name, "allow_anonymous", "false", 1, cancellationToken);
            await publish.ExecuteSpChangePublicationAsync(Name, "immediate_sync", "false", 1, cancellationToken);
            await publish.ExecuteSpChangePublicationAsync(Name, "sync_method", "database snapshot", 1, cancellationToken);
        }

        /// <summary>
        /// Updates the ACLs of the snapshot directory.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task UpdateSnapshotDirectoryAcl(SqlDeploymentExecuteContext context, SqlConnection connection, string publication, CancellationToken cancellationToken)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (publication == null)
                throw new ArgumentNullException(nameof(publication));

            // find login of publiation
            var publicationInfo = await connection.ExecuteSpHelpPublicationSnapshotAsync(publication, cancellationToken);
            var logReaderInfo = await connection.ExecuteSpHelpLogReaderAgentAsync(cancellationToken);

            // nothing to update
            if (publicationInfo?.JobLogin == null &&
                logReaderInfo?.JobLogin == null)
                return;

            var d = new DirectoryInfo(await GetSnapshotFolder(connection, publication, cancellationToken));
            if (d.Exists == false)
            {
                // attempt to determine domain name of SQL instance and use to append to path
                var u = new Uri(d.FullName);
                if (u.IsUnc && u.Host.Contains(".") == false)
                {
                    var n = await connection.GetServerDomainName(cancellationToken);
                    if (string.IsNullOrWhiteSpace(n) == false)
                    {
                        var b = new UriBuilder(u);
                        b.Host += "." + n;
                        d = new DirectoryInfo(b.Uri.LocalPath);
                    }
                }

                if (d.Exists == false)
                    return;
            }

            // file access security only functions on Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
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
                        context.Logger.LogError(e, "Unexpected exception updating snapshot directory permissions.");
                    }
                });
        }

        /// <summary>
        /// Gets the snapshot folder for the publication.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<string> GetSnapshotFolder(SqlConnection connection, string publication, CancellationToken cancellationToken)
        {
            var publicationInfo = await connection.ExecuteSpHelpPublicationAsync(publication, cancellationToken);

            // uses default distributor folder
            if (publicationInfo?.SnapshotInDefaultFolder == true)
            {
                // fix permissions on replication directory
                var distributorInfo = await connection.ExecuteSpHelpDistributorAsync(cancellationToken);
                if (distributorInfo.Directory != null)
                    return distributorInfo.Directory;
            }

            // specified directly on publication
            if (publicationInfo?.AltSnapshotFolder != null)
                return publicationInfo.AltSnapshotFolder;

            return null;
        }

    }

}
