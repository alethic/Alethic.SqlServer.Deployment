using System.Collections.Generic;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Describes a snapshot publication.
    /// </summary>
    public class SqlDeploymentSnapshotPublication : SqlDeploymentPublication
    {

        /// <summary>
        /// Describes the configuration of the Snapshot Agent for this publication.
        /// </summary>
        public SqlDeploymentSnapshotAgent SnapshotAgent { get; } = new SqlDeploymentSnapshotAgent();

        public override IEnumerable<SqlDeploymentStep> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            yield return new SqlDeploymentPublicationSnapshotStep(context.InstanceName, databaseName, Name);

            foreach (var s in base.Compile(context, databaseName))
                yield return s;
        }

    }

}
