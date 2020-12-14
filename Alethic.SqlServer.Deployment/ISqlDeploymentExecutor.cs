using System.Threading;
using System.Threading.Tasks;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Defines an instance capable of executing a SQL deployment plan.
    /// </summary>
    public interface ISqlDeploymentExecutor
    {

        /// <summary>
        /// Executes all targets of the plan.
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the given target of the plan.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(string targetName, CancellationToken cancellationToken = default);

        /// <summary>
        /// Executes the given targets of the plan.
        /// </summary>
        /// <param name="targetNames"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(string[] targetNames, CancellationToken cancellationToken = default);

    }

}