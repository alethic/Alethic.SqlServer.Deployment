using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures that a SQL server instance is properly installed.
    /// </summary>
    public class SqlDeploymentInstall
    {

        /// <summary>
        /// Gets or sets the path to the SQL server installation.
        /// </summary>
        public SqlDeploymentExpression? SetupExe { get; set; }

        /// <summary>
        /// Compiles the setup actions.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) == false)
                throw new SqlDeploymentException("SQL setup is only supported on Windows.");
            if (context.Instance.Authentication != SqlAuthenticationMethod.Windows)
                throw new SqlDeploymentException("SQL setup is only supported with Windows authentication.");

            if (context.Instance.Name.StartsWith(@"(localdb)\", StringComparison.OrdinalIgnoreCase))
                yield return new SqlDeploymentInstallLocalDbAction(context.Instance);
            else
                yield return new SqlDeploymentInstallAction(context.Instance, SetupExe?.Expand(context));
        }

    }

}
