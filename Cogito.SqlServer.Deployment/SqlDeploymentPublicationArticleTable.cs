using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleTable : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentAction> Compile(string databaseName, string publicationName, SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleTableAction(context.InstanceName, databaseName, publicationName, Name.Expand(context));
        }

    }

}
