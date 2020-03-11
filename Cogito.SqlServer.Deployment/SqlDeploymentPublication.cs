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
        /// Gets or sets the name of the database to be published.
        /// </summary>
        public SqlDeploymentExpression DatabaseName { get; set; }

        /// <summary>
        /// Gets the set of articles to configure for this publication.
        /// </summary>
        public SqlDeploymentPublicationArticleCollection Articles { get; } = new SqlDeploymentPublicationArticleCollection();

        /// <summary>
        /// Generates the steps required to ensure the publication.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public virtual IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            foreach (var article in Articles)
                foreach (var step in article.Compile(context))
                    yield return step;
        }

    }

}
