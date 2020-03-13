using System;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a compiled target.
    /// </summary>
    class SqlDeploymentPlanTarget
    {

        readonly string[] dependsOn;
        readonly SqlDeploymentStep[] steps;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependsOn"></param>
        /// <param name="steps"></param>
        public SqlDeploymentPlanTarget(string[] dependsOn, SqlDeploymentStep[] steps)
        {
            this.dependsOn = dependsOn ?? throw new ArgumentNullException(nameof(dependsOn));
            this.steps = steps ?? throw new ArgumentNullException(nameof(steps));
        }

        /// <summary>
        /// Gets the targets this target depends on.
        /// </summary>
        public string[] DependsOn => dependsOn;

        /// <summary>
        /// Gets the steps that make up the body of the target.
        /// </summary>
        public SqlDeploymentStep[] Steps => steps;

    }

}
