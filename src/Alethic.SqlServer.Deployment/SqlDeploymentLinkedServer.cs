using System.Collections.Generic;

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
        /// Gets or sets the remote login the linked server authenticates with, mapped for all
        /// local logins. When absent (or when the expression expands to an empty string) local
        /// logins map to themselves instead.
        /// </summary>
        public SqlDeploymentExpression? RemoteUser { get; set; }

        /// <summary>
        /// Gets or sets the password of the remote login.
        /// </summary>
        public SqlDeploymentExpression? RemotePassword { get; set; }

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

            // an empty expansion means unset, so a manifest can wire the attributes to optional
            // parameters that default to empty
            var remoteUser = RemoteUser?.Expand(context);
            if (string.IsNullOrEmpty(remoteUser))
                remoteUser = null;
            var remotePassword = RemotePassword?.Expand(context);
            if (string.IsNullOrEmpty(remotePassword))
                remotePassword = null;

            if (remoteUser == null != (remotePassword == null))
                throw new SqlDeploymentException($"Linked server '{Name.Expression}' specifies only one of RemoteUser and RemotePassword; they must be provided together.");

            yield return new SqlDeploymentLinkedServerAction(
                context.Instance,
                Name.Expand(context),
                product,
                provider,
                ProviderString?.Expand(context),
                DataSource?.Expand(context),
                Location?.Expand(context),
                Catalog?.Expand(context),
                remoteUser,
                remotePassword);
        }

    }

}
