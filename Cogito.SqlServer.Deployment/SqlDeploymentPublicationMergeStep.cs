using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationMergeStep : SqlDeploymentPublicationStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationMergeStep(string instanceName, string databaseName, string name) :
            base(instanceName, databaseName, name)
        {

        }

        public override Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
