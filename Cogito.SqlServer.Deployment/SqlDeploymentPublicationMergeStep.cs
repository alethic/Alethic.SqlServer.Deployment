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
        /// <param name="publicationName"></param>
        /// <param name="databaseName"></param>
        public SqlDeploymentPublicationMergeStep(string instanceName, string publicationName, string databaseName) :
            base(instanceName, publicationName, databaseName)
        {

        }

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
