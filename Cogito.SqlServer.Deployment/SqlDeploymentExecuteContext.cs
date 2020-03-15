using System;

using Microsoft.Extensions.Logging;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Context associated with an ongoing deployment execution.
    /// </summary>
    public class SqlDeploymentExecuteContext
    {

        readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="logger"></param>
        public SqlDeploymentExecuteContext(ILogger logger)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the logger for the execute context.
        /// </summary>
        public ILogger Logger => logger;

    }

}
