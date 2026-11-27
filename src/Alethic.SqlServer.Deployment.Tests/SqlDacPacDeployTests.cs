using System;
using System.Collections.Generic;
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
        /// The same package deployed with different SQLCMD variable values is a different
        /// deployment, so the values are part of its tag; without any, the tag is the package's
        /// alone, as it always was.
        /// </summary>
        [TestMethod]
        public void Dac_tag_should_follow_sqlcmd_variable_values()
        {
            var dacpac = BuildPackage("AlethicDacTag_" + Guid.NewGuid().ToString("N"));

            try
            {
                var none = SqlDacPacDeploy.GetDacTag(dacpac, null);
                SqlDacPacDeploy.GetDacTag(dacpac, new Dictionary<string, string>()).Should().Be(none);

                var ab = SqlDacPacDeploy.GetDacTag(dacpac, new Dictionary<string, string>() { ["A"] = "1", ["B"] = "2" });
                ab.Should().NotBe(none);

                // the order the values were added in is not part of the deployment
                SqlDacPacDeploy.GetDacTag(dacpac, new Dictionary<string, string>() { ["B"] = "2", ["A"] = "1" }).Should().Be(ab);

                // a changed value, or the same characters split differently between name and value, is a different deployment
                SqlDacPacDeploy.GetDacTag(dacpac, new Dictionary<string, string>() { ["A"] = "1", ["B"] = "3" }).Should().NotBe(ab);
                SqlDacPacDeploy.GetDacTag(dacpac, new Dictionary<string, string>() { ["A"] = "bc" })
                    .Should().NotBe(SqlDacPacDeploy.GetDacTag(dacpac, new Dictionary<string, string>() { ["Ab"] = "c" }));
            }
            finally
            {
                File.Delete(dacpac);
            }
        }

        /// <summary>
        /// The tag decides whether a deployment differs from the last one, so the same package
        /// deployed again with a changed SQLCMD variable value deploys; the version only stops an
        /// older package from being deployed over the database.
        /// </summary>
        [TestMethod]
        public async Task Deploy_should_follow_variable_values_and_refuse_older_packages()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            var databaseName = "AlethicVariableDeploy_" + Guid.NewGuid().ToString("N");
            var dacpac = BuildPackage(databaseName, "2.0.0");
            var older = BuildPackage(databaseName + "_older", "1.0.0", "CREATE TABLE [dbo].[OlderTable] ([Id] INT NOT NULL PRIMARY KEY);");

            try
            {
                await ExecuteAsync($"CREATE DATABASE [{databaseName}]");

                // the same package and version, deployed with each value in turn
                await DeployAsync(dacpac, databaseName, "one");
                var one = await GetPropertyAsync(databaseName, "DACTAG");
                one.Should().Be(SqlDacPacDeploy.GetDacTag(dacpac, Variables("one")));

                await DeployAsync(dacpac, databaseName, "two");
                (await GetPropertyAsync(databaseName, "DACTAG")).Should().Be(SqlDacPacDeploy.GetDacTag(dacpac, Variables("two")));

                // an older package is not deployed over it: the database keeps its version, its tag and its tables
                await DeployAsync(older, databaseName, "two");
                (await GetPropertyAsync(databaseName, "DACVERSION")).Should().Be("2.0.0");
                (await GetPropertyAsync(databaseName, "DACTAG")).Should().Be(SqlDacPacDeploy.GetDacTag(dacpac, Variables("two")));
                (await ScalarAsync(databaseName, "SELECT COUNT(*) FROM sys.tables WHERE name = 'OlderTable'")).Should().Be(0);
            }
            finally
            {
                await ExecuteAsync($"IF DB_ID('{databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{databaseName}]; END");
                File.Delete(dacpac);
                File.Delete(older);
            }
        }

        static Dictionary<string, string> Variables(string value) => new Dictionary<string, string>() { ["Setting"] = value };

        static Task DeployAsync(string dacpac, string databaseName, string value)
        {
            var profile = new DacProfile();
            profile.DeployOptions.AllowIncompatiblePlatform = true;
            profile.DeployOptions.BlockOnPossibleDataLoss = false;
            profile.DeployOptions.SqlCommandVariableValues["Setting"] = value;

            return new SqlDacPacDeploy(dacpac, NullLogger.Instance, SqlPackageLockMode.Server).DeployAsync(InstanceConnectionString, databaseName, profile, false, false);
        }

        static async Task<string> GetPropertyAsync(string databaseName, string name)
        {
            return (string)await ScalarAsync(databaseName, $"SELECT CONVERT(nvarchar(4000), value) FROM sys.extended_properties WHERE class = 0 AND name = '{name}'");
        }

        static async Task<object> ScalarAsync(string databaseName, string sql)
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
            {
                await connection.OpenAsync();
                connection.ChangeDatabase(databaseName);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    return await command.ExecuteScalarAsync();
                }
            }
        }

        /// <summary>
        /// Builds a one-table package with the broker option DacFx applies on a fresh database.
        /// </summary>
        static string BuildPackage(string name, string version = "1.0.0", string sql = "CREATE TABLE [dbo].[FreshDeployTable] ([Id] INT NOT NULL PRIMARY KEY, [Name] NVARCHAR(50) NULL);")
        {
            var path = Path.Combine(Path.GetTempPath(), name + ".dacpac");

            using (var model = new TSqlModel(SqlServerVersion.Sql150, new TSqlModelOptions() { ServiceBrokerOption = ServiceBrokerOption.DisableBroker }))
            {
                model.AddObjects(sql);
                DacPackageExtensions.BuildPackage(path, model, new PackageMetadata() { Name = name, Version = version });
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
