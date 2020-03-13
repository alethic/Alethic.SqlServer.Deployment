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

    /// <summary>
    /// Configures the instance as a distributor.
    /// </summary>
    public class SqlDeploymentDistributorStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        public SqlDeploymentDistributorStep(string instanceName) :
            base(instanceName)
        {

        }

        /// <summary>
        /// Gets the name of the distribution database to create.
        /// </summary>
        public string DatabaseName { get; internal set; } = "distribution";

        /// <summary>
        /// Gets the admin password to be configured on the distributor.
        /// </summary>
        public string AdminPassword { get; internal set; }

        /// <summary>
        /// Gets the path of the distribution database data files.
        /// </summary>
        public string DataPath { get; internal set; }

        /// <summary>
        /// Gets the path of the distribution database log files.
        /// </summary>
        public string LogsPath { get; internal set; }

        /// <summary>
        /// 
        /// </summary>
        public int? LogFileSize { get; internal set; } = 2;

        public int? MinimumRetention { get; internal set; } = 0;

        public int? MaximumRetention { get; internal set; } = 72;

        public int? HistoryRetention { get; internal set; } = 48;

        public string SnapshotPath { get; internal set; }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            cnn.ChangeDatabase("master");

            // find proper name of server
            var distributorName = await cnn.GetServerNameAsync();

            // configure as distributor if required
            var currentDistributorName = (string)await cnn.ExecuteScalarAsync($"SELECT name FROM sys.servers WHERE is_distributor = 1");
            if (currentDistributorName != "repl_distributor")
                await cnn.ExecuteNonQueryAsync($@"
                    EXEC sp_adddistributor
                        @distributor = {distributorName},
                        @password = {AdminPassword}");

            // reset distributor password, if specified
            if (AdminPassword != null)
                await cnn.ExecuteNonQueryAsync($@"
                    EXEC sp_changedistributor_password
                        @password = {AdminPassword}");

            // confgiure distribution database if required
            var databaseName = DatabaseName ?? "distribution";
            var currentDistributionDb = await cnn.ExecuteSpHelpDistributionDbAsync(databaseName, cancellationToken);
            if (currentDistributionDb == null)
                await cnn.ExecuteNonQueryAsync($@"
                    EXEC sp_adddistributiondb
                        @database = {databaseName},
                        @security_mode = 1");

            // should be derived from information on server
            var defaultDataRootTable = await cnn.LoadDataTableAsync(@"EXEC master.dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE', N'Software\Microsoft\MSSQLServer\Setup', N'SQLDataRoot'");
            var defaultDataRootMap = defaultDataRootTable.Rows.Cast<DataRow>().ToDictionary(i => (string)i["Value"], i => i["Data"]);
            var defaultDataRoot = (string)defaultDataRootMap.GetOrDefault("SQLDataRoot");
            var defaultReplData = defaultDataRoot != null ? Path.Combine(defaultDataRoot, "ReplData") : null;

            // update snapshot folder if determined
            var snapshotFolder = SnapshotPath ?? defaultReplData;
            if (snapshotFolder != null)
                await cnn.ExecuteNonQueryAsync($@"
                    IF (NOT EXISTS (SELECT * from sysobjects where name = 'UIProperties' and type = 'U '))
                        CREATE TABLE UIProperties(id int)
                    IF (EXISTS (SELECT * from ::fn_listextendedproperty('SnapshotFolder', 'user', 'dbo', 'table', 'UIProperties', null, null))) 
                        EXEC sp_updateextendedproperty N'SnapshotFolder', {snapshotFolder}, 'user', dbo, 'table', 'UIProperties' 
                    ELSE 
                        EXEC sp_addextendedproperty N'SnapshotFolder', {snapshotFolder}, 'user', dbo, 'table', 'UIProperties'");
        }

        /// <summary>
        /// Updates the ACLs of the distributor directory.
        /// </summary>
        /// <param name="cnn"></param>
        /// <returns></returns>
        async Task UpdateDirectoryAcl(SqlConnection cnn)
        {
            // fix permissions on replication directory
            var distributorInfo = await cnn.ExecuteSpHelpDistributorAsync();
            if (distributorInfo.Directory != null &&
                distributorInfo.Account != null)
            {
                // distributor directory?
                var d = new DirectoryInfo(distributorInfo.Directory);
                if (d.Exists == false)
                {
                    // path stored as local UNC, append default domain of distributor
                    var u = new Uri(d.FullName);
                    if (u.IsUnc &&
                        u.Host.Contains(".") == false)
                    {
                        var b = new UriBuilder(u);
                        if (await cnn.GetServerDomainName() is string domainName)
                            b.Host += "." + domainName;
                        d = new DirectoryInfo(b.Uri.LocalPath);
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
                        // grant permissions to directory to distributor agent account
                        var acl = d.GetAccessControl();
                        acl.AddAccessRule(new FileSystemAccessRule(
                            distributorInfo.Account,
                            FileSystemRights.FullControl,
                            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                            PropagationFlags.InheritOnly,
                            AccessControlType.Allow));
                        d.SetAccessControl(acl);
                    }
                    catch (Exception e)
                    {
                        //logger.Error(e, "Unable to update distribution directory ACLs.");
                    }
                });
            }
        }

    }

}
