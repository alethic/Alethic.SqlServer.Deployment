using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleViewAction : SqlDeploymentPublicationArticleAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="publicationName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationArticleViewAction(string instanceName, string databaseName, string publicationName, string name) :
            base(instanceName, databaseName, publicationName, name)
        {

        }

        public override Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            throw new System.NotImplementedException();
        }

    }

}
