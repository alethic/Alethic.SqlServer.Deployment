using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleTable : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentAction> Compile(string databaseName, string publicationName, SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleTableAction(context.Instance, databaseName, publicationName, Name.Expand(context));
        }

    }

}
