using System;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    public class SqlDeploymentPublicationArticleTableStep : SqlDeploymentPublicationArticleStep
    {

        class TableArticle
        {

            /// <summary>
            /// Initializes a new instance.
            /// </summary>
            /// <param name="schemaId"></param>
            /// <param name="schemaName"></param>
            /// <param name="objectId"></param>
            /// <param name="objectName"></param>
            /// <param name="publicationId"></param>
            /// <param name="publicationName"></param>
            /// <param name="articleId"></param>
            /// <param name="articleName"></param>
            public TableArticle(int schemaId, string schemaName, int objectId, string objectName, int publicationId, string publicationName, object articleId, object articleName)
            {
                SchemaId = schemaId;
                SchemaName = schemaName;
                ObjectId = objectId;
                ObjectName = objectName;
                PublicationId = publicationId;
                PublicationName = publicationName;
                ArticleId = articleId;
                ArticleName = articleName;
            }

            public int SchemaId { get; }

            public string SchemaName { get; }

            public int ObjectId { get; }

            public string ObjectName { get; }

            public int PublicationId { get; }

            public string PublicationName { get; }

            public object ArticleId { get; }

            public object ArticleName { get; }

        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="publicationName"></param>
        /// <param name="name"></param>
        public SqlDeploymentPublicationArticleTableStep(string instanceName, string databaseName, string publicationName, string name) :
            base(instanceName, databaseName, publicationName, name)
        {

        }

        /// <summary>
        /// Loads the table articles.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<TableArticle> LoadTableArticle(SqlConnection connection, CancellationToken cancellationToken)
        {
            return (await connection.LoadDataTableAsync($@"
                    SELECT      s.schema_id         AS schema_id,
                                NULLIF(s.name, '')  AS schema_name,
                                t.object_id         AS object_id,
                                NULLIF(t.name, '')  AS object_name,
                                p.pubid             AS publication_id,
                                NULLIF(p.name, '')  AS publication_name,
                                a.artid             AS article_id,
                                NULLIF(a.name, '')  AS article_name
                    FROM        sys.tables t
                    INNER JOIN  syspublications p
                        ON      1 = 1
                    INNER JOIN  sys.schemas s
                        ON      s.schema_id = t.schema_id
                    LEFT JOIN   sysarticles a
                        ON      a.objid = t.object_id
                        AND     a.pubid = p.pubid
                    WHERE       p.name = {PublicationName}
                        AND     t.name = {Name}"))
                .Rows.Cast<DataRow>()
                .Select(i => new TableArticle(
                    (int)i["schema_id"],
                    (string)i["schema_name"],
                    (int)i["object_id"],
                    (string)i["object_name"],
                    (int)i["publication_id"],
                    (string)i["publication_name"],
                    i["article_id"] != DBNull.Value ? (int?)i["article_id"] : null,
                    i["article_name"] != DBNull.Value ? (string)i["article_name"] : null))
                .FirstOrDefault();
        }

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var connection = await OpenConnectionAsync(cancellationToken);
            connection.ChangeDatabase(DatabaseName);

            var table = await LoadTableArticle(connection, cancellationToken);
            if (table == null)
                throw new InvalidOperationException($"Missing table '{Name}'.");

            if (table.ArticleId == null)
            {
                await connection.ExecuteSpAddArticleAsync(
                    publication: table.PublicationName,
                    article: table.ObjectName,
                    sourceOwner: table.SchemaName,
                    sourceObject: table.ObjectName,
                    destinationTable: table.ObjectName,
                    destinationOwner: table.SchemaName,
                    status: 24,
                    forceInvalidateSnapshot: true);

                // start publication
                try
                {
                    await connection.ExecuteSpStartPublicationSnapshotAsync(PublicationName);
                }
                catch (SqlException)
                {
                    // ignore
                }
            }
        }

    }

}
