using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using FluentAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Cogito.SqlServer.Deployment.Tests.Internal
{

    [TestClass]
    public class AsyncJobTests
    {

        [TestMethod]
        public async Task Should_abort_job_when_canceled_early()
        {
            var s = false;
            var j = new AsyncJob<bool>(async cancellationToken => { await Task.Delay(60000, cancellationToken); s = true; return true; });
            var cts1 = new CancellationTokenSource();
            var cts2 = new CancellationTokenSource();
            j.Status.Should().Be(TaskStatus.WaitingForActivation);
            s.Should().BeFalse();
            var t1 = j.WaitAsync(cts1.Token);
            await Task.Delay(1000);
            j.Status.Should().Be(TaskStatus.Running);
            s.Should().BeFalse();
            var t2 = j.WaitAsync(cts2.Token);
            await Task.Delay(1000);
            j.Status.Should().Be(TaskStatus.Running);
            s.Should().BeFalse();
            cts1.Cancel();
            await Task.Delay(1000);
            t1.Status.Should().Be(TaskStatus.Canceled);
            j.Status.Should().Be(TaskStatus.Running);
            s.Should().BeFalse();
            cts2.Cancel();
            await Task.Delay(1000);
            t2.Status.Should().Be(TaskStatus.Canceled);
            j.Status.Should().Be(TaskStatus.WaitingForActivation);
            s.Should().BeFalse();
            await Task.Delay(1000);
        }

        [TestMethod]
        public async Task Should_finish_job()
        {
            var s = false;
            var j = new AsyncJob<bool>(async cancellationToken => { await Task.Delay(10000, cancellationToken); s = true; return true; });
            j.Status.Should().Be(TaskStatus.WaitingForActivation);
            s.Should().BeFalse();
            var t1 = j.WaitAsync();
            j.Status.Should().Be(TaskStatus.Running);
            s.Should().BeFalse();
            var t2 = j.WaitAsync();
            await Task.Delay(2000);
            j.Status.Should().Be(TaskStatus.Running);
            s.Should().BeFalse();
            await Task.Delay(10000);
            j.Status.Should().Be(TaskStatus.RanToCompletion);
            t1.Status.Should().Be(TaskStatus.RanToCompletion);
            t2.Status.Should().Be(TaskStatus.RanToCompletion);
            s.Should().BeTrue();
            t1.Result.Should().BeTrue();
            t2.Result.Should().BeTrue();
        }

        [TestMethod]
        public void Should_cancel_all_waiters()
        {
            var j = new AsyncJob<bool>(async cancellationToken => { await Task.Delay(10000, cancellationToken); return true; });
            var t1 = j.WaitAsync();
            var t2 = j.WaitAsync();
            j.Dispose();
            t1.Status.Should().Be(TaskStatus.Canceled);
            t2.Status.Should().Be(TaskStatus.Canceled);
        }

        [TestMethod]
        public async Task Should_complete_async()
        {
            var j = new AsyncJob<bool>(async cancellationToken => { await Task.Delay(1000, cancellationToken); return true; });
            var t = Enumerable.Range(0, 16).Select(i => j.WaitAsync()).ToList();
            var l = await Task.WhenAll(t);
            l.Should().AllBeEquivalentTo(true);
        }

    }

}
