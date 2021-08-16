using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a subscription is in place at the publisher.
    /// </summary>
    public class SqlDeploymentPullSubscriptionAction : SqlDeploymentSubscriptionAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="publisherInstance"></param>
        /// <param name="publicationDatabaseName"></param>
        /// <param name="publicationName"></param>
        public SqlDeploymentPullSubscriptionAction(
            SqlInstance instance,
            string databaseName,
            SqlInstance publisherInstance,
            string publicationDatabaseName,
            string publicationName) :
            base(instance, databaseName, publisherInstance, publicationDatabaseName, publicationName)
        {

        }

        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var sub = await OpenConnectionAsync(cancellationToken);
            sub.ChangeDatabase(DatabaseName);

            using var pub = await OpenConnectionAsync(PublisherInstance, cancellationToken);
            pub.ChangeDatabase(PublicationDatabaseName);

            await pub.ExecuteNonQueryAsync($@"
                IF NOT EXISTS ( SELECT * FROM syssubscriptions WHERE srvname = {Instance} AND dest_db = {DatabaseName} )
                    EXEC sp_addsubscription
                        @publication = {PublicationName},
                        @subscriber = {Instance},
                        @destination_db = {DatabaseName},
                        @subscription_type = N'Pull',
                        @sync_type = N'automatic',
                        @article = N'all',
                        @update_mode = N'read only',
                        @subscriber_type = 0",
                cancellationToken: cancellationToken);

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
                        AND     u.srvname = {Instance}
                    WHERE       p.name = {PublicationName}",
                    cancellationToken: cancellationToken))
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
                        subscriber: Instance.Name,
                        subscriberType: 0,
                        subscriptionType: "Pull",
                        destinationDb: DatabaseName,
                        article: article.ArticleName,
                        syncType: "automatic",
                        updateMode: "read only",
                        cancellationToken: cancellationToken);

            try
            {
                await sub.ExecuteSpAddPullSubscriptionAgentAsync(
                    publication: PublicationName,
                    subscriber: Instance.Name,
                    subscriberDb: DatabaseName,
                    subscriberSecurityMode: 1,
                    cancellationToken: cancellationToken);
            }
            catch (Exception e)
            {

            }

            // start publication
            try
            {
                await pub.ExecuteSpStartPublicationSnapshotAsync(PublicationName, cancellationToken);
            }
            catch (Exception)
            {
                // ignore
            }
        }

    }

}
