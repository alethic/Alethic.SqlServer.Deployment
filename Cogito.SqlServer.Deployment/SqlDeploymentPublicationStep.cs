using System;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Step that ensures the existance and configuration of a publication.
    /// </summary>
    public abstract class SqlDeploymentPublicationStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instnace.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="publicationName"></param>
        /// <param name="databaseName"></param>
        public SqlDeploymentPublicationStep(string instanceName, string publicationName, string databaseName) :
            base(instanceName)
        {
            Name = publicationName ?? throw new ArgumentNullException(nameof(publicationName));
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
        }

        /// <summary>
        /// Gets the name of the publication to deploy.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the name of the database to be published.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the instance name of the distributor.
        /// </summary>
        public string DistributorName { get; internal set; }

        /// <summary>
        /// Gets the password of the distributor admin user.
        /// </summary>
        public string DistributorAdminPassword { get; internal set; }

        /// <summary>
        /// Configures a publisher on the specified distributor.
        /// </summary>
        /// <param name="distributor"></param>
        /// <param name="publisher"></param>
        /// <returns></returns>
        internal async Task ConfigurePublisher(SqlConnection distributor, SqlConnection publisher)
        {
            if (distributor == null)
                throw new ArgumentNullException(nameof(distributor));
            if (publisher == null)
                throw new ArgumentNullException(nameof(publisher));

            // load name of publisher
            var publisherName = await publisher.GetServerPropertyAsync("SERVERNAME");
            if (publisherName == null)
                throw new InvalidOperationException();

            // find existing publisher or create
            var knownPublisherName = (string)await distributor.ExecuteScalarAsync($@"
                SELECT  name
                FROM    msdb.dbo.MSdistpublishers
                WHERE   name = {publisherName}");
            if (knownPublisherName == null)
                await distributor.ExecuteSpAddDistPublisherAsync(publisherName, "distribution", 1, "false", 0, "MSSQLSERVER");

            // grant distributor permissions to server if not already
            var distributorInfo = await distributor.ExecuteSpHelpDistributorAsync();
            if (distributorInfo?.Account != null)
            {
                await publisher.CreateWindowsLoginIfNotExistsAsync(distributorInfo.Account);
                await publisher.ExecuteSpAddSrvRoleMemberAsync(distributorInfo.Account, "sysadmin");
            }

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
