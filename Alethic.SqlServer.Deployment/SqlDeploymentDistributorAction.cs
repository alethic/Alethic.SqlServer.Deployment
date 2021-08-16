using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Cogito.Collections;
using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Configures the instance as a distributor.
    /// </summary>
    public class SqlDeploymentDistributorAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        public SqlDeploymentDistributorAction(SqlInstance instance) :
            base(instance)
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
        /// Gets the minimum retention period, in hours.
        /// </summary>
        public int? MinimumRetention { get; internal set; }

        /// <summary>
        /// Gets the maximum retention period, in hours.
        /// </summary>
        public int? MaximumRetention { get; internal set; }

        /// <summary>
        /// Gets the number of hours to retain history.
        /// </summary>
        public int? HistoryRetention { get; internal set; }

        /// <summary>
        /// Gets the path to snapshot output for this distributor.
        /// </summary>
        public string SnapshotPath { get; internal set; }

        /// <summary>
        /// Configures the distributor.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            cnn.ChangeDatabase("master");

            // find proper name of server
            var distributorName = await cnn.GetServerNameAsync();

            // configure as distributor if required
            var currentDistributorName = (string)await cnn.ExecuteScalarAsync($"SELECT name FROM sys.servers WHERE is_distributor = 1");
            if (currentDistributorName != "repl_distributor")
            {
                if (AdminPassword == null)
                    throw new SqlDeploymentException("Cannot configure distributor: missing AdminPassword.");

                context.Logger?.LogInformation("Creating distributor on {InstanceName}.", Instance);
                await cnn.ExecuteNonQueryAsync($@"
                    EXEC sp_adddistributor
                        @distributor = {distributorName},
                        @password = {AdminPassword}");
            }
            else if (AdminPassword != null)
            {
                context.Logger?.LogInformation("Changing distributor password on {InstanceName}.", Instance);
                await cnn.ExecuteNonQueryAsync($@"
                    EXEC sp_changedistributor_password
                        @password = {AdminPassword}");
            }

            // configure distribution database if required
            var databaseName = DatabaseName ?? "distribution";
            var currentDistributionDbs = await cnn.ExecuteSpHelpDistributionDbAsync(cancellationToken);
            var currentDistributionDb = currentDistributionDbs?.FirstOrDefault(i => i.Name == databaseName);
            if (currentDistributionDb == null)
            {
                context.Logger?.LogInformation("Adding distribution database {DatabaseName} on {InstanceName}.", databaseName, Instance);

                using var cmd = cnn.CreateCommand();
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_adddistributiondb";

                cmd.Parameters.AddWithValue("@database", databaseName);
                cmd.Parameters.AddWithValue("@security_mode", 1);

                if (MinimumRetention != null)
                    cmd.Parameters.AddWithValue("@min_distretention", MinimumRetention);

                if (MaximumRetention != null)
                    cmd.Parameters.AddWithValue("@max_distretention", MaximumRetention);

                if (HistoryRetention != null)
                    cmd.Parameters.AddWithValue("@history_retention", HistoryRetention);

                if (await cmd.ExecuteScalarAsync(cancellationToken) is int i && i != 0)
                    throw new SqlDeploymentException("Error code returned executing sp_adddistributiondb.");
            }
            else
            {
                if (MinimumRetention != null && currentDistributionDb.MinDistRetention != MinimumRetention)
                {
                    using var cmd = cnn.CreateCommand();
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_changedistributiondb";

                    cmd.Parameters.AddWithValue("@database", databaseName);
                    cmd.Parameters.AddWithValue("@property", "min_distretention");
                    cmd.Parameters.AddWithValue("@value", MinimumRetention);

                    if (await cmd.ExecuteScalarAsync(cancellationToken) is int i && i != 0)
                        throw new SqlDeploymentException("Error code returned executing sp_changedistributiondb.");
                }

                if (MaximumRetention != null && currentDistributionDb.MaxDistRetention != MaximumRetention)
                {
                    using var cmd = cnn.CreateCommand();
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_changedistributiondb";

                    cmd.Parameters.AddWithValue("@database", databaseName);
                    cmd.Parameters.AddWithValue("@property", "max_distretention");
                    cmd.Parameters.AddWithValue("@value", MaximumRetention);

                    if (await cmd.ExecuteScalarAsync(cancellationToken) is int i && i != 0)
                        throw new SqlDeploymentException("Error code returned executing sp_changedistributiondb.");
                }

                if (HistoryRetention != null && currentDistributionDb.HistoryRetention != HistoryRetention)
                {
                    using var cmd = cnn.CreateCommand();
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "sp_changedistributiondb";

                    cmd.Parameters.AddWithValue("@database", databaseName);
                    cmd.Parameters.AddWithValue("@property", "history_retention");
                    cmd.Parameters.AddWithValue("@value", HistoryRetention);

                    if (await cmd.ExecuteScalarAsync(cancellationToken) is int i && i != 0)
                        throw new SqlDeploymentException("Error code returned executing sp_changedistributiondb.");
                }
            }

            // should be derived from information on server
            var defaultDataRootTable = await cnn.LoadDataTableAsync(@"EXEC master.dbo.xp_instance_regread N'HKEY_LOCAL_MACHINE', N'Software\Microsoft\MSSQLServer\Setup', N'SQLDataRoot'");
            var defaultDataRootMap = defaultDataRootTable.Rows.Cast<DataRow>().ToDictionary(i => (string)i["Value"], i => i["Data"]);
            var defaultDataRoot = (string)defaultDataRootMap.GetOrDefault("SQLDataRoot");
            var defaultReplData = defaultDataRoot != null ? Path.Combine(defaultDataRoot, "ReplData") : null;

            // update snapshot folder if determined
            var snapshotPath = SnapshotPath ?? defaultReplData;
            if (snapshotPath != null)
            {
                await cnn.ExecuteNonQueryAsync($@"
                    IF NOT EXISTS (SELECT * from sysobjects where name = 'UIProperties' and type = 'U')
                        CREATE TABLE UIProperties(id int)
                    IF EXISTS (SELECT * from ::fn_listextendedproperty('SnapshotFolder', 'user', 'dbo', 'table', 'UIProperties', null, null))
                        EXEC sp_updateextendedproperty N'SnapshotFolder', {snapshotPath}, 'user', dbo, 'table', 'UIProperties' 
                    ELSE 
                        EXEC sp_addextendedproperty N'SnapshotFolder', {snapshotPath}, 'user', dbo, 'table', 'UIProperties'");
            }
        }

    }

}
