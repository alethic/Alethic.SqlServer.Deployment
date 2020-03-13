using System;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Configures the instance to refer to a remote distributor.
    /// </summary>
    public class SqlDeploymentPublisherStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        public SqlDeploymentPublisherStep(string instanceName) :
            base(instanceName)
        {

        }

        /// <summary>
        /// Gets the name of the distribution instance to connect to.
        /// </summary>
        public string DistributorInstanceName { get; internal set; }

        /// <summary>
        /// Gets the distributor admin password for joining the distributor.
        /// </summary>
        public string DistributorAdminPassword { get; internal set; }

        /// <summary>
        /// Gets the name of the distributor database.
        /// </summary>
        public string DistributorDatabaseName { get; internal set; } = "distribution";

        public override Task<bool> ShouldExecute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(true);
        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var publisher = await OpenConnectionAsync(cancellationToken);
            using var distributor = DistributorInstanceName != null ? await OpenConnectionAsync(DistributorInstanceName, cancellationToken) : publisher;

            // load name of publisher
            var publisherName = await publisher.GetServerPropertyAsync("SERVERNAME");
            if (publisherName == null)
                throw new InvalidOperationException();

            var knownPublisherName = (string)await distributor.ExecuteScalarAsync($@"
                    SELECT  name
                    FROM    msdb.dbo.MSdistpublishers
                    WHERE   name = {publisherName}");
            if (knownPublisherName == null)
                await distributor.ExecuteSpAddDistPublisherAsync(publisherName, DistributorDatabaseName, 1, "false", 0, "MSSQLSERVER");

            // grant distributor permissions to server if not already
            var distributorInfo = await distributor.ExecuteSpHelpDistributorAsync();
            if (distributorInfo?.Account != null)
                await publisher.ExecuteSpAddSrvRoleMemberAsync(distributorInfo.Account, "sysadmin");

            // ensure server is enabled with the distributor
            await publisher.ExecuteNonQueryAsync($@"
                IF NOT EXISTS ( SELECT * from sys.servers WHERE is_distributor = 1 )
                BEGIN
                    EXEC sp_adddistributor
                        @distributor = {await distributor.GetServerPropertyAsync("SERVERNAME")},
                        @password = {DistributorAdminPassword}
                END");
        }

    }

}
