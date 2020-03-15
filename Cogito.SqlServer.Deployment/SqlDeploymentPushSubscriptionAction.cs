using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a push subscription is in place at the publisher.
    /// </summary>
    public class SqlDeploymentPushSubscriptionAction : SqlDeploymentSubscriptionAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="publisherInstanceName"></param>
        /// <param name="publicationDatabaseName"></param>
        /// <param name="publicationName"></param>
        public SqlDeploymentPushSubscriptionAction(
            string instanceName,
            string databaseName,
            string publisherInstanceName,
            string publicationDatabaseName,
            string publicationName) :
            base(instanceName, databaseName, publisherInstanceName, publicationDatabaseName, publicationName)
        {

        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var sub = await OpenConnectionAsync(cancellationToken);
            sub.ChangeDatabase(DatabaseName);
            var subServerName = await sub.GetServerNameAsync();

            using var pub = await OpenConnectionAsync(PublisherInstanceName, cancellationToken);
            pub.ChangeDatabase(PublicationDatabaseName);
            var pubServerName = await pub.GetServerNameAsync();

            await pub.ExecuteNonQueryAsync($@"
                IF NOT EXISTS ( SELECT * FROM syssubscriptions WHERE srvname = {subServerName} AND dest_db = {DatabaseName} )
                    EXEC sp_addsubscription
                        @publication = {PublicationName},
                        @subscriber = {subServerName},
                        @destination_db = {DatabaseName},
                        @subscription_type = N'Push',
                        @sync_type = N'automatic',
                        @article = N'all',
                        @update_mode = N'read only',
                        @subscriber_type = 0");

            var articles = (await pub.LoadDataTableAsync($@"
                SELECT      DISTINCT
                            NULLIF(a.artid, '')             as article_id,
                            NULLIF(a.name, '')              as article_name,
                            COALESCE(u.srvname, '')         as subscriber_name
                FROM        syspublications p
                INNER JOIN  sysarticles a
                    ON      a.pubid = p.pubid
                INNER JOIN  sys.tables t
                    ON      t.object_id = a.objid
                INNER JOIN  sys.schemas s
                    ON      s.schema_id = t.schema_id
                LEFT JOIN   syssubscriptions u
                    ON      u.artid = a.artid
                    AND     u.srvname = {subServerName}
                WHERE       p.name = {PublicationName}"))
                .Rows.Cast<DataRow>()
                .Select(i => new
                {
                    ArticleId = (int)i["article_id"],
                    ArticleName = (string)i["article_name"],
                    SubscriberName = (string)i["subscriber_name"]
                });

            // add missing articles to the subscription
            foreach (var article in articles)
                if (!string.IsNullOrEmpty(article.ArticleName) && string.IsNullOrEmpty(article.SubscriberName))
                    await pub.ExecuteSpAddSubscriptionAsync(
                        publication: PublicationName,
                        subscriber: subServerName,
                        subscriberType: 0,
                        subscriptionType: "Push",
                        destinationDb: DatabaseName,
                        article: article.ArticleName,
                        syncType: "automatic",
                        updateMode: "read only");

            try
            {
                await pub.ExecuteSpAddPushSubscriptionAgentAsync(
                    publication: PublicationName,
                    subscriber: subServerName,
                    subscriberDb: DatabaseName,
                    subscriberSecurityMode: 1);
            }
            catch (Exception e)
            {

            }

            // start publication
            try
            {
                await pub.ExecuteSpStartPublicationSnapshotAsync(PublicationName);
            }
            catch (Exception)
            {
                // ignore
            }
        }

    }

}
