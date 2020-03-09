using System;
using System.Collections.Generic;

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

        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            if (context.InstanceName.StartsWith(@"(localdb)\", StringComparison.OrdinalIgnoreCase))
                yield return new SqlDeploymentSetupLocalDbStep(context.InstanceName);
            else
                yield return new SqlDeploymentSetupStep(context.InstanceName, Exe?.Expand(context));
        }

    }

}
