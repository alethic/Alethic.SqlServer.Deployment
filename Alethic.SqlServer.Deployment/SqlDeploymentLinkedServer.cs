﻿using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures the existence of a linked server.
    /// </summary>
    public class SqlDeploymentLinkedServer
    {

        /// <summary>
        /// Gets or sets the name of the linked server.
        /// </summary>
        public SqlDeploymentExpression Name { get; set; }

        public SqlDeploymentExpression? Product { get; set; }

        public SqlDeploymentExpression? Provider { get; set; }

        public SqlDeploymentExpression? ProviderString { get; set; }

        public SqlDeploymentExpression? DataSource { get; set; }

        public SqlDeploymentExpression? Location { get; set; }

        public SqlDeploymentExpression? Catalog { get; set; }

        /// <summary>
        /// Generates the steps required to ensure the linked server.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            var product = Product?.Expand(context);
            var provider = Provider?.Expand(context);

            // default provider; if completely unspecified
            if (product == null || provider == null)
                provider = "MSOLEDBSQL";

            yield return new SqlDeploymentLinkedServerAction(
                context.Instance,
                Name.Expand(context),
                product,
                provider,
                ProviderString?.Expand(context),
                DataSource?.Expand(context),
                Location?.Expand(context),
                Catalog?.Expand(context));
        }

    }

}
