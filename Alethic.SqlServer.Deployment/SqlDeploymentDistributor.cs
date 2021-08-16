using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Specifies the configuration of the server as a distributor.
    /// </summary>
    public class SqlDeploymentDistributor
    {

        /// <summary>
        /// Gets or sets the name of the distribution database.
        /// </summary>
        public SqlDeploymentExpression? DatabaseName { get; set; } = "distribution";

        /// <summary>
        /// Specifies the administrator password to configure on the distributor.
        /// </summary>
        public SqlDeploymentExpression? AdminPassword { get; set; }

        /// <summary>
        /// Gets the minimum amount to retail transactions.
        /// </summary>
        public SqlDeploymentExpression? MinimumRetention { get; set; }

        /// <summary>
        /// Gets the maximum amount to retain transactions.
        /// </summary>
        public SqlDeploymentExpression? MaximumRetention { get; set; }

        /// <summary>
        /// Gets the maximum amount to retain history.
        /// </summary>
        public SqlDeploymentExpression? HistoryRetention { get; set; }

        /// <summary>
        /// Gets the path to the distributor snapshots.
        /// </summary>
        public SqlDeploymentExpression? SnapshotPath { get; set; }

        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentDistributorAction(context.Instance)
            {
                DatabaseName = DatabaseName?.Expand(context),
                AdminPassword = AdminPassword?.Expand(context),
                MinimumRetention = MinimumRetention?.Expand<int>(context),
                MaximumRetention = MaximumRetention?.Expand<int>(context),
                HistoryRetention = HistoryRetention?.Expand<int>(context),
                SnapshotPath = SnapshotPath?.Expand(context),
            };
        }

    }

}

