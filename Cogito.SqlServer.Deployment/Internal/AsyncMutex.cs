using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment.Internal
{

    public class AsyncMutex : IDisposable
    {

        /// <summary>
        /// Invokes an action on disposal.
        /// </summary>
        public class AsyncMutexLock : IDisposable, IAsyncDisposable
        {

            readonly Action onDispose;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="onDispose"></param>
            public AsyncMutexLock(Action onDispose)
            {
                this.onDispose = onDispose ?? throw new ArgumentNullException(nameof(onDispose));
            }

            /// <summary>
            /// Disposes of the instance.
            /// </summary>
            public void Dispose()
            {
                onDispose?.Invoke();
                GC.SuppressFinalize(this);
            }

            /// <summary>
            /// Disposes of the instance.
            /// </summary>
            /// <returns></returns>
            public ValueTask DisposeAsync()
            {
                Dispose();
                return new ValueTask(Task.CompletedTask);
            }

            /// <summary>
            /// Finalizes the instance
            /// </summary>
            ~AsyncMutexLock()
            {
                Dispose();
            }

        }

        readonly Mutex mutex;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="name"></param>
        public AsyncMutex(string name)
        {
            mutex = new Mutex(false, name);
        }

        /// <summary>
        /// Returns a <see cref="Task"/> that continues when the mutex is acquired.
        /// </summary>
        /// <returns></returns>
        public Task<AsyncMutexLock> WaitOneAsync(CancellationToken cancellationToken = default)
        {
            var h = new TaskCompletionSource<AsyncMutexLock>();
            Task.Run(() => LockThread(h, cancellationToken));
            return h.Task;
        }

        /// <summary>
        /// Ensures acquisition and release of the mutex occurs on the same thread.
        /// </summary>
        /// <param name="tcs"></param>
        /// <param name="cancellationToken"></param>
        void LockThread(TaskCompletionSource<AsyncMutexLock> tcs, CancellationToken cancellationToken)
        {
            var captured = (Exception)null; // disposer exception
            var disposer = new ManualResetEvent(false); // signals disposal
            var disposed = new ManualResetEvent(false); // signals completion of disposal; resuming disposer

            try
            {
                // wait for mutex; and send disposer as result
                while (true)
                {
                    // bail out if cancelled
                    if (cancellationToken.IsCancellationRequested)
                    {
                        tcs.SetCanceled();
                        return;
                    }

                    // wait a bit for the mutex before trying again
                    if (mutex.WaitOne(TimeSpan.FromSeconds(2)))
                    {
                        tcs.SetResult(new AsyncMutexLock(() => { disposer.Set(); disposed.WaitOne(); if (captured != null) ExceptionDispatchInfo.Capture(captured).Throw(); }));
                        break;
                    }
                }

                // wait for call to disposer
                disposer.WaitOne();

                // release mutex
                mutex.ReleaseMutex();
            }
            catch (Exception e)
            {
                if (tcs.Task.IsCompleted == false)
                    // relay exception to waiter if possible
                    tcs.SetException(e);
                else
                    // otherwise schedule exception to disposer
                    captured = e;
            }
            finally
            {
                // resume disposer
                disposed.Set();
            }
        }

        /// <summary>
        /// Disposes of the mutex.
        /// </summary>
        public void Dispose()
        {
            mutex.Dispose();
        }

        /// <summary>
        /// Finalizes the instance.
        /// </summary>
        ~AsyncMutex()
        {
            Dispose();
            GC.SuppressFinalize(this);
        }

    }

}
