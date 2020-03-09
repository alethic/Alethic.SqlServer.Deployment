using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

using Autofac;

using Cogito.Autofac;

using MartinCostello.SqlLocalDb;

using Microsoft.Extensions.Options;

using Serilog;

namespace Cogito.SqlServer.Deployment.Tests.Framework
{

    /// <summary>
    /// Injects a virtual SQL environment into the container which makes available locally deployed copies of the
    /// FSX SQL Server set.
    /// </summary>
    public class SqlTestFrameworkModule : ModuleBase
    {

        /// <summary>
        /// Gets information for the SqlEnvironment to associate to. If running from Visual Studio, a non
        /// temporary environment is requested.
        /// </summary>
        /// <returns></returns>
        static (int EnvironmentId, bool Temporary) SqlEnvironmentInfo(ILogger logger = null)
        {
            using (var md5 = MD5.Create())
            {
                var (text, temp) = GetEnvironmentData();
                logger?.Information("SQL Environment Hash Text: {Text}", text);
                return (BitConverter.ToInt32(md5.ComputeHash(Encoding.UTF8.GetBytes(text)), 0), temp);
            }
        }

        /// <summary>
        /// Gets the hash data and temporary value for the environment.
        /// </summary>
        /// <returns></returns>
        static (string EnvironmentHashText, bool Temporary) GetEnvironmentData()
        {
            var hash = new StringBuilder();
            hash.AppendLine(Environment.UserDomainName);
            hash.AppendLine(Environment.UserName);

            // if running inside VS use an instance unique to the VS version and solution
            var v = Environment.GetEnvironmentVariable("VSAPPIDNAME");
            if (!string.IsNullOrWhiteSpace(v))
            {
                hash.AppendLine(v);
                hash.AppendLine(Environment.GetEnvironmentVariable("VSAPPIDDIR") ?? "");
                hash.AppendLine(Environment.GetEnvironmentVariable("SolutionPath") ?? "");
                hash.AppendLine(DateTime.Today.Ticks.ToString()); // new instance each day
                return (hash.ToString(), false);
            }

            // we're running inside vstest console, share
            var vstestConsole = GetParentProcesses(Process.GetCurrentProcess())
                .FirstOrDefault(i => i.ProcessName.Equals("vstest.console", StringComparison.OrdinalIgnoreCase));
            if (vstestConsole != null)
                hash.AppendLine(vstestConsole.Id.ToString());

            return (hash.ToString(), true);
        }

        /// <summary>
        /// Returns an iteration of parent processes.
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        static IEnumerable<Process> GetParentProcesses(Process process)
        {
            var parent = process.GetParentProcess();
            if (parent != null)
            {
                // yield this parent
                yield return parent;

                // recurse into parents
                foreach (var p in GetParentProcesses(parent))
                    yield return p;
            }
        }

        /// <summary>
        /// Gets the existing SQL test environment or creates one.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        SqlTestEnvironmentInfo CreateSqlEnvironmentInfo(IComponentContext context)
        {
            var (id, temporary) = SqlEnvironmentInfo(context.Resolve<ILogger>());
            return new SqlTestEnvironmentInfo(id, temporary);
        }

        string ConnectionStringFor(IComponentContext context, string instanceId, string databaseId)
        {
            var p = context.Resolve<SqlEnvironmentDatabaseProvider>();
            var t = p.GetDatabaseAsync(instanceId, databaseId);
            return t.GetAwaiter().GetResult().ConnectionString;
        }

        string ConnectionStringForSQL(IComponentContext context, string databaseId)
        {
            return ConnectionStringFor(context, "SQL", databaseId);
        }

        string ConnectionStringForWHSE(IComponentContext context, string databaseId)
        {
            return ConnectionStringFor(context, "WHSE", databaseId);
        }

        string ConnectionStringForEFM(IComponentContext context, string databaseId)
        {
            return ConnectionStringFor(context, "EFM", databaseId);
        }

        string ConnectionStringForDM(IComponentContext context, string databaseId)
        {
            return ConnectionStringFor(context, "DM", databaseId);
        }

        protected override void Register(ContainerBuilder builder)
        {
            builder.RegisterModule<SqlEnvironmentModule>();

            builder.RegisterFromAttributes(typeof(SqlTestFrameworkModule).Assembly);

            builder.RegisterInstance(Options.Create(new SqlLocalDbOptions() { AutomaticallyDeleteInstanceFiles = true }));
            builder.RegisterType<SqlLocalDbApi>().As<ISqlLocalDbApi>();

            builder.Register(ctx => new SqlEnvironmentConfig() { DistributorAdminPassword = "BgtG4r2VdHnVWzMwxLrYa763D6621jKx" });
            builder.Register(ctx => CreateSqlEnvironmentInfo(ctx)).AsSelf().SingleInstance();

            // register configuration for virtual FSX SQL environment
        }

    }

}
