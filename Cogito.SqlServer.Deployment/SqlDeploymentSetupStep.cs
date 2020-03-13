using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Security;
using System.Security.Cryptography;
using System.Security.Principal;
using System.ServiceProcess;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;
using Cogito.Threading;

using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures that a SQL server instance is properly installed.
    /// </summary>
    public class SqlDeploymentSetupStep : SqlDeploymentStep
    {

        /// <summary>
        /// Returns <c>true</c> if the currnet user is a local administrator.
        /// </summary>
        static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

        const string DEFAULT_INSTANCE_NAME = @"MSSQLSERVER";
        const string SQL_EXPRESS_URI = @"https://download.microsoft.com/download/2/1/6/216eb471-e637-4517-97a6-b247d8051759/SQL2019-SSEI-Expr.exe";
        const string SQL_EXPRESS_MD5 = "5B232C8BB56935B9E99A09D97D3494EA";
        static readonly Lazy<Task<string>> SETUP = new Lazy<Task<string>>(() => Task.Run(() => GetSqlExpressInstaller()), true);
        static readonly AsyncLock SYNC = new AsyncLock();

        /// <summary>
        /// Gets the path to the SQL Express installer.
        /// </summary>
        /// <returns></returns>
        static async Task<string> GetSqlExpressInstaller()
        {
            // temporary file
            var f = Path.Combine(Path.GetTempPath(), "SQL2019-SSEI-Expr.exe");

            // if file does not match delete
            if (File.Exists(f))
                if (MD5SumOfFile(f) != SQL_EXPRESS_MD5)
                    File.Delete(f);

            if (File.Exists(f) == false)
            {
                // download new file
                using (var http = new HttpClient())
                {
                    using (var s = await new HttpClient().GetStreamAsync(SQL_EXPRESS_URI))
                    using (var o = File.OpenWrite(f))
                        await s.CopyToAsync(o);
                }
            }

            // if file does not match delete
            if (File.Exists(f))
                if (MD5SumOfFile(f) != SQL_EXPRESS_MD5)
                    File.Delete(f);

            // still not there?
            if (File.Exists(f) == false)
                throw new FileNotFoundException("Could not download SQL Express Installer.");

            // return final path
            return f;
        }

        /// <summary>
        /// Returns the MD5 sum of the given file.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static string MD5SumOfFile(string path)
        {
            if (File.Exists(path))
                using (var stm = File.OpenRead(path))
                    return BytesToHex(MD5.Create().ComputeHash(stm));

            throw new FileNotFoundException();
        }

        /// <summary>
        /// Converts the given byte array to hex notation.
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        static string BytesToHex(byte[] bytes)
        {
            var c = new char[bytes.Length * 2];

            for (var i = 0; i < bytes.Length; i++)
            {
                var b = bytes[i] >> 4;
                c[i * 2] = (char)(55 + b + (((b - 10) >> 31) & -7));
                b = bytes[i] & 0xF;
                c[i * 2 + 1] = (char)(55 + b + (((b - 10) >> 31) & -7));
            }

            return new string(c);
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="exe"></param>
        public SqlDeploymentSetupStep(string instanceName, string exe = null) :
            base(instanceName)
        {
            Exe = exe;
        }

        /// <summary>
        /// Gets the path to the SQL server setup binary.
        /// </summary>
        public string Exe { get; }

        public override Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            return CreateInstance(InstanceName);
        }

        /// <summary>
        /// Attempts to locate the existing SQL server instance, or creates it.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        async Task CreateInstance(string instanceName)
        {
            if (instanceName == null)
                throw new ArgumentNullException(nameof(instanceName));

            // attempt to refresh server name
            var serverName = await TryGetServerName(instanceName) ?? instanceName;

            // parse instance name
            var m = serverName.Split('\\');
            var host = m.Length >= 1 ? m[0].TrimOrNull() : null;
            var name = m.Length >= 2 ? m[1].TrimOrNull() : null;

            // instance requires host name
            if (host == null)
                throw new InvalidOperationException("Missing host name for instance.");

            // fallback to default
            if (name == null)
                name = DEFAULT_INSTANCE_NAME;

            // instance is local
            if (GetLocalServerNames().Any(i => host.Equals(i, StringComparison.OrdinalIgnoreCase)))
            {
                // install instance if missing
                if (GetLocalInstances().Contains(serverName, StringComparer.OrdinalIgnoreCase) == false)
                    await InstallSqlServer(name);

                // test connection and return instance
                if (await TryGetServerName(instanceName) is string s)
                {
                    // required for deployment
                    if (await IsSysAdmin(instanceName) != true)
                        throw new InvalidOperationException("Unable to verify membership in sysadmin role.");

                    await ConfigureSqlAgent(instanceName);
                    return;
                }

                throw new InvalidOperationException("Could not establish connection to local server.");
            }
            else
            {
                // test connection and return instance
                if (await TryGetServerName(instanceName) is string s)
                {
                    // required for deployment
                    if (await IsSysAdmin(instanceName) != true)
                        throw new InvalidOperationException("Unable to verify membership in sysadmin role.");

                    await ConfigureSqlAgent(instanceName);
                    return;
                }

                throw new NotSupportedException("Unable to connect to remote instance, and creation of remote instance is not supported.");
            }
        }

        /// <summary>
        /// Returns <c>true</c> if we're logged in as a member of the sysadmin role.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns></returns>
        async Task<bool> IsSysAdmin(string dataSource)
        {
            using var cnn = await OpenConnectionAsync(dataSource);
            return await cnn.ExecuteScalarAsync("SELECT IS_SRVROLEMEMBER('sysadmin')") is int i && i == 1;
        }

        /// <summary>
        /// Returns a new open connection to the specified data source.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns></returns>
        async Task<SqlConnection> OpenConnectionAsync(string dataSource)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));

            var b = new SqlConnectionStringBuilder
            {
                DataSource = dataSource,
                IntegratedSecurity = true,
            };

            var cnn = new SqlConnection(b.ConnectionString);
            await cnn.OpenAsync();
            return cnn;
        }

        /// <summary>
        /// Ensure the SQL agent is configured properly.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns></returns>
        async Task ConfigureSqlAgent(string dataSource)
        {
            if (dataSource == null)
                throw new ArgumentNullException(nameof(dataSource));

            // ensure agent extended procedures are enabled
            using var cnn = await OpenConnectionAsync(dataSource);
            await cnn.ExecuteNonQueryAsync(@"sp_configure 'show advanced options', 1");
            await cnn.ExecuteNonQueryAsync(@"RECONFIGURE WITH OVERRIDE");
            await cnn.ExecuteNonQueryAsync(@"sp_configure 'Agent XPs', 1");
            await cnn.ExecuteNonQueryAsync(@"RECONFIGURE WITH OVERRIDE");

            if (IsAdmin)
            {
                // start agent if stopped
                using (var controller = new ServiceController(await GetSqlAgentServiceName(cnn), await cnn.GetFullyQualifiedServerName()))
                    if (controller.Status == ServiceControllerStatus.Stopped)
                        controller.Start();
            }
        }

        /// <summary>
        /// Gets the SQL agent service name.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        async Task<string> GetSqlAgentServiceName(SqlConnection connection)
        {
            var n = (await connection.GetServerPropertyAsync("InstanceName"))?.TrimOrNull() ?? "MSSQLSERVER";
            var s = n == "MSSQLSERVER" ? "SQLSERVERAGENT" : "SQLAgent$" + n;
            return s;
        }

        /// <summary>
        /// Attempts to get the server name.
        /// </summary>
        /// <param name="dataSource"></param>
        /// <returns></returns>
        async Task<string> TryGetServerName(string dataSource)
        {
            try
            {
                // pull actual instance name from server itself
                using var cnn = await OpenConnectionAsync(dataSource);
                return (string)await cnn.ExecuteScalarAsync((string)$@"SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(256))");
            }
            catch (SqlException)
            {
                return null;
            }
        }

        /// <summary>
        /// Ensures the specified SQL server instance is installed.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        async Task InstallSqlServer(string instanceName)
        {
            // one instance at a time
            using (await SYNC.LockAsync())
            {
                if (Exe != null)
                {
                    if (File.Exists(Exe) == false)
                        throw new FileNotFoundException($"Unable to find {Exe}.");

                    // run setup
                    var exitCode = await RunInstallAction(Exe, instanceName);
                    if (exitCode != 0)
                        throw new InvalidOperationException($"Setup exited with exit code {exitCode}.");
                }
                else
                {
                    // lets run the setup
                    var setup = await SETUP.Value;
                    var media = Path.Combine(Path.GetTempPath(), "SQLServer2019Media");

                    // run setup to download installer files
                    var downloadExitCode = await RunSqlExeAsync(setup, new Dictionary<string, string>()
                    {
                        ["/Q"] = null,
                        ["/ACTION"] = "Download",
                        ["/MEDIAPATH"] = media,
                        ["/MEDIATYPE"] = "Advanced",
                    });
                    if (downloadExitCode != 0)
                        throw new InvalidOperationException($"Setup exited with exit code {downloadExitCode}.");

                    // run setup
                    var exitCode = await RunInstallAction(Path.Combine(media, "SQLEXPRADV_x64_ENU.exe"), instanceName);
                    if (exitCode != 0)
                        throw new InvalidOperationException($"Setup exited with exit code {exitCode}.");
                }
            }
        }

        /// <summary>
        /// Runs the actual install executable.
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        async Task<int> RunInstallAction(string executable, string instanceName)
        {
            if (executable == null)
                throw new ArgumentNullException(nameof(executable));
            if (instanceName == null)
                throw new ArgumentNullException(nameof(instanceName));

            return await RunSqlExeAsync(executable, new Dictionary<string, string>()
            {
                ["/Q"] = null,
                ["/ACTION"] = "Install",
                ["/FEATURES"] = "SQL",
                ["/INSTANCENAME"] = instanceName,
                ["/SQLSYSADMINACCOUNTS"] = System.Security.Principal.WindowsIdentity.GetCurrent().Name,
                ["/IACCEPTSQLSERVERLICENSETERMS"] = null,
            });
        }

        /// <summary>
        /// Runs the given executable with the given named arguments.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        async Task<int> RunSqlExeAsync(string path, Dictionary<string, string> arguments)
        {
            if (new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator) == false)
                throw new SecurityException("Adminstrator access is required to install SQL server locally.");

            return await RunExeAsync(
                path,
                string.Join(" ", arguments.Select(i => i.Key + (i.Value != null ? ("=\"" + i.Value + "\"") : ""))));
        }

        /// <summary>
        /// Runs the given executable and returns the result.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        async Task<int> RunExeAsync(string path, string arguments)
        {
            // start process and wait for exit
            var p = Process.Start(path, arguments);
            await Task.Run(() => p.WaitForExit());
            return p.ExitCode;
        }

        /// <summary>
        /// Gets the names known to be local server names.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetLocalServerNames()
        {
            yield return "localhost";
            yield return "(local)";
            yield return ".";
            yield return Environment.MachineName.ToLower();

            if (GetFqdnLocalServerName() is string fqdn)
                yield return fqdn.ToLower();
        }

        /// <summary>
        /// Gets the fully qualified local server name.
        /// </summary>
        /// <returns></returns>
        string GetFqdnLocalServerName()
        {
            try
            {
                var d = IPGlobalProperties.GetIPGlobalProperties().DomainName;
                var h = Dns.GetHostName();

                d = "." + d;
                if (h.EndsWith(d) == false)
                    h += d;

                return h;
            }
            catch
            {
                // ignore
            }

            return null;
        }

        /// <summary>
        /// Gets the locally installed SQL instances.
        /// </summary>
        /// <returns></returns>
        IEnumerable<string> GetLocalInstances()
        {
            var registryView = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;

            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
            {
                var instanceKey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL", false);
                if (instanceKey != null)
                    foreach (var instanceName in instanceKey.GetValueNames())
                        yield return Environment.MachineName + @"\" + instanceName ?? DEFAULT_INSTANCE_NAME;
            }
        }

    }

}
