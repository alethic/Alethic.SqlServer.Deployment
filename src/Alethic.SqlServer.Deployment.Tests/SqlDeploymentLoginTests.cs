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
    /// The Login step: what a manifest compiles to, and what it does to a LocalDB instance. The
    /// LocalDB tests are inconclusive when LocalDB is not available on the machine.
    /// </summary>
    [TestClass]
    public class SqlDeploymentLoginTests
    {

        const string InstanceConnectionString = @"Data Source=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True";

        /// <summary>
        /// A manifest whose login comes entirely from parameters that default to empty.
        /// </summary>
        const string ManifestXml = @"
<Deployment xmlns=""https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020"">
    <Parameter Name=""LoginName"" DefaultValue="""" />
    <Parameter Name=""LoginType"" DefaultValue="""" />
    <Parameter Name=""LoginPassword"" DefaultValue="""" />
    <Target Name=""Login"">
        <Instance Name=""(localdb)\MSSQLLocalDB"">
            <Login Name=""[LoginName]"" Type=""[LoginType]"" Password=""[LoginPassword]"" />
        </Instance>
    </Target>
</Deployment>";

        static SqlDeploymentPlan Compile(string name = "", string type = "", string password = "")
        {
            return SqlDeployment.Load(XDocument.Parse(ManifestXml)).Compile(new Dictionary<string, string>()
            {
                ["LoginName"] = name,
                ["LoginType"] = type,
                ["LoginPassword"] = password,
            });
        }

        [TestMethod]
        public void Login_without_name_should_compile_no_steps()
        {
            Compile().Targets["Login"].Actions.Should().BeEmpty();
        }

        [TestMethod]
        public void Login_should_compile_name_type_and_password()
        {
            var a = (SqlDeploymentLoginAction)Compile("app", "sql", "secret").Targets["Login"].Actions[0];
            a.Name.Should().Be("app");
            a.Type.Should().Be(SqlDeploymentLoginType.Sql);
            a.Password.Should().Be("secret");
        }

        [TestMethod]
        public void Login_empty_password_should_compile_as_absent()
        {
            var a = (SqlDeploymentLoginAction)Compile("app", "Sql").Targets["Login"].Actions[0];
            a.Password.Should().BeNull();
        }

        [TestMethod]
        public void Login_without_type_should_fail_compile()
        {
            Action compile = () => Compile("app");
            compile.Should().Throw<SqlDeploymentException>();
        }

        [TestMethod]
        public void Login_with_unknown_type_should_fail_compile()
        {
            Action compile = () => Compile("app", "1");
            compile.Should().Throw<SqlDeploymentException>();
        }

        [TestMethod]
        public void Windows_login_with_password_should_fail_compile()
        {
            Action compile = () => Compile(@"DOMAIN\app$", "Windows", "secret");
            compile.Should().Throw<SqlDeploymentException>();
        }

        [TestMethod]
        public async Task Sql_login_should_be_created_and_its_password_kept_in_line()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            var name = "AlethicLogin_" + Guid.NewGuid().ToString("N");

            try
            {
                // missing, with a password: created with it
                await ExecuteAsync(name, "Sql", "Fi7st-Passw0rd!");
                (await ScalarAsync($"SELECT type FROM sys.server_principals WHERE name = N'{name}'")).Should().Be("S");
                (await PasswordMatchesAsync(name, "Fi7st-Passw0rd!")).Should().BeTrue();

                // existing, with another password: set to it
                await ExecuteAsync(name, "Sql", "Sec0nd-Passw0rd!");
                (await PasswordMatchesAsync(name, "Sec0nd-Passw0rd!")).Should().BeTrue();

                // existing, without a password: only validated, the password left as it is
                await ExecuteAsync(name, "Sql");
                (await PasswordMatchesAsync(name, "Sec0nd-Passw0rd!")).Should().BeTrue();
            }
            finally
            {
                await ExecuteNonQueryAsync($"IF SUSER_ID(N'{name}') IS NOT NULL DROP LOGIN [{name}]");
            }
        }

        [TestMethod]
        public async Task Missing_sql_login_without_password_should_fail()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            var name = "AlethicLogin_" + Guid.NewGuid().ToString("N");

            Func<Task> execute = () => ExecuteAsync(name, "Sql");
            await execute.Should().ThrowAsync<SqlDeploymentException>();
            (await ScalarAsync($"SELECT SUSER_ID(N'{name}')")).Should().Be(DBNull.Value);
        }

        [TestMethod]
        public async Task Dry_run_should_not_create_a_login()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            var name = "AlethicLogin_" + Guid.NewGuid().ToString("N");

            await ExecuteAsync(name, "Sql", "Fi7st-Passw0rd!", dryRun: true);
            (await ScalarAsync($"SELECT SUSER_ID(N'{name}')")).Should().Be(DBNull.Value);
        }

        [TestMethod]
        public async Task Existing_windows_login_should_be_validated()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            // the account running the tests owns the LocalDB instance, so its Windows login exists
            var name = (string)await ScalarAsync("SELECT SUSER_SNAME()");

            await ExecuteAsync(name, "Windows");
        }

        [TestMethod]
        public async Task Login_of_another_type_should_fail_and_be_left_alone()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            var name = (string)await ScalarAsync("SELECT SUSER_SNAME()");

            Func<Task> execute = () => ExecuteAsync(name, "Sql", "Fi7st-Passw0rd!");
            await execute.Should().ThrowAsync<SqlDeploymentException>();
            (await ScalarAsync($"SELECT type FROM sys.server_principals WHERE name = N'{name.Replace("'", "''")}'")).Should().Be("U");
        }

        [TestMethod]
        public async Task Missing_windows_login_should_be_created()
        {
            if (await TryOpenAsync() == false)
                Assert.Inconclusive("LocalDB is not available.");

            const string name = @"NT AUTHORITY\NETWORK SERVICE";
            if (await ScalarAsync($"SELECT SUSER_ID(N'{name}')") is not DBNull)
                Assert.Inconclusive($"{name} already has a login on this instance.");

            try
            {
                await ExecuteAsync(name, "Windows");
                (await ScalarAsync($"SELECT type FROM sys.server_principals WHERE name = N'{name}'")).Should().Be("U");
            }
            finally
            {
                await ExecuteNonQueryAsync($"IF SUSER_ID(N'{name}') IS NOT NULL DROP LOGIN [{name}]");
            }
        }

        static Task ExecuteAsync(string name, string type, string password = "", bool dryRun = false)
        {
            return new SqlDeploymentExecutor(Compile(name, type, password), NullLogger.Instance, dryRun).ExecuteAsync("Login");
        }

        static async Task<bool> PasswordMatchesAsync(string name, string password)
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT PWDCOMPARE(@password, CONVERT(varbinary(256), LOGINPROPERTY(@name, 'PasswordHash')))";
                    command.Parameters.AddWithValue("@password", password);
                    command.Parameters.AddWithValue("@name", name);
                    return await command.ExecuteScalarAsync() is int and 1;
                }
            }
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

        static async Task<object> ScalarAsync(string sql)
        {
            using (var connection = new SqlConnection(InstanceConnectionString))
            {
                await connection.OpenAsync();

                using (var command = connection.CreateCommand())
                {
                    command.CommandText = sql;
                    return await command.ExecuteScalarAsync();
                }
            }
        }

        static async Task ExecuteNonQueryAsync(string sql)
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
