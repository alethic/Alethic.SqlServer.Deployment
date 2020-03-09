using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Specifies the configuration of the server to refer to a remote distributor.
    /// </summary>
    public class SqlDeploymentDistribution
    {

        /// <summary>
        /// Gets or sets the name of the distributor instance.
        /// </summary>
        public SqlDeploymentExpression InstanceName { get; set; }

        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentDistributionStep(context.InstanceName)
            {
                DistributionInstanceName = InstanceName.Expand(context),
            };
        }

    }

}

