using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Cogito.SqlServer.Deployment.Internal;
using Cogito.Threading;

using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Dac;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// 
    /// </summary>
    public class SqlDeploymentDatabasePackageStep : SqlDeploymentStep
    {

        static readonly XNamespace dacRptNs = "http://schemas.microsoft.com/sqlserver/dac/DeployReport/2012/02";
        static readonly XNamespace dacSrsNs = "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02";

        static readonly AsyncLock staticSync = new AsyncLock();
        static readonly ConcurrentDictionary<string, string> dacTagCache = new ConcurrentDictionary<string, string>();

        /// <summary>
        /// Gets the DACTAG to be used to identify modifications to the given DAC file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        static string GetDacTag(string file)
        {
            if (File.Exists(file) == false)
                throw new FileNotFoundException();

            return dacTagCache.GetOrAdd(GetFullPath(file), _ =>
            {
                using (var sha1 = SHA256.Create())
                {
                    var s = 0;
                    var b = new byte[sha1.InputBlockSize];

                    // hash each set of hashable data
                    foreach (var stream in GetStreamsToHashForDacTag(file))
                        while ((s = stream.Read(b, 0, b.Length)) > 0)
                            sha1.TransformBlock(b, 0, s, b, 0);

                    // end hashing
                    sha1.TransformFinalBlock(b, 0, 0);
                    var h = sha1.Hash;

                    // final hash output as string
                    return BitConverter.ToString(h).Replace("-", "");
                }
            });
        }

        /// <summary>
        /// Yields a series of streams to contribute to the DACTAG hash.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        static IEnumerable<Stream> GetStreamsToHashForDacTag(string file)
        {
            using (var pkg = Package.Open(file, FileMode.Open))
            {
                if (GetPart(pkg, "/Origin.xml") is PackagePart origin)
                    foreach (var checksum in
                        XDocument.Load(origin.GetStream())
                            .Elements(dacSrsNs + "DacOrigin")
                            .Elements(dacSrsNs + "Checksums")
                            .Elements(dacSrsNs + "Checksum")
                            .OrderBy(i => (string)i.Attribute("Uri"))
                            .Select(i => i.Value))
                        yield return new MemoryStream(ParseHex(checksum));

                if (GetPart(pkg, "/predeploy.sql") is PackagePart predeploy)
                    using (var stream = predeploy.GetStream())
                        yield return stream;

                if (GetPart(pkg, "/postdeploy.sql") is PackagePart postdeploy)
                    using (var stream = postdeploy.GetStream())
                        yield return stream;
            }
        }

        /// <summary>
        /// Gets the specified part from the package, or returns <c>null</c>.
        /// </summary>
        /// <param name="pkg"></param>
        /// <param name="partUri"></param>
        /// <returns></returns>
        static PackagePart GetPart(Package pkg, string partUri)
        {
            var u = new Uri(partUri, UriKind.Relative);
            return pkg.PartExists(u) ? pkg.GetPart(u) : null;
        }

        /// <summary>
        /// Converts a hexadecimal string into a byte array.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        static byte[] ParseHex(string input)
        {
            var i = 0;
            var x = 0;
            var bytes = new byte[input.Length / 2];

            while (input.Length > i + 1)
            {
                long lngDecimal = Convert.ToInt32(input.Substring(i, 2), 16);
                bytes[x] = Convert.ToByte(lngDecimal);
                i += 2;
                ++x;
            }

            return bytes;
        }

        /// <summary>
        /// Converts the given path into a full path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        static string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path) == false)
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);

            return path;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        /// <param name="source"></param>
        /// <param name="profile"></param>
        public SqlDeploymentDatabasePackageStep(string instanceName, string name, string source, DacProfile profile) :
            base(instanceName)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Source = source ?? throw new ArgumentNullException(nameof(source));
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
        }

        /// <summary>
        /// Gets the name of the database.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the path to the DACPAC.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// Gets the profile to be configured for the deployment.
        /// </summary>
        public DacProfile Profile { get; }

        /// <summary>
        /// Loads the DACPAC file in the executable directory with the given name.
        /// </summary>
        /// <param name="relativeFileName"></param>
        /// <returns></returns>
        DacPackage LoadDacPackage(string relativeFileName)
        {
            return DacPackage.Load(GetFullPath(relativeFileName), DacSchemaModelStorageType.Memory);
        }

        /// <summary>
        /// Returns <c>true</c> if the database is to be deployed.
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<bool> IsOutOfDate(SqlConnection cnn, CancellationToken cancellationToken)
        {
            // MD5SUM of the DACPAC is put onto the database to indicate no change
            var tag = GetDacTag(Source);
            if (tag == await GetDacTag(cnn, cancellationToken))
                return false;

            return true;
        }

        /// <summary>
        /// Deploys the database.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public override async Task Execute(SqlDeploymentExecuteContext context, CancellationToken cancellationToken)
        {
            using var cnn = await OpenConnectionAsync(cancellationToken);

            // check that existing database does not already exist with tag
            if (await IsOutOfDate(cnn, cancellationToken) == false)
                return;

            // load up the DAC services
            using var dac = LoadDacPackage(Source);
            var svc = new DacServices(cnn.ConnectionString);
            var prf = Profile ?? new DacProfile();
            var opt = prf.DeployOptions;

            // check if database exists
            if (await cnn.ExecuteScalarAsync((string)$"SELECT db_id('{Name}')") is short dbid)
            {
                cnn.ChangeDatabase(Name);

                // some items are replicated
                var helpDbReplicationOption = await cnn.ExecuteSpHelpReplicationDbOptionAsync(Name, cancellationToken);
                if (helpDbReplicationOption.TransactionalPublish)
                {
                    if ((int)await cnn.ExecuteScalarAsync("SELECT COUNT(*) FROM sysarticles", null, cancellationToken) > 0)
                    {
                        var reportTxt = svc.GenerateDeployReport(dac, Name, opt, cancellationToken);
                        var reportXml = XDocument.Parse(reportTxt);

                        foreach (var operation in reportXml.Root.Elements(dacRptNs + "Operations").Elements(dacRptNs + "Operation"))
                            if ((string)operation.Attribute("Name") == "TableRebuild" ||
                                (string)operation.Attribute("Name") == "Drop" ||
                                (string)operation.Attribute("Name") == "Alter" ||
                                (string)operation.Attribute("Name") == "Rename")
                                foreach (var item in operation.Elements(dacRptNs + "Item"))
                                    if ((string)item.Attribute("Type") == "SqlTable" ||
                                        (string)item.Attribute("Type") == "SqlSimpleColumn")
                                        await DropReplicatedTableAsync(cnn, (string)item.Attribute("Value"), cancellationToken);
                    }
                }
            }

            // deploy database
            svc.Deploy(dac, Name, true, opt, cancellationToken);

            // record that the version we just deployed
            await SetDacTag(cnn, GetDacTag(Source), cancellationToken);
        }

        /// <summary>
        /// Sets the DacTag on the given database.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tag"></param>
        /// <param name="cancellationToken"></param>
        async Task SetDacTag(SqlConnection connection, string tag, CancellationToken cancellationToken)
        {
            connection.ChangeDatabase(Name);
            await connection.ExecuteNonQueryAsync($@"EXEC sys.sp_addextendedproperty @name = N'DACTAG', @value = {tag}", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the DacTag for a given database.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<string> GetDacTag(SqlConnection connection, CancellationToken cancellationToken)
        {
            // check if database exists
            if (await connection.ExecuteScalarAsync((string)$"SELECT db_id('{Name}')", cancellationToken: cancellationToken) is short dbid)
            {
                // switch to database
                connection.ChangeDatabase(Name);

                // select tag
                if (await connection.ExecuteScalarAsync((string)$@"SELECT TOP 1 value FROM sys.extended_properties WHERE class = 0 AND name = 'DACTAG'", cancellationToken: cancellationToken) is string value)
                    return value;
            }

            return null;
        }

        /// <summary>
        /// Drops the specified table.
        /// </summary>
        /// <param name="cnn"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task DropReplicatedTableAsync(SqlConnection cnn, string tableName, CancellationToken cancellationToken)
        {
            var subscriptions = await cnn.LoadDataTableAsync($@"
                SELECT      DISTINCT
                            syspublications.name    pubname,
                            sysarticles.name        artname
                FROM        sysarticles
                INNER JOIN  syspublications
                    ON      syspublications.pubid = sysarticles.pubid
                INNER JOIN  syssubscriptions
                    ON      syssubscriptions.artid = sysarticles.artid
                INNER JOIN  sys.tables systables
                    ON      systables.object_id = sysarticles.objid
                INNER JOIN  sys.schemas sysschemas
                    ON      sysschemas.schema_id = systables.schema_id
                WHERE       sysarticles.type = 1
                    AND     '[' + sysschemas.name + '].[' + systables.name + ']' = {tableName}",
                    cancellationToken: cancellationToken);

            foreach (var row in subscriptions.Rows.Cast<DataRow>())
                await cnn.ExecuteNonQueryAsync($@"EXEC sp_dropsubscription @publication = {row["pubname"]}, @article = {row["artname"]}, @subscriber = N'all'", cancellationToken: cancellationToken);

            var articles = await cnn.LoadDataTableAsync($@"
                SELECT      DISTINCT
                            syspublications.name    pubname,
                            sysarticles.name        artname
                FROM        sysarticles
                INNER JOIN  syspublications
                    ON      syspublications.pubid = sysarticles.pubid
                INNER JOIN  sys.tables systables
                    ON      systables.object_id = sysarticles.objid
                INNER JOIN  sys.schemas sysschemas
                    ON      sysschemas.schema_id = systables.schema_id
                WHERE       sysarticles.type = 1
                    AND     '[' + sysschemas.name + '].[' + systables.name + ']' = {tableName}",
                    cancellationToken: cancellationToken);

            foreach (var row in articles.Rows.Cast<DataRow>())
                await cnn.ExecuteNonQueryAsync($@"EXEC sp_droparticle @publication = {row["pubname"]}, @article = {row["artname"]}", cancellationToken: cancellationToken);
        }

    }

}
