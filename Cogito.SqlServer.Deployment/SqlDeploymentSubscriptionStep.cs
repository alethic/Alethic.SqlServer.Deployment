using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentSubscriptionStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="subscriptionName"></param>
        public SqlDeploymentSubscriptionStep(string instanceName, string subscriptionName) :
            base(instanceName)
        {
            SubscriptionName = subscriptionName ?? throw new ArgumentNullException(nameof(subscriptionName));
        }

        /// <summary>
        /// Gets the name of the subscription to deploy.
        /// </summary>
        public string SubscriptionName { get; }

        public override Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
