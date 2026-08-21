using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Extensions.Logging.Abstractions;
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

        /// <summary>
        /// A manifest with a conditional target, and a second target that depends on it.
        /// </summary>
        const string ConditionManifestXml = @"
<Deployment xmlns=""https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020"">
    <Parameter Name=""Flag"" DefaultValue=""false"" />
    <Target Name=""Conditional"" Condition=""[Flag]"">
        <Instance Name=""(localdb)\Test"">
            <Configuration Name=""clr enabled"" Value=""1"" />
        </Instance>
    </Target>
    <Target Name=""Dependent"">
        <DependsOn Name=""Conditional"" />
        <Instance Name=""(localdb)\Test"">
            <Configuration Name=""clr enabled"" Value=""1"" />
        </Instance>
    </Target>
</Deployment>";

        [TestMethod]
        public void Condition_true_should_compile_target_steps()
        {
            var d = SqlDeployment.Load(XDocument.Parse(ConditionManifestXml));
            var p = d.Compile(new Dictionary<string, string>() { ["Flag"] = "true" });
            Assert.AreEqual(1, p.Targets["Conditional"].Actions.Length);
        }

        [TestMethod]
        public void Condition_false_should_compile_no_target_steps()
        {
            var d = SqlDeployment.Load(XDocument.Parse(ConditionManifestXml));
            var p = d.Compile(new Dictionary<string, string>() { ["Flag"] = "false" });
            Assert.AreEqual(0, p.Targets["Conditional"].Actions.Length);
        }

        [TestMethod]
        public void Condition_false_target_should_remain_addressable_as_dependency()
        {
            var d = SqlDeployment.Load(XDocument.Parse(ConditionManifestXml));
            var p = d.Compile(new Dictionary<string, string>() { ["Flag"] = "false" });
            Assert.AreEqual(1, p.Targets["Dependent"].Actions.Length);
            CollectionAssert.Contains(p.Targets["Dependent"].DependsOn, "Conditional");
        }

        [TestMethod]
        public void Condition_should_expand_parameter_default_value()
        {
            var d = SqlDeployment.Load(XDocument.Parse(ConditionManifestXml));
            var p = d.Compile();
            Assert.AreEqual(0, p.Targets["Conditional"].Actions.Length);
        }

        /// <summary>
        /// A manifest with a linked server whose credentials come from optional parameters.
        /// </summary>
        const string LinkedServerManifestXml = @"
<Deployment xmlns=""https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020"">
    <Parameter Name=""RemoteUser"" DefaultValue="""" />
    <Parameter Name=""RemotePassword"" DefaultValue="""" />
    <Target Name=""Link"">
        <Instance Name=""(localdb)\Test"">
            <LinkedServer Name=""REMOTE"" Product="""" Provider=""SQLNCLI"" DataSource=""remote,1433"" RemoteUser=""[RemoteUser]"" RemotePassword=""[RemotePassword]"" />
        </Instance>
    </Target>
</Deployment>";

        [TestMethod]
        public void LinkedServer_should_compile_remote_credentials()
        {
            var d = SqlDeployment.Load(XDocument.Parse(LinkedServerManifestXml));
            var p = d.Compile(new Dictionary<string, string>() { ["RemoteUser"] = "user", ["RemotePassword"] = "password" });
            var a = (SqlDeploymentLinkedServerAction)p.Targets["Link"].Actions[0];
            Assert.AreEqual("user", a.RemoteUser);
            Assert.AreEqual("password", a.RemotePassword);
        }

        [TestMethod]
        public void LinkedServer_empty_credentials_should_compile_as_absent()
        {
            var d = SqlDeployment.Load(XDocument.Parse(LinkedServerManifestXml));
            var p = d.Compile();
            var a = (SqlDeploymentLinkedServerAction)p.Targets["Link"].Actions[0];
            Assert.IsNull(a.RemoteUser);
            Assert.IsNull(a.RemotePassword);
        }

        [TestMethod]
        public void LinkedServer_user_without_password_should_fail_compile()
        {
            var d = SqlDeployment.Load(XDocument.Parse(LinkedServerManifestXml));
            Assert.ThrowsException<SqlDeploymentException>(() => d.Compile(new Dictionary<string, string>() { ["RemoteUser"] = "user" }));
        }

        /// <summary>
        /// A manifest with a trusted assembly registered by an explicit hash.
        /// </summary>
        const string TrustedAssemblyManifestXml = @"
<Deployment xmlns=""https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020"">
    <Target Name=""Trust"">
        <Instance Name=""(localdb)\Test"">
            <TrustedAssembly Hash=""0x0102ABCD"" Description=""MyAssembly"" />
        </Instance>
    </Target>
</Deployment>";

        [TestMethod]
        public void TrustedAssembly_should_compile_hash_and_description()
        {
            var d = SqlDeployment.Load(XDocument.Parse(TrustedAssemblyManifestXml));
            var p = d.Compile();
            var a = (SqlDeploymentTrustedAssemblyAction)p.Targets["Trust"].Actions[0];
            CollectionAssert.AreEqual(new byte[] { 0x01, 0x02, 0xAB, 0xCD }, a.Hash);
            Assert.AreEqual("MyAssembly", a.Description);
        }

        [TestMethod]
        public void TrustedAssembly_without_source_or_hash_should_fail_compile()
        {
            const string xml = @"
<Deployment xmlns=""https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020"">
    <Target Name=""Trust"">
        <Instance Name=""(localdb)\Test"">
            <TrustedAssembly Description=""MyAssembly"" />
        </Instance>
    </Target>
</Deployment>";
            var d = SqlDeployment.Load(XDocument.Parse(xml));
            Assert.ThrowsException<SqlDeploymentException>(() => d.Compile());
        }

        /// <summary>
        /// A manifest whose actions are unreachable: the instance name does not resolve, and the
        /// package source does not exist. Only a dry run that skips or avoids them can complete.
        /// </summary>
        const string DryRunManifestXml = @"
<Deployment xmlns=""https://cogito.cx/schemas/SqlServer.Deployment/manifest/2020"">
    <Target Name=""Link"">
        <Instance Name=""dry-run-test.invalid"">
            <LinkedServer Name=""REMOTE"" Product="""" Provider=""SQLNCLI"" DataSource=""remote,1433"" />
        </Instance>
    </Target>
    <Target Name=""Database"">
        <Instance Name=""dry-run-test.invalid"">
            <Database Name=""Db"">
                <Package Source=""missing.dacpac"" />
            </Database>
        </Instance>
    </Target>
    <Target Name=""Trust"">
        <Instance Name=""dry-run-test.invalid"">
            <TrustedAssembly Hash=""0x0102ABCD"" Description=""MyAssembly"" />
        </Instance>
    </Target>
</Deployment>";

        [TestMethod]
        public void Execute_context_should_default_to_not_dry_run()
        {
            Assert.IsFalse(new SqlDeploymentExecuteContext(NullLogger.Instance).DryRun);
            Assert.IsTrue(new SqlDeploymentExecuteContext(NullLogger.Instance, true).DryRun);
        }

        [TestMethod]
        public void Configuration_should_support_dry_run()
        {
            var d = SqlDeployment.Load(XDocument.Parse(ConditionManifestXml));
            var p = d.Compile(new Dictionary<string, string>() { ["Flag"] = "true" });
            Assert.IsTrue(p.Targets["Conditional"].Actions[0].SupportsDryRun);
        }

        [TestMethod]
        public void Create_database_and_package_should_support_dry_run()
        {
            var d = SqlDeployment.Load(XDocument.Parse(DryRunManifestXml));
            var p = d.Compile();
            Assert.IsInstanceOfType(p.Targets["Database"].Actions[0], typeof(SqlDeploymentCreateDatabaseAction));
            Assert.IsTrue(p.Targets["Database"].Actions[0].SupportsDryRun);
            Assert.IsInstanceOfType(p.Targets["Database"].Actions[1], typeof(SqlDeploymentDatabasePackageAction));
            Assert.IsTrue(p.Targets["Database"].Actions[1].SupportsDryRun);
        }

        [TestMethod]
        public void LinkedServer_should_support_dry_run()
        {
            var d = SqlDeployment.Load(XDocument.Parse(DryRunManifestXml));
            var p = d.Compile();
            Assert.IsTrue(p.Targets["Link"].Actions[0].SupportsDryRun);
        }

        [TestMethod]
        public void TrustedAssembly_should_not_support_dry_run()
        {
            var d = SqlDeployment.Load(XDocument.Parse(DryRunManifestXml));
            var p = d.Compile();
            Assert.IsFalse(p.Targets["Trust"].Actions[0].SupportsDryRun);
        }

        [TestMethod]
        public async Task Dry_run_should_not_execute_actions_that_cannot_report()
        {
            var d = SqlDeployment.Load(XDocument.Parse(DryRunManifestXml));
            var p = d.Compile();

            // the trusted assembly action would have to connect to the unresolvable instance;
            // only the dry run executor reporting it without executing it lets execution complete
            await new SqlDeploymentExecutor(p, NullLogger.Instance, true).ExecuteAsync("Trust");
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
