using System;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Step that ensures the existance and configuration of a publication.
    /// </summary>
    public abstract class SqlDeploymentPublicationAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instnace.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationAction(SqlInstance instance, string databaseName, string name) :
            base(instance)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the name of the database to be published.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the name of the publication to deploy.
        /// </summary>
        public string Name { get; }

    }

}
