using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a potential step during a deployment plan.
    /// </summary>
    public abstract class SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        protected SqlDeploymentAction(SqlInstance instance)
        {
            Instance = instance ?? throw new ArgumentNullException(nameof(instance));
        }

        /// <summary>
        /// Gets the instance that the step should be executed against.
        /// </summary>
        public SqlInstance Instance { get; }

        /// <summary>
        /// Applies the step to the instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        public abstract Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken);

        /// <summary>
        /// Opens a new connection to the targeted SQL instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        protected async Task<SqlConnection> OpenConnectionAsync(SqlInstance instance, CancellationToken cancellationToken)
        {
            var b = new SqlConnectionStringBuilder(instance.ConnectionString ?? "");
            if (string.IsNullOrEmpty(b.DataSource))
                b.DataSource = instance.Name;

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
            return OpenConnectionAsync(Instance, cancellationToken);
        }

    }

}
