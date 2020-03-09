using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cogito.SqlServer.Deployment.Tests
{

    [TestClass]
    public class SqlDeploymentTests
    {

        [TestMethod]
        public void Can_load_example()
        {
            var x = XDocument.Load(typeof(SqlDeploymentTests).Assembly.GetManifestResourceStream("Cogito.SqlServer.Deployment.Tests.Example.xml"));
            var d = SqlDeployment.Load(x);
        }

        [TestMethod]
        public void Can_compile_example()
        {
            var x = XDocument.Load(typeof(SqlDeploymentTests).Assembly.GetManifestResourceStream("Cogito.SqlServer.Deployment.Tests.Example.xml"));
            var d = SqlDeployment.Load(x);
            var l = d.Compile("EFM_JCMS");
        }

        [TestMethod]
        public async Task Can_execute_example()
        {
            var x = XDocument.Load(typeof(SqlDeploymentTests).Assembly.GetManifestResourceStream("Cogito.SqlServer.Deployment.Tests.Example.xml"));
            var d = SqlDeployment.Load(x);
            await d.Compile("EFM_JCMS").ExecuteAsync();
        }

    }

}
