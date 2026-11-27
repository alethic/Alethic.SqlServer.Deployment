using System;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a server login exists as its type, and that a SQL login's password is the one given.
    /// It never drops a login, nor changes one that exists as another type.
    /// </summary>
    public class SqlDeploymentLoginAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="name"></param>
        /// <param name="type"></param>
        /// <param name="password"></param>
        public SqlDeploymentLoginAction(SqlInstance instance, string name, SqlDeploymentLoginType type, string password = null) :
            base(instance)
        {
            if (string.IsNullOrEmpty(name))
                throw new ArgumentException("A login needs a name.", nameof(name));
            if (type == SqlDeploymentLoginType.Windows && password != null)
                throw new ArgumentException("A Windows login has no password.", nameof(password));

            Name = name;
            Type = type;
            Password = password;
        }

        /// <summary>
        /// Gets the name of the login.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the type of the login.
        /// </summary>
        public SqlDeploymentLoginType Type { get; }

        /// <summary>
        /// Gets the password of a SQL login; null to only validate an existing login.
        /// </summary>
        public string Password { get; }

        /// <inheritdoc />
        public override bool SupportsDryRun => true;

        /// <summary>
        /// Applies the step to the instance.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);

            // the login as it stands, if it exists: its type code, and for a SQL login whether its
            // password is the given one (null when it cannot be told, for want of permission to read
            // the hash), so a password that already matches is not needlessly reset
            var t = await cnn.LoadDataTableAsync($@"
                SELECT      p.type,
                            CASE WHEN p.type = 'S' THEN PWDCOMPARE({Password ?? ""}, CONVERT(varbinary(256), LOGINPROPERTY(p.name, 'PasswordHash'))) END AS password_matches
                FROM        sys.server_principals p
                WHERE       p.name = {Name}");

            if (t.Rows.Count == 0)
            {
                // a SQL login cannot be created without its password; a Windows login needs none
                if (Type == SqlDeploymentLoginType.Sql && Password == null)
                {
                    Fail(context, $"SQL login '{Name}' does not exist, and no password was given to create it with.");
                    return;
                }

                if (context.DryRun)
                {
                    context.Logger.LogInformation("Dry run: would create {Type} login {Name}.", Type, Name);
                    return;
                }

                if (Type == SqlDeploymentLoginType.Sql)
                    await cnn.ExecuteNonQueryAsync($@"
                        DECLARE @sql nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME({Name}) + N' WITH PASSWORD = N''' + REPLACE({Password}, N'''', N'''''') + N'''';
                        EXEC sys.sp_executesql @sql;", cancellationToken: cancellationToken);
                else
                    await cnn.ExecuteNonQueryAsync($@"
                        DECLARE @sql nvarchar(max) = N'CREATE LOGIN ' + QUOTENAME({Name}) + N' FROM WINDOWS';
                        EXEC sys.sp_executesql @sql;", cancellationToken: cancellationToken);

                context.Logger.LogInformation("Created {Type} login {Name}.", Type, Name);
                return;
            }

            // a login that exists as another type is not this login: replacing it would take away
            // whatever it is mapped to and granted, so the deployment stops instead
            var code = ((string)t.Rows[0]["type"]).Trim();
            var matchesType = Type == SqlDeploymentLoginType.Sql ? code == "S" : code == "U" || code == "G";
            if (matchesType == false)
            {
                Fail(context, $"Login '{Name}' exists, but not as a {Type} login (its type is '{code}'); it is left as it is.");
                return;
            }

            if (Password == null || t.Rows[0]["password_matches"] is int and 1)
            {
                context.Logger.LogInformation("{Type} login {Name} exists.", Type, Name);
                return;
            }

            if (context.DryRun)
            {
                context.Logger.LogInformation("Dry run: would set the password of SQL login {Name}.", Name);
                return;
            }

            await cnn.ExecuteNonQueryAsync($@"
                DECLARE @sql nvarchar(max) = N'ALTER LOGIN ' + QUOTENAME({Name}) + N' WITH PASSWORD = N''' + REPLACE({Password}, N'''', N'''''') + N'''';
                EXEC sys.sp_executesql @sql;", cancellationToken: cancellationToken);

            context.Logger.LogInformation("Set the password of SQL login {Name}.", Name);
        }

        /// <summary>
        /// Stops the deployment over a login that cannot be brought into line; a dry run reports it
        /// and carries on, so the rest of the plan is still reported.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        static void Fail(SqlDeploymentExecuteContext context, string message)
        {
            if (context.DryRun)
                context.Logger.LogWarning("Dry run: a deployment would fail here: {Message}", message);
            else
                throw new SqlDeploymentException(message);
        }

    }

}
