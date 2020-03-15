﻿using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cogito.SqlServer.Deployment.Tests
{

    [TestClass]
    public class SqlDeploymentTests
    {

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
            var p = d.Compile();
        }

        [TestMethod]
        public async Task Can_execute_example()
        {
            using var l = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
            var x = XDocument.Load(File.OpenRead("Cogito.SqlServer.Deployment.Tests.Database.xml"));
            var d = SqlDeployment.Load(x);
            var p = d.Compile();

            await new SqlDeploymentSequentialExecutor(p, l.CreateLogger<SqlDeploymentSequentialExecutor>()).ExecuteAsync();
        }

    }

}
