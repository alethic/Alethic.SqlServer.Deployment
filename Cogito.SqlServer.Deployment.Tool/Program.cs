using System;
using System.Threading.Tasks;

using McMaster.Extensions.CommandLineUtils;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment.Tool
{

    [Command]
    [HelpOption]
    [Subcommand(typeof(Deploy))]
    [Subcommand(typeof(License))]
    public partial class Program
    {

        /// <summary>
        /// Main application entry point.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task<int> Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"Cogito SQL Server Deployment Tool");
            Console.WriteLine(@"Copyright 2020 Jerome Haltom");
            Console.WriteLine();
            Console.ResetColor();

            return await new HostBuilder()
                .ConfigureLogging(o => o.AddConsole().SetMinimumLevel(LogLevel.Information))
                .ConfigureServices((ctx, svc) => svc.AddSingleton(PhysicalConsole.Singleton))
                .RunCommandLineApplicationAsync<Program>(args);
        }

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="console"></param>
        /// <returns></returns>
        public int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.Error.WriteLine("You must specify a subcommand.");
            console.Error.WriteLine();
            app.ShowHelp();
            return 1;
        }

    }

}
