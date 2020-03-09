using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;

namespace Cogito.SqlServer.Deployment.Console
{

    public static class Program
    {

        public static void Main(string[] args)
        {
            var c = new ServerConnection("KYOTO");
            c.Connect();
            var s = new Server(c);
            var z = s.Configuration;
            s.Publi
        }

    }

}
