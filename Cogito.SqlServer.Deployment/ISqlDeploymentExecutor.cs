using System.Threading;
using System.Threading.Tasks;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Defines an instance capable of executing a SQL deployment plan.
    /// </summary>
    public interface ISqlDeploymentExecutor
    {

        /// <summary>
        /// Begins execution of the SQL deployment plan.
        /// </summary>
        /// <param name="targetName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task ExecuteAsync(string targetName = null, CancellationToken cancellationToken = default);

    }

}