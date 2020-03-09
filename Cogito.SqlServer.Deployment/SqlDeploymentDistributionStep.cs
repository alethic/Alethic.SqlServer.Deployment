using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Configures the instance to refer to a remote distributor.
    /// </summary>
    public class SqlDeploymentDistributionStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        public SqlDeploymentDistributionStep(string instanceName) :
            base(instanceName)
        {

        }

        /// <summary>
        /// Gets the name of the distribution instance to connect to.
        /// </summary>
        public string DistributionInstanceName { get; internal set; }

        public override async Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
