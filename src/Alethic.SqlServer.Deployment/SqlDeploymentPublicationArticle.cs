using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    public abstract class SqlDeploymentPublicationArticle
    {

        /// <summary>
        /// Gets or sets the name of the article.
        /// </summary>
        public SqlDeploymentExpression Name { get; set; }

        /// <summary>
        /// Generates the steps required to ensure the publication.
        /// </summary>
        /// <param name="databaseName"></param>
        /// <param name="publicationName"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        public abstract IEnumerable<SqlDeploymentAction> Compile(string databaseName, string publicationName, SqlDeploymentCompileContext context);

    }

}
