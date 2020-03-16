using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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

        readonly object sync = new object();
        readonly ConcurrentDictionary<SqlDeploymentPlanTarget, AsyncLock> locks = new ConcurrentDictionary<SqlDeploymentPlanTarget, AsyncLock>();
        readonly HashSet<SqlDeploymentPlanTarget> completed = new HashSet<SqlDeploymentPlanTarget>();

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
        public async Task ExecuteAsync(string targetName = null, CancellationToken cancellationToken = default)
        {
            var context = new SqlDeploymentExecuteContext(logger);

            // execute steps involed in target or all targets
            foreach (var target in targetName != null ? CollectTargets(targetName) : CollectTargets())
            {
                // ensure target does not run twice at the same time
                using (await locks.GetOrAdd(target, _ => new AsyncLock()).LockAsync())
                {
                    // only execute target if not already completed
                    if (IsComplete(target) == false)
                    {
                        foreach (var step in target.Actions)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            await step.Execute(context, cancellationToken);
                        }

                        Complete(target);
                    }
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the specified target has been completed.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        bool IsComplete(SqlDeploymentPlanTarget target)
        {
            lock (sync)
                return completed.Contains(target);
        }

        /// <summary>
        /// Marks the specified target as completed.
        /// </summary>
        /// <param name="target"></param>
        /// <returns></returns>
        bool Complete(SqlDeploymentPlanTarget target)
        {
            lock (sync)
                return completed.Add(target);
        }

        /// <summary>
        /// Collects the steps necessary for executing the specified target.
        /// </summary>
        /// <param name="targetName"></param>
        /// <returns></returns>
        IEnumerable<SqlDeploymentPlanTarget> CollectTargets(string targetName)
        {
            if (targetName == null)
                throw new ArgumentNullException(nameof(targetName));

            if (plan.Targets.TryGetValue(targetName, out var target) == false)
                throw new SqlDeploymentException($"Could not resolve target '{targetName}'.");

            return CollectTargets(target.Yield());
        }

        /// <summary>
        /// Collects the steps necessary for executing all of the targets.
        /// </summary>
        /// <returns></returns>
        IEnumerable<SqlDeploymentPlanTarget> CollectTargets()
        {
            return CollectTargets(plan.Targets.Values);
        }

        /// <summary>
        /// Collections the dependencies of the given target.
        /// </summary>
        /// <param name="targets"></param>
        /// <returns></returns>
        IEnumerable<SqlDeploymentPlanTarget> CollectTargets(IEnumerable<SqlDeploymentPlanTarget> targets)
        {
            // collect list of all steps
            var l = new List<SqlDeploymentPlanTarget>(plan.Targets.Count);
            foreach (var target in targets)
                Visit(target, l);

            // return collected targets
            return l;
        }

        /// <summary>
        /// Recursive call that adds the dependencies to the list.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="build"></param>
        void Visit(SqlDeploymentPlanTarget target, List<SqlDeploymentPlanTarget> build)
        {
            // recurse into dependency targets
            foreach (var targetName in target.DependsOn)
                Visit(plan.Targets.TryGetValue(targetName, out var dep) ? dep : throw new SqlDeploymentException($"Could not resolve target '{targetName}'."), build);

            // not already in recursed list
            if (build.Contains(target) == false)
                build.Add(target);
        }

    }

}
