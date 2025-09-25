using System;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Alethic.SqlServer.Deployment.Tests.Internal
{

    [TestClass]
    public class AsyncMutexTests
    {

        [TestMethod]
        public async Task Test()
        {
            var m = new AsyncMutex("TheName");
            var d = await m.WaitOneAsync();
            await Task.Delay(TimeSpan.FromSeconds(2));
            d.Dispose();
        }

    }

}
