using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Autofac;

using Cogito.Autofac;
using Cogito.SqlServer.Deployment.Tool;

using CommandLine;

using Microsoft.Extensions.Options;

namespace Cogito.SqlServer.Deployment.Deployment.Console
{

    public static class Program
    {

        const string DIST_ADMIN_PWD = "q2pd9CE9F$yKyN4SYVJt";

        static readonly Dictionary<string, string> DEFAULT_INSTANCES = new Dictionary<string, string>()
        {
            ["SQL"] = @"(local)\FSX_SQL",
            ["WHSE"] = @"(local)\FSX_WHSE",
            ["EFM"] = @"(local)\FSX_EFM",
            ["DM"] = @"(local)\FSX_DM",
            ["DI"] = @"(local)\FSX_DI",
            ["DIST"] = @"(local)\FSX_DIST",
        };

        /// <summary>
        /// Options that specify the local instance names.
        /// </summary>
        class InstanceOptions
        {

            [Option('i', "instances", Separator = ',', Default = null)]
            public IEnumerable<string> Instances { get; set; }

            /// <summary>
            /// Path to the SQL server setup files to use for installing instances.
            /// </summary>
            [Option('s', "setup")]
            public string Setup { get; set; }

            /// <summary>
            /// Password to use against the distributor.
            /// </summary>
            [Option("distAdminPwd")]
            public string DistributorAdminPassword { get; set; }

            /// <summary>
            /// Username of the distributor agent.
            /// </summary>
            [Option("distAgentUserName")]
            public string DistributorAgentUserName { get; set; }

            /// <summary>
            /// Password of the distributor agent.
            /// </summary>
            [Option("distAgentPassword")]
            public string DistributorAgentPassword { get; set; }

            /// <summary>
            /// Username of the snapshot agent.
            /// </summary>
            [Option("snapshotAgentUserName")]
            public string SnapshotAgentUserName { get; set; }

            /// <summary>
            /// Password of the snapshot agent.
            /// </summary>
            [Option("snapshotAgentPassword")]
            public string SnapshotAgentPassword { get; set; }

            /// <summary>
            /// Username of the logreader agent.
            /// </summary>
            [Option("logReaderAgentUserName")]
            public string LogReaderAgentUserName { get; set; }

            /// <summary>
            /// Password of the logreader agent.
            /// </summary>
            [Option("logReaderAgentPassword")]
            public string LogReaderAgentPassword { get; set; }

        }

        /// <summary>
        /// Options based to Deploy action.
        /// </summary>
        [Verb("deploy")]
        class DeployOptions : InstanceOptions
        {



        }

        public static void Main(string[] args)
        {
            Parser.Default.ParseArguments(args, new[] {
                typeof(DeployOptions)
            })
                .WithParsed<DeployOptions>(RunDeployAndReturnExitCode);

            System.Console.ReadLine();
        }

        static void RunDeployAndReturnExitCode(DeployOptions o)
        {
            // import instance map
            var map = new Dictionary<string, string>(DEFAULT_INSTANCES);
            foreach (var kvp in o.Instances.Select(i => i.Split(':')).Select(i => (i[0], i[1])))
                map[kvp.Item1] = kvp.Item2;

            var b = new ContainerBuilder();
            b.RegisterAllAssemblyModules();
            b.RegisterInstance(Options.Create(new SqlEnvironmentOptions() { Setup = o.Setup }));
            b.RegisterInstance(GetConfig(o));
            b.RegisterInstance(new SqlInstanceMap(map.Select(i => (i.Key, i.Value))));
            var c = b.Build();

            Run(c).Wait();
        }

        static SqlEnvironmentConfig GetConfig(DeployOptions o)
        {
            var c = new SqlEnvironmentConfig();
            c.DistributorAdminPassword = o.DistributorAdminPassword ?? DIST_ADMIN_PWD;

            if (o.DistributorAgentUserName != null)
                c.DistributorAgentCredential = new NetworkCredential(o.DistributorAgentUserName, o.DistributorAgentPassword);

            if (o.SnapshotAgentUserName != null)
                c.SnapshotAgentCredential = new NetworkCredential(o.SnapshotAgentUserName, o.SnapshotAgentPassword);

            if (o.LogReaderAgentUserName != null)
                c.LogReaderAgentCredential = new NetworkCredential(o.LogReaderAgentUserName, o.LogReaderAgentPassword);

            return c;
        }

        /// <summary>
        /// Runs the currently configured SQL environment builder.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        static async Task Run(IComponentContext context)
        {
            await Task.WhenAll(
                context.Resolve<IEnumerable<SqlEnvironmentDatabaseBuilder>>()
                    .Select(i => context.Resolve<SqlEnvironmentDatabaseProvider>().GetDatabaseAsync(i.InstanceId, i.DatabaseId))
                    .ToList());
        }

    }

}
