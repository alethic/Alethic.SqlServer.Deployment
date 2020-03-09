using System;
using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a publication to configure on the instance.
    /// </summary>
    public class SqlDeploymentPublication
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
        /// Gets or sets the name of the distributor to use for the publication.
        /// </summary>
        public SqlDeploymentExpression? DistributorName { get; set; }

        /// <summary>
        /// Gets or sets the password of the distributor admin to use when creating the publication.
        /// </summary>
        public SqlDeploymentExpression? DistributorAdminPassword { get; set; }

        /// <summary>
        /// Gets or sets the type of publication.
        /// </summary>
        public SqlDeploymentPublicationType Type { get; set; }

        /// <summary>
        /// Gets the set of articles to configure for this publication.
        /// </summary>
        public SqlDeploymentPublicationArticleCollection Articles { get; } = new SqlDeploymentPublicationArticleCollection();

        /// <summary>
        /// Generates the steps required to ensure the publication.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return Type switch
            {
                SqlDeploymentPublicationType.Snapshot => ApplyStepProperties(context, new SqlDeploymentPublicationSnapshotStep(context.InstanceName, Name.Expand(context), DatabaseName.Expand(context))),
                SqlDeploymentPublicationType.Transactional => ApplyStepProperties(context, new SqlDeploymentPublicationTransactionalStep(context.InstanceName, Name.Expand(context), DatabaseName.Expand(context))),
                SqlDeploymentPublicationType.Merge => ApplyStepProperties(context, new SqlDeploymentPublicationMergeStep(context.InstanceName, Name.Expand(context), DatabaseName.Expand(context))),
                _ => throw new InvalidOperationException(),
            };

            foreach (var article in Articles)
                foreach (var step in article.Compile(context))
                    yield return step;
        }

        SqlDeploymentPublicationStep ApplyStepProperties(SqlDeploymentCompileContext context, SqlDeploymentPublicationStep step)
        {
            step.DistributorName = DistributorName?.Expand(context);
            step.DistributorAdminPassword = DistributorAdminPassword?.Expand(context);
            return step;
        }

    }

}
