using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures that a SQL server instance is properly installed.
    /// </summary>
    public class SqlDeploymentSetup
    {

        /// <summary>
        /// Gets or sets the path to the SQL server installation.
        /// </summary>
        public SqlDeploymentExpression? Exe { get; set; }

        /// <summary>
        /// Compiles the setup actions.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
                throw new SqlDeploymentException("SQL setup is only supported on Windows.");

            if (context.InstanceName.StartsWith(@"(localdb)\", StringComparison.OrdinalIgnoreCase))
                yield return new SqlDeploymentSetupLocalDbAction(context.InstanceName);
            else
                yield return new SqlDeploymentSetupAction(context.InstanceName, Exe?.Expand(context));
        }

    }

}
