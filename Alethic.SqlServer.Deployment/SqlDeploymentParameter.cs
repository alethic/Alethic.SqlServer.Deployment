namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a parameter available to be passed to a SQL deployment.
    /// </summary>
    public class SqlDeploymentParameter
    {

        /// <summary>
        /// Gets the name of the parameters.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets the default value of the parameter.
        /// </summary>
        public string DefaultValue { get; set; }

    }

}