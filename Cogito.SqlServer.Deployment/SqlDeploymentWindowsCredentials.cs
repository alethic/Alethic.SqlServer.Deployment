namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a set of credentials.
    /// </summary>
    public class SqlDeploymentWindowsCredentials
    {

        /// <summary>
        /// Gets or sets the username of the log reader agent.
        /// </summary>
        public SqlDeploymentExpression UserName { get; set; }

        /// <summary>
        /// Gets or sets the password of the log reader agent.
        /// </summary>
        public SqlDeploymentExpression Password { get; set; }

    }

}