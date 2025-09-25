using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Alethic.SqlServer.Deployment.Tests
{

    [TestClass]
    public class SqlDeploymentTests
    {

        public TestContext TestContext { get; set; }

        /// <summary>
        /// Gets the arguments for the test run.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, string> GetArgs()
        {
            return new Dictionary<string, string>()
            {
                ["SetupExePath"] = (string)TestContext.Properties["SqlSetupExePath"],
            };
        }

        [TestMethod]
        public void Can_load_devel_test()
        {
            var x = XDocument.Load(File.OpenRead("devel_test.xml"));
            var d = SqlDeployment.Load(x);
        }

        //[TestMethod]
        //public void Can_compile_devel_test()
        //{
        //    var x = XDocument.Load(File.OpenRead("devel_test.xml"));
        //    var d = SqlDeployment.Load(x);
        //    var p = d.Compile(GetArgs());
        //}

        //[TestMethod]
        //public async Task Can_execute_devel_test()
        //{
        //    using var l = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
        //    var x = XDocument.Load(File.OpenRead("devel_test.xml"));
        //    var d = SqlDeployment.Load(x);
        //    var p = d.Compile(GetArgs());

        //    await new SqlDeploymentExecutor(p, l.CreateLogger<SqlDeploymentExecutor>()).ExecuteAsync();
        //}

        [TestMethod]
        public void Can_load_local_test()
        {
            var x = XDocument.Load(File.OpenRead("local_test.xml"));
            var d = SqlDeployment.Load(x);
        }

        [TestMethod]
        public void Can_compile_local_test()
        {
            var x = XDocument.Load(File.OpenRead("local_test.xml"));
            var d = SqlDeployment.Load(x);
            var p = d.Compile(GetArgs());
        }

        //[TestMethod]
        //public async Task Can_execute_local_test()
        //{
        //    using var l = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
        //    var x = XDocument.Load(File.OpenRead("local_test.xml"));
        //    var d = SqlDeployment.Load(x);
        //    var p = d.Compile(GetArgs());

        //    await new SqlDeploymentExecutor(p, l.CreateLogger<SqlDeploymentExecutor>()).ExecuteAsync();

        //    var api = new MartinCostello.SqlLocalDb.SqlLocalDbApi();
        //    api.AutomaticallyDeleteInstanceFiles = true;
        //    api.StopInstance("SQL");
        //    api.StopInstance("EFM");
        //    api.DeleteInstance("SQL");
        //    api.DeleteInstance("EFM");
        //}

        //[TestMethod]
        //public async Task Should_allow_catchup_of_targets()
        //{
        //    using var l = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
        //    var x = XDocument.Load(File.OpenRead("local_test.xml"));
        //    var d = SqlDeployment.Load(x);
        //    var a = GetArgs();
        //    var r = new Random();
        //    var n = r.Next(0, int.MaxValue);
        //    a["SQL_InstanceName"] = "(localdb)\\SQL_" + n.ToString("X8");
        //    a["EFM_InstanceName"] = "(localdb)\\EFM_" + n.ToString("X8");
        //    var p = d.Compile(a);

        //    var e = new SqlDeploymentExecutor(p, l.CreateLogger<SqlDeploymentExecutor>());
        //    await e.ExecuteAsync("SQL");
        //    await e.ExecuteAsync("SQL_TO_EFM");

        //    var api = new MartinCostello.SqlLocalDb.SqlLocalDbApi();
        //    api.AutomaticallyDeleteInstanceFiles = true;
        //    api.StopInstance("SQL_" + n.ToString("X8"));
        //    api.StopInstance("EFM_" + n.ToString("X8"));
        //    api.DeleteInstance("SQL_" + n.ToString("X8"));
        //    api.DeleteInstance("EFM_" + n.ToString("X8"));
        //}

        //[TestMethod]
        //public async Task Can_execute_azure_test()
        //{
        //    using var l = LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Trace));
        //    var x = XDocument.Load(File.OpenRead("azure_test.xml"));
        //    var d = SqlDeployment.Load(x);
        //    var p = d.Compile(GetArgs());

        //    await new SqlDeploymentExecutor(p, l.CreateLogger<SqlDeploymentExecutor>()).ExecuteAsync();
        //}

    }

}
