
using McMaster.Extensions.CommandLineUtils;

namespace Cogito.SqlServer.Deployment.Tool
{

    [Command]
    [Subcommand(typeof(Deploy))]
    public class Program
    {

        public static void Main(string[] args) => CommandLineApplication.Execute<Program>(args);

    }

}
