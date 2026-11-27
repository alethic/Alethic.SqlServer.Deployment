using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Xml.Linq;

using FluentAssertions;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Alethic.SqlServer.Deployment.Tests
{

    /// <summary>
    /// The User step: what a manifest compiles to, and what it does to a database on a LocalDB
    /// instance. The LocalDB tests are inconclusive when LocalDB is not available on the machine.
    /// </summary>
    [TestClass]
    public class SqlDeploymentDatabaseUserTests
    {

        const string InstanceConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

        /// <summary>
        /// A manifest whose user comes entirely from parameters that default to empty.
        /// </summary>
        const string ManifestXml = @"
<Deployment xmlns=""https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020"">
    <Parameter Name=""DatabaseName"" DefaultValue=""Unused"" />
    <Parameter Name=""UserLogin"" DefaultValue="""" />
    <Parameter Name=""UserName"" DefaultValue="""" />
    <Parameter Name=""UserRole"" DefaultValue="""" />
    <Target Name=""User"">
        <Instance Name=""(localdb)\MSSQLLocalDB"">
            <Database Name=""[DatabaseName]"" Create=""false"">
                <User Login=""[UserLogin]"" Name=""[UserName]"">
                    <Role Name=""[UserRole]"" />
                </User>
            </Database>
        </Instance>
    </Target>
</Deployment>";

        static SqlDeploymentPlan Compile(string database = "Unused", string login = "", string name = "", string role = "")
        {
            return SqlDeployment.Load(XDocument.Parse(ManifestXml)).Compile(new Dictionary<string, string>()
            {
                ["DatabaseName"] = database,
                ["UserLogin"] = login,
                ["UserName"] = name,
                ["UserRole"] = role,
            });
        }

        [TestMethod]
        public void User_without_login_should_compile_no_steps()
        {
            Compile().Targets["User"].Actions.Should().BeEmpty();
        }

        [TestMethod]
        public void User_should_compile_its_name_from_its_login()
        {
            var a = (SqlDeploymentDatabaseUserAction)Compile("Db", "app_login", role: "app").Targets["User"].Actions[0];
            a.DatabaseName.Should().Be("Db");
            a.Login.Should().Be("app_login");
            a.Name.Should().Be("app_login");
            a.Roles.Should().Equal("app");
        }

        [TestMethod]
        public void User_should_compile_its_own_name_and_drop_empty_roles()
        {
            var a = (SqlDeploymentDatabaseUserAction)Compile("Db", "app_login", "app_user").Targets["User"].Actions[0];
            a.Name.Should().Be("app_user");
            a.Roles.Should().BeEmpty();
        }

        [TestMethod]
        public async Task User_should_be_created_for_its_login_and_join_its_role()
        {
            await WithDatabaseAsync(async (database, login) =>
            {
                await ExecuteAsync(database, login, "app");
                (await ScalarAsync(database, $"SELECT name FROM sys.database_principals WHERE sid = SUSER_SID(N'{login}')")).Should().Be(login);
                (await ScalarAsync(database, $"SELECT IS_ROLEMEMBER(N'app', N'{login}')")).Should().Be(1);

                // a second run finds it all in place
                await ExecuteAsync(database, login, "app");
                (await ScalarAsync(database, $"SELECT COUNT(*) FROM sys.database_principals WHERE sid = SUSER_SID(N'{login}')")).Should().Be(1);
            });
        }

        [TestMethod]
        public async Task User_of_its_name_mapped_to_another_login_should_be_mapped_to_its_own()
        {
            await WithDatabaseAsync(async (database, login) =>
            {
                // a user of the login's name, mapped to some other login, as a restore from another server leaves it
                var other = "AlethicOther_" + Guid.NewGuid().ToString("N")[..8];
                await RunAsync("master", $"CREATE LOGIN [{other}] WITH PASSWORD = N'Pr00f-Passw0rd!'");
                try
                {
                    await RunAsync(database, $"CREATE USER [{login}] FOR LOGIN [{other}]");

                    await ExecuteAsync(database, login, "app");
                    (await ScalarAsync(database, $"SELECT CASE WHEN sid = SUSER_SID(N'{login}') THEN 1 ELSE 0 END FROM sys.database_principals WHERE name = N'{login}'")).Should().Be(1);
                    (await ScalarAsync(database, $"SELECT IS_ROLEMEMBER(N'app', N'{login}')")).Should().Be(1);
                }
                finally
                {
                    await RunAsync("master", $"IF SUSER_ID(N'{other}') IS NOT NULL DROP LOGIN [{other}]");
                }
            });
        }

        [TestMethod]
        public async Task User_mapped_to_its_login_under_another_name_should_be_kept()
        {
            await WithDatabaseAsync(async (database, login) =>
            {
                await RunAsync(database, $"CREATE USER [legacy_name] FOR LOGIN [{login}]");

                await ExecuteAsync(database, login, "app");
                (await ScalarAsync(database, $"SELECT name FROM sys.database_principals WHERE sid = SUSER_SID(N'{login}')")).Should().Be("legacy_name");
                (await ScalarAsync(database, "SELECT IS_ROLEMEMBER(N'app', N'legacy_name')")).Should().Be(1);
            });
        }

        [TestMethod]
        public async Task Missing_role_should_fail()
        {
            await WithDatabaseAsync(async (database, login) =>
            {
                Func<Task> execute = () => ExecuteAsync(database, login, "no_such_role");
                await execute.Should().ThrowAsync<SqlDeploymentException>();
            });
        }

        [TestMethod]
        public async Task Missing_login_should_fail_and_create_no_user()
        {
            await WithDatabaseAsync(async (database, login) =>
            {
                Func<Task> execute = () => ExecuteAsync(database, "AlethicMissing_" + Guid.NewGuid().ToString("N")[..8], "app");
                await execute.Should().ThrowAsync<SqlDeploymentException>();
                (await ScalarAsync(database, "SELECT COUNT(*) FROM sys.database_principals WHERE name LIKE N'AlethicMissing[_]%'")).Should().Be(0);
            });
        }

        [TestMethod]
        public async Task Dry_run_should_change_nothing()
        {
            await WithDatabaseAsync(async (database, login) =>
            {
                await ExecuteAsync(database, login, "app", dryRun: true);
                (await ScalarAsync(database, $"SELECT COUNT(*) FROM sys.database_principals WHERE sid = SUSER_SID(N'{login}')")).Should().Be(0);
            });
        }

        /// <summary>
        /// Runs a test against a fresh database holding an [app] role, with a SQL login that has no user
        /// in it, and removes both afterwards.
        /// </summary>
        static async Task WithDatabaseAsync(Func<string, string, Task> test)
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            var suffix = Guid.NewGuid().ToString("N")[..8];
            var database = "AlethicUser_" + suffix;
            var login = "AlethicLogin_" + suffix;

            try
            {
                await RunAsync("master", $"CREATE DATABASE [{database}]");
                await RunAsync(database, "CREATE ROLE [app]");
                await RunAsync("master", $"CREATE LOGIN [{login}] WITH PASSWORD = N'Pr00f-Passw0rd!'");
                await test(database, login);
            }
            finally
            {
                await RunAsync("master", $"IF DB_ID('{database}') IS NOT NULL BEGIN ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{database}]; END");
                await RunAsync("master", $"IF SUSER_ID(N'{login}') IS NOT NULL DROP LOGIN [{login}]");
            }
        }

        static Task ExecuteAsync(string database, string login, string role, bool dryRun = false)
        {
            return new SqlDeploymentExecutor(Compile(database, login, role: role), NullLogger.Instance, dryRun).ExecuteAsync("User");
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

        static async Task<object> ScalarAsync(string database, string sql)
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
            {
                await connection.OpenAsync();
                connection.ChangeDatabase(database);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    return await command.ExecuteScalarAsync();
                }
            }
        }

        static async Task RunAsync(string database, string sql)
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
            {
                await connection.OpenAsync();
                connection.ChangeDatabase(database);

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

    }

}
