using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlTypes;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment.Internal
{

    public static class SqlConnectionExtensions
    {

        /// <summary>
        /// Returns the appropriate <see cref="SqlDbType"/> for the given object value.
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        static SqlDbType GetSqlDbType(object o)
        {
            switch (o)
            {
                case null:
                    return SqlDbType.Variant;
                case string s:
                    return SqlDbType.NVarChar;
                case int i:
                    return SqlDbType.Int;
                case short s:
                    return SqlDbType.SmallInt;
                case long l:
                    return SqlDbType.BigInt;
                case byte b:
                    return SqlDbType.TinyInt;
                case Guid p2:
                    return SqlDbType.UniqueIdentifier;
                case byte[] b:
                    return SqlDbType.VarBinary;
                case XDocument a:
                case XElement b:
                case XmlDocument c:
                case XmlElement d:
                    return SqlDbType.Xml;
                default:
                    throw new NotSupportedException($"Unsupported CLR parameter type {o.GetType()}.");
            }
        }

        /// <summary>
        /// Returns the appropriate SQL value for the given object value.
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        static object GetSqlValue(object o)
        {
            switch (o)
            {
                case null:
                    return null;
                case byte[] b:
                    return new SqlBinary(b);
                case XDocument x:
                    return new SqlXml(x.CreateReader());
                case XElement x:
                    return new SqlXml(x.CreateReader());
                case XmlDocument x:
                    return new SqlXml(new XmlNodeReader(x));
                case XmlElement x:
                    return new SqlXml(new XmlNodeReader(x));
                default:
                    return o;
            }
        }

        /// <summary>
        /// Formats the formattable string with parameter names.
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        static string GenerateQueryString(FormattableString query)
        {
            var a = new string[query.ArgumentCount];
            for (var i = 0; i < query.ArgumentCount; i++)
                a[i] = "@p" + i;

            return string.Format(query.Format, a);
        }

        /// <summary>
        /// Generates parameters for the query string.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        static SqlParameter[] GenerateParameters(object[] parameters, Func<SqlParameter> createParameter)
        {
            var o = new SqlParameter[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i] as SqlParameter;
                if (p == null)
                {
                    p = createParameter();
                    p.ParameterName = "@p" + i;
                    p.SqlDbType = GetSqlDbType(parameters[i]);
                    p.SqlValue = GetSqlValue(parameters[i]);
                    p.Direction = ParameterDirection.Input;
                }

                o[i] = p;
            }

            return o;
        }

        /// <summary>
        /// Generates parameters for the query string.
        /// </summary>
        /// <param name="command"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        static IEnumerable<SqlParameter> GenerateParameters(FormattableString query, Func<SqlParameter> createParameter)
        {
            return GenerateParameters(query.GetArguments(), createParameter);
        }

        /// <summary>
        /// Generates a SQL command for the formattable query string.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        static SqlCommand GenerateCommand(SqlConnection connection, SqlTransaction transaction, FormattableString query)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = GenerateQueryString(query);

            // attach transaction
            if (transaction != null)
                cmd.Transaction = transaction;

            // append parameters inferred from format string
            foreach (var p in GenerateParameters(query, () => cmd.CreateParameter()))
                cmd.Parameters.Add(p);

            return cmd;
        }

        /// <summary>
        /// Generates a SQL command for the formattable query string.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="transaction"></param>
        /// <param name="query"></param>
        /// <returns></returns>
        static SqlCommand GenerateCommand(SqlConnection connection, SqlTransaction transaction, string query)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = query;

            // attach transaction
            if (transaction != null)
                cmd.Transaction = transaction;

            return cmd;
        }

        public static async Task<int> ExecuteNonQueryAsync(this SqlConnection connection, FormattableString query, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            using (var cmd = GenerateCommand(connection, transaction, query))
                return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public static async Task<int> ExecuteNonQueryAsync(this SqlConnection connection, SqlString query, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            using (var cmd = GenerateCommand(connection, transaction, query.Value))
                return await cmd.ExecuteNonQueryAsync(cancellationToken);
        }

        public static async Task<object> ExecuteScalarAsync(this SqlConnection connection, FormattableString query, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            using (var cmd = GenerateCommand(connection, transaction, query))
                return await cmd.ExecuteScalarAsync(cancellationToken);
        }

        public static async Task<object> ExecuteScalarAsync(this SqlConnection connection, SqlString query, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            using (var cmd = GenerateCommand(connection, transaction, query.Value))
                return await cmd.ExecuteScalarAsync(cancellationToken);
        }

        /// <summary>
        /// An asynchronous version of <see cref="SqlCommand.ExecuteReaderAsync" /> which executes a Transact-SQL
        /// statement against the connection and returns the rows populated into a <see cref="DataTable"/>. The
        /// cancellation token can be used to request that the operation be abandoned before the command timeout
        /// elapses. Exceptions will be reported via the returned <see cref="Task"/> object.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="query"></param>
        /// <param name="transaction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<DataTable> LoadDataTableAsync(this SqlConnection connection, FormattableString query, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (query == null)
                throw new ArgumentNullException(nameof(query));

            using (var cmd = GenerateCommand(connection, transaction, query))
            {
                var t = new DataTable();
                t.Load(await cmd.ExecuteReaderAsync(cancellationToken));
                return t;
            }
        }

        /// <summary>
        /// An asynchronous version of <see cref="SqlCommand.ExecuteReaderAsync" /> which executes a Transact-SQL
        /// statement against the connection and returns the rows populated into a <see cref="DataTable"/>. The
        /// cancellation token can be used to request that the operation be abandoned before the command timeout
        /// elapses. Exceptions will be reported via the returned <see cref="Task"/> object.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="query"></param>
        /// <param name="transaction"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<DataTable> LoadDataTableAsync(this SqlConnection connection, SqlString query, SqlTransaction transaction = null, CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            using (var cmd = GenerateCommand(connection, transaction, query.Value))
            {
                var t = new DataTable();
                t.Load(await cmd.ExecuteReaderAsync(cancellationToken));
                return t;
            }
        }

        /// <summary>
        /// Returns the named server property.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="serverPropertyName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<string> GetServerPropertyAsync(this SqlConnection connection, string serverPropertyName, CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));
            if (serverPropertyName == null)
                throw new ArgumentNullException(nameof(serverPropertyName));

            var r = await connection.ExecuteScalarAsync($"SELECT SERVERPROPERTY({serverPropertyName})", cancellationToken: cancellationToken);
            return r != DBNull.Value ? (string)r : null;
        }

        /// <summary>
        /// Gets the name of the server.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static Task<string> GetServerNameAsync(this SqlConnection connection, CancellationToken cancellationToken = default)
        {
            return connection.GetServerPropertyAsync("SERVERNAME", cancellationToken);
        }

        /// <summary>
        /// Gets the domain name of the connected server.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<string> GetServerDomainName(this SqlConnection connection, CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            var d = (string)await connection.ExecuteScalarAsync($@"
                DECLARE @DomainName nvarchar(256)
                EXEC    master.dbo.xp_regread 'HKEY_LOCAL_MACHINE', 'SYSTEM\CurrentControlSet\Services\Tcpip\Parameters', N'Domain', @DomainName OUTPUT
                SELECT  COALESCE(@DomainName, '')",
                cancellationToken: cancellationToken);

            return d?.TrimOrNull();
        }

        /// <summary>
        /// Gets the fully qualified name of the connected server.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public static async Task<string> GetFullyQualifiedServerName(this SqlConnection connection, CancellationToken cancellationToken = default)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            var m = await connection.GetServerPropertyAsync("MACHINENAME", cancellationToken);
            var d = await connection.GetServerDomainName(cancellationToken);

            if (!string.IsNullOrWhiteSpace(d))
                return m + "." + d;
            else
                return m;
        }

        /// <summary>
        /// Gets the instance name of the connected server.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        public static async Task<string> GetServerInstanceName(this SqlConnection connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            var n = (await connection.GetServerPropertyAsync("InstanceName"))?.TrimOrNull() ?? "MSSQLSERVER";
            var s = n == "MSSQLSERVER" ? null : n;
            return s;
        }

        /// <summary>
        /// Begins a database application lock.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="resource"></param>
        /// <param name="mode"></param>
        /// <param name="owner"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static async Task<int> GetAppLock(this SqlConnection connection, string resource, string mode = "Exclusive", string owner = "Session", int timeout = 30)
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_getapplock";
                cmd.CommandTimeout = (int)TimeSpan.FromMilliseconds(timeout).TotalSeconds + 1;

                cmd.Parameters.AddWithValue("Resource", resource);
                cmd.Parameters.AddWithValue("LockMode", mode);
                cmd.Parameters.AddWithValue("LockOwner", owner);
                cmd.Parameters.AddWithValue("LockTimeout", timeout);

                var result = cmd.CreateParameter();
                result.DbType = DbType.Int32;
                result.Direction = ParameterDirection.ReturnValue;
                cmd.Parameters.Add(result);

                await cmd.ExecuteNonQueryAsync();

                return (int)result.Value;
            }
        }
        /// <summary>
        /// Begins a database application lock.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="resource"></param>
        /// <param name="mode"></param>
        /// <param name="owner"></param>
        /// <param name="timeout"></param>
        /// <returns></returns>
        public static async Task<int> ReleaseAppLock(this SqlConnection connection, string resource, string owner = "Session")
        {
            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.CommandText = "sp_releaseapplock";

                cmd.Parameters.AddWithValue("Resource", resource);
                cmd.Parameters.AddWithValue("LockOwner", owner);

                var result = cmd.CreateParameter();
                result.DbType = DbType.Int32;
                result.Direction = ParameterDirection.ReturnValue;
                cmd.Parameters.Add(result);

                await cmd.ExecuteNonQueryAsync();

                return (int)result.Value;
            }
        }

    }

}
