using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using Cogito.Collections;

namespace Alethic.SqlServer.Deployment
{

    /// <summary>
    /// Supports reading a <see cref="SqlDeployment"/> instance from XML.
    /// </summary>
    public static class SqlDeploymentXmlReader
    {

        class ReaderContext
        {

            public Dictionary<string, Lazy<SqlDeploymentTarget>> Targets { get; } = new Dictionary<string, Lazy<SqlDeploymentTarget>>();

        }

        static readonly XNamespace Xmlns = SqlDeploymentXml.Xmlns;

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static SqlDeployment Load(string path)
        {
            using var rdr = File.OpenRead(path);
            return Load(rdr, path);
        }

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="stream"></param>
        /// <param name="baseUri"></param>
        /// <returns></returns>
        public static SqlDeployment Load(Stream stream, string baseUri = null)
        {
            using var rdr = XmlReader.Create(stream, null, baseUri);
            return Load(rdr);
        }

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="TextReader"/>.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="baseUri"></param>
        /// <returns></returns>
        public static SqlDeployment Load(TextReader reader, string baseUri = null)
        {
            using var xml = XmlReader.Create(reader, null, baseUri);
            return Load(xml);
        }

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="XmlReader"/>.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="baseUri"></param>
        /// <returns></returns>
        public static SqlDeployment Load(XmlReader reader)
        {
            return Load(XDocument.Load(reader, LoadOptions.SetBaseUri));
        }

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="XDocument"/>.
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public static SqlDeployment Load(XDocument xml)
        {
            return Load(xml.Root);
        }

        /// <summary>
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="XElement"/>.
        /// </summary>
        /// <param name="xml"></param>
        /// <param name="baseUri"></param>
        /// <returns></returns>
        public static SqlDeployment Load(XElement xml, string baseUri = null)
        {
            if (xml.Name != Xmlns + "Deployment")
                throw new SqlDeploymentXmlException("Element must be a Deployment element.");

            var c = new ReaderContext();
            var d = new SqlDeployment();

            // default to XML provided base URI
            if (baseUri == null)
                baseUri = xml.BaseUri;

            // we received a base URI, try to parse for a path
            if (baseUri != null && Uri.TryCreate(baseUri, UriKind.Absolute, out var b))
            {
                // source path is a file
                if (b.Scheme == Uri.UriSchemeFile)
                    d.SourcePath = b.LocalPath;
            }

            foreach (var p in LoadParameters(c, xml))
                d.Parameters.Add(p);

            foreach (var t in LoadTargets(c, xml))
                d.Targets.Add(t);

            return d;
        }

        static IEnumerable<SqlDeploymentParameter> LoadParameters(ReaderContext context, XElement xml)
        {
            return xml.Elements(Xmlns + "Parameter").Select(i => LoadParameter(context, i));
        }

        static SqlDeploymentParameter LoadParameter(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentParameter();
            p.Name = (string)element.Attribute("Name");
            p.DefaultValue = (string)element.Attribute("DefaultValue");
            return p;
        }

        static IEnumerable<SqlDeploymentTarget> LoadTargets(ReaderContext context, XElement xml)
        {
            // provide lazy target loader to sub-loading to handle recursion
            foreach (var i in xml.Elements(Xmlns + "Target"))
                context.Targets[(string)i.Attribute("Name")] = new Lazy<SqlDeploymentTarget>(() => LoadTarget(context, i));

            // pull out resolved targets
            return context.Targets.Values.Select(i => i.Value);
        }

        static SqlDeploymentTarget LoadTarget(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentTarget();
            p.Name = (string)element.Attribute("Name");

            foreach (var d in element.Elements(Xmlns + "DependsOn"))
            {
                if (context.Targets.TryGetValue((string)d.Attribute("Name"), out var t) == false)
                    throw new SqlDeploymentXmlException($"Could not find dependent target '{(string)d.Attribute("Name")}'.");

                p.DependsOn.Add(t.Value);
            }

            foreach (var i in element.Elements(Xmlns + "Instance"))
                p.Instances.Add(LoadInstance(context, i));

            return p;
        }

        static SqlDeploymentInstance LoadInstance(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentInstance();
            p.Name = (string)element.Attribute("Name");
            p.ConnectionString = (string)element.Attribute("ConnectionString");
            p.Install = element.Element(Xmlns + "Install") is XElement setup ? LoadInstall(setup) : null;

            foreach (var configurationElement in element.Elements(Xmlns + "Configuration"))
                p.Configuration[(string)configurationElement.Attribute("Name")] = (string)configurationElement.Attribute("Value");

            p.Databases.AddRange(element.Elements(Xmlns + "Database").Select(i => LoadDatabase(context, i)));
            p.LinkedServers.AddRange(element.Elements(Xmlns + "LinkedServer").Select(i => LoadLinkedServer(context, i)));
            p.Distributor = element.Element(Xmlns + "Distributor") is XElement distributor ? LoadDistributor(context, distributor) : null;
            p.Publisher = element.Element(Xmlns + "Publisher") is XElement publisher ? LoadPublisher(context, publisher) : null;
            return p;
        }

        static SqlDeploymentInstall LoadInstall(XElement element)
        {
            var p = new SqlDeploymentInstall();
            p.SetupExe = (string)element.Attribute("SetupExe");
            return p;
        }

        static SqlDeploymentDatabase LoadDatabase(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentDatabase();
            p.Name = (string)element.Attribute("Name");
            p.Owner = (string)element.Attribute("Owner");

            p.DefaultDataFilePath = (string)element.Attribute("DefaultDataFilePath");
            p.DefaultLogFilePath = (string)element.Attribute("DefaultLogFilePath");
            p.Overwrite = (string)element.Attribute("Overwrite");

            if (element.Element(Xmlns + "Package") is XElement packageElement)
            {
                var l = new SqlDeploymentDatabasePackage();
                l.Source = (string)packageElement.Attribute("Source");

                if (packageElement.Element(Xmlns + "DeployOptions") is XElement deployOptionsElement)
                    l.DeployOptions = LoadDatabasePackageDeployOptions(context, deployOptionsElement);

                p.Package = l;
            }

            foreach (var extendedPropertyElement in element.Elements(Xmlns + "ExtendedProperty"))
                p.ExtendedProperties.Add(LoadExtendedProperty(context, extendedPropertyElement));

            if (element.Element(Xmlns + "Publications") is XElement publicationsElement)
                p.Publications.AddRange(LoadPublications(context, publicationsElement));

            if (element.Element(Xmlns + "Subscriptions") is XElement subscriptionsElement)
                p.Subscriptions.AddRange(LoadSubscriptions(context, subscriptionsElement));

            return p;
        }

        static SqlDeploymentDatabasePackageDeployOptions LoadDatabasePackageDeployOptions(ReaderContext context, XElement element)
        {
            var o = new SqlDeploymentDatabasePackageDeployOptions();
            o.AdditionalDeploymentContributorArguments = (string)element.Element(Xmlns + "AdditionalDeploymentContributorArguments");
            o.AdditionalDeploymentContributorPaths = (string)element.Element(Xmlns + "AdditionalDeploymentContributorPaths");
            o.AdditionalDeploymentContributors = (string)element.Element(Xmlns + "AdditionalDeploymentContributors");
            o.AllowDropBlockingAssemblies = (string)element.Element(Xmlns + "AllowDropBlockingAssemblies");
            o.AllowIncompatiblePlatform = (string)element.Element(Xmlns + "AllowIncompatiblePlatform");
            o.AllowUnsafeRowLevelSecurityDataMovement = (string)element.Element(Xmlns + "AllowUnsafeRowLevelSecurityDataMovement");
            o.BackupDatabaseBeforeChanges = (string)element.Element(Xmlns + "BackupDatabaseBeforeChanges");
            o.BlockOnPossibleDataLoss = (string)element.Element(Xmlns + "BlockOnPossibleDataLoss");
            o.BlockWhenDriftDetected = (string)element.Element(Xmlns + "BlockWhenDriftDetected");
            o.CommandTimeout = (string)element.Element(Xmlns + "CommandTimeout");
            o.CommentOutSetVarDeclarations = (string)element.Element(Xmlns + "CommentOutSetVarDeclarations");
            o.CompareUsingTargetCollation = (string)element.Element(Xmlns + "CompareUsingTargetCollation");
            o.CreateNewDatabase = (string)element.Element(Xmlns + "CreateNewDatabase");
            o.DatabaseLockTimeout = (string)element.Element(Xmlns + "DatabaseLockTimeout");
            //o.DatabaseSpecification = (string)element.Element(Xmlns+"DatabaseSpecification");
            o.DeployDatabaseInSingleUserMode = (string)element.Element(Xmlns + "DeployDatabaseInSingleUserMode");
            o.DisableAndReenableDdlTriggers = (string)element.Element(Xmlns + "DisableAndReenableDdlTriggers");
            o.DoNotAlterChangeDataCaptureObjects = (string)element.Element(Xmlns + "DoNotAlterChangeDataCaptureObjects");
            o.DoNotAlterReplicatedObjects = (string)element.Element(Xmlns + "DoNotAlterReplicatedObjects");
            o.DoNotDropObjectTypes = element.Elements(Xmlns + "DoNotDropObjectTypes").Select(i => new SqlDeploymentExpression(i.Value)).ToArray();
            o.DropConstraintsNotInSource = (string)element.Element(Xmlns + "DropConstraintsNotInSource");
            o.DropDmlTriggersNotInSource = (string)element.Element(Xmlns + "DropDmlTriggersNotInSource");
            o.DropExtendedPropertiesNotInSource = (string)element.Element(Xmlns + "DropExtendedPropertiesNotInSource");
            o.DropIndexesNotInSource = (string)element.Element(Xmlns + "DropIndexesNotInSource");
            o.DropObjectsNotInSource = (string)element.Element(Xmlns + "DropObjectsNotInSource");
            o.DropPermissionsNotInSource = (string)element.Element(Xmlns + "DropPermissionsNotInSource");
            o.DropRoleMembersNotInSource = (string)element.Element(Xmlns + "DropRoleMembersNotInSource");
            o.DropStatisticsNotInSource = (string)element.Element(Xmlns + "DropStatisticsNotInSource");
            o.ExcludeObjectTypes = element.Elements(Xmlns + "ExcludeObjectTypes").Select(i => new SqlDeploymentExpression(i.Value)).ToArray();
            o.GenerateSmartDefaults = (string)element.Element(Xmlns + "GenerateSmartDefaults");
            o.IgnoreAnsiNulls = (string)element.Element(Xmlns + "IgnoreAnsiNulls");
            o.IgnoreAuthorizer = (string)element.Element(Xmlns + "IgnoreAuthorizer");
            o.IgnoreColumnCollation = (string)element.Element(Xmlns + "IgnoreColumnCollation");
            o.IgnoreColumnOrder = (string)element.Element(Xmlns + "IgnoreColumnOrder");
            o.IgnoreComments = (string)element.Element(Xmlns + "IgnoreComments");
            o.IgnoreCryptographicProviderFilePath = (string)element.Element(Xmlns + "IgnoreCryptographicProviderFilePath");
            o.IgnoreDdlTriggerOrder = (string)element.Element(Xmlns + "IgnoreDdlTriggerOrder");
            o.IgnoreDdlTriggerState = (string)element.Element(Xmlns + "IgnoreDdlTriggerState");
            o.IgnoreDefaultSchema = (string)element.Element(Xmlns + "IgnoreDefaultSchema");
            o.IgnoreDmlTriggerOrder = (string)element.Element(Xmlns + "IgnoreDmlTriggerOrder");
            o.IgnoreDmlTriggerState = (string)element.Element(Xmlns + "IgnoreDmlTriggerState");
            o.IgnoreExtendedProperties = (string)element.Element(Xmlns + "IgnoreExtendedProperties");
            o.IgnoreFileAndLogFilePath = (string)element.Element(Xmlns + "IgnoreFileAndLogFilePath");
            o.IgnoreFilegroupPlacement = (string)element.Element(Xmlns + "IgnoreFilegroupPlacement");
            o.IgnoreFileSize = (string)element.Element(Xmlns + "IgnoreFileSize");
            o.IgnoreFillFactor = (string)element.Element(Xmlns + "IgnoreFillFactor");
            o.IgnoreFullTextCatalogFilePath = (string)element.Element(Xmlns + "IgnoreFullTextCatalogFilePath");
            o.IgnoreIdentitySeed = (string)element.Element(Xmlns + "IgnoreIdentitySeed");
            o.IgnoreIncrement = (string)element.Element(Xmlns + "IgnoreIncrement");
            o.IgnoreIndexOptions = (string)element.Element(Xmlns + "IgnoreIndexOptions");
            o.IgnoreIndexPadding = (string)element.Element(Xmlns + "IgnoreIndexPadding");
            o.IgnoreKeywordCasing = (string)element.Element(Xmlns + "IgnoreKeywordCasing");
            o.IgnoreLockHintsOnIndexes = (string)element.Element(Xmlns + "IgnoreLockHintsOnIndexes");
            o.IgnoreLoginSids = (string)element.Element(Xmlns + "IgnoreLoginSids");
            o.IgnoreNotForReplication = (string)element.Element(Xmlns + "IgnoreNotForReplication");
            o.IgnoreObjectPlacementOnPartitionScheme = (string)element.Element(Xmlns + "IgnoreObjectPlacementOnPartitionScheme");
            o.IgnorePartitionSchemes = (string)element.Element(Xmlns + "IgnorePartitionSchemes");
            o.IgnorePermissions = (string)element.Element(Xmlns + "IgnorePermissions");
            o.IgnoreQuotedIdentifiers = (string)element.Element(Xmlns + "IgnoreQuotedIdentifiers");
            o.IgnoreRoleMembership = (string)element.Element(Xmlns + "IgnoreRoleMembership");
            o.IgnoreRouteLifetime = (string)element.Element(Xmlns + "IgnoreRouteLifetime");
            o.IgnoreSemicolonBetweenStatements = (string)element.Element(Xmlns + "IgnoreSemicolonBetweenStatements");
            o.IgnoreTableOptions = (string)element.Element(Xmlns + "IgnoreTableOptions");
            o.IgnoreUserSettingsObjects = (string)element.Element(Xmlns + "IgnoreUserSettingsObjects");
            o.IgnoreWhitespace = (string)element.Element(Xmlns + "IgnoreWhitespace");
            o.IgnoreWithNocheckOnCheckConstraints = (string)element.Element(Xmlns + "IgnoreWithNocheckOnCheckConstraints");
            o.IgnoreWithNocheckOnForeignKeys = (string)element.Element(Xmlns + "IgnoreWithNocheckOnForeignKeys");
            o.IncludeCompositeObjects = (string)element.Element(Xmlns + "IncludeCompositeObjects");
            o.IncludeTransactionalScripts = (string)element.Element(Xmlns + "IncludeTransactionalScripts");
            o.LongRunningCommandTimeout = (string)element.Element(Xmlns + "LongRunningCommandTimeout");
            o.NoAlterStatementsToChangeClrTypes = (string)element.Element(Xmlns + "NoAlterStatementsToChangeClrTypes");
            o.PopulateFilesOnFileGroups = (string)element.Element(Xmlns + "PopulateFilesOnFileGroups");
            o.RegisterDataTierApplication = (string)element.Element(Xmlns + "RegisterDataTierApplication");
            o.RunDeploymentPlanExecutors = (string)element.Element(Xmlns + "RunDeploymentPlanExecutors");
            o.ScriptDatabaseCollation = (string)element.Element(Xmlns + "ScriptDatabaseCollation");
            o.ScriptDatabaseCompatibility = (string)element.Element(Xmlns + "ScriptDatabaseCompatibility");
            o.ScriptDatabaseOptions = (string)element.Element(Xmlns + "ScriptDatabaseOptions");
            o.ScriptDeployStateChecks = (string)element.Element(Xmlns + "ScriptDeployStateChecks");
            o.ScriptFileSize = (string)element.Element(Xmlns + "ScriptFileSize");
            o.ScriptNewConstraintValidation = (string)element.Element(Xmlns + "ScriptNewConstraintValidation");
            o.ScriptRefreshModule = (string)element.Element(Xmlns + "ScriptRefreshModule");

            if (element.Element(Xmlns + "SqlCommandVariableValues") is XElement sqlCommandVariableValuesElement)
                foreach (var sqlCommandVariableValue in sqlCommandVariableValuesElement.Elements(Xmlns + "SqlCommandVariableValue"))
                    o.SqlCommandVariableValues[(string)sqlCommandVariableValue.Attribute("Name")] = (string)sqlCommandVariableValue.Attribute("Value");

            o.TreatVerificationErrorsAsWarnings = (string)element.Element(Xmlns + "TreatVerificationErrorsAsWarnings");
            o.UnmodifiableObjectWarnings = (string)element.Element(Xmlns + "UnmodifiableObjectWarnings");
            o.VerifyCollationCompatibility = (string)element.Element(Xmlns + "VerifyCollationCompatibility");
            o.VerifyDeployment = (string)element.Element(Xmlns + "VerifyDeployment");
            return o;
        }

        static SqlDeploymentDatabaseExtendedProperty LoadExtendedProperty(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentDatabaseExtendedProperty();
            p.Name = (string)element.Attribute("Name");
            p.Value = (string)element.Attribute("Value");
            return p;
        }

        static SqlDeploymentLinkedServer LoadLinkedServer(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentLinkedServer();
            p.Name = (string)element.Attribute("Name");
            p.Product = (string)element.Attribute("Product");
            p.Provider = (string)element.Attribute("Provider");
            p.ProviderString = (string)element.Attribute("ProviderString");
            p.DataSource = (string)element.Attribute("DataSource");
            p.Location = (string)element.Attribute("Location");
            p.Catalog = (string)element.Attribute("Catalog");
            return p;
        }

        static SqlDeploymentDistributor LoadDistributor(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentDistributor();
            p.DatabaseName = (string)element.Attribute("DatabaseName");
            p.AdminPassword = (string)element.Attribute("AdminPassword");
            p.MinimumRetention = (string)element.Attribute("MinimumRetention");
            p.MaximumRetention = (string)element.Attribute("MaximumRetention");
            p.HistoryRetention = (string)element.Attribute("HistoryRetention");
            p.SnapshotPath = (string)element.Attribute("SnapshotPath");
            return p;
        }

        static SqlDeploymentPublisher LoadPublisher(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentPublisher();
            p.DistributorInstanceName = (string)element.Attribute("DistributorInstanceName");
            p.DistributorAdminPassword = (string)element.Attribute("DistributorAdminPassword");
            return p;
        }

        static IEnumerable<SqlDeploymentPublication> LoadPublications(ReaderContext context, XElement element)
        {
            foreach (var i in element.Elements(Xmlns + "Snapshot"))
                yield return LoadSnapshotPublication(context, i);

            foreach (var i in element.Elements(Xmlns + "Transactional"))
                yield return LoadTransactionalPublication(context, i);

            foreach (var i in element.Elements(Xmlns + "Merge"))
                yield return LoadMergePublication(context, i);
        }

        static SqlDeploymentSnapshotPublication LoadSnapshotPublication(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentSnapshotPublication();
            p.Name = (string)element.Attribute("Name");

            if (element.Element(Xmlns + "SnapshotAgent") is XElement snapshotAgentElement)
                LoadSnapshotAgent(p.SnapshotAgent, context, snapshotAgentElement);

            if (element.Element(Xmlns + "Articles") is XElement articlesElement)
                p.Articles.AddRange(LoadArticles(context, articlesElement));

            return p;
        }

        static SqlDeploymentTransactionalPublication LoadTransactionalPublication(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentTransactionalPublication();
            p.Name = (string)element.Attribute("Name");

            if (element.Element(Xmlns + "SnapshotAgent") is XElement snapshotAgentElement)
                LoadSnapshotAgent(p.SnapshotAgent, context, snapshotAgentElement);

            if (element.Element(Xmlns + "LogReaderAgent") is XElement logReaderAgentElement)
                LoadLogReaderAgent(p.LogReaderAgent, context, logReaderAgentElement);

            if (element.Element(Xmlns + "Articles") is XElement articlesElement)
                p.Articles.AddRange(LoadArticles(context, articlesElement));

            return p;
        }

        static SqlDeploymentMergePublication LoadMergePublication(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentMergePublication();
            p.Name = (string)element.Attribute("Name");

            if (element.Element(Xmlns + "SnapshotAgent") is XElement snapshotAgentElement)
                LoadSnapshotAgent(p.SnapshotAgent, context, snapshotAgentElement);

            if (element.Element(Xmlns + "Articles") is XElement articlesElement)
                p.Articles.AddRange(LoadArticles(context, articlesElement));

            return p;
        }

        static void LoadSnapshotAgent(SqlDeploymentSnapshotAgent p, ReaderContext context, XElement snapshotAgentElement)
        {
            p.ProcessCredentials = snapshotAgentElement.Element(Xmlns + "ProcessCredentials") is XElement e1 ? LoadWindowsCredentials(e1) : null;
            p.ConnectCredentials = snapshotAgentElement.Element(Xmlns + "ConnectCredentials") is XElement e2 ? LoadSqlCredentials(e2) : null;
        }

        static void LoadLogReaderAgent(SqlDeploymentLogReaderAgent p, ReaderContext context, XElement logReaderAgentElement)
        {
            p.ProcessCredentials = logReaderAgentElement.Element(Xmlns + "ProcessCredentials") is XElement e1 ? LoadWindowsCredentials(e1) : null;
            p.ConnectCredentials = logReaderAgentElement.Element(Xmlns + "ConnectCredentials") is XElement e2 ? LoadSqlCredentials(e2) : null;
        }

        static SqlDeploymentWindowsCredentials LoadWindowsCredentials(XElement element)
        {
            var c = new SqlDeploymentWindowsCredentials();
            c.UserName = (string)element.Attribute("UserName");
            c.Password = (string)element.Attribute("Password");
            return c;
        }

        static SqlDeploymentSqlCredentials LoadSqlCredentials(XElement element)
        {
            var c = new SqlDeploymentSqlCredentials();
            c.Login = (string)element.Attribute("Login");
            c.Password = (string)element.Attribute("Password");
            return c;
        }

        static IEnumerable<SqlDeploymentPublicationArticle> LoadArticles(ReaderContext context, XElement element)
        {
            foreach (var tableElement in element.Elements(Xmlns + "Table"))
                yield return LoadArticleTable(context, tableElement);
        }

        static SqlDeploymentPublicationArticle LoadArticleTable(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentPublicationArticleTable();
            p.Name = (string)element.Attribute("Name");
            return p;
        }

        static IEnumerable<SqlDeploymentSubscription> LoadSubscriptions(ReaderContext context, XElement element)
        {
            foreach (var i in element.Elements(Xmlns + "Push"))
                yield return LoadPushSubscription(context, i);

            foreach (var i in element.Elements(Xmlns + "Pull"))
                yield return LoadPullSubscription(context, i);
        }

        static SqlDeploymentPushSubscription LoadPushSubscription(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentPushSubscription();
            p.PublisherInstanceName = (string)element.Attribute("PublisherInstanceName");
            p.PublicationDatabaseName = (string)element.Attribute("PublicationDatabaseName");
            p.PublicationName = (string)element.Attribute("PublicationName");
            return p;
        }

        static SqlDeploymentPullSubscription LoadPullSubscription(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentPullSubscription();
            p.PublisherInstanceName = (string)element.Attribute("PublisherInstanceName");
            p.PublicationDatabaseName = (string)element.Attribute("PublicationDatabaseName");
            p.PublicationName = (string)element.Attribute("PublicationName");
            return p;
        }

    }

}
