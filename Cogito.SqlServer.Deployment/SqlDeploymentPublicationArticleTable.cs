using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleTable : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentStep> Compile(string databaseName, string publicationName, SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleTableStep(context.InstanceName, databaseName, publicationName, Name.Expand(context));
        }

    }

}
