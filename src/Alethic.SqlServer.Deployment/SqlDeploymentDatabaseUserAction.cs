using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures a database user mapped to a server login, and its membership of database roles. The
    /// user is found by the login's SID; failing that, a user of its name that maps to another login,
    /// or to none, as a restore from another server leaves it, is mapped to the login; failing that,
    /// the user is created. It never drops a user, nor removes a membership.
    /// </summary>
    public class SqlDeploymentDatabaseUserAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="databaseName"></param>
        /// <param name="name"></param>
        /// <param name="login"></param>
        /// <param name="roles"></param>
        public SqlDeploymentDatabaseUserAction(SqlInstance instance, string databaseName, string name, string login, IReadOnlyList<string> roles) :
            base(instance)
        {
            DatabaseName = databaseName ?? throw new ArgumentNullException(nameof(databaseName));
            Name = string.IsNullOrEmpty(name) ? throw new ArgumentException("A user needs a name.", nameof(name)) : name;
            Login = string.IsNullOrEmpty(login) ? throw new ArgumentException("A user needs a login.", nameof(login)) : login;
            Roles = roles ?? Array.Empty<string>();
        }

        /// <summary>
        /// Gets the name of the database the user is in.
        /// </summary>
        public string DatabaseName { get; }

        /// <summary>
        /// Gets the name the user is created with.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the server login the user maps to.
        /// </summary>
        public string Login { get; }

        /// <summary>
        /// Gets the database roles the user is a member of.
        /// </summary>
        public IReadOnlyList<string> Roles { get; }

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

            // under a dry run the database may be one the deployment would only now create
            if (await cnn.ExecuteScalarAsync($"SELECT DB_ID({DatabaseName})", cancellationToken: cancellationToken) is DBNull)
            {
                Fail(context, $"Database '{DatabaseName}' does not exist, so its user {Name} cannot be ensured.", $"Dry run: would create user {Name} for login {Login} in {DatabaseName} once it exists.");
                return;
            }

            if (await cnn.ExecuteScalarAsync($"SELECT SUSER_SID({Login})", cancellationToken: cancellationToken) is DBNull)
            {
                Fail(context, $"Login '{Login}' does not exist, so database user {Name} in {DatabaseName} cannot map to it.", null);
                return;
            }

            cnn.ChangeDatabase(DatabaseName);

            // the user mapped to the login, whatever its name; else one of the name it should have
            var user = await cnn.ExecuteScalarAsync($"SELECT name FROM sys.database_principals WHERE sid = SUSER_SID({Login})", cancellationToken: cancellationToken) as string;
            if (user == null)
            {
                var named = await cnn.ExecuteScalarAsync($"SELECT type FROM sys.database_principals WHERE name = {Name}", cancellationToken: cancellationToken) as string;
                if (named != null && named.Trim() is not ("S" or "U" or "G"))
                {
                    Fail(context, $"'{Name}' in {DatabaseName} is not a user that can map to a login (its type is '{named.Trim()}'); it is left as it is.", null);
                    return;
                }

                if (context.DryRun)
                {
                    context.Logger.LogInformation(named != null ? "Dry run: would map user {Name} in {DatabaseName} to login {Login}." : "Dry run: would create user {Name} in {DatabaseName} for login {Login}.", Name, DatabaseName, Login);
                }
                else if (named != null)
                {
                    await cnn.ExecuteNonQueryAsync($@"
                        DECLARE @sql nvarchar(max) = N'ALTER USER ' + QUOTENAME({Name}) + N' WITH LOGIN = ' + QUOTENAME({Login});
                        EXEC sys.sp_executesql @sql;", cancellationToken: cancellationToken);
                    context.Logger.LogInformation("Mapped user {Name} in {DatabaseName} to login {Login}.", Name, DatabaseName, Login);
                }
                else
                {
                    await cnn.ExecuteNonQueryAsync($@"
                        DECLARE @sql nvarchar(max) = N'CREATE USER ' + QUOTENAME({Name}) + N' FOR LOGIN ' + QUOTENAME({Login});
                        EXEC sys.sp_executesql @sql;", cancellationToken: cancellationToken);
                    context.Logger.LogInformation("Created user {Name} in {DatabaseName} for login {Login}.", Name, DatabaseName, Login);
                }

                user = Name;
            }

            // the database's owner maps to dbo, which can join no role and needs none
            if (user == "dbo")
            {
                if (Roles.Count > 0)
                    context.Logger.LogInformation("Login {Login} owns {DatabaseName}; it needs no role there.", Login, DatabaseName);
                return;
            }

            foreach (var role in Roles)
            {
                if (await cnn.ExecuteScalarAsync($"SELECT type FROM sys.database_principals WHERE name = {role}", cancellationToken: cancellationToken) is not string type || type.Trim() != "R")
                {
                    Fail(context, $"Role '{role}' does not exist in {DatabaseName}, so user {user} cannot join it.", null);
                    continue;
                }

                if (await cnn.ExecuteScalarAsync($"SELECT IS_ROLEMEMBER({role}, {user})", cancellationToken: cancellationToken) is int and 1)
                    continue;

                if (context.DryRun)
                {
                    context.Logger.LogInformation("Dry run: would add user {User} to role {Role} in {DatabaseName}.", user, role, DatabaseName);
                    continue;
                }

                await cnn.ExecuteNonQueryAsync($@"
                    DECLARE @sql nvarchar(max) = N'ALTER ROLE ' + QUOTENAME({role}) + N' ADD MEMBER ' + QUOTENAME({user});
                    EXEC sys.sp_executesql @sql;", cancellationToken: cancellationToken);
                context.Logger.LogInformation("Added user {User} to role {Role} in {DatabaseName}.", user, role, DatabaseName);
            }
        }

        /// <summary>
        /// Stops the deployment over a user that cannot be brought into line; a dry run reports it and
        /// carries on, so the rest of the plan is still reported.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="message"></param>
        /// <param name="dryRunMessage">What a dry run reports instead, when the problem is only that the
        /// dry run made nothing; null when a deployment would fail too.</param>
        static void Fail(SqlDeploymentExecuteContext context, string message, string dryRunMessage)
        {
            if (context.DryRun)
                context.Logger.LogWarning(dryRunMessage ?? $"Dry run: a deployment would fail here: {message}");
            else
                throw new SqlDeploymentException(message);
        }

    }

}
