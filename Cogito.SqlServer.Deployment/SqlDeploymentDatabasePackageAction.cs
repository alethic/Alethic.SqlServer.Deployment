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

using Cogito.SqlServer.Deployment.Internal;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Microsoft.SqlServer.Dac;

namespace Cogito.SqlServer.Deployment
{

    /// <summary>
    /// Deploys a DACPAC against a database.
    /// </summary>
    public class SqlDeploymentDatabasePackageAction : SqlDeploymentAction
    {

        static readonly XNamespace dacRptNs = "http://schemas.microsoft.com/sqlserver/dac/DeployReport/2012/02";
        static readonly XNamespace dacSrsNs = "http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02";

        /// <summary>
        /// Gets the DACTAG to be used to identify modifications to the given DAC file.
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        static string GetDacTag(string file)
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
        static string GetFullPath(string path)
        {
            if (Path.IsPathRooted(path) == false)
                path = Path.Combine(Environment.CurrentDirectory, path);

            return path;
        }

        /// <summary>
        /// Initializes a new instance.
        /// </summary>
        /// <param name="instanceName"></param>
        /// <param name="name"></param>
        /// <param name="source"></param>
        /// <param name="profile"></param>
        public SqlDeploymentDatabasePackageAction(string instanceName, string name, string source, DacProfile profile) :
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
        /// <param name="connection"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        async Task<bool> IsOutOfDate(SqlConnection connection, CancellationToken cancellationToken)
        {
            // MD5SUM of the DACPAC is put onto the database to indicate no change
            var tag = GetDacTag(Source);
            if (tag == await GetDacTag(connection, cancellationToken))
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
            if (File.Exists(Source) == false)
                throw new FileNotFoundException("Missing DACPAC. Ensure project has been built successfully.", Source);

            using var cnn = await OpenConnectionAsync(cancellationToken);

            // check that existing database does not already exist with tag
            if (await IsOutOfDate(cnn, cancellationToken) == false)
            {
                context.Logger.LogInformation("Database {Name} is up to date.", Name);
                return;
            }

            try
            {
                // lock database for deployment
                cnn.ChangeDatabase("master");
                if (await cnn.GetAppLock($"DATABASE::{Name}", timeout: (int)TimeSpan.FromMinutes(5).TotalMilliseconds) < 0)
                    throw new SqlDeploymentException($"Unable to acquire database lock on '{Name}'.");

                // load up the DAC services
                using var dac = LoadDacPackage(Source);
                var svc = new DacServices(cnn.ConnectionString);
                svc.Message += (s, a) => LogDacServiceMessage(context, a);
                svc.ProgressChanged += (s, a) => LogDacServiceProgress(context, a);
                var prf = Profile ?? new DacProfile();
                var opt = prf.DeployOptions;

                // will specifically drop these
                opt.DoNotAlterReplicatedObjects = false;

                // check if database exists
                if (await cnn.ExecuteScalarAsync((string)$"SELECT db_id('{Name}')") is short dbid)
                {
                    cnn.ChangeDatabase(Name);

                    // some items are replicated
                    var helpDbReplicationOption = await cnn.ExecuteSpHelpReplicationDbOptionAsync(Name, cancellationToken);
                    if (helpDbReplicationOption.TransactionalPublish ||
                        helpDbReplicationOption.MergePublish)
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
                cnn.ChangeDatabase("master");
                context.Logger.LogInformation("Publishing {DacPacFile} to {Instance}:{Database}.", Source, InstanceName, Name);
                svc.Deploy(dac, Name, true, opt, cancellationToken);

                // generate files for file groups
                cnn.ChangeDatabase(Name);
                foreach (var group in await GetFileGroupsWithMissingFiles(cnn, cancellationToken))
                    await CreateDefaultFilesForFileGroup(cnn, group, cancellationToken);

                // record that the version we just deployed
                cnn.ChangeDatabase(Name);
                await SetDacTag(cnn, GetDacTag(Source), cancellationToken);
            }
            catch (SqlException e)
            {
                context.Logger.LogError(e, "Exception deploying DACPAC to {Name}.", Name);
                throw;
            }
            finally
            {
                try
                {
                    cnn.ChangeDatabase("master");
                    await cnn.ReleaseAppLock($"DATABASE::{Name}");
                }
                catch (SqlException e)
                {
                    context.Logger.LogError(e, "Unable to release database lock on {Name}.", Name);
                    throw;
                }
            }
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
        /// Drops the specified replicated table.
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
                await cnn.ExecuteNonQueryAsync($@"
                    EXEC sp_dropsubscription
                        @publication = {row["pubname"]},
                        @article = {row["artname"]},
                        @subscriber = N'all'",
                    cancellationToken: cancellationToken);

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
                await cnn.ExecuteNonQueryAsync($@"
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
        /// <param name="groupName"></param>
        /// <param name="cancellationToken"></param>
        async Task CreateDefaultFilesForFileGroup(SqlConnection connection, string groupName, CancellationToken cancellationToken)
        {
            if (connection is null)
                throw new ArgumentNullException(nameof(connection));

            var dataPath = Path.Combine(await GetDefaultDataPathForDatabase(connection, cancellationToken), Name + "_" + groupName + ".mdf");

            await connection.ExecuteNonQueryAsync((string)$@"
                ALTER DATABASE [{Name}]
                    ADD FILE ( NAME = {groupName}, FILENAME = '{dataPath}' )
                    TO FILEGROUP {groupName}",
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Logs the given <see cref="DacMessage"/>.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="args"></param>
        void LogDacServiceMessage(SqlDeploymentExecuteContext context, DacMessageEventArgs args)
        {
            switch (args.Message.MessageType)
            {
                case DacMessageType.Message:
                    context.Logger.LogDebug("{Instance}.{Database}: {Message}", InstanceName, Name, args.Message.Message);
                    break;
                case DacMessageType.Warning:
                    context.Logger.LogWarning("{Instance}.{Database}: {Message}", InstanceName, Name, args.Message.Message);
                    break;
                case DacMessageType.Error:
                    context.Logger.LogError("{Instance}.{Database}: {Message}", InstanceName, Name, args.Message.Message);
                    break;
            }
        }

        /// <summary>
        /// Logs the given DAC progress.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="args"></param>
        void LogDacServiceProgress(SqlDeploymentExecuteContext context, DacProgressEventArgs args)
        {
            context.Logger.LogInformation("{Instance}.{Database}: {Message}", InstanceName, Name, args.Message);
        }

    }

}
