using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.SqlServer.Dac;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Deploys a DACPAC against a database.
    /// </summary>
    public class SqlDeploymentDatabasePackageAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="name"></param>
        /// <param name="source"></param>
        /// <param name="profile"></param>
        /// <param name="ignoreDacTag"></param>
        /// <param name="ignoreDacVersion"></param>
        public SqlDeploymentDatabasePackageAction(SqlInstance instance, string name, string source, DacProfile profile, SqlPackageLockMode lockMode, bool ignoreDacTag, bool ignoreDacVersion) :
            base(instance)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            LockMode = lockMode;
            IgnoreDacTag = ignoreDacTag;
            IgnoreDacVersion = ignoreDacVersion;
        }

        /// <summary>
        /// Gets the name of the database.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the path to the DACPAC.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the profile to be configured for the deployment.
        /// </summary>
        public DacProfile Profile { get; }

        /// <summary>
        /// Gets the lock mode for the deployment.
        /// </summary>
        public SqlPackageLockMode LockMode { get; }

        /// <summary>
        /// Ignore comparison of the DACTAG with the target database.
        /// </summary>
        public bool IgnoreDacTag { get; set; }

        /// <summary>
        /// Ignore comparison of the DACVERSION with the target database.
        /// </summary>
        public bool IgnoreDacVersion { get; set; }

        /// <summary>
        /// Deploys the database.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken)
        {
            using (var cnn = await OpenConnectionAsync(cancellationToken))
                await new SqlDacPacDeploy(Source, context.Logger, LockMode).DeployAsync(cnn, Name, Profile, IgnoreDacTag, IgnoreDacVersion, cancellationToken);
        }

    }

}
