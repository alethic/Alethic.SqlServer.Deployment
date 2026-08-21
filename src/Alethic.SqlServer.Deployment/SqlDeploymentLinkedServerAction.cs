using System;
using System.Threading;
using System.Threading.Tasks;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Ensures the deployment of a linked server.
    /// </summary>
    public class SqlDeploymentLinkedServerAction : SqlDeploymentAction
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instance"></param>
        /// <param name="name"></param>
        /// <param name="product"></param>
        /// <param name="provider"></param>
        /// <param name="providerString"></param>
        /// <param name="dataSource"></param>
        /// <param name="location"></param>
        /// <param name="catalog"></param>
        /// <param name="remoteUser"></param>
        /// <param name="remotePassword"></param>
        public SqlDeploymentLinkedServerAction(SqlInstance instance, string name, string product, string provider, string providerString, string dataSource, string location, string catalog, string remoteUser = null, string remotePassword = null) :
            base(instance)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Product = product;
            Provider = provider;
            ProviderString = providerString;
            DataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            Location = location;
            Catalog = catalog;
            RemoteUser = remoteUser;
            RemotePassword = remotePassword;
        }

        /// <summary>
        /// Gets the name of the linked server.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the product of the linked server.
        /// </summary>
        public string Product { get; }

        /// <summary>
        /// Gets the provider of the linked server.
        /// </summary>
        public string Provider { get; }

        /// <summary>
        /// Gets the provider string of the linked server.
        /// </summary>
        public string ProviderString { get; }

        /// <summary>
        /// Gets the data source of the linked server.
        /// </summary>
        public string DataSource { get; }

        /// <summary>
        /// Gets the location of the linked server.
        /// </summary>
        public string Location { get; }

        /// <summary>
        /// Gets the catalog of the linked server.
        /// </summary>
        public string Catalog { get; }

        /// <summary>
        /// Gets the remote login the linked server authenticates with, mapped for all local
        /// logins. When null local logins map to themselves instead.
        /// </summary>
        public string RemoteUser { get; }

        /// <summary>
        /// Gets the password of the remote login.
        /// </summary>
        public string RemotePassword { get; }

        /// <inheritdoc />
        public override bool SupportsDryRun => true;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<bool> ShouldExecute(SqlConnection cnn, CancellationToken cancellationToken)
        {
            var t = await cnn.LoadDataTableAsync(@$"
                SELECT      *
                FROM        sys.servers
                WHERE       name = {Name}");
            if (t.Rows.Count == 0)
                return true;

            var r = t.Rows[0];
            if (Product != null && (string)r["product"] != Product)
                return true;
            if (Provider != null && (string)r["provider"] != Provider)
                return true;
            if (ProviderString != null && (string)r["provider_string"] != ProviderString)
                return true;
            if (DataSource != null && (string)r["data_source"] != DataSource)
                return true;
            if (Location != null && (string)r["location"] != Location)
                return true;
            if (Catalog != null && (string)r["catalog"] != Catalog)
                return true;

            return false;
        }

        public override async Task ExecuteAsync(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using (var cnn = await OpenConnectionAsync(cancellationToken))
            {
                // a dry run reports each step and changes nothing; the mapping refresh and
                // connectivity test are unconditional on a real deploy (the stored remote
                // password cannot be read back for comparison), so they report unconditionally
                if (context.DryRun)
                {
                    if (await ShouldExecute(cnn, cancellationToken))
                        context.Logger.LogInformation("Dry run: would drop and recreate linked server {Name} pointing at {DataSource}.", Name, DataSource);
                    else
                        context.Logger.LogInformation("Dry run: linked server {Name} definition already matches.", Name);

                    if (RemoteUser != null)
                        context.Logger.LogInformation("Dry run: would refresh the default login mapping of {Name} to remote login {RemoteUser} and test connectivity.", Name, RemoteUser);
                    else
                        context.Logger.LogInformation("Dry run: would refresh the default login mapping of {Name} to self (passthrough) and test connectivity.", Name);

                    return;
                }

                // recreate the server definition when it is missing or differs; the login mapping
                // and connectivity test below run either way, so a changed remote credential
                // takes effect on redeploy without recreating a matching server
                if (await ShouldExecute(cnn, cancellationToken))
                    await CreateServerAsync(cnn, cancellationToken);

                // replace whatever mapping exists for the default (all local logins) entry
                await cnn.ExecuteNonQueryAsync($@"
                    BEGIN TRY
                        EXEC sp_droplinkedsrvlogin
                            @rmtsrvname = {Name},
                            @locallogin = NULL
                    END TRY
                    BEGIN CATCH
                    END CATCH");

                if (RemoteUser != null)
                    await cnn.ExecuteNonQueryAsync($@"
                        EXEC sp_addlinkedsrvlogin
                            @rmtsrvname = {Name},
                            @locallogin = NULL,
                            @useself = N'False',
                            @rmtuser = {RemoteUser},
                            @rmtpassword = {RemotePassword}");
                else
                    await cnn.ExecuteNonQueryAsync($@"
                        EXEC sp_addlinkedsrvlogin
                            @rmtsrvname = {Name},
                            @locallogin = NULL,
                            @useself = N'True'");

                // ensure the linked server is configured correctly and accessible
                await cnn.ExecuteNonQueryAsync($"EXEC sp_testlinkedserver @servername = {Name}");
            }
        }

        /// <summary>
        /// Drops any existing linked server definition and creates the configured one.
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task CreateServerAsync(SqlConnection cnn, CancellationToken cancellationToken)
        {
            await cnn.ExecuteNonQueryAsync($@"
                IF EXISTS ( SELECT * FROM sys.servers WHERE name = {Name} )
                BEGIN
                    EXEC sp_dropserver
                        @server = {Name},
                        @droplogins = 'droplogins'
                END");

            using (var cmd = cnn.CreateCommand())
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.CommandText = "sp_addlinkedserver";

                var p0 = cmd.CreateParameter();
                p0.ParameterName = "@server";
                p0.Value = Name;
                cmd.Parameters.Add(p0);

                var p1 = cmd.CreateParameter();
                p1.ParameterName = "@srvproduct";
                p1.Value = Product ?? "";
                cmd.Parameters.Add(p1);

                var p2 = cmd.CreateParameter();
                p2.ParameterName = "@datasrc";
                p2.Value = DataSource;
                cmd.Parameters.Add(p2);

                if (Provider != null)
                {
                    var p3 = cmd.CreateParameter();
                    p3.ParameterName = "@provider";
                    p3.Value = Provider;
                    cmd.Parameters.Add(p3);
                }

                if (ProviderString != null)
                {
                    var p4 = cmd.CreateParameter();
                    p4.ParameterName = "@provstr";
                    p4.Value = ProviderString;
                    cmd.Parameters.Add(p4);
                }

                if (Location != null)
                {
                    var p5 = cmd.CreateParameter();
                    p5.ParameterName = "@location";
                    p5.Value = Location;
                    cmd.Parameters.Add(p5);
                }

                if (Catalog != null)
                {
                    var p6 = cmd.CreateParameter();
                    p6.ParameterName = "@catalog";
                    p6.Value = Catalog;
                    cmd.Parameters.Add(p6);
                }

                await cmd.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Attempts to normalize the data source value.
        /// </summary>
        /// <returns></returns>
        async Task<string> NormalizeDataSource(CancellationToken cancellationToken)
        {
            var targetDataSource = DataSource;

            if (string.IsNullOrEmpty(Product) || Product == "SQL Server")
            {
                using (var source = await OpenConnectionAsync(cancellationToken))
                using (var target = new SqlConnection($"Data Source={DataSource};Integrated Security=SSPI;"))
                {
                    await target.OpenAsync();

                    // get the fully qualified name of the target
                    var qualifedName = await target.GetFullyQualifiedServerName();
                    if (await target.GetServerInstanceName() is string instanceName)
                        qualifedName += $@"\{instanceName}";

                    // check if domains are different, if so, prefer qualified name
                    if (await source.GetServerDomainName() != await target.GetServerDomainName())
                        targetDataSource = qualifedName;
                }
            }

            return targetDataSource;
        }

    }

}
