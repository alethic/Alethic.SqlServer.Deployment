using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes a transactional publication.
    /// </summary>
    public class SqlDeploymentTransactionalPublication : SqlDeploymentPublication
    {

        /// <summary>
        /// Describes the configuration of the Snapshot Agent for this publication.
        /// </summary>
        public SqlDeploymentSnapshotAgent SnapshotAgent { get; } = new SqlDeploymentSnapshotAgent();

        /// <summary>
        /// Describes the configuration of the Log Reader Agent for this publication.
        /// </summary>
        public SqlDeploymentLogReaderAgent LogReaderAgent { get; } = new SqlDeploymentLogReaderAgent();

        public override IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            yield return new SqlDeploymentPublicationTransactionalAction(context.Instance, databaseName, Name);

            foreach (var s in base.Compile(context, databaseName))
                yield return s;
        }

    }

}
