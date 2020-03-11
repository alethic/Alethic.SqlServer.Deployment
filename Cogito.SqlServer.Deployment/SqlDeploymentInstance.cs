﻿using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a step that ensures an instance.
    /// </summary>
    public class SqlDeploymentInstance
    {

        /// <summary>
        /// Describes the name of the instance.
        /// </summary>
        public SqlDeploymentExpression Name { get; set; }

        /// <summary>
        /// Gets the information regarding the setup of the instance.
        /// </summary>
        public SqlDeploymentSetup Setup { get; set; }

        /// <summary>
        /// Gets the information regarding the properties to configure on the instance.
        /// </summary>
        public SqlDeploymentConfiguration Configuration { get; } = new SqlDeploymentConfiguration();

        /// <summary>
        /// Gets the information regarding the databases to deploy to the instance.
        /// </summary>
        public ICollection<SqlDeploymentDatabase> Databases { get; } = new List<SqlDeploymentDatabase>();

        /// <summary>
        /// Gets the information regarding the linked servers to configure on the instance.
        /// </summary>
        public ICollection<SqlDeploymentLinkedServer> LinkedServers { get; } = new List<SqlDeploymentLinkedServer>();

        /// <summary>
        /// If provided ensures the instance is configured as a distributor.
        /// </summary>
        public SqlDeploymentDistributor Distributor { get; set; }

        /// <summary>
        /// If provided ensures the instance is configured to refer to a remote distributor.
        /// </summary>
        public SqlDeploymentRemoteDistributor Distribution { get; set; }

        /// <summary>
        /// Gets the information regarding any publications to be configurd on the instance.
        /// </summary>
        public ICollection<SqlDeploymentPublication> Publications { get; } = new List<SqlDeploymentPublication>();

        /// <summary>
        /// Gets the information regarding any publications to be configurd on the instance.
        /// </summary>
        public ICollection<SqlDeploymentSubscription> Subscriptions { get; } = new List<SqlDeploymentSubscription>();

        /// <summary>
        /// Generates the steps required to ensure the instance.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        internal IEnumerable<SqlDeploymentStep> Compile(IDictionary<string, string> arguments)
        {
            var context = new SqlDeploymentCompileContext(arguments, Name.Expand<string>(arguments));

            if (Setup != null)
                foreach (var s in Setup.Compile(context))
                    yield return s;

            if (Configuration != null)
                foreach (var s in Configuration.Compile(context))
                    yield return s;

            foreach (var i in LinkedServers)
                foreach (var s in i.Generate(context))
                    yield return s;

            if (Distributor != null)
                foreach (var s in Distributor.Compile(context))
                    yield return s;

            foreach (var i in Subscriptions)
                foreach (var s in i.Compile(context))
                    yield return s;

            foreach (var i in Databases)
                foreach (var s in i.Compile(context))
                    yield return s;

            foreach (var i in Publications)
                foreach (var s in i.Compile(context))
                    yield return s;
        }

    }

}
