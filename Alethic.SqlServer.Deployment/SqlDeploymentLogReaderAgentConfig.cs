using System.Net;

using Microsoft.Data.SqlClient;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Describes the desired configuration of the Log Reader Agent.
    /// </summary>
    public class SqlDeploymentLogReaderAgentConfig
    {

        /// <summary>
        /// Gets or sets the credentials under which to run the Log Reader Agent.
        /// </summary>
        public NetworkCredential ProcessCredentials { get; set; }

        /// <summary>
        /// Gets or sets the optional credentials with which to connect to the publisher.
        /// </summary>
        public SqlCredential ConnectCredentials { get; set; }

    }

}