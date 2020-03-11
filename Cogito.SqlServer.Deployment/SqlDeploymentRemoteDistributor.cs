using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Specifies the configuration of the server to refer to a remote distributor.
    /// </summary>
    public class SqlDeploymentRemoteDistributor
    {

        /// <summary>
        /// Gets or sets the name of the distributor instance.
        /// </summary>
        public SqlDeploymentExpression InstanceName { get; set; }

        /// <summary>
        /// Gets or sets the admin password to use for connecting to the existing distributor instance.
        /// </summary>
        public SqlDeploymentExpression? AdminPassword { get; set; }

        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentRemoteDistributorStep(context.InstanceName)
            {
                DistributorInstanceName = InstanceName.Expand(context),
                DistributorAdminPassword = AdminPassword?.Expand(context),
            };
        }

    }

}

