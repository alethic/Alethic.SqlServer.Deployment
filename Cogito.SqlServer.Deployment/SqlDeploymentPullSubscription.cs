using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a subscription to configure on the instance.
    /// </summary>
    public class SqlDeploymentPullSubscription : SqlDeploymentSubscription
    {

        /// <summary>
        /// Generates the steps required to ensure the subscription.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public override IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            yield return new SqlDeploymentPullSubscriptionStep(context.InstanceName, databaseName)
            {
                PublisherInstanceName = PublisherInstanceName.Expand(context),
                PublisherDatabaseName = databaseName,
                PublicationName = PublicationName.Expand(context),
            };
        }

    }

}
