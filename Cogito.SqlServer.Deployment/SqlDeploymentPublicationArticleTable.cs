using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleTable : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleTableStep(context.InstanceName, Name.Expand(context));
        }

    }

}
