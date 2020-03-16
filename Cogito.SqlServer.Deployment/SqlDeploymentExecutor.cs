using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Cogito.Collections;
using Cogito.Linq;
using Cogito.Threading;

using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Maintains an execution context over a plan.
    /// </summary>
    public class SqlDeploymentExecutor : ISqlDeploymentExecutor
    {

        readonly SqlDeploymentPlan plan;
        readonly ILogger logger;

        readonly ConcurrentDictionary<SqlDeploymentPlanTarget, Task> tasks = new ConcurrentDictionary<SqlDeploymentPlanTarget, Task>();

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
        /// Executes the given target of the plan, or all targets.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task ExecuteAsync(string targetName = null, CancellationToken cancellationToken = default)
        {
            if (targetName != null)
                return ExecuteAsync(new SqlDeploymentExecuteContext(logger), targetName, cancellationToken);
            else
                return ExecuteAsync(new SqlDeploymentExecuteContext(logger), plan.Targets.Values, cancellationToken);
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
            return tasks.GetOrAdd(target, _ => ExecuteAsync(context, _.Actions, cancellationToken));
        }

        /// <summary>
        /// Executes each of the given actions in order.
        /// </summary>
        /// <param name="actions"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task ExecuteAsync(SqlDeploymentExecuteContext context, SqlDeploymentAction[] actions, CancellationToken cancellationToken)
        {
            foreach (var step in actions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await step.ExecuteAsync(context, cancellationToken);
            }
        }

    }

}
