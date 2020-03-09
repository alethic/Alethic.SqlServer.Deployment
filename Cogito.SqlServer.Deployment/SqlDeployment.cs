using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a loaded SQL deployment manifest and provides the capability to execute targets.
    /// </summary>
    public class SqlDeployment
    {

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="XDocument"/>.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static SqlDeployment Load(XDocument xml) => SqlDeploymentXmlReader.Load(xml);

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="XElement"/>.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static SqlDeployment Load(XElement xml) => SqlDeploymentXmlReader.Load(xml);

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="XmlReader"/>.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static SqlDeployment Load(XmlReader reader) => SqlDeploymentXmlReader.Load(reader);

        /// <summary>
        /// Gets the parameters that can be passed to the deployment.
        /// </summary>
        public ICollection<SqlDeploymentParameter> Parameters { get; set; } = new List<SqlDeploymentParameter>();

        /// <summary>
        /// Gets the set of targets included within the deployment.
        /// </summary>
        public ICollection<SqlDeploymentTarget> Targets { get; } = new List<SqlDeploymentTarget>();

        /// <summary>
        /// Compiles the steps required for executing the specified target.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public SqlDeploymentPlan Compile(string targetName, IDictionary<string, string> arguments = null)
        {
            return new SqlDeploymentPlan(CompileIter(targetName, arguments).ToList());
        }

        /// <summary>
        /// Compiles the steps required for executing the specified target.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        IEnumerable<SqlDeploymentStep> CompileIter(string targetName, IDictionary<string, string> arguments)
        {
            // apply arguments to parameters
            var args = new Dictionary<string, string>();
            foreach (var kvp in Parameters)
                args[kvp.Name] = arguments != null && arguments.TryGetValue(kvp.Name, out var v) ? v : kvp.DefaultValue;

            if (Targets.FirstOrDefault(i => i.Name == targetName) is SqlDeploymentTarget target)
            {
                var l = new List<SqlDeploymentTarget>(Targets.Count);
                Visit(target, l);

                foreach (var i in l)
                    foreach (var s in i.Compile(args))
                        yield return s;
            }

            yield break;
        }

        /// <summary>
        /// Recursive call that adds the dependencies to the list.
        /// </summary>
        /// <param name="target"></param>
        /// <param name="build"></param>
        void Visit(SqlDeploymentTarget target, List<SqlDeploymentTarget> build)
        {
            // add dependencies
            foreach (var dependency in target.DependsOn)
                Visit(dependency, build);

            // not already in dependency list
            if (build.Contains(target) == false)
                build.Add(target);
        }

    }

}
