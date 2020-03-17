using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using McMaster.Extensions.CommandLineUtils;

using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment.Tool
{

    /// <summary>
    /// Executes a SQL Server Deployment.
    /// </summary>
    [Command(Name = "deploy", Description = "Execute SQL Server Deployment")]
    class Deploy
    {

        [Argument(0, "manifest", "deployment manifest file")]
        public string Manifest { get; set; }

        [Option("-t | --target", "target name", CommandOptionType.SingleOrNoValue)]
        public string Target { get; set; }

        [Option("-p | --parameter", "parameter value", CommandOptionType.MultipleValue)]
        public List<string> Parameters { get; set; }

        /// <summary>
        /// Executes the deployment.
        /// </summary>
        /// <returns></returns>
        public async Task<int> OnExecute()
        {
            using var f = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
            var l = f.CreateLogger("SqlDeployment");

            var manifest = Path.IsPathFullyQualified(Manifest) == false ? Path.GetFullPath(Manifest, Environment.CurrentDirectory) : Manifest;
            if (File.Exists(manifest) == false)
            {
                l.LogError("Could not find SQL Deployment manifest file: {Manifest}.", manifest);
                return 1;
            }

            // extract parameters
            var args = Parameters
                .Select(i => i.Split(new[] { '=' }, 2))
                .ToDictionary(i => i[0], i => i.Length > 1 ? i[1] : null);

            // load SQL deployment
            var dply = SqlDeployment.Load(XDocument.Load(manifest, LoadOptions.SetBaseUri));

            try
            {
                var plan = dply.Compile(args, Path.GetDirectoryName(manifest));
                await new SqlDeploymentExecutor(plan, l).ExecuteAsync();
            }
            catch (SqlDeploymentException e)
            {
                l.LogError(e, "Unable to compile or execute SQL deployment.");
                return 1;
            }

            return 0;
        }

    }

}
