using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationSnapshotStep : SqlDeploymentPublicationStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="publicationName"></param>
        /// <param name="databaseName"></param>
        public SqlDeploymentPublicationSnapshotStep(string instanceName, string publicationName, string databaseName) :
            base(instanceName, publicationName, databaseName)
        {

        }

        public override Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
