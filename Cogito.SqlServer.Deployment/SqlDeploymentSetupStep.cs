using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures that a SQL server instance is properly installed.
    /// </summary>
    public class SqlDeploymentSetupStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="exe"></param>
        public SqlDeploymentSetupStep(string instanceName, string exe = null) :
            base(instanceName)
        {
            Exe = exe;
        }

        /// <summary>
        /// Gets the path to the SQL server setup binary.
        /// </summary>
        public string Exe { get; }

        public override Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public override Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
