using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.SqlServer.Deployment
{

    public class SqlDeploymentPublicationMergeAction : SqlDeploymentPublicationAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationMergeAction(SqlInstance instance, string databaseName, string name) :
            base(instance, databaseName, name)
        {

        }

        public override Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
