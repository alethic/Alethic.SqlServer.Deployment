using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a merge publication.
    /// </summary>
    public class SqlDeploymentMergePublication : SqlDeploymentPublication
    {

        /// <summary>
        /// Describes the configuration of the Snapshot Agent for this publication.
        /// </summary>
        public SqlDeploymentSnapshotAgent SnapshotAgent { get; } = new SqlDeploymentSnapshotAgent();

        public override IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            yield return new SqlDeploymentPublicationMergeStep(context.InstanceName, databaseName, Name);

            foreach (var s in base.Compile(context, databaseName))
                yield return s;
        }

    }

}
