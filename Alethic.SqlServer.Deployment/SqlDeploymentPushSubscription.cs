using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a subscription to configure on the instance.
    /// </summary>
    public class SqlDeploymentPushSubscription : SqlDeploymentSubscription
    {

        /// <summary>
        /// Generates the steps required to ensure the subscription.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public override IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            yield return new SqlDeploymentPushSubscriptionAction(
                context.InstanceName,
                databaseName,
                PublisherInstanceName.Expand(context),
                PublicationDatabaseName.Expand(context),
                PublicationName.Expand(context));
        }

    }

}