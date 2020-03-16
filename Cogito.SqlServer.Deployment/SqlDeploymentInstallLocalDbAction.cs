using System;
using System.Threading;
using System.Threading.Tasks;

using MartinCostello.SqlLocalDb;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures that a SQL server local DB instance is properly configured.
    /// </summary>
    public class SqlDeploymentInstallLocalDbAction : SqlDeploymentAction
    {

        static readonly ISqlLocalDbApi Api = new SqlLocalDbApi();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="exe"></param>
        public SqlDeploymentInstallLocalDbAction(string instanceName) :
            base(instanceName)
        {

        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            var instanceName = InstanceName.Replace(@"(localdb)\", "");
            var instance = await Task.Run(() => GetOrCreateLocalDbInstance(Api, instanceName));
        }

        /// <summary>
        /// Creates a new DB instance with the given instance name.
        /// </summary>
        /// <param name="api"></param>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        ISqlLocalDbInstanceInfo GetOrCreateLocalDbInstance(ISqlLocalDbApi api, string instanceName)
        {
            // get existing instance
            var i = GetLocalDbInstance(api, instanceName);
            if (i != null)
            {
                if (i.IsRunning == false)
                {
                    var m = i.Manage();
                    m.Start();
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }

                return i;
            }

            // retry get and create with synchronous region
            using (new Mutex(true, typeof(SqlDeploymentInstallLocalDbAction).FullName))
            using (new Mutex(true, typeof(SqlDeploymentInstallLocalDbAction).FullName + "::" + instanceName))
            {
                i = GetLocalDbInstance(api, instanceName) ?? CreateLocalDbInstance(api, instanceName);
                if (i.IsRunning == false)
                {
                    var m = i.Manage();
                    m.Start();
                    Thread.Sleep(TimeSpan.FromSeconds(5));
                }

                return i;
            }
        }

        /// <summary>
        /// Gets the instance with the existing name.
        /// </summary>
        /// <param name="api"></param>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        ISqlLocalDbInstanceInfo GetLocalDbInstance(ISqlLocalDbApi api, string instanceName)
        {
            try
            {
                if (api.GetInstanceInfo(instanceName) is ISqlLocalDbInstanceInfo info && info.Exists)
                    return info;
            }
            catch (InvalidOperationException)
            {
                // ignore
            }

            return null;
        }

        /// <summary>
        /// Creates a new local instance, either by finding an existing instance, or generating a temporary instance.
        /// </summary>
        /// <param name="api"></param>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        ISqlLocalDbInstanceInfo CreateLocalDbInstance(ISqlLocalDbApi api, string instanceName)
        {
            if (api.GetOrCreateInstance(instanceName) is ISqlLocalDbInstanceInfo info && info.Exists)
                return info;

            throw new InvalidOperationException("Unable to create instance.");
        }

    }

}
