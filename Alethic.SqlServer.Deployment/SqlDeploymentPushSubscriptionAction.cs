﻿using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a push subscription is in place at the publisher.
    /// </summary>
    public class SqlDeploymentPushSubscriptionAction : SqlDeploymentSubscriptionAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="publisherInstance"></param>
        /// <param name="publicationDatabaseName"></param>
        /// <param name="publicationName"></param>
        public SqlDeploymentPushSubscriptionAction(
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
            if (sub.Database != DatabaseName)
                sub.ChangeDatabase(DatabaseName);
            var subServerName = await sub.GetServerNameAsync(cancellationToken);

            using var pub = await OpenConnectionAsync(PublisherInstance, cancellationToken);
            if (pub.Database != PublicationDatabaseName)
                pub.ChangeDatabase(PublicationDatabaseName);
            var pubServerName = await pub.GetServerNameAsync(cancellationToken);

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
                        AND     u.srvname = {subServerName}
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
                        subscriber: subServerName,
                        subscriberType: 0,
                        subscriptionType: "Push",
                        destinationDb: DatabaseName,
                        article: article.ArticleName,
                        syncType: "automatic",
                        updateMode: "read only",
                        cancellationToken: cancellationToken);

            try
            {
                await pub.ExecuteSpAddPushSubscriptionAgentAsync(
                    publication: PublicationName,
                    subscriber: subServerName,
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
