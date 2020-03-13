using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleView : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentStep> Compile(string databaseName, string publicationName, SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleViewStep(context.InstanceName, databaseName, publicationName, Name.Expand(context));
        }

    }

}
