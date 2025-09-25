using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.SqlServer.Deployment
{

    public class SqlDeploymentPublicationSnapshotAction : SqlDeploymentPublicationAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="publicationName"></param>
        /// <param name="databaseName"></param>
        public SqlDeploymentPublicationSnapshotAction(SqlInstance instance, string publicationName, string databaseName) :
            base(instance, publicationName, databaseName)
        {

        }

        public override Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
