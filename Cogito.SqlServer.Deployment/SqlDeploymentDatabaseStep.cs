using System;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// 
    /// </summary>
    public class SqlDeploymentDatabaseStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        public SqlDeploymentDatabaseStep(string instanceName, string name) :
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
        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using (var cnn = await OpenConnectionAsync(cancellationToken))
                if ((string)await cnn.ExecuteScalarAsync($"SELECT name FROM sys.databases WHERE name = {Name}") == null)
                    await cnn.ExecuteNonQueryAsync((string)$"CREATE DATABASE [{Name}]");
        }

    }

}
