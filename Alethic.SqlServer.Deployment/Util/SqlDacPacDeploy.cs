using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.IO.Packaging;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

using Alethic.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Provides high-level methods for deploying DACPACs.
    /// </summary>
    public class SqlDacPacDeploy
    {

        static readonly XNamespace dacRptNs = "http://schemas.microsoft.com/sqlserver/dac/DeployReport/2012/02";
        static readonly XNamespace dacSrsNs = "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02";

        readonly string source;
        readonly ILogger logger;

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="logger"></param>
        public SqlDacPacDeploy(string source, ILogger logger)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
            this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Gets the DACTAG to be used to identify modifications to the given DAC file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        string GetDacTag(string file)
        {
            if (File.Exists(file) == false)
                throw new FileNotFoundException();

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
            };
        }

        /// <summary>
        /// Yields a series of streams to contribute to the DACTAG hash.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        IEnumerable<Stream> GetStreamsToHashForDacTag(string file)
        {
            using (var pkg = Package.Open(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (GetPart(pkg, "/Origin.xml") is PackagePart origin)
                    foreach (var checksum in
                        XDocument.Load(origin.GetStream(FileMode.Open, FileAccess.Read))
                            .Elements(dacSrsNs + "DacOrigin")
                            .Elements(dacSrsNs + "Checksums")
                            .Elements(dacSrsNs + "Checksum")
                            .OrderBy(i => (string)i.Attribute("Uri"))
                            .Select(i => i.Value))
                        yield return new MemoryStream(ParseHex(checksum));

                if (GetPart(pkg, "/predeploy.sql") is PackagePart predeploy)
                    using (var stream = predeploy.GetStream(FileMode.Open, FileAccess.Read))
                        yield return stream;

                if (GetPart(pkg, "/postdeploy.sql") is PackagePart postdeploy)
                    using (var stream = postdeploy.GetStream(FileMode.Open, FileAccess.Read))
                        yield return stream;
            }
        }

        /// <summary>
        /// Gets the specified part from the package, or returns <c>null</c>.
        /// </summary>
        /// <param name="pkg"></param>
        /// <param name="partUri"></param>
        /// <returns></returns>
        PackagePart GetPart(Package pkg, string partUri)
        {
            var u = new Uri(partUri, UriKind.Relative);
            return pkg.PartExists(u) ? pkg.GetPart(u) : null;
        }

        /// <summary>
        /// Converts a hexadecimal string into a byte array.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        byte[] ParseHex(string input)
        {
            var i = 0;
            var x = 0;
            var bytes = new byte[input.Length / 2];

            while (input.Length > i + 1)
            {
                long l = Convert.ToInt32(input.Substring(i, 2), 16);
                bytes[x] = Convert.ToByte(l);
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
        string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path) == false)
                path = Path.Combine(Environment.CurrentDirectory, path);

            return path;
        }

        /// <summary>
        /// Loads the DACPAC file in the executable directory with the given name.
        /// </summary>
        /// <param name="relativeFileName"></param>
        /// <returns></returns>
        DacPackage LoadDacPackage(string relativeFileName)
        {
            return DacPackage.Load(GetFullPath(relativeFileName), DacSchemaModelStorageType.Memory, FileAccess.Read);
        }

        /// <summary>
        /// Returns <c>true</c> if the database is to be deployed.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="databaseName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<bool> IsOutOfDate(SqlConnection connection, string databaseName, CancellationToken cancellationToken)
        {
            // MD5SUM of the DACPAC is put onto the database to indicate no change
            var tag = GetDacTag(source);
            if (tag == await GetDacTag(connection, databaseName, cancellationToken))
                return false;

            return true;
        }

        /// <summary>
        /// Sets the DacTag on the given database.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="databaseName"></param>
        /// <param name="tag"></param>
        /// <param name="cancellationToken"></param>
        async Task SetDacTag(SqlConnection connection, string databaseName, string tag, CancellationToken cancellationToken)
        {
            connection.ChangeDatabase(databaseName);
            await connection.ExecuteNonQueryAsync($@"EXEC sys.sp_addextendedproperty @name = N'DACTAG', @value = {tag}", cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the DacTag for a given database.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<string> GetDacTag(SqlConnection connection, string databaseName, CancellationToken cancellationToken)
        {
            // check if database exists
            if (await connection.ExecuteScalarAsync((string)$"SELECT db_id('{databaseName}')", cancellationToken: cancellationToken) is short dbid)
            {
                // switch to database
                connection.ChangeDatabase(databaseName);

                // select tag
                if (await connection.ExecuteScalarAsync((string)$@"SELECT TOP 1 value FROM sys.extended_properties WHERE class = 0 AND name = 'DACTAG'", cancellationToken: cancellationToken) is string value)
                    return value;
            }

            return null;
        }

        /// <summary>
        /// Drops the specified replicated table.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="tableName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task DropReplicatedTableAsync(SqlConnection connection, string tableName, CancellationToken cancellationToken)
        {
            var subscriptions = await connection.LoadDataTableAsync($@"
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
                await connection.ExecuteNonQueryAsync($@"
                    EXEC sp_dropsubscription
                        @publication = {row["pubname"]},
                        @article = {row["artname"]},
                        @subscriber = N'all'",
                    cancellationToken: cancellationToken);

            var articles = await connection.LoadDataTableAsync($@"
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
                await connection.ExecuteNonQueryAsync($@"
                    EXEC sp_droparticle
                        @publication = {row["pubname"]},
                        @article = {row["artname"]}",
                    cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Gets the names of filegroups for the given database that are missing files.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<string[]> GetFileGroupsWithMissingFiles(SqlConnection connection, CancellationToken cancellationToken)
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            using (var cmd = connection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT      *
                    FROM        sysfilegroups
                    LEFT JOIN   sysfiles
                        ON      sysfiles.groupid = sysfilegroups.groupid
                    WHERE       sysfiles.fileid IS NULL";

                var l = new List<string>();
                using (var rdr = await cmd.ExecuteReaderAsync(cancellationToken))
                    while (await rdr.ReadAsync())
                        l.Add((string)rdr["groupname"]);

                return l.ToArray();
            }
        }

        /// <summary>
        /// Finds the path of the first known datafile within the database, which will serve as the default file path.
        /// </summary>
        /// <param name="connection"></param>
        /// <returns></returns>
        async Task<string> GetDefaultDataPathForDatabase(SqlConnection connection, CancellationToken cancellationToken)
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            var file = (string)await connection.ExecuteScalarAsync($@"
                SELECT  TOP 1
                        filename
                FROM    sys.database_files 
                WHERE   type = 0",
                cancellationToken: cancellationToken);

            return new FileInfo(file).DirectoryName;
        }

        /// <summary>
        /// Creates a default set of files for the given file group.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="databaseName"></param>
        /// <param name="groupName"></param>
        /// <param name="cancellationToken"></param>
        async Task CreateDefaultFilesForFileGroup(SqlConnection connection, string databaseName, string groupName, CancellationToken cancellationToken)
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            var dataPath = Path.Combine(await GetDefaultDataPathForDatabase(connection, cancellationToken), databaseName + "_" + groupName + ".mdf");

            await connection.ExecuteNonQueryAsync((string)$@"
                ALTER DATABASE [{databaseName}]
                    ADD FILE ( NAME = {groupName}, FILENAME = '{dataPath}' )
                    TO FILEGROUP {groupName}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Logs the given <see cref="DacMessage"/>.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="args"></param>
        void LogDacServiceMessage(string instanceName, string databaseName, DacMessageEventArgs args)
        {
            switch (args.Message.MessageType)
            {
                case DacMessageType.Message:
                    logger.LogDebug("{Instance}.{Database}: {Message}", instanceName, databaseName, args.Message.Message);
                    break;
                case DacMessageType.Warning:
                    logger.LogWarning("{Instance}.{Database}: {Message}", instanceName, databaseName, args.Message.Message);
                    break;
                case DacMessageType.Error:
                    logger.LogError("{Instance}.{Database}: {Message}", instanceName, databaseName, args.Message.Message);
                    break;
            }
        }

        /// <summary>
        /// Logs the given DAC progress.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="databaseName"></param>
        /// <param name="args"></param>
        void LogDacServiceProgress(string instanceName, string databaseName, DacProgressEventArgs args)
        {
            logger.LogInformation("{Instance}.{Database}: {Message}", instanceName, databaseName, args.Message);
        }

        /// <summary>
        /// Opens a new connection to the target database.
        /// </summary>
        /// <param name="connectionString"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<SqlConnection> OpenConnection(string connectionString, CancellationToken cancellationToken)
        {
            var cnn = new SqlConnection(connectionString);
            await cnn.OpenAsync(cancellationToken);
            return cnn;
        }

        /// <summary>
        /// Deploys the database.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="databaseName"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task DeployAsync(SqlConnection connection, string databaseName, DacProfile profile, CancellationToken cancellationToken)
        {
            if (File.Exists(source) == false)
                throw new FileNotFoundException($"Missing DACPAC '{source}'. Ensure project has been built successfully.", source);

            // will use to identify instance
            var instanceName = await connection.GetServerInstanceName(cancellationToken);

            // check that existing database does not already exist with tag
            if (await IsOutOfDate(connection, databaseName, cancellationToken) == false)
            {
                logger.LogInformation("Database {Name} is up to date.", databaseName);
                return;
            }

            try
            {
                // lock database for deployment
                connection.ChangeDatabase("master");
                if (await connection.GetAppLock($"DATABASE::{databaseName}", timeout: (int)TimeSpan.FromMinutes(5).TotalMilliseconds) < 0)
                    throw new SqlDeploymentException($"Unable to acquire database lock on '{databaseName}'.");

                // load up the DAC services
                using var dac = LoadDacPackage(source);
                var svc = new DacServices(connection.ConnectionString);
                svc.Message += (s, a) => LogDacServiceMessage(instanceName, databaseName, a);
                svc.ProgressChanged += (s, a) => LogDacServiceProgress(instanceName, databaseName, a);
                var prf = profile ?? new DacProfile();
                var opt = prf.DeployOptions;

                // will specifically drop these
                opt.DoNotAlterReplicatedObjects = false;

                // check if database exists
                if (await connection.ExecuteScalarAsync((string)$"SELECT db_id('{databaseName}')") is short dbid)
                {
                    connection.ChangeDatabase(databaseName);

                    // some items are replicated
                    var helpDbReplicationOption = await connection.ExecuteSpHelpReplicationDbOptionAsync(databaseName, cancellationToken);
                    if (helpDbReplicationOption.TransactionalPublish ||
                        helpDbReplicationOption.MergePublish)
                    {
                        if ((int)await connection.ExecuteScalarAsync("SELECT COUNT(*) FROM sysarticles", null, cancellationToken) > 0)
                        {
                            var reportTxt = svc.GenerateDeployReport(dac, databaseName, opt, cancellationToken);
                            var reportXml = XDocument.Parse(reportTxt);

                            foreach (var operation in reportXml.Root.Elements(dacRptNs + "Operations").Elements(dacRptNs + "Operation"))
                                if ((string)operation.Attribute("Name") == "TableRebuild" ||
                                    (string)operation.Attribute("Name") == "Drop" ||
                                    (string)operation.Attribute("Name") == "Alter" ||
                                    (string)operation.Attribute("Name") == "Rename")
                                    foreach (var item in operation.Elements(dacRptNs + "Item"))
                                        if ((string)item.Attribute("Type") == "SqlTable" ||
                                            (string)item.Attribute("Type") == "SqlSimpleColumn")
                                            await DropReplicatedTableAsync(connection, (string)item.Attribute("Value"), cancellationToken);
                        }
                    }
                }

                // deploy database
                connection.ChangeDatabase("master");
                logger.LogInformation("Publishing {DacPacFile} to {Database} at {InstanceName}.", source, databaseName, instanceName);
                svc.Deploy(dac, databaseName, true, opt, cancellationToken);

                // generate files for file groups
                connection.ChangeDatabase(databaseName);
                foreach (var group in await GetFileGroupsWithMissingFiles(connection, cancellationToken))
                    await CreateDefaultFilesForFileGroup(connection, databaseName, group, cancellationToken);

                // record that the version we just deployed
                await SetDacTag(connection, databaseName, GetDacTag(source), cancellationToken);
            }
            catch (SqlException e)
            {
                logger.LogError(e, "Exception deploying DACPAC to {Name} at {InstanceName}.", databaseName, instanceName);
                throw;
            }
            finally
            {
                try
                {
                    // we might have been closed as part of the error
                    if (connection.State == ConnectionState.Open)
                    {
                        connection.ChangeDatabase("master");
                        await connection.ReleaseAppLock($"DATABASE::{databaseName}");
                    }
                }
                catch (SqlException e)
                {
                    logger.LogError(e, "Unable to release database lock on {Name}.", databaseName);
                    throw;
                }
            }
        }

    }

}
