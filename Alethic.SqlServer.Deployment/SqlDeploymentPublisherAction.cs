﻿using System;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Configures the instance to refer to a remote distributor.
    /// </summary>
    public class SqlDeploymentPublisherAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        public SqlDeploymentPublisherAction(SqlInstance instance) :
            base(instance)
        {

        }

        /// <summary>
        /// Gets the name of the distribution instance to connect to.
        /// </summary>
        public SqlInstance DistributorInstance { get; internal set; }

        /// <summary>
        /// Gets the distributor admin password for joining the distributor.
        /// </summary>
        public string DistributorAdminPassword { get; internal set; }

        /// <summary>
        /// Gets the name of the distributor database.
        /// </summary>
        public string DistributorDatabaseName { get; internal set; } = "distribution";

        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var publisher = await OpenConnectionAsync(cancellationToken);
            using var distributor = DistributorInstance != null ? await OpenConnectionAsync(DistributorInstance, cancellationToken) : publisher;

            // load name of publisher
            var publisherName = await publisher.GetServerNameAsync(cancellationToken);
            if (publisherName == null)
                throw new InvalidOperationException();

            var knownPublisherName = (string)await distributor.ExecuteScalarAsync($@"
                SELECT  name
                FROM    msdb.dbo.MSdistpublishers
                WHERE   name = {publisherName}",
                cancellationToken: cancellationToken);
            if (knownPublisherName == null)
                await distributor.ExecuteSpAddDistPublisherAsync(publisherName, DistributorDatabaseName, 1, "false", 0, "MSSQLSERVER", cancellationToken: cancellationToken);

            // grant distributor permissions to server if not already
            var distributorInfo = await distributor.ExecuteSpHelpDistributorAsync(cancellationToken);
            if (distributorInfo?.Account != null)
                await publisher.ExecuteSpAddSrvRoleMemberAsync(distributorInfo.Account, "sysadmin");

            // ensure server is enabled with the distributor
            await publisher.ExecuteNonQueryAsync($@"
                IF NOT EXISTS ( SELECT * from sys.servers WHERE is_distributor = 1 )
                BEGIN
                    EXEC sp_adddistributor
                        @distributor = {await distributor.GetServerPropertyAsync("SERVERNAME", cancellationToken)},
                        @password = {DistributorAdminPassword}
                END",
                cancellationToken: cancellationToken);
        }

    }

}
