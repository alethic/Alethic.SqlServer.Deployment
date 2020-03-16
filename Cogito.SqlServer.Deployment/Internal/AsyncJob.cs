using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Cogito.Threading;

namespace Cogito.SqlServer.Deployment.Internal
{

    /// <summary>
    /// Represents a unit of execution that will be started on demand and canceled when no longer needed.
    /// </summary>
    public class AsyncJob<TResult> : IDisposable
    {

        /// <summary>
        /// Describes an attempt to run the job.
        /// </summary>
        class AsyncJobAttempt : IDisposable
        {

            readonly Func<CancellationToken, Task<TResult>> work;
            readonly Action exit;
            readonly Task<TResult> task;
            readonly CancellationTokenSource stop;
            readonly List<TaskCompletionSource<TResult>> wait;
            readonly object sync = new object();

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="work"></param>
            /// <param name="exit"></param>
            public AsyncJobAttempt(Func<CancellationToken, Task<TResult>> work, Action exit)
            {
                this.work = work ?? throw new ArgumentNullException(nameof(work));
                this.exit = exit ?? throw new ArgumentNullException(nameof(exit));

                // begin job
                stop = new CancellationTokenSource();
                task = Task.Run(async () => await work(stop.Token));
                task.ContinueWith(Complete, CancellationToken.None);
                wait = new List<TaskCompletionSource<TResult>>();
            }

            /// <summary>
            /// Attaches a new waiter to the attempt.
            /// </summary>
            /// <param name="cancellationToken"></param>
            /// <returns></returns>
            public Task<TResult> WaitAsync(CancellationToken cancellationToken)
            {
                lock (sync)
                {
                    // work is already done; just return result
                    if (task.IsCompleted)
                        return task;

                    // add new waiter to be notified upon completion of work
                    var waiter = new TaskCompletionSource<TResult>();
                    wait.Add(waiter);
                    cancellationToken.Register(() => OnCancelled(waiter));
                    return waiter.Task;
                }
            }

            /// <summary>
            /// Invoked when a single waiter is canceled.
            /// </summary>
            /// <param name="waiter"></param>
            void OnCancelled(TaskCompletionSource<TResult> waiter)
            {
                lock (sync)
                {
                    // cancel the waiter that was canceled
                    wait.Remove(waiter);
                    waiter.SetCanceled();

                    // last waiter; exit
                    if (wait.Count == 0)
                    {
                        // cancel all outstanding waiters
                        foreach (var i in wait)
                            i.TrySetCanceled();

                        // remove references to waiters
                        wait.Clear();

                        // signal the current task to stop
                        stop.Cancel();

                        // signal that we've exited early
                        exit();
                    }
                }
            }

            /// <summary>
            /// Invoked when the work is completed.
            /// </summary>
            /// <param name="task"></param>
            /// <param name="state"></param>
            void Complete(Task<TResult> task, object state)
            {
                lock (sync)
                {
                    // notify the known waiters that we're completed
                    foreach (var i in wait)
                        i.TrySetFrom(task);

                    // remove references to waiters
                    wait.Clear();
                }
            }

            /// <summary>
            /// Disposes of the instance.
            /// </summary>
            public void Dispose()
            {
                lock (sync)
                {
                    // cancel all outstanding waiters
                    foreach (var i in wait)
                        i.TrySetCanceled();

                    // remove references to waiters
                    wait.Clear();

                    // signal the current task to stop
                    stop.Cancel();

                    // signal that we've exited early
                    exit();
                }
            }

            /// <summary>
            /// Gets the status of the job.
            /// </summary>
            public TaskStatus Status
            {
                get
                {
                    lock (sync)
                    {
                        if (stop.IsCancellationRequested)
                            return TaskStatus.Canceled;
                        if (task.IsFaulted)
                            return TaskStatus.Faulted;
                        if (task.IsCompleted)
                            return TaskStatus.RanToCompletion;

                        return TaskStatus.Running;
                    }
                }
            }

        }

        readonly Func<CancellationToken, Task<TResult>> work;
        readonly object sync = new object();

        AsyncJobAttempt attempt;
        bool disposed;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="work"></param>
        public AsyncJob(Func<CancellationToken, Task<TResult>> work)
        {
            this.work = work ?? throw new ArgumentNullException(nameof(work));
        }

        /// <summary>
        /// Returns a task that completes when the job is complete.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task<TResult> WaitAsync(CancellationToken cancellationToken = default)
        {
            lock (sync)
            {
                if (disposed)
                    throw new ObjectDisposedException(nameof(AsyncJob<TResult>));

                // entire job was canceled
                cancellationToken.ThrowIfCancellationRequested();

                // initialize attempt
                if (attempt == null)
                    attempt = new AsyncJobAttempt(work, Exit);

                // subscribe new waiter
                return attempt.WaitAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Invoked when the attempt exits early.
        /// </summary>
        void Exit()
        {
            lock (sync)
                attempt = null;
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        public void Dispose()
        {
            lock (sync)
            {
                if (disposed)
                    return;

                if (attempt != null)
                {
                    attempt.Dispose();
                    attempt = null;
                }

                disposed = true;
            }
        }

        /// <summary>
        /// Returns the current status of the job.
        /// </summary>
        public TaskStatus Status
        {
            get
            {
                lock (sync)
                    return attempt == null ? TaskStatus.WaitingForActivation : attempt.Status;
            }
        }

    }

}
