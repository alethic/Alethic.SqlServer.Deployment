using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cogito.SqlServer.Deployment.Tests
{

    [TestClass]
    public class SqlDeploymentTests
    {

        public TestContext TestContext { get; set; }

        Dictionary<string, string> GetArgs()
        {
            return new Dictionary<string, string>()
            {
                ["SetupExePath"] = (string)TestContext.Properties["SqlSetupExePath"],
            };
        }

        [TestMethod]
        public void Can_load_example()
        {
            var x = XDocument.Load(File.OpenRead("Cogito.SqlServer.Deployment.Tests.Database.xml"));
            var d = SqlDeployment.Load(x);
        }

        [TestMethod]
        public void Can_compile_example()
        {
            var x = XDocument.Load(File.OpenRead("Cogito.SqlServer.Deployment.Tests.Database.xml"));
            var d = SqlDeployment.Load(x);
            var p = d.Compile(GetArgs());
        }

        [TestMethod]
        public async Task Can_execute_example()
        {
            using var l = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
            var x = XDocument.Load(File.OpenRead("Cogito.SqlServer.Deployment.Tests.Database.xml"));
            var d = SqlDeployment.Load(x);
            var p = d.Compile(GetArgs());

            await new SqlDeploymentSequentialExecutor(p, l.CreateLogger<SqlDeploymentSequentialExecutor>()).ExecuteAsync();
        }

    }

}
