using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a subscription to configure on the instance.
    /// </summary>
    public abstract class SqlDeploymentSubscription
    {

        /// <summary>
        /// Gets or sets the name of the publisher to subscribe to.
        /// </summary>
        public SqlDeploymentExpression PublisherInstanceName { get; set; }

        /// <summary>
        /// Gets or sets the name of the publication to subscribe to.
        /// </summary>
        public SqlDeploymentExpression PublicationName { get; set; }

        /// <summary>
        /// Generates the steps required to ensure the subscription.
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract IEnumerable<SqlDeploymentStep> Compile(string databaseName, SqlDeploymentCompileContext context);

    }

}
