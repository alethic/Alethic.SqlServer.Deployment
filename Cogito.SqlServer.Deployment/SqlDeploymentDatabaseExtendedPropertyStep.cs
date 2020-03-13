using System;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentDatabaseExtendedPropertyStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public SqlDeploymentDatabaseExtendedPropertyStep(string instanceName, string databaseName, string name, string value) :
            base(instanceName)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }

        public string DatabaseName { get; }

        public string Name { get; }

        public string Value { get; }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            await cnn.ExecuteNonQueryAsync($@"EXEC sys.sp_addextendedproperty @name = {Name}, @value = {Value}", cancellationToken: cancellationToken);
        }

    }

}
