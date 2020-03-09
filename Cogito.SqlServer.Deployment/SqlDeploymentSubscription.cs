using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a subscription to configure on the instance.
    /// </summary>
    public class SqlDeploymentSubscription
    {

        /// <summary>
        /// Gets the name of the subscription.
        /// </summary>
        public SqlDeploymentExpression Name { get; set; }

        /// <summary>
        /// Generates the steps required to ensure the subscription.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentSubscriptionStep(context.InstanceName, Name.Expand(context));
        }

    }

}
