using System;

using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
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

        /// <summary>
        /// Adds a new action to the stack of actions to be executed.
        /// </summary>
        /// <param name="action"></param>
        public void AddAction(SqlDeploymentAction action)
        {

        }

    }

}
