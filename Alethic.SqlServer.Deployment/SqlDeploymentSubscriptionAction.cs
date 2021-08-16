using System;

namespace Alethic.SqlServer.Deployment
{

    public abstract class SqlDeploymentSubscriptionAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="publisherInstance"></param>
        /// <param name="publicationDatabaseName"></param>
        /// <param name="publicationName"></param>
        public SqlDeploymentSubscriptionAction(
            SqlInstance instance,
            string databaseName,
            SqlInstance publisherInstance,
            string publicationDatabaseName,
            string publicationName) :
            base(instance)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            PublisherInstance = publisherInstance ?? throw new ArgumentNullException(nameof(publisherInstance));
            PublicationDatabaseName = publicationDatabaseName ?? throw new ArgumentNullException(nameof(publicationDatabaseName));
            PublicationName = publicationName ?? throw new ArgumentNullException(nameof(publicationName));
        }

        /// <summary>
        /// Gets the name of the subscriber database.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the instance on which the publication exists.
        /// </summary>
        public SqlInstance PublisherInstance { get; }

        /// <summary>
        /// Gets the name of the publication database.
        /// </summary>
        public string PublicationDatabaseName { get; }

        /// <summary>
        /// Gets the name of the publication within the publication database.
        /// </summary>
        public string PublicationName { get; }

    }

}
