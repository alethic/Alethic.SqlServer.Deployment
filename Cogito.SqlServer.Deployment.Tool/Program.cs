using System;
using System.IO;

using McMaster.Extensions.CommandLineUtils;

namespace Cogito.SqlServer.Deployment.Tool
{

    [Command]
    [Subcommand(typeof(Deploy))]
    [Subcommand(typeof(License))]
    public class Program
    {

        [Command("license", Description = "displays the license")]
        public class License
        {

            public int OnExecute(CommandLineApplication app, IConsole console)
            {
                using var l = new StreamReader(typeof(License).Assembly.GetManifestResourceStream("Cogito.SqlServer.Deployment.Tool.LICENSE"));
                console.WriteLine(l.ReadToEnd());
                return 0;
            }

        }

        public static void Main(string[] args)
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(@"Cogito SQL Server Deployment Tool");
            Console.WriteLine(@"Copyright 2020 Jerome Haltom");
            Console.WriteLine();
            Console.ResetColor();
            CommandLineApplication.Execute<Program>(args);
        }

        /// <summary>
        /// Sets whether or not debug level output will be produced.
        /// </summary>
        [Option("-d | --debug", Description = "enable debug mode", Inherited = true)]
        public bool Debug { get; set; } = false;

        /// <summary>
        /// Executes the command.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="console"></param>
        /// <returns></returns>
        public int OnExecute(CommandLineApplication app, IConsole console)
        {
            console.Error.Write("You must specify a subcommand.");
            app.ShowHelp();
            return 1;
        }

    }

}
