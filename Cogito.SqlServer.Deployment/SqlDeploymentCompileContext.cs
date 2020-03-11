﻿using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Context associated with an ongoing deployment evaluation.
    /// </summary>
    public class SqlDeploymentCompileContext
    {

        readonly IDictionary<string, string> arguments;
        readonly string instanceName;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="instanceName"></param>
        public SqlDeploymentCompileContext(IDictionary<string, string> arguments, string instanceName)
        {
            this.arguments = arguments ?? new Dictionary<string, string>();
            this.instanceName = instanceName;
        }

        /// <summary>
        /// Gets the step of arguments passed into the compilation.
        /// </summary>
        public IDictionary<string, string> Arguments => arguments;

        /// <summary>
        /// Gets the name of the instance.
        /// </summary>
        public string InstanceName => instanceName;

        /// <summary>
        /// Gets the value of the specified variable.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public string GetVariable(string name)
        {
            return arguments.TryGetValue(name, out var value) ? value : null;
        }

    }

}