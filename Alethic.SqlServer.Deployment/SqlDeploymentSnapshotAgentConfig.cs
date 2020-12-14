using System.Net;

using Microsoft.Data.SqlClient;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes the desired configuration of the Snapshot Agent.
    /// </summary>
    public class SqlDeploymentSnapshotAgentConfig
    {

        /// <summary>
        /// Gets or sets the credentials under which to run the Snapshot Agent.
        /// </summary>
        public NetworkCredential ProcessCredentials { get; set; }

        /// <summary>
        /// Gets or sets the optional credentials with which to connect to the publisher.
        /// </summary>
        public SqlCredential ConnectCredentials { get; set; }

    }

}