using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleView : SqlDeploymentPublicationArticle
    {

        public override IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationArticleViewStep(context.InstanceName, Name.Expand(context));
        }

    }

}
