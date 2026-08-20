using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a set of execution steps that occur as part of a SQL deployment.
    /// </summary>
    public class SqlDeploymentTarget
    {

        /// <summary>
        /// Gets or sets the name of the target.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets an optional condition that determines whether the target contributes its
        /// steps to the plan. The expression is expanded against the deployment arguments and must
        /// result in a boolean value. A target whose condition evaluates to false compiles to an
        /// empty set of steps: it remains addressable by name and as a dependency, but executes
        /// nothing.
        /// </summary>
        public SqlDeploymentExpression? Condition { get; set; }

        /// <summary>
        /// Gets the other <see cref="SqlDeploymentTarget"/>s that this one depends on.
        /// </summary>
        public ICollection<SqlDeploymentTarget> DependsOn { get; } = new List<SqlDeploymentTarget>();

        /// <summary>
        /// Gets the set of instances to be concerned with as part of the target.
        /// </summary>
        public ICollection<SqlDeploymentInstance> Instances { get; } = new List<SqlDeploymentInstance>();

        /// <summary>
        /// Generates the series of deployment steps that execute the target.
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="relativeRoot"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(IDictionary<string, string> arguments, string relativeRoot)
        {
            if (Condition?.Expand<bool>(arguments) == false)
                yield break;

            foreach (var instance in Instances)
                foreach (var step in instance.Compile(arguments, relativeRoot))
                    yield return step;
        }

    }

}
