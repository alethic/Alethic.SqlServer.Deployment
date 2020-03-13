using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Cogito.Threading;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Maintains an execution context over a plan.
    /// </summary>
    public class SqlDeploymentPlanExecutor
    {

        readonly SqlDeploymentPlan plan;
        readonly object sync = new object();
        readonly ConcurrentDictionary<SqlDeploymentPlanTarget, AsyncLock> locks = new ConcurrentDictionary<SqlDeploymentPlanTarget, AsyncLock>();
        readonly HashSet<SqlDeploymentPlanTarget> completed = new HashSet<SqlDeploymentPlanTarget>();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="plan"></param>
        public SqlDeploymentPlanExecutor(SqlDeploymentPlan plan)
        {
            this.plan = plan ?? throw new ArgumentNullException(nameof(plan));
        }

        /// <summary>
        /// Executes the given target of the plan, or all targets.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(string targetName = null, CancellationToken cancellationToken = default)
        {
            var context = new SqlDeploymentExecuteContext();

            // execute steps involed in target or all targets
            foreach (var target in targetName != null ? CollectTargets(targetName) : CollectTargets())
            {
                // ensure target does not run twice at the same time
                using (await locks.GetOrAdd(target, _ => new AsyncLock()).LockAsync())
                {
                    // only execute target if not already completed
                    if (IsComplete(target) == false)
                    {
                        foreach (var step in target.Steps)
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

            // collect list of steps for target
            var l = new List<SqlDeploymentPlanTarget>(plan.Targets.Count);
            Visit(target, l);

            // return collected targets
            return l;
        }

        /// <summary>
        /// Collects the steps necessary for executing all of the targets.
        /// </summary>
        /// <returns></returns>
        IEnumerable<SqlDeploymentPlanTarget> CollectTargets()
        {
            // collect list of all steps
            var l = new List<SqlDeploymentPlanTarget>(plan.Targets.Count);
            foreach (var target in plan.Targets.Values)
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
