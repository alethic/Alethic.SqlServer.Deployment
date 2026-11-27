using System;
using System.Collections.Generic;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a server login exists as its type, and that a SQL login's password is the one given.
    /// </summary>
    public class SqlDeploymentLogin
    {

        /// <summary>
        /// Gets or sets the name of the login: a SQL login's name, or a Windows principal as
        /// DOMAIN\name (a managed service account's name ends in '$'). When absent, or when the
        /// expression expands to an empty string, the login is left alone and nothing is compiled, so
        /// a manifest can wire it to optional parameters that default to empty.
        /// </summary>
        public SqlDeploymentExpression? Name { get; set; }

        /// <summary>
        /// Gets or sets the type of the login: 'Sql' or 'Windows'. Required when a name is given.
        /// </summary>
        public SqlDeploymentExpression? Type { get; set; }

        /// <summary>
        /// Gets or sets the password of a SQL login. Given, the login is created with it, or an
        /// existing login's password is set to it; absent (or empty), an existing login is only
        /// validated. A Windows login has no password.
        /// </summary>
        public SqlDeploymentExpression? Password { get; set; }

        /// <summary>
        /// Generates the steps required to ensure the login.
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public IEnumerable<SqlDeploymentAction> Compile(SqlDeploymentCompileContext context)
        {
            // an empty expansion means unset: no login is described, so none is touched
            var name = Name?.Expand(context);
            if (string.IsNullOrEmpty(name))
                yield break;

            var typeName = Type?.Expand(context);
            var type = typeName switch
            {
                _ when string.Equals(typeName, "Sql", StringComparison.OrdinalIgnoreCase) => SqlDeploymentLoginType.Sql,
                _ when string.Equals(typeName, "Windows", StringComparison.OrdinalIgnoreCase) => SqlDeploymentLoginType.Windows,
                null or "" => throw new SqlDeploymentException($"Login '{name}' has no Type; it must be 'Sql' or 'Windows'."),
                _ => throw new SqlDeploymentException($"Login '{name}' has Type '{typeName}'; it must be 'Sql' or 'Windows'."),
            };

            var password = Password?.Expand(context);
            if (string.IsNullOrEmpty(password))
                password = null;

            if (type == SqlDeploymentLoginType.Windows && password != null)
                throw new SqlDeploymentException($"Login '{name}' is a Windows login and cannot have a Password.");

            yield return new SqlDeploymentLoginAction(context.Instance, name, type, password);
        }

    }

}
