using System;
using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
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
        /// Describes the connection string to connect to the SQL server instance.
        /// </summary>
        public SqlDeploymentExpression? ConnectionString { get; set; }

        /// <summary>
        /// Gets the information regarding the setup of the instance.
        /// </summary>
        public SqlDeploymentInstall Install { get; set; }

        /// <summary>
        /// Gets the information regarding the properties to configure on the instance.
        /// </summary>
        public SqlDeploymentConfiguration Configuration { get; } = new SqlDeploymentConfiguration();

        /// <summary>
        /// Gets the CLR assemblies to register in the instance's trusted assembly list.
        /// </summary>
        public ICollection<SqlDeploymentTrustedAssembly> TrustedAssemblies { get; } = new List<SqlDeploymentTrustedAssembly>();

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
        public SqlDeploymentPublisher Publisher { get; set; }

        /// <summary>
        /// Generates the steps required to ensure the instance.
        /// </summary>
        /// <param name="arguments"></param>
        /// <param name="relativeRoot"></param>
        /// <returns></returns>
        internal IEnumerable<SqlDeploymentAction> Compile(IDictionary<string, string> arguments, string relativeRoot)
        {
            if (relativeRoot is null)
                throw new ArgumentNullException(nameof(relativeRoot));

            var context = new SqlDeploymentCompileContext(arguments, new SqlInstance(Name.Expand<string>(arguments), ConnectionString?.Expand<string>(arguments)), relativeRoot);

            if (Install != null)
                foreach (var s in Install.Compile(context))
                    yield return s;

            if (Configuration != null)
                foreach (var s in Configuration.Compile(context))
                    yield return s;

            // trusted assemblies register before the databases deploy, so CREATE ASSEMBLY succeeds
            foreach (var i in TrustedAssemblies)
                foreach (var s in i.Compile(context))
                    yield return s;

            foreach (var i in LinkedServers)
                foreach (var s in i.Compile(context))
                    yield return s;

            if (Distributor != null)
                foreach (var s in Distributor.Compile(context))
                    yield return s;

            if (Publisher != null)
                foreach (var s in Publisher.Compile(context))
                    yield return s;

            foreach (var i in Databases)
                foreach (var s in i.Compile(context))
                    yield return s;
        }

    }

}
