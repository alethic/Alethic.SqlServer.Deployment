using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures that a SQL server database is deployed against the instance.
    /// </summary>
    public class SqlDeploymentDatabase
    {

        /// <summary>
        /// Gets or sets the name of the database.
        /// </summary>
        public SqlDeploymentExpression Name { get; set; }

        /// <summary>
        /// Gets or sets the path to the DACPAC to be deployed to the database.
        /// </summary>
        public SqlDeploymentDatabasePackage Package { get; set; }

        /// <summary>
        /// Gets or sets the owner of the database.
        /// </summary>
        public SqlDeploymentExpression? Owner { get; set; }

        /// <summary>
        /// Extended properties to be set on the database.
        /// </summary>
        public ICollection<SqlDeploymentDatabaseExtendedProperty> ExtendedProperties { get; } = new List<SqlDeploymentDatabaseExtendedProperty>();

        /// <summary>
        /// Gets the publications configured for this database.
        /// </summary>
        public ICollection<SqlDeploymentPublication> Publications { get; } = new List<SqlDeploymentPublication>();

        /// <summary>
        /// Gets the subscriptions configured for this database.
        /// </summary>
        public ICollection<SqlDeploymentSubscription> Subscriptions { get; } = new List<SqlDeploymentSubscription>();

        /// <summary>
        /// Generates the steps required to ensure the database.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            // creates the database if it does not exist
            yield return new SqlDeploymentCreateDatabaseAction(context.InstanceName, Name.Expand(context));

            // potentially deploy a DAC package
            if (Package != null)
                foreach (var s in Package.Compile(context, Name))
                    yield return s;

            // potentially alter the owner
            if (Owner != null)
                yield return new SqlDeploymentDatabaseOwnerAction(context.InstanceName, Name.Expand(context), Owner?.Expand(context));

            // apply any extended properties
            foreach (var extendedProperty in ExtendedProperties)
                foreach (var s in extendedProperty.Compile(context, Name))
                    yield return s;

            // apply any publications
            foreach (var publication in Publications)
                foreach (var s in publication.Compile(context, Name))
                    yield return s;

            // apply any subscriptions
            foreach (var subscription in Subscriptions)
                foreach (var s in subscription.Compile(context, Name))
                    yield return s;
        }

    }

}

