using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Cogito.Autofac;
using Cogito.Collections;

using MartinCostello.SqlLocalDb;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment.Tests.Framework
{

    /// <summary>
    /// Provides SQL Local DB instances for the given FSX instance names.
    /// </summary>
    [RegisterAs(typeof(SqlEnvironmentDbInstanceProvider))]
    [RegisterSingleInstance]
    public class SqlEnvironmentLocalDbInstanceProvider : SqlEnvironmentDbInstanceProvider
    {

        /// <summary>
        /// Provides a <see cref="ISqlDbInstance"/> for a SQL Server LocalDb instance.
        /// </summary>
        class SqlLocalDbInstanceObject : ISqlDbInstance
        {

            readonly ISqlLocalDbInstanceInfo instance;

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="instance"></param>
            public SqlLocalDbInstanceObject(ISqlLocalDbInstanceInfo instance)
            {
                this.instance = instance ?? throw new ArgumentNullException(nameof(instance));
            }

            public string ServerName => $"(localdb)\\{instance.Name}";

            public SqlConnection CreateConnection() => instance.CreateConnection();

            public SqlConnectionStringBuilder CreateConnectionStringBuilder() => instance.CreateConnectionStringBuilder();

        }

        static readonly string PREFIX = "FSX";
        static readonly TimeSpan MAX_AGE = TimeSpan.FromHours(2);

        readonly ISqlLocalDbApi provider;
        readonly SqlTestEnvironmentInfo info;
        readonly Dictionary<string, Task<ISqlDbInstance>> instances = new Dictionary<string, Task<ISqlDbInstance>>();

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="info"></param>
        public SqlEnvironmentLocalDbInstanceProvider(ISqlLocalDbApi provider, SqlTestEnvironmentInfo info)
        {
            this.provider = provider ?? throw new ArgumentNullException(nameof(provider));
            this.info = info ?? throw new ArgumentNullException(nameof(info));

            foreach (var i in provider.GetInstances()
                .Where(i => i.Name.StartsWith(PREFIX + "_"))
                .Where(i => i.LastStartTimeUtc < DateTime.UtcNow.Subtract(MAX_AGE)))
            {
                try
                {
                    provider.StopInstance(i.Name, TimeSpan.FromSeconds(30));
                    provider.DeleteInstance(i.Name);
                }
                catch (SqlLocalDbException)
                {
                    // ignore
                }
            }

            // does a single cleanup of any hanging localdb directories
            CleanLocalDbDirectory();
        }

        /// <summary>
        /// Attempts to remove any LocalDB instance directories that are unused.
        /// </summary>
        /// <returns></returns>
        void CleanLocalDbDirectory()
        {
            using (new Mutex(true, typeof(SqlEnvironmentLocalDbInstanceProvider).FullName))
            {
                var instanceNames = new HashSet<string>(provider.GetInstanceNames(), StringComparer.OrdinalIgnoreCase);
                var defaultInstancePath = new DirectoryInfo(SqlLocalDbApi.GetInstancesFolderPath());
                if (defaultInstancePath.Exists)
                    foreach (var instanceDirectory in defaultInstancePath.GetDirectories())
                        if (instanceDirectory.Name.StartsWith(PREFIX + "_"))
                            if (instanceNames.Contains(instanceDirectory.Name) == false)
                                TryDeleteDirectory(instanceDirectory);
            }
        }

        /// <summary>
        /// Attempts to delete the specified directory.
        /// </summary>
        /// <param name="directory"></param>
        void TryDeleteDirectory(DirectoryInfo directory)
        {
            try
            {
                directory.Delete(true);
            }
            catch (IOException)
            {
                // ignore
            }
        }

        /// <summary>
        /// Creates a new local instance, either by finding an existing instance, or generating a temporary instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        ISqlLocalDbInstanceInfo CreateLocalDbInstance(string instanceName, bool temporary)
        {
            if (provider.GetOrCreateInstance(instanceName) is ISqlLocalDbInstanceInfo info && info.Exists)
                return info;

            throw new InvalidOperationException("Unable to create instance.");
        }

        /// <summary>
        /// Gets the instance with the existing name.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        ISqlLocalDbInstanceInfo GetLocalDbInstance(string instanceName)
        {
            try
            {
                if (provider.GetInstanceInfo(instanceName) is ISqlLocalDbInstanceInfo info && info.Exists)
                    return info;
            }
            catch (InvalidOperationException)
            {
                // ignore
            }

            return null;
        }

        /// <summary>
        /// Creates a new DB instance with the given instance name.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <returns></returns>
        ISqlLocalDbInstanceInfo GetOrCreateLocalDbInstance(string instanceName, bool temporary)
        {
            // get existing instance
            var i = GetLocalDbInstance(instanceName);
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
            using (new Mutex(true, typeof(SqlEnvironmentLocalDbInstanceProvider).FullName))
            using (new Mutex(true, typeof(SqlEnvironmentLocalDbInstanceProvider).FullName + "::" + instanceName))
            {
                i = GetLocalDbInstance(instanceName) ?? CreateLocalDbInstance(instanceName, temporary);
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
        /// Gets the LocalDB with the given relative instance name.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public override Task<ISqlDbInstance> GetOrCreateInstanceAsync(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException(nameof(name));

            lock (instances)
                return instances.GetOrAdd(name, _ => CreateLocalDbInstanceObject(_));
        }

        /// <summary>
        /// Gets the instance and wraps it in an instance object.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        async Task<ISqlDbInstance> CreateLocalDbInstanceObject(string name)
        {
            return new SqlLocalDbInstanceObject(await Task.Run(() => GetOrCreateLocalDbInstance($"{PREFIX}_{info.UniqueId}_{name}", info.Temporary)));
        }

    }

}
