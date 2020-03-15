using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment.Tool
{

    public static class Program
    {

        public static void Main(string[] args)
        {
            var cmd = new CommandLineApplication();

            cmd.Command("deploy", deploy =>
            {
                deploy.HelpOption("-? | -h | --help");
                deploy.Description = "Execute SQL Server Deployment";
                var input = deploy.Argument("input", "Deployment XML file");
                var properties = deploy.Option("-p | --parameter", "Parameter value", CommandOptionType.MultipleValue);
                deploy.OnExecute(async () => await InvokeDeployAsync(input, properties));
            });

            cmd.HelpOption("-? | -h | --help");
            cmd.Execute(args);

#if DEBUG
            Console.ReadLine();
#endif
        }

        static async Task<int> InvokeDeployAsync(CommandArgument input, CommandOption parameters)
        {
            using var f = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
            var l = f.CreateLogger("SqlDeployment");

            if (File.Exists(input.Value) == false)
            {
                l.LogError("Could not find SQL Deployment XML file.", input.Value);
                return 1;
            }

            // extract parameters
            var args = parameters.Values
                .Select(i => i.Split(new[] { '=' }, 2))
                .ToDictionary(i => i[0], i => i.Length > 1 ? i[1] : null);

            // load SQL deployment
            var dply = SqlDeployment.Load(XDocument.Load(input.Value));

            try
            {
                var plan = dply.Compile(args);
                await new SqlDeploymentSequentialExecutor(plan, l).ExecuteAsync();
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
