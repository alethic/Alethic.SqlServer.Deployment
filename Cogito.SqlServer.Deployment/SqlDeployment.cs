using System;
using System.Collections.Generic;
using System.IO;
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
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static SqlDeployment Load(TextReader reader, string baseUri = null) => SqlDeploymentXmlReader.Load(reader, baseUri);

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="Stream"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static SqlDeployment Load(Stream stream, string baseUri = null) => SqlDeploymentXmlReader.Load(stream, baseUri);

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified file path.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        public static SqlDeployment Load(string file) => SqlDeploymentXmlReader.Load(file);

        /// <summary>
        /// Gets the path to the loaded manifest, if available.
        /// </summary>
        public string SourcePath { get; set; }

        /// <summary>
        /// Gets the parameters that can be passed to the deployment.
        /// </summary>
        public ICollection<SqlDeploymentParameter> Parameters { get; set; } = new List<SqlDeploymentParameter>();

        /// <summary>
        /// Gets the set of targets included within the deployment.
        /// </summary>
        public ICollection<SqlDeploymentTarget> Targets { get; } = new List<SqlDeploymentTarget>();

        /// <summary>
        /// Compiles the <see cref="SqlDeployment"/> into a <see cref="SqlDeploymentPlan"/>.
        /// </summary>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public SqlDeploymentPlan Compile(IDictionary<string, string> arguments = null, string relativeRoot = null)
        {
            // create a local copy of the arguments for modification
            var args = arguments != null ? new Dictionary<string, string>(arguments) : new Dictionary<string, string>();

            // populate missing arguments with default values
            foreach (var param in Parameters)
                if (args.ContainsKey(param.Name) == false)
                    args[param.Name] = param.DefaultValue;

            // check for missing parameters
            foreach (var param in Parameters)
                if (args.TryGetValue(param.Name, out var val) == false || val == null)
                    throw new SqlDeploymentException($"Missing value for parameter '{param.Name}'.");

            // default value
            if (relativeRoot == null)
                relativeRoot = SourcePath != null ? Path.GetDirectoryName(SourcePath) : Environment.CurrentDirectory;

            // generate a deployment plan
            return new SqlDeploymentPlan(
                Targets.ToDictionary(
                    i => i.Name,
                    i => new SqlDeploymentPlanTarget(i.DependsOn.Select(j => j.Name).ToArray(), i.Compile(args, relativeRoot).ToArray())));
        }

    }

}
