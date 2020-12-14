using System;

namespace Alethic.SqlServer.Deployment
{

    public abstract class SqlDeploymentPublicationArticleAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="publicationName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationArticleAction(string instanceName, string databaseName, string publicationName, string name) :
            base(instanceName)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            PublicationName = publicationName ?? throw new ArgumentNullException(nameof(publicationName));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the name of the database that is published.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the name of the publication that will hold the article.
        /// </summary>
        public string PublicationName { get; }

        /// <summary>
        /// Gets the name of the article.
        /// </summary>
        public string Name { get; }

    }

}
