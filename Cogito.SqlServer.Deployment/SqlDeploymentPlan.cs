using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a compiled SQL deployment plan.
    /// </summary>
    public class SqlDeploymentPlan : IEnumerable<SqlDeploymentStep>
    {

        readonly List<SqlDeploymentStep> steps;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="steps"></param>
        internal SqlDeploymentPlan(List<SqlDeploymentStep> steps)
        {
            this.steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }

        /// <summary>
        /// Executes the plan.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            var context = new SqlDeploymentExecuteContext();

            foreach (var step in steps)
                if (await step.ShouldExecute(context, cancellationToken))
                    await step.Execute(context, cancellationToken);
        }

        public IEnumerator<SqlDeploymentStep> GetEnumerator()
        {
            return steps.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

    }

}
