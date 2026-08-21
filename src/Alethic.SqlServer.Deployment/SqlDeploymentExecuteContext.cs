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
        readonly bool dryRun;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="dryRun"></param>
        public SqlDeploymentExecuteContext(ILogger logger, bool dryRun = false)
        {
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
            this.dryRun = dryRun;
        }

        /// <summary>
        /// Gets the logger for the execute context.
        /// </summary>
        public ILogger Logger => logger;

        /// <summary>
        /// Gets whether the execution is a dry run: actions report what they would change but
        /// change nothing. Actions whose <see cref="SqlDeploymentAction.SupportsDryRun"/> is
        /// <c>false</c> are skipped by the executor instead of being executed.
        /// </summary>
        public bool DryRun => dryRun;

        /// <summary>
        /// Adds a new action to the stack of actions to be executed.
        /// </summary>
        /// <param name="action"></param>
        public void AddAction(SqlDeploymentAction action)
        {

        }

    }

}
