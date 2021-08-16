using System;
using System.Threading;
using System.Threading.Tasks;

namespace Alethic.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleViewAction : SqlDeploymentPublicationArticleAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="publicationName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationArticleViewAction(SqlInstance instance, string databaseName, string publicationName, string name) :
            base(instance, databaseName, publicationName, name)
        {

        }

        public override Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

    }

}
