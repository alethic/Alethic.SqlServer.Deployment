﻿using System.IO;

using McMaster.Extensions.CommandLineUtils;

namespace Alethic.SqlServer.Deployment.Tool
{

    [Command("license", Description = "displays the license")]
    public class License
    {

        public int OnExecute(CommandLineApplication app, IConsole console)
        {
            using var l = new StreamReader(typeof(License).Assembly.GetManifestResourceStream("Alethic.SqlServer.Deployment.Tool.LICENSE"));
            console.WriteLine(l.ReadToEnd());
            return 0;
        }

    }

}
