using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures the existence of a database.
    /// </summary>
    public class SqlDeploymentCreateDatabaseAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="name"></param>
        public SqlDeploymentCreateDatabaseAction(SqlInstance instance, string name, string defaultDataFilePath, string defaultLogFilePath, bool overwrite) :
            base(instance)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DefaultDataFilePath = defaultDataFilePath;
            DefaultLogFilePath = defaultLogFilePath;
            Overwrite = overwrite;
        }

        /// <summary>
        /// Gets the name of the database.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the default path to the data file.
        /// </summary>
        public string DefaultDataFilePath { get; }

        /// <summary>
        /// Gets the default path to the log file.
        /// </summary>
        public string DefaultLogFilePath { get; }

        /// <summary>
        /// Gets whehter to overwrite existing files.
        /// </summary>
        public bool Overwrite { get; }

        /// <summary>
        /// Creates the database.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            if ((string)await cnn.ExecuteScalarAsync($"SELECT name FROM sys.databases WHERE name = {Name}") == null)
                await CreateDatabase(context, cnn, cancellationToken);
        }

        async Task CreateDatabase(SqlDeploymentExecuteContext context, SqlConnection cnn, CancellationToken cancellationToken)
        {
            var defaultDataFilePath = DefaultDataFilePath ?? Path.Combine(await cnn.GetServerPropertyAsync("InstanceDefaultDataPath", cancellationToken), Name + ".mdf");
            var dataFileExistsResult = await cnn.ExecuteXpFileExist(defaultDataFilePath, cancellationToken);
            if (dataFileExistsResult.FileExists == 1 && Overwrite)
            {
                await cnn.ExecuteXpDeleteFiles(new[] { defaultDataFilePath }, cancellationToken);
                dataFileExistsResult = await cnn.ExecuteXpFileExist(defaultDataFilePath, cancellationToken);
            }

            if (dataFileExistsResult.FileExists == 1)
                throw new SqlDeploymentException($"Data file '{defaultDataFilePath}' already exists.");

            var defaultLogFilePath = DefaultLogFilePath ?? Path.Combine(await cnn.GetServerPropertyAsync("InstanceDefaultLogPath", cancellationToken), Name + "_log.ldf");
            var logFileExistsResult = await cnn.ExecuteXpFileExist(defaultLogFilePath, cancellationToken);
            if (logFileExistsResult.FileExists == 1 && Overwrite)
            {
                await cnn.ExecuteXpDeleteFiles(new[] { defaultLogFilePath }, cancellationToken);
                logFileExistsResult = await cnn.ExecuteXpFileExist(defaultLogFilePath, cancellationToken);
            }

            if (logFileExistsResult.FileExists == 1)
                throw new SqlDeploymentException($"Log file '{defaultLogFilePath}' already exists.");

            context.Logger.LogInformation("Creating database {DatabaseName}.", Name);
            await cnn.ExecuteNonQueryAsync((string)$"CREATE DATABASE [{Name}]", cancellationToken: cancellationToken);
        }

    }

}
