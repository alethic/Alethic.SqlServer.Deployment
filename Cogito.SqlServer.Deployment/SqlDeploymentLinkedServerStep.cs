using System;
using System.Threading;
using System.Threading.Tasks;

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Ensures the deployment of a linked server.
    /// </summary>
    public class SqlDeploymentLinkedServerStep : SqlDeploymentStep
    {

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        /// <param name="product"></param>
        /// <param name="provider"></param>
        /// <param name="providerString"></param>
        /// <param name="dataSource"></param>
        /// <param name="location"></param>
        /// <param name="catalog"></param>
        public SqlDeploymentLinkedServerStep(string instanceName, string name, string product, string provider, string providerString, string dataSource, string location, string catalog) :
            base(instanceName)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Product = product;
            Provider = provider;
            ProviderString = providerString;
            DataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
            Location = location;
            Catalog = catalog;
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

        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken = default)
        {
            using (var cnn = await OpenConnectionAsync(cancellationToken))
            {
                // check that server already exists
                if (await ShouldExecute(cnn, cancellationToken) == false)
                    return;

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
