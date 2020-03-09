using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Sets the value of a SQL server property.
    /// </summary>
    public class SqlDeploymentConfigurationStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public SqlDeploymentConfigurationStep(string instanceName, string name, int value) :
            base(instanceName)
        {
            Name = name;
            Value = value;
        }

        /// <summary>
        /// Gets the name of the configuration option to apply.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the value to be applied.
        /// </summary>
        public int Value { get; }

        public override async Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            return true;
        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using (var cnn = await OpenConnectionAsync(cancellationToken))
            {
                await cnn.ExecuteNonQueryAsync((string)$"EXEC sp_configure '{Name}', {Value}");
                await cnn.ExecuteNonQueryAsync($"RECONFIGURE");
            }
        }

    }

}
