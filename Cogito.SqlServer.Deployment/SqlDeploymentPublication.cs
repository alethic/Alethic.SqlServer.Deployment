using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a publication to configure on the instance.
    /// </summary>
    public abstract class SqlDeploymentPublication
    {

        /// <summary>
        /// Gets the name of the publication.
        /// </summary>
        public SqlDeploymentExpression Name { get; set; }

        /// <summary>
        /// Gets the set of articles to configure for this publication.
        /// </summary>
        public SqlDeploymentPublicationArticleCollection Articles { get; } = new SqlDeploymentPublicationArticleCollection();

        /// <summary>
        /// Generates the steps required to ensure the publication.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public virtual IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            foreach (var article in Articles)
                foreach (var step in article.Compile(databaseName, Name, context))
                    yield return step;
        }

    }

}
