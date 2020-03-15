using System;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a compiled target.
    /// </summary>
    class SqlDeploymentPlanTarget
    {

        readonly string[] dependsOn;
        readonly SqlDeploymentAction[] actions;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="dependsOn"></param>
        /// <param name="actions"></param>
        public SqlDeploymentPlanTarget(string[] dependsOn, SqlDeploymentAction[] actions)
        {
            this.dependsOn = dependsOn ?? throw new ArgumentNullException(nameof(dependsOn));
            this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        }

        /// <summary>
        /// Gets the targets this target depends on.
        /// </summary>
        public string[] DependsOn => dependsOn;

        /// <summary>
        /// Gets the actions that make up the body of the target.
        /// </summary>
        public SqlDeploymentAction[] Actions => actions;

    }

}
