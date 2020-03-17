using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
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
            foreach (var instance in Instances)
                foreach (var step in instance.Compile(arguments, relativeRoot))
                    yield return step;
        }

    }

}
