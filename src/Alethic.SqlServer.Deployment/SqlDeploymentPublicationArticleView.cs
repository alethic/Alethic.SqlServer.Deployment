using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleView : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentAction> Compile(string databaseName, string publicationName, SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleViewAction(context.Instance, databaseName, publicationName, Name.Expand(context));
        }

    }

}
