using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    public abstract class SqlDeploymentPublicationArticleStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationArticleStep(string instanceName, string name) :
            base(instanceName)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
        }

        /// <summary>
        /// Gets the name of the article.
        /// </summary>
        public string Name { get; }

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
