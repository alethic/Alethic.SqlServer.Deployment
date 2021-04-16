using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
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
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        /// <param name="source"></param>
        /// <param name="profile"></param>
        public SqlDeploymentDatabasePackageAction(string instanceName, string name, string source, DacProfile profile) :
            base(instanceName)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
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
        /// Deploys the database.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken)
        {
            using (var cnn = await OpenConnectionAsync(cancellationToken))
                await new SqlDacPacDeploy(Source, context.Logger).DeployAsync(cnn, Name, Profile, cancellationToken);
        }

    }

}
