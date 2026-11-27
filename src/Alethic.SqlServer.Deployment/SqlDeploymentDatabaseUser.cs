using System.Collections.Generic;
using System.Linq;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a database user mapped to a server login, and its membership of database roles.
    /// </summary>
    public class SqlDeploymentDatabaseUser
    {

        /// <summary>
        /// Gets or sets the server login the user maps to: a SQL login, or a Windows login or group.
        /// When absent, or when the expression expands to an empty string, the user is left alone and
        /// nothing is compiled, so a manifest can wire it to optional parameters that default to empty.
        /// </summary>
        public SqlDeploymentExpression? Login { get; set; }

        /// <summary>
        /// Gets or sets the name of the user; the login's name when absent or empty.
        /// </summary>
        public SqlDeploymentExpression? Name { get; set; }

        /// <summary>
        /// Gets the database roles the user is a member of. A role the user is already a member of is
        /// left as it is, and no membership is ever removed.
        /// </summary>
        public ICollection<SqlDeploymentExpression> Roles { get; } = new List<SqlDeploymentExpression>();

        /// <summary>
        /// Generates the steps required to ensure the user.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="databaseName"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context, string databaseName)
        {
            // an empty expansion means unset: no user is described, so none is touched
            var login = Login?.Expand(context);
            if (string.IsNullOrEmpty(login))
                yield break;

            var name = Name?.Expand(context);
            if (string.IsNullOrEmpty(name))
                name = login;

            var roles = Roles.Select(i => i.Expand(context)).Where(i => string.IsNullOrEmpty(i) == false).ToArray();

            yield return new SqlDeploymentDatabaseUserAction(context.Instance, databaseName, name, login, roles);
        }

    }

}
