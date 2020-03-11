namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes the settings for a log reader agent.
    /// </summary>
    public class SqlDeploymentLogReaderAgent
    {

        /// <summary>
        /// If specified, describes the Windows credentials to use for the log reader process. If not specified, the
        /// Log Reader Agent is configured to run under the SQL Server Agent service account.
        /// </summary>
        public SqlDeploymentWindowsCredentials ProcessCredentials { get; set; }

        /// <summary>
        /// If specified, describes the SQL credentials used to connect to the publisher.
        /// </summary>
        public SqlDeploymentSqlCredentials ConnectCredentials { get; set; }

    }

}
