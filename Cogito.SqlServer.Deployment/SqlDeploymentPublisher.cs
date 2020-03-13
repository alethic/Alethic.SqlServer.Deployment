using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Specifies the configuration of the server to refer to a remote distributor.
    /// </summary>
    public class SqlDeploymentPublisher
    {

        /// <summary>
        /// Gets or sets the name of the distributor instance. If not specified, configures the local instance as the distributor.
        /// </summary>
        public SqlDeploymentExpression? DistributorInstanceName { get; set; }

        /// <summary>
        /// Gets or sets the admin password to use for connecting to the existing distributor instance.
        /// </summary>
        public SqlDeploymentExpression? DistributorAdminPassword { get; set; }

        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublisherStep(context.InstanceName)
            {
                DistributorInstanceName = DistributorInstanceName?.Expand(context),
                DistributorAdminPassword = DistributorAdminPassword?.Expand(context),
            };
        }

    }

}

