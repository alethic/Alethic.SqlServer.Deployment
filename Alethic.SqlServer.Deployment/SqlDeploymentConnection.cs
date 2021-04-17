namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes connection information to the instance.
    /// </summary>
    public class SqlDeploymentConnection
    {

        /// <summary>
        /// Connection string used to establish a connection to the insance. Data Source and Initial Catalog are not required.
        /// </summary>
        public SqlDeploymentExpression ConnectionString { get; set; }

    }

}