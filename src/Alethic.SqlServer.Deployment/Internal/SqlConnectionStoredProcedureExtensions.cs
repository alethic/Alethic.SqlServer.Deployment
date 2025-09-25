using System;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.SqlClient;

namespace Alethic.SqlServer.Deployment.Internal
{

    /// <summary>
    /// Provides various extensions to SQL connections for executing system stored procedures.
    /// </summary>
    static class SqlConnectionStoredProcedureExtensions
    {

        public class HelpDistributorResults
        {

            public string Distributor { get; set; }

            public string DistributionDatabase { get; set; }

            public string Directory { get; set; }

            public string Account { get; set; }

        }

        public class HelpDistributionDbResults
        {

            public string Name { get; set; }

            public int MinDistRetention { get; set; }

            public int MaxDistRetention { get; set; }

            public int HistoryRetention { get; set; }

            public string HistoryCleanupAgent { get; set; }

            public int Status { get; set; }

            public string DataFolder { get; set; }

            public string DataFile { get; set; }

            public string LogFolder { get; set; }

            public string LogFile { get; set; }

        }

        public class HelpLogReaderAgentResults
        {

            public int? Id { get; set; }

            public string Name { get; set; }

            public short? PublisherSecurityMode { get; set; }

            public string PublisherLogin { get; set; }

            public string PublisherPassword { get; set; }

            public Guid? JobId { get; set; }

            public string JobLogin { get; set; }

            public string JobPassword { get; set; }

        }

        public class HelpPublicationResults
        {

            public int? PubId { get; set; }

            public string Name { get; set; }

            public bool? SnapshotInDefaultFolder { get; set; }

            public string AltSnapshotFolder { get; set; }

        }

        public class HelpPublicationSnapshotResults
        {

            public string JobLogin { get; set; }

        }

        public class HelpSubscriptionResults
        {

            public string Subscriber { get; set; }

            public string Publication { get; set; }

            public string Article { get; set; }

            public string DestinationDatabase { get; set; }

            public int SubscriptionStatus { get; set; }

            public int SynchronizationType { get; set; }

            public int SubscriptionType { get; set; }

        }

        public class HelpReplicationDbOption
        {

            public string Name { get; set; }

            public int Id { get; set; }

            public bool TransactionalPublish { get; set; }

            public bool MergePublish { get; set; }

            public bool DbOwner { get; set; }

            public bool DbReadOnly { get; set; }

        }

        public class ConfigureResults
        {

            public string Name { get; set; }

            public int Minimum { get; set; }

            public int Maximum { get; set; }

            public int ConfigValue { get; set; }

            public int RunValue { get; set; }

        }

        /// <summary>
        /// Executes the 'sp_configure' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static async Task<ConfigureResults[]> ExecuteSpConfigure(
            this SqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync($@"EXEC sp_configure", cancellationToken: cancellationToken);

            return t.Rows.Cast<DataRow>()
                .Select(i => new ConfigureResults()
                {
                    Name = (string)i["name"],
                    Minimum = (int)i["minimum"],
                    Maximum = (int)i["maximum"],
                    ConfigValue = (int)i["config_value"],
                    RunValue = (int)i["run_value"],
                })
                .ToArray();
        }

        /// <summary>
        /// Executes the 'sp_configure' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="configname"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<ConfigureResults> ExecuteSpConfigure(
            this SqlConnection connection,
            string configname,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync($@"EXEC sp_configure @configname = {configname}", cancellationToken: cancellationToken);

            return t.Rows.Cast<DataRow>()
                .Select(i => new ConfigureResults()
                {
                    Name = (string)i["name"],
                    Minimum = (int)i["minimum"],
                    Maximum = (int)i["maximum"],
                    ConfigValue = (int)i["config_value"],
                    RunValue = (int)i["run_value"],
                })
                .FirstOrDefault();
        }

        /// <summary>
        /// Executes the 'sp_configure' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="configname"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpConfigure(
            this SqlConnection connection,
            string configname,
            int configvalue,
            CancellationToken cancellationToken = default)
        {
            await connection.LoadDataTableAsync($@"EXEC sp_configure @configname = {configname}, @configvalue = {configvalue}", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_addsrvrolemember' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="loginame"></param>
        /// <param name="rolename"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddSrvRoleMemberAsync(
            this SqlConnection connection,
            string loginame,
            string rolename)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_addsrvrolemember
                    @loginame = {loginame},
                    @rolename = {rolename}");
        }

        /// <summary>
        /// Executes the 'sp_replicationdboption' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="dbName"></param>
        /// <param name="optName"></param>
        /// <param name="value"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpSetReplicationDbOptionAsync(
            this SqlConnection connection,
            string dbName,
            string optName,
            string value,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_replicationdboption
                    @dbname = {dbName},
                    @optname = {optName},
                    @value = {value}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_changepublication' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="property"></param>
        /// <param name="value"></param>
        /// <param name="forceReinitSubscription"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpChangePublicationAsync(
            this SqlConnection connection,
            string publication,
            string property,
            string value,
            int forceReinitSubscription,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_changepublication
                    @publication = {publication},
                    @property = {property},
                    @value = {value},
                    @force_reinit_subscription = {forceReinitSubscription}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_helpdistributiondb' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<HelpDistributionDbResults[]> ExecuteSpHelpDistributionDbAsync(
            this SqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            return (await connection.LoadDataTableAsync(
                    $"EXEC sp_helpdistributiondb",
                    cancellationToken: cancellationToken))
                .Rows.Cast<DataRow>()
                .Select(i => new HelpDistributionDbResults()
                {
                    Name = (string)i["name"],
                    MinDistRetention = (int)i["min_distretention"],
                    MaxDistRetention = (int)i["max_distretention"],
                    HistoryRetention = (int)i["history_retention"],
                    HistoryCleanupAgent = (string)i["history_cleanup_agent"],
                    Status = (int)i["status"],
                    DataFolder = (string)i["data_folder"],
                    DataFile = (string)i["data_file"],
                    LogFolder = (string)i["log_folder"],
                    LogFile = (string)i["log_file"],
                })
                .ToArray();
        }

        /// <summary>
        /// Executes the 'sp_helpdistributiondb' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="database"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<HelpDistributionDbResults> ExecuteSpHelpDistributionDbAsync(
            this SqlConnection connection,
            string database,
            CancellationToken cancellationToken = default)
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            return (await connection.LoadDataTableAsync(
                    $"EXEC sp_helpdistributiondb @database = {database}",
                    cancellationToken: cancellationToken))
                .Rows.Cast<DataRow>()
                .Select(i => new HelpDistributionDbResults()
                {
                    Name = (string)i["name"],
                    MinDistRetention = (int)i["min_distretention"],
                    MaxDistRetention = (int)i["max_distretention"],
                    HistoryRetention = (int)i["history_retention"],
                    HistoryCleanupAgent = (string)i["history_cleanup_agent"],
                    Status = (int)i["status"],
                    DataFolder = (string)i["data_folder"],
                    DataFile = (string)i["data_file"],
                    LogFolder = (string)i["log_folder"],
                    LogFile = (string)i["log_file"],
                })
                .FirstOrDefault();
        }

        /// <summary>
        /// Executes the 'sp_helpdistributor' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<HelpDistributorResults> ExecuteSpHelpDistributorAsync(
            this SqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync("EXEC sp_helpdistributor", cancellationToken: cancellationToken);
            if (t.Rows.Count < 1)
                return null;

            return new HelpDistributorResults()
            {
                Distributor = t.Rows[0]["distributor"] is string distributor ? distributor : null,
                DistributionDatabase = t.Rows[0]["distribution database"] is string database ? database : null,
                Directory = t.Rows[0]["directory"] is string directory ? directory : null,
                Account = t.Rows[0]["account"] is string account ? account : null,
            };
        }

        /// <summary>
        /// Executes the 'sp_helpdistributor' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<HelpPublicationResults> ExecuteSpHelpPublicationAsync(
            this SqlConnection connection,
            string publication,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync($@"EXEC sp_helppublication @publication = {publication}", cancellationToken: cancellationToken);
            if (t.Rows.Count < 1)
                return null;

            return new HelpPublicationResults()
            {
                PubId = t.Rows[0]["pubid"] is int pubid ? (int?)pubid : null,
                Name = t.Rows[0]["name"] is string name ? name : null,
                SnapshotInDefaultFolder = t.Rows[0]["snapshot_in_defaultfolder"] is bool snapshot_in_defaultfolder ? (bool?)snapshot_in_defaultfolder : null,
                AltSnapshotFolder = t.Rows[0]["alt_snapshot_folder"] is string alt_snapshot_folder ? alt_snapshot_folder : null
            };
        }

        /// <summary>
        /// Executes the 'sp_helpsubscription' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <returns></returns>
        public static async Task<HelpSubscriptionResults[]> ExecuteSpHelpSubscriptionAsync(
            this SqlConnection connection,
            string publication = null,
            string article = null,
            string subscriber = null,
            string destinationDb = null,
            string publisher = null)
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.CommandText = "sp_helpsubscription";

            if (publication != null)
                cmd.Parameters.AddWithValue("@publication", publication);
            if (article != null)
                cmd.Parameters.AddWithValue("@article", article);
            if (subscriber != null)
                cmd.Parameters.AddWithValue("@subscriber", subscriber);
            if (destinationDb != null)
                cmd.Parameters.AddWithValue("@destination_db", destinationDb);
            if (publisher != null)
                cmd.Parameters.AddWithValue("@publisher", publisher);

            using var r = await cmd.ExecuteReaderAsync();
            var t = new DataTable();
            t.Load(r);

            var l = t.Rows.Cast<DataRow>()
                .Select(i => new HelpSubscriptionResults()
                {
                    Subscriber = (string)i["subscriber"],
                    Publication = (string)i["publication"],
                    Article = i["article"] is string article ? article : null,
                    DestinationDatabase = (string)i["destination database"],
                    SubscriptionStatus = (int)i["subscription status"],
                    SynchronizationType = (int)i["synchronization type"],
                    SubscriptionType = (int)i["subscription type"],
                })
                .ToArray();

            return l;
        }

        /// <summary>
        /// Executes the 'sp_helppublication_snapshot' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<HelpPublicationSnapshotResults> ExecuteSpHelpPublicationSnapshotAsync(
            this SqlConnection connection,
            string publication,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync($@"EXEC sp_helppublication_snapshot @publication = {publication}", cancellationToken: cancellationToken);
            if (t.Rows.Count < 1)
                return null;

            return new HelpPublicationSnapshotResults()
            {
                JobLogin = t.Rows[0]["job_login"] is string job_login ? job_login : null,
            };
        }

        /// <summary>
        /// Executes the 'sp_helppublication_snapshot' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="dbName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<HelpReplicationDbOption> ExecuteSpHelpReplicationDbOptionAsync(
            this SqlConnection connection,
            string dbName,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync($@"EXEC sp_helpreplicationdboption @dbname = {dbName}", cancellationToken: cancellationToken);
            if (t.Rows.Count < 1)
                return null;

            return new HelpReplicationDbOption()
            {
                Name = (string)t.Rows[0]["name"],
                Id = (int)t.Rows[0]["id"],
                TransactionalPublish = (bool)t.Rows[0]["transpublish"],
                MergePublish = (bool)t.Rows[0]["mergepublish"],
                DbOwner = (bool)t.Rows[0]["dbowner"],
                DbReadOnly = (bool)t.Rows[0]["dbreadonly"],
            };
        }

        /// <summary>
        /// Executes the 'sp_helplogreader_agent' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<HelpLogReaderAgentResults> ExecuteSpHelpLogReaderAgentAsync(
            this SqlConnection connection,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync("EXEC sp_helplogreader_agent", cancellationToken: cancellationToken);
            if (t.Rows.Count < 1)
                return null;

            return new HelpLogReaderAgentResults()
            {
                Id = t.Rows[0]["id"] is int id ? (int?)id : null,
                Name = t.Rows[0]["name"] is string name ? name : null,
                PublisherSecurityMode = t.Rows[0]["publisher_security_mode"] is short publisherSecurityMode ? (short?)publisherSecurityMode : null,
                PublisherLogin = t.Rows[0]["publisher_login"] is string publisherLogin ? publisherLogin : null,
                PublisherPassword = t.Rows[0]["publisher_password"] is string publisher_password ? publisher_password : null,
                JobId = t.Rows[0]["job_id"] is Guid job_id ? (Guid?)job_id : null,
                JobLogin = t.Rows[0]["job_login"] is string job_login ? job_login : null,
                JobPassword = t.Rows[0]["job_password"] is string job_password ? job_password : null,
            };
        }

        /// <summary>
        /// Executes the 'sp_adddistpublisher' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publisher"></param>
        /// <param name="distributionDb"></param>
        /// <param name="securityMode"></param>
        /// <param name="trusted"></param>
        /// <param name="thirdPartyFlag"></param>
        /// <param name="publisherType"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddDistPublisherAsync(
            this SqlConnection connection,
            string publisher,
            string distributionDb,
            int securityMode,
            string trusted,
            int thirdPartyFlag,
            string publisherType,
            SqlTransaction transaction = null,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_adddistpublisher
                    @publisher = {publisher},
                    @distribution_db = {distributionDb},
                    @security_mode = {securityMode},
                    @trusted = {trusted},
                    @thirdparty_flag = {thirdPartyFlag},
                    @publisher_type = {publisherType}",
                transaction,
                cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_startpublication_snapshot' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpStartPublicationSnapshotAsync(
            this SqlConnection connection,
            string publication,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_startpublication_snapshot
                    @publication = {publication}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_addpublication' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="status"></param>
        /// <param name="allowPush"></param>
        /// <param name="allowPull"></param>
        /// <param name="independentAgent"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddPublicationAsync(
            this SqlConnection connection,
            string publication,
            string status,
            bool allowPush,
            bool allowPull,
            bool independentAgent,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_addpublication
                    @publication = {publication},
                    @status = {status},
                    @allow_push = {(allowPush ? "true" : "false")},
                    @allow_pull = {(allowPull ? "true" : "false")},
                    @independent_agent = {(independentAgent ? "true" : "false")}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_addpublication_snapshot' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="jobLogin"></param>
        /// <param name="jobPassword"></param>
        /// <param name="publisherSecurityMode"></param>
        /// <param name="publisherLogin"></param>
        /// <param name="publisherPassword"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddPublicationSnapshotAsync(
            this SqlConnection connection,
            string publication,
            string jobLogin,
            string jobPassword,
            int? publisherSecurityMode,
            string publisherLogin,
            string publisherPassword,
            CancellationToken cancellationToken = default)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_addpublication_snapshot";

                cmd.Parameters.AddWithValue("@publication", publication);

                if (jobLogin != null)
                    cmd.Parameters.AddWithValue("@job_login", jobLogin);

                if (jobPassword != null)
                    cmd.Parameters.AddWithValue("@job_password", jobPassword);

                if (publisherSecurityMode != null)
                    cmd.Parameters.AddWithValue("@publisher_security_mode", (int)publisherSecurityMode);

                if (publisherLogin != null)
                    cmd.Parameters.AddWithValue("@publisher_login", publisherLogin);

                if (publisherPassword != null)
                    cmd.Parameters.AddWithValue("@publisher_password", publisherPassword);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Executes the 'sp_addsubscription' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="subscriber"></param>
        /// <param name="subscriberType"></param>
        /// <param name="subscriptionType"></param>
        /// <param name="destinationDb"></param>
        /// <param name="article"></param>
        /// <param name="syncType"></param>
        /// <param name="updateMode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddSubscriptionAsync(
            this SqlConnection connection,
            string publication,
            string subscriber,
            int subscriberType,
            string subscriptionType,
            string destinationDb,
            string article,
            string syncType,
            string updateMode,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_addsubscription
                    @publication = {publication},
                    @subscriber = {subscriber},
                    @subscriber_type = {subscriberType},
                    @subscription_type = {subscriptionType},
                    @destination_db = {destinationDb},
                    @article = {article},
                    @sync_type = {syncType},
                    @update_mode = {updateMode}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_addlogreader_agent' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="jobLogin"></param>
        /// <param name="jobPassword"></param>
        /// <param name="publisherSecurityMode"></param>
        /// <param name="publisherLogin"></param>
        /// <param name="publisherPassword"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddLogReaderAgentAsync(
            this SqlConnection connection,
            string jobLogin,
            string jobPassword,
            int? publisherSecurityMode,
            string publisherLogin,
            string publisherPassword,
            CancellationToken cancellationToken = default)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_addlogreader_agent";

                if (jobLogin != null)
                    cmd.Parameters.AddWithValue("@job_login", jobLogin);

                if (jobLogin != null)
                    cmd.Parameters.AddWithValue("@job_password", jobPassword);

                if (publisherSecurityMode != null)
                    cmd.Parameters.AddWithValue("@publisher_security_mode", (int)publisherSecurityMode);

                if (publisherLogin != null)
                    cmd.Parameters.AddWithValue("@publisher_login ", publisherLogin);

                if (publisherPassword != null)
                    cmd.Parameters.AddWithValue("@publisher_password ", publisherPassword);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        /// <summary>
        /// Executes the 'sp_addpushsubscription_agent' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="subscriber"></param>
        /// <param name="subscriberDb"></param>
        /// <param name="subscriberSecurityMode"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddPushSubscriptionAgentAsync(
            this SqlConnection connection,
            string publication,
            string subscriber,
            string subscriberDb,
            int subscriberSecurityMode,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_addpushsubscription_agent
                    @publication = {publication},
                    @subscriber = {subscriber},
                    @subscriber_db = {subscriberDb},
                    @subscriber_security_mode = {subscriberSecurityMode}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_addpushsubscription_agent' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="subscriber"></param>
        /// <param name="subscriberDb"></param>
        /// <param name="subscriberSecurityMode"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddPullSubscriptionAgentAsync(
            this SqlConnection connection,
            string publication,
            string subscriber,
            string subscriberDb,
            int subscriberSecurityMode,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_addpullsubscription_agent
                    @publication = {publication},
                    @subscriber = {subscriber},
                    @subscriber_db = {subscriberDb},
                    @subscriber_security_mode = {subscriberSecurityMode}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Executes the 'sp_addarticle' stored procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="publication"></param>
        /// <param name="article"></param>
        /// <param name="sourceOwner"></param>
        /// <param name="sourceObject"></param>
        /// <param name="type"></param>
        /// <param name="description"></param>
        /// <param name="creationScript"></param>
        /// <param name="preCreationCmd"></param>
        /// <param name="schemaOption"></param>
        /// <param name="identityRangeManagementOption"></param>
        /// <param name="destinationTable"></param>
        /// <param name="destinationOwner"></param>
        /// <param name="status"></param>
        /// <param name="verticalPartition"></param>
        /// <param name="insCmd"></param>
        /// <param name="delCmd"></param>
        /// <param name="updCmd"></param>
        /// <param name="forceInvalidateSnapshot"></param>
        /// <returns></returns>
        public static async Task ExecuteSpAddArticleAsync(
            this SqlConnection connection,
            string publication,
            string article,
            string sourceOwner,
            string sourceObject,
            string destinationTable,
            string destinationOwner,
            int status,
            bool forceInvalidateSnapshot,
            CancellationToken cancellationToken = default)
        {
            await connection.ExecuteNonQueryAsync($@"
                EXEC sp_addarticle
                    @publication = {publication},
                    @article = {article},
                    @source_owner = {sourceOwner},
                    @source_object = {sourceObject},
                    @destination_table = {destinationTable},
                    @destination_owner = {destinationOwner},
                    @status = {status},
                    @force_invalidate_snapshot = {(forceInvalidateSnapshot ? 1 : 0)}",
                cancellationToken: cancellationToken);
        }

        public class XpFileExistResults
        {

            public byte FileExists { get; set; }

            public byte FileIsADirectory { get; set; }

            public byte ParentDirectoryExists { get; set; }

        }

        /// <summary>
        /// Executes the 'xp_fileexist' extended procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="fullPath"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<XpFileExistResults> ExecuteXpFileExist(
            this SqlConnection connection,
            string fullPath,
            CancellationToken cancellationToken = default)
        {
            var t = await connection.LoadDataTableAsync($@"EXEC xp_fileexist @fullPath = {fullPath}", cancellationToken: cancellationToken);
            if (t.Rows.Count < 1)
                return null;

            return new XpFileExistResults()
            {
                FileExists = t.Rows[0]["File Exists"] is byte exists ? exists : (byte)0,
                FileIsADirectory = t.Rows[0]["File is a Directory"] is byte isDirectory ? isDirectory : (byte)0,
                ParentDirectoryExists = t.Rows[0]["Parent Directory Exists"] is byte publisherSecurityMode ? publisherSecurityMode : (byte)0,
            };
        }

        /// <summary>
        /// Executes the 'xp_delete_files' extended procedure.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="files"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<int> ExecuteXpDeleteFiles(
            this SqlConnection connection,
            string[] files,
            CancellationToken cancellationToken = default)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.Text;

                var o = cmd.CreateParameter();
                o.ParameterName = "@output";
                o.Direction = ParameterDirection.Output;
                o.DbType = DbType.Int32;
                cmd.Parameters.Add(o);

                var s = new StringBuilder("EXEC @output = xp_delete_files ");
                var l = new string[files.Length];
                for (var i = 0; i < files.Length; i++)
                {
                    var z = cmd.CreateParameter();
                    z.ParameterName = "@p" + i;
                    z.DbType = DbType.String;
                    z.Value = files[i];
                    l[i] = z.ParameterName;
                    cmd.Parameters.Add(z);
                }
                s.Append(string.Join(", ", l));

                cmd.CommandText = s.ToString();

                await cmd.ExecuteNonQueryAsync(cancellationToken);
                return (int)o.Value;
            }
        }

    }

}
