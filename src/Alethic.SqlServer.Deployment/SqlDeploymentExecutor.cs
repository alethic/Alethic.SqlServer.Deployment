using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Cogito.Collections;
using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Maintains an execution context over a plan.
    /// </summary>
    public class SqlDeploymentExecutor : ISqlDeploymentExecutor, IDisposable
    {

        readonly SqlDeploymentPlan plan;
        readonly ILogger logger;
        readonly bool dryRun;

        readonly ConcurrentDictionary<SqlDeploymentAction, Lazy<AsyncJob<bool>>> tasks = new ConcurrentDictionary<SqlDeploymentAction, Lazy<AsyncJob<bool>>>();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="plan"></param>
        /// <param name="logger"></param>
        /// <param name="dryRun">When <c>true</c>, actions report what they would change but change nothing; actions that cannot report are skipped.</param>
        public SqlDeploymentExecutor(SqlDeploymentPlan plan, ILogger logger, bool dryRun = false)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.dryRun = dryRun;
        }

        /// <summary>
        /// Executes all targets of the plan.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            logger.LogInformation("Executing all targets...");
            await ExecuteAsync(new SqlDeploymentExecuteContext(logger, dryRun), plan.Targets.Values, cancellationToken);
            logger.LogInformation("Done executing all targets.");
        }

        /// <summary>
        /// Executes the given target of the plan.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(string targetName, CancellationToken cancellationToken = default)
        {
            if (targetName is null)
                throw new ArgumentNullException(nameof(targetName));

            logger.LogInformation("Executing {Target}...", targetName);
            await ExecuteAsync(new SqlDeploymentExecuteContext(logger, dryRun), targetName, cancellationToken);
            logger.LogInformation("Done executing {Target}.", targetName);
        }

        /// <summary>
        /// Executes the given targets of the plan.
        /// </summary>
        /// <param name="targetNames"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(string[] targetNames, CancellationToken cancellationToken = default)
        {
            if (targetNames is null)
                throw new ArgumentNullException(nameof(targetNames));

            logger.LogInformation("Executing {Targets}...", targetNames);
            var targets = targetNames.Select(i => plan.Targets.GetOrDefault(i)).Where(i => i != null);
            await ExecuteAsync(new SqlDeploymentExecuteContext(logger, dryRun), targets, cancellationToken);
            logger.LogInformation("Done executing {Targets}.", targetNames);
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
            if (context.DryRun && action.SupportsDryRun == false)
            {
                context.Logger.LogInformation("Dry run: would execute {Action} against {InstanceName}; the action cannot report its changes in detail.", action.GetType().Name, action.Instance);
                return;
            }

            if (context.DryRun)
            {
                // a reporting action inspects the target to build its report; when the target
                // cannot be inspected - commonly because an earlier action that would have
                // provided it was itself only reported - the report degrades to a warning
                // instead of failing the remainder of the run; failures that no target state
                // can explain (a corrupt package, an internal error) still fail the run
                try
                {
                    context.Logger.LogDebug("Starting action {Action} against {InstanceName}.", action.GetType().Name, action.Instance);
                    await action.ExecuteAsync(context, cancellationToken);
                    context.Logger.LogDebug("Finished action {Action} against {InstanceName}.", action.GetType().Name, action.Instance);
                }
                catch (Exception e) when (IsSqlFailure(e))
                {
                    context.Logger.LogWarning("Dry run: could not inspect {InstanceName} for {Action}; the report for this step is incomplete. A real deployment executes the preceding steps first, which may provide what was missing here. ({Message})", action.Instance, action.GetType().Name, e.Message);
                }

                return;
            }

            context.Logger.LogDebug("Starting action {Action} against {InstanceName}.", action.GetType().Name, action.Instance);
            await action.ExecuteAsync(context, cancellationToken);
            context.Logger.LogDebug("Finished action {Action} against {InstanceName}.", action.GetType().Name, action.Instance);
        }

        /// <summary>
        /// Returns <c>true</c> if the exception is, or wraps, a SQL error - a failure the state
        /// of the target can explain, as opposed to a defect in the deployment itself. DacFx
        /// wraps the SQL errors it encounters, so the inner exceptions are searched.
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        static bool IsSqlFailure(Exception exception)
        {
            for (var e = exception; e != null; e = e.InnerException)
                if (e is SqlException)
                    return true;

            return false;
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
