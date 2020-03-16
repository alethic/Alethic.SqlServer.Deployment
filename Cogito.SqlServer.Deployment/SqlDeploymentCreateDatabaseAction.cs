using System;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures the existence of a database.
    /// </summary>
    public class SqlDeploymentCreateDatabaseAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        public SqlDeploymentCreateDatabaseAction(string instanceName, string name) :
            base(instanceName)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the name of the database.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Creates the database.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using (var cnn = await OpenConnectionAsync(cancellationToken))
                if ((string)await cnn.ExecuteScalarAsync($"SELECT name FROM sys.databases WHERE name = {Name}") == null)
                    await CreateDatabase(context, cnn);
        }

        async Task CreateDatabase(SqlDeploymentExecuteContext context, SqlConnection cnn)
        {
            context.Logger.LogInformation("Creating database {DatabaseName}.", Name);
            await cnn.ExecuteNonQueryAsync((string)$"CREATE DATABASE [{Name}]");
        }

    }

}
