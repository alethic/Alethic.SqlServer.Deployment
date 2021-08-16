using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Sets the value of a SQL server property.
    /// </summary>
    public class SqlDeploymentConfigurationAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="name"></param>
        /// <param name="value"></param>
        public SqlDeploymentConfigurationAction(SqlInstance instance, string name, int value) :
            base(instance)
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

        /// <summary>
        /// Applies the configuration value.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);

            // load existing information about value
            var config = await cnn.ExecuteSpConfigure(Name, cancellationToken);
            if (config == null)
                throw new SqlDeploymentException("Unknown configuration name.");

            // check for range
            if (Value < config.Minimum || Value > config.Maximum)
                throw new SqlDeploymentException("Configuration value out of range.");

            // has the value changed?
            if (config.ConfigValue != Value || config.RunValue != Value)
            {
                context.Logger.LogInformation("Setting server configuration '{Name}' to {Value}.", Name, Value);
                await cnn.ExecuteSpConfigure(Name, Value, cancellationToken);
                await cnn.ExecuteNonQueryAsync($"RECONFIGURE", cancellationToken: cancellationToken);
            }
        }

    }

}
