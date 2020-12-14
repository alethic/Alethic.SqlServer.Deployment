using System;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Applies a database owner.
    /// </summary>
    public class SqlDeploymentDatabaseOwnerAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="login"></param>
        public SqlDeploymentDatabaseOwnerAction(string instanceName, string databaseName, string login) :
            base(instanceName)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            Login = login ?? throw new ArgumentNullException(nameof(login));
        }

        /// <summary>
        /// Gets the name of the distribution database to create.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the name of the login to set as the owner.
        /// </summary>
        public string Login { get; }

        /// <summary>
        /// Executes the step.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            cnn.ChangeDatabase(DatabaseName);

            var owner = (string)await cnn.ExecuteScalarAsync($"SELECT SUSER_SNAME(owner_sid) owner_name FROM sys.databases WHERE name = {DatabaseName}", cancellationToken: cancellationToken);
            if (owner != Login)
                await SetDatabaseOwner(context, cnn, cancellationToken);
        }

        /// <summary>
        /// Sets the database owner.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task SetDatabaseOwner(SqlDeploymentExecuteContext context, SqlConnection connection, CancellationToken cancellationToken)
        {
            context.Logger.LogInformation("Changing database owner of {DatabaseName} to {Owner}.", DatabaseName, Login);
            await connection.ExecuteNonQueryAsync((string)$@"ALTER AUTHORIZATION ON DATABASE::[{DatabaseName}] TO [{Login}]", cancellationToken: cancellationToken);
        }

    }

}
