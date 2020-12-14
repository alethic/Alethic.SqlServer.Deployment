namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a set of credentials.
    /// </summary>
    public class SqlDeploymentSqlCredentials
    {

        /// <summary>
        /// Gets or sets the login of the account.
        /// </summary>
        public SqlDeploymentExpression Login { get; set; }

        /// <summary>
        /// Gets or sets the password of the account.
        /// </summary>
        public SqlDeploymentExpression Password { get; set; }

    }

}