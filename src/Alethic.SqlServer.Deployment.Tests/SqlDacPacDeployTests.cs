using System;
using System.IO;
using System.Threading.Tasks;

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.SqlServer.Dac;
using Microsoft.SqlServer.Dac.Model;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Alethic.SqlServer.Deployment.Tests
{

    /// <summary>
    /// Deploys a package built on the fly to a LocalDB instance. Skips when LocalDB is not
    /// available on the machine.
    /// </summary>
    [TestClass]
    public class SqlDacPacDeployTests
    {

        const string InstanceConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

        /// <summary>
        /// Deploying into a database that was just created, the way a plan's Database element
        /// creates it before its Package deploys, must succeed in one pass. DacFx sets the options
        /// of such a database WITH ROLLBACK IMMEDIATE, and SET DISABLE_BROKER kills every other
        /// session inside the database: a deploy connection left parked there fails right after
        /// the deploy with "the connection is broken and recovery is not possible", and so does
        /// the lock release.
        /// </summary>
        [TestMethod]
        public async Task Deploy_into_newly_created_database_should_succeed_in_one_pass()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            var databaseName = "AlethicFreshDeploy_" + Guid.NewGuid().ToString("N");
            var dacpac = BuildPackage(databaseName);

            try
            {
                await ExecuteAsync($"CREATE DATABASE [{databaseName}]");

                var profile = new DacProfile();
                profile.DeployOptions.AllowIncompatiblePlatform = true;
                profile.DeployOptions.BlockOnPossibleDataLoss = false;

                var deploy = new SqlDacPacDeploy(dacpac, NullLogger.Instance, SqlPackageLockMode.Server);
                await deploy.DeployAsync(InstanceConnectionString, databaseName, profile, false, false);

                // the stamps after the deploy prove the connection that finished up was alive
                using (var connection = new SqlConnection(InstanceConnectionString))
                {
                    await connection.OpenAsync();
                    connection.ChangeDatabase(databaseName);

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM sys.extended_properties WHERE class = 0 AND name IN ('DACTAG', 'DACVERSION')";
                        ((int)await command.ExecuteScalarAsync()).Should().Be(2);
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT COUNT(*) FROM sys.tables WHERE name = 'FreshDeployTable'";
                        ((int)await command.ExecuteScalarAsync()).Should().Be(1);
                    }
                }

                // a second deploy of the same package stops at the matching tag
                await deploy.DeployAsync(InstanceConnectionString, databaseName, profile, false, false);
            }
            finally
            {
                await ExecuteAsync($"IF DB_ID('{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
                File.Delete(dacpac);
            }
        }

        /// <summary>
        /// Builds a one-table package with the broker option DacFx applies on a fresh database.
        /// </summary>
        static string BuildPackage(string name)
        {
            var path = Path.Combine(Path.GetTempPath(), name + ".dacpac");

            using (var model = new TSqlModel(SqlServerVersion.Sql150, new TSqlModelOptions() { ServiceBrokerOption = ServiceBrokerOption.DisableBroker }))
            {
                model.AddObjects("CREATE TABLE [dbo].[FreshDeployTable] ([Id] INT NOT NULL PRIMARY KEY, [Name] NVARCHAR(50) NULL);");
                DacPackageExtensions.BuildPackage(path, model, new PackageMetadata() { Name = name, Version = "1.0.0" });
            }

            return path;
        }

        static async Task<bool> TryOpenAsync()
        {
            try
            {
                using (var connection = new SqlConnection(InstanceConnectionString))
                {
                    await connection.OpenAsync();
                    return true;
                }
            }
            catch (SqlException)
            {
                return false;
            }
        }

        static async Task ExecuteAsync(string sql)
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

    }

}
