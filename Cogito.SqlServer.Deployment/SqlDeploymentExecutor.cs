using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cogito.Collections;
using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Maintains an execution context over a plan.
    /// </summary>
    public class SqlDeploymentExecutor : ISqlDeploymentExecutor, IDisposable
    {

        readonly SqlDeploymentPlan plan;
        readonly ILogger logger;

        readonly ConcurrentDictionary<SqlDeploymentAction, Lazy<AsyncJob<bool>>> tasks = new ConcurrentDictionary<SqlDeploymentAction, Lazy<AsyncJob<bool>>>();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="plan"></param>
        public SqlDeploymentExecutor(SqlDeploymentPlan plan, ILogger logger)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Executes all targets of the plan.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            return ExecuteAsync(new SqlDeploymentExecuteContext(logger), plan.Targets.Values, cancellationToken);
        }

        /// <summary>
        /// Executes the given target of the plan.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task ExecuteAsync(string targetName, CancellationToken cancellationToken = default)
        {
            if (targetName is null)
                throw new ArgumentNullException(nameof(targetName));

            return ExecuteAsync(new SqlDeploymentExecuteContext(logger), targetName, cancellationToken);
        }

        /// <summary>
        /// Executes the given targets of the plan.
        /// </summary>
        /// <param name="targetNames"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task ExecuteAsync(string[] targetNames, CancellationToken cancellationToken = default)
        {
            if (targetNames is null)
                throw new ArgumentNullException(nameof(targetNames));

            var targets = targetNames.Select(i => plan.Targets.GetOrDefault(i)).Where(i => i != null);
            return ExecuteAsync(new SqlDeploymentExecuteContext(logger), targets, cancellationToken);
        }

        /// <summary>
        /// Executes the given target by name.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="targetName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(SqlDeploymentExecuteContext context, string targetName, CancellationToken cancellationToken)
        {
            if (plan.Targets.TryGetValue(targetName, out var target) == false)
                throw new SqlDeploymentException($"Could not resolve target '{targetName}'.");

            return ExecuteAsync(context, target, cancellationToken);
        }

        /// <summary>
        /// Executes the given targets in parallel.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="targets"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(SqlDeploymentExecuteContext context, IEnumerable<SqlDeploymentPlanTarget> targets, CancellationToken cancellationToken)
        {
            return Task.WhenAll(targets.Select(i => ExecuteAsync(context, i, cancellationToken)));
        }

        /// <summary>
        /// Executes the given target.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task ExecuteAsync(SqlDeploymentExecuteContext context, SqlDeploymentPlanTarget target, CancellationToken cancellationToken)
        {
            await Task.WhenAll(target.DependsOn.Select(i => ExecuteAsync(context, i, cancellationToken)));
            await GetExecuteTaskAsync(context, target, cancellationToken);
        }

        /// <summary>
        /// Gets the task that executes the actions of a target.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="target"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task GetExecuteTaskAsync(SqlDeploymentExecuteContext context, SqlDeploymentPlanTarget target, CancellationToken cancellationToken)
        {
            return ExecuteAsync(context, target.Actions, cancellationToken);
        }

        /// <summary>
        /// Executes each of the given actions in order.
        /// </summary>
        /// <param name="actions"></param>
        /// <returns></returns>
        async Task ExecuteAsync(SqlDeploymentExecuteContext context, SqlDeploymentAction[] actions, CancellationToken cancellationToken)
        {
            foreach (var action in actions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ExecuteAsync(context, action, cancellationToken);
            }
        }

        /// <summary>
        /// Returns a task that is completed when the specified action is complete.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="action"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(SqlDeploymentExecuteContext context, SqlDeploymentAction action, CancellationToken cancellationToken)
        {
            return tasks.GetOrAdd(action, _ => new Lazy<AsyncJob<bool>>(() => new AsyncJob<bool>(async ct => { await ExecuteActionAsync(context, _, ct); return true; }), true)).Value.WaitAsync(cancellationToken);
        }

        /// <summary>
        /// Returns a task that is completed when the specified action is complete.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="action"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task ExecuteActionAsync(SqlDeploymentExecuteContext context, SqlDeploymentAction action, CancellationToken cancellationToken)
        {
            context.Logger.LogInformation("Starting action {Action} against {InstanceName}.", action.GetType().FullName, action.InstanceName);
            await action.ExecuteAsync(context, cancellationToken);
            context.Logger.LogInformation("Finished action {Action} against {InstanceName}.", action.GetType().FullName, action.InstanceName);
        }

        /// <summary>
        /// Disposes of the instance.
        /// </summary>
        public void Dispose()
        {
            if (tasks != null)
                foreach (var i in tasks)
                    if (i.Value.IsValueCreated)
                        TryDisposeJob(i.Value.Value);
        }

        /// <summary>
        /// Attempts to dispose of the job.
        /// </summary>
        /// <param name="value"></param>
        void TryDisposeJob(AsyncJob<bool> value)
        {
            try
            {
                value.Dispose();
            }
            catch
            {
                // ignore
            }
        }

        /// <summary>
        /// Finalizes the instance.
        /// </summary>
        ~SqlDeploymentExecutor()
        {
            Dispose();
        }

    }

}
