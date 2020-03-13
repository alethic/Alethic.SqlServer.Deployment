using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a potential step during a deployment plan.
    /// </summary>
    public abstract class SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        protected SqlDeploymentStep(string instanceName)
        {
            InstanceName = instanceName ?? throw new ArgumentNullException(nameof(instanceName));
        }

        /// <summary>
        /// Gets the instance that the step should be executed against.
        /// </summary>
        public string InstanceName { get; }

        /// <summary>
        /// Applies the step to the instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        public abstract Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Opens a new connection to the targeted SQL instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected async Task<SqlConnection> OpenConnectionAsync(string instanceName, CancellationToken cancellationToken)
        {
            var b = new SqlConnectionStringBuilder();
            b.DataSource = instanceName;
            b.IntegratedSecurity = true;

            var c = new SqlConnection(b.ToString());
            await c.OpenAsync(cancellationToken);

            return c;
        }

        /// <summary>
        /// Opens a new connection to the targeted SQL instance.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
        {
            return OpenConnectionAsync(InstanceName, cancellationToken);
        }

    }

}
