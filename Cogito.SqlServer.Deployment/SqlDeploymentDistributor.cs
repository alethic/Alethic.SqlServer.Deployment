using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Specifies the configuration of the server as a distributor.
    /// </summary>
    public class SqlDeploymentDistributor
    {

        /// <summary>
        /// Gets or sets the name of the distribution database.
        /// </summary>
        public SqlDeploymentExpression? DatabaseName { get; set; }

        /// <summary>
        /// Specifies the administrator password to configure on the distributor.
        /// </summary>
        public SqlDeploymentExpression? AdminPassword { get; private set; }

        /// <summary>
        /// Gets or sets the path to the distribution database data files.
        /// </summary>
        public SqlDeploymentExpression? DataPath { get; set; }

        /// <summary>
        /// Gets or sets the path to the distribution database log files.
        /// </summary>
        public SqlDeploymentExpression? LogsPath { get; set; }

        public SqlDeploymentExpression? LogFileSize { get; set; }

        public SqlDeploymentExpression? MinimumRetention { get; set; }

        public SqlDeploymentExpression? MaximumRetention { get; set; }

        public SqlDeploymentExpression? HistoryRetention { get; set; }

        public SqlDeploymentExpression? SnapshotPath { get; set; }

        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentDistributorStep(context.InstanceName)
            {
                DatabaseName = DatabaseName?.Expand(context),
                AdminPassword = AdminPassword?.Expand(context),
                DataPath = DataPath?.Expand(context),
                LogsPath = LogsPath?.Expand(context),
                LogFileSize = LogFileSize?.Expand<int>(context),
                MinimumRetention = MinimumRetention?.Expand<int>(context),
                MaximumRetention = MaximumRetention?.Expand<int>(context),
                HistoryRetention = HistoryRetention?.Expand<int>(context),
                SnapshotPath = SnapshotPath?.Expand(context),
            };
        }

    }

}

