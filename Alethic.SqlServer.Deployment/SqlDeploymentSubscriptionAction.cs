using System;

namespace Alethic.SqlServer.Deployment
{

    public abstract class SqlDeploymentSubscriptionAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="publisherInstanceName"></param>
        /// <param name="publicationDatabaseName"></param>
        /// <param name="publicationName"></param>
        public SqlDeploymentSubscriptionAction(
            string instanceName,
            string databaseName,
            string publisherInstanceName,
            string publicationDatabaseName,
            string publicationName) :
            base(instanceName)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            PublisherInstanceName = publisherInstanceName ?? throw new ArgumentNullException(nameof(publisherInstanceName));
            PublicationDatabaseName = publicationDatabaseName ?? throw new ArgumentNullException(nameof(publicationDatabaseName));
            PublicationName = publicationName ?? throw new ArgumentNullException(nameof(publicationName));
        }

        /// <summary>
        /// Gets the name of the subscriber database.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the name of the publisher instance on which the publication exists.
        /// </summary>
        public string PublisherInstanceName { get; }

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
