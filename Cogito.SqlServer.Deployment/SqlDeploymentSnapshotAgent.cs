namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes the settings for a snapshot agent.
    /// </summary>
    public class SqlDeploymentSnapshotAgent
    {

        /// <summary>
        /// If specified, describes the Windows credentials to use for the snapshot agent process. If not specified, the
        /// Snapshot Agent is configured to run under the SQL Server Agent service account.
        /// </summary>
        public SqlDeploymentWindowsCredentials ProcessCredentials { get; set; }

        /// <summary>
        /// If specified, describes the SQL credentials used to connect to the publisher.
        /// </summary>
        public SqlDeploymentSqlCredentials ConnectCredentials { get; set; }

    }

}
