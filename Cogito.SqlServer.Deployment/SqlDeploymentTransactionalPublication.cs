using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
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

        public override IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context)
        {
            yield return new SqlDeploymentPublicationTransactionalStep(context.InstanceName, Name, DatabaseName);

            foreach (var s in base.Compile(context))
                yield return s;
        }

    }

}
