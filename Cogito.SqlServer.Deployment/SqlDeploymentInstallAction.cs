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

using Microsoft.Data.SqlClient;
using Microsoft.Win32;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures that a SQL server instance is properly installed.
    /// </summary>
    public class SqlDeploymentInstallAction : SqlDeploymentAction
    {

        const string DEFAULT_INSTANCE_NAME = @"MSSQLSERVER";
        const string SQL_EXPRESS_URI = @"https://download.microsoft.com/download/2/1/6/216eb471-e637-4517-97a6-b247d8051759/SQL2019-SSEI-Expr.exe";
        const string SQL_EXPRESS_MD5 = "5B232C8BB56935B9E99A09D97D3494EA";

        static readonly AsyncMutex Mutex = new AsyncMutex("Cogito.SqlServer.Deployment.SqlDeploymentInstallAction");
        static readonly Lazy<Task<string>> SetupTask = new Lazy<Task<string>>(() => Task.Run(() => GetSqlExpressInstaller()), true);

        static bool IsAdmin => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

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
        /// <param name="setupExe"></param>
        public SqlDeploymentInstallAction(string instanceName, string setupExe = null) :
            base(instanceName)
        {
            SetupExe = setupExe;
        }

        /// <summary>
        /// Gets the path to the SQL server setup binary.
        /// </summary>
        public string SetupExe { get; }

        /// <summary>
        /// Deploys a new instance of SQL Server.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken)
        {
            using (await Mutex.WaitOneAsync(cancellationToken))
                await ExecuteAsyncInternal(context, cancellationToken);
        }

        /// <summary>
        /// Does the work of installing SQL server.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task ExecuteAsyncInternal(SqlDeploymentExecuteContext context, CancellationToken cancellationToken)
        {
            // acquire breakdown of instance information
            var serverName = await TryGetServerName(cancellationToken) ?? InstanceName;
            var m = serverName.Split(new[] { '\\' }, 2);
            var host = m.Length >= 1 ? m[0].TrimOrNull() : null;
            var name = m.Length >= 2 ? m[1].TrimOrNull() : null;

            // instance requires host name
            if (host == null)
                throw new InvalidOperationException("Missing host name for instance.");

            // fallback to default
            if (name == null)
                name = DEFAULT_INSTANCE_NAME;

            // target is current machine, but missing: install
            if (GetLocalServerNames().Contains(host, StringComparer.OrdinalIgnoreCase))
                if (GetLocalInstanceNames().Contains(name, StringComparer.OrdinalIgnoreCase) == false)
                    await InstallSqlServer(name, cancellationToken);

            // test connection now that installation has completed
            if (await TryGetServerName(cancellationToken) is string s)
            {
                // required for deployment
                if (await IsSysAdmin(cancellationToken) != true)
                    throw new InvalidOperationException($"Unable to verify membership in sysadmin role on '{InstanceName}'.");

                // ensure agent is setup properly
                await ConfigureSqlAgent(cancellationToken);
                return;
            }

            throw new InvalidOperationException($"Could not establish connection SQL Server '{InstanceName}'.");
        }

        /// <summary>
        /// Returns <c>true</c> if we're logged in as a member of the sysadmin role.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<bool> IsSysAdmin(CancellationToken cancellationToken)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);
            return await cnn.ExecuteScalarAsync("SELECT IS_SRVROLEMEMBER('sysadmin')", cancellationToken: cancellationToken) is int i && i == 1;
        }

        /// <summary>
        /// Ensure the SQL agent is configured properly.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task ConfigureSqlAgent(CancellationToken cancellationToken)
        {
            // ensure agent extended procedures are enabled
            using var cnn = await OpenConnectionAsync(cancellationToken);
            await cnn.ExecuteNonQueryAsync(@"sp_configure 'show advanced options', 1", cancellationToken: cancellationToken);
            await cnn.ExecuteNonQueryAsync(@"RECONFIGURE WITH OVERRIDE", cancellationToken: cancellationToken);
            await cnn.ExecuteNonQueryAsync(@"sp_configure 'Agent XPs', 1", cancellationToken: cancellationToken);
            await cnn.ExecuteNonQueryAsync(@"RECONFIGURE WITH OVERRIDE", cancellationToken: cancellationToken);

            if (IsAdmin)
            {
                using var controller = new ServiceController(
                    await GetSqlAgentServiceName(cnn, cancellationToken),
                    await cnn.GetFullyQualifiedServerName(cancellationToken));

                // start agent if stopped
                if (controller.Status == ServiceControllerStatus.Stopped)
                {
                    // schedule in the background to allow cancellation
                    await Task.Run(() =>
                    {
                        controller.Start();
                        controller.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromMinutes(1));
                    }, cancellationToken);
                }
            }
        }

        /// <summary>
        /// Gets the SQL agent service name.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        async Task<string> GetSqlAgentServiceName(SqlConnection connection, CancellationToken cancellationToken)
        {
            var n = (await connection.GetServerPropertyAsync("InstanceName", cancellationToken))?.TrimOrNull() ?? "MSSQLSERVER";
            var s = n == "MSSQLSERVER" ? "SQLSERVERAGENT" : "SQLAgent$" + n;
            return s;
        }

        /// <summary>
        /// Attempts to get the server name.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<string> TryGetServerName(CancellationToken cancellationToken)
        {
            try
            {
                // pull actual instance name from server itself
                using var cnn = await OpenConnectionAsync(cancellationToken);
                return await cnn.GetServerNameAsync(cancellationToken);
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
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task InstallSqlServer(string instanceName, CancellationToken cancellationToken)
        {
            if (IsAdmin == false)
                throw new SqlDeploymentException("Not running as local Administrator.");

            if (SetupExe != null)
            {
                if (File.Exists(SetupExe) == false)
                    throw new FileNotFoundException($"Unable to find {SetupExe}.");

                // run setup
                var exitCode = await RunInstallAction(SetupExe, instanceName, cancellationToken);
                if (exitCode != 0)
                    throw new InvalidOperationException($"Setup exited with exit code {exitCode}.");
            }
            else
            {
                // lets run the setup
                var setup = await SetupTask.Value;
                var media = Path.Combine(Path.GetTempPath(), "SQLServer2019Media");

                // run setup to download installer files
                var downloadExitCode = await RunSqlExeAsync(setup, new Dictionary<string, string>()
                {
                    ["/Q"] = null,
                    ["/ACTION"] = "Download",
                    ["/MEDIAPATH"] = media,
                    ["/MEDIATYPE"] = "Advanced",
                }, cancellationToken);
                if (downloadExitCode != 0)
                    throw new InvalidOperationException($"Setup exited with exit code {downloadExitCode}.");

                // exit after download?
                cancellationToken.ThrowIfCancellationRequested();

                // run setup
                var exitCode = await RunInstallAction(Path.Combine(media, "SQLEXPRADV_x64_ENU.exe"), instanceName, cancellationToken);
                if (exitCode != 0)
                    throw new InvalidOperationException($"Setup exited with exit code {exitCode}.");
            }

            // just to ensure everything is cleared out
            await Task.Delay(TimeSpan.FromSeconds(5));
        }

        /// <summary>
        /// Runs the actual install executable.
        /// </summary>
        /// <param name="executable"></param>
        /// <param name="instanceName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<int> RunInstallAction(string executable, string instanceName, CancellationToken cancellationToken)
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
                ["/SQLSYSADMINACCOUNTS"] = WindowsIdentity.GetCurrent().Name,
                ["/IACCEPTSQLSERVERLICENSETERMS"] = null,
            }, cancellationToken);
        }

        /// <summary>
        /// Runs the given executable with the given named arguments.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        async Task<int> RunSqlExeAsync(string path, Dictionary<string, string> arguments, CancellationToken cancellationToken)
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
        IEnumerable<string> GetLocalInstanceNames()
        {
            var registryView = Environment.Is64BitOperatingSystem ? RegistryView.Registry64 : RegistryView.Registry32;

            using (var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, registryView))
            {
                var instanceKey = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL", false);
                if (instanceKey != null)
                    foreach (var instanceName in instanceKey.GetValueNames())
                        yield return instanceName;
            }
        }

    }

}
