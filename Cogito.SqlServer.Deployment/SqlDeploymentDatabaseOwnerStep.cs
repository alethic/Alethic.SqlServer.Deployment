using System;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Applies a new database owner.
    /// </summary>
    public class SqlDeploymentDatabaseOwnerStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="login"></param>
        public SqlDeploymentDatabaseOwnerStep(string instanceName, string databaseName, string login) :
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
        /// Sets the database owner.
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task SetDatabaseOwner(SqlConnection cnn, CancellationToken cancellationToken)
        {
            cnn.ChangeDatabase(DatabaseName);

            var owner = (string)await cnn.ExecuteScalarAsync($"SELECT SUSER_SNAME(owner_sid) owner_name FROM sys.databases WHERE name = {DatabaseName}", cancellationToken: cancellationToken);
            if (owner != Login)
                await cnn.ExecuteNonQueryAsync((string)$@"ALTER AUTHORIZATION ON DATABASE::[{DatabaseName}] TO [{Login}]", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the step.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            await SetDatabaseOwner(cnn, cancellationToken);
        }

    }

}
