using System;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures the value of an extended property.
    /// </summary>
    public class SqlDeploymentDatabaseExtendedPropertyAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public SqlDeploymentDatabaseExtendedPropertyAction(SqlInstance instance, string databaseName, string name, string value) :
            base(instance)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets the database name upon which to set the extended property.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the name of the extended property.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the value of the extended property.
        /// </summary>
        public string Value { get; }

        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            await cnn.ExecuteNonQueryAsync($@"EXEC sys.sp_addextendedproperty @name = {Name}, @value = {Value}", cancellationToken: cancellationToken);
        }

    }

}
