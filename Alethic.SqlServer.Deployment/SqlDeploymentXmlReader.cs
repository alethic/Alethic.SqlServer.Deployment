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
            o.AdditionalDeploymentContributorArguments = (string)element.Element("AdditionalDeploymentContributorArguments");
            o.AdditionalDeploymentContributorPaths = (string)element.Element("AdditionalDeploymentContributorPaths");
            o.AdditionalDeploymentContributors = (string)element.Element("AdditionalDeploymentContributors");
            o.AllowDropBlockingAssemblies = (string)element.Element("AllowDropBlockingAssemblies");
            o.AllowIncompatiblePlatform = (string)element.Element("AllowIncompatiblePlatform");
            o.AllowUnsafeRowLevelSecurityDataMovement = (string)element.Element("AllowUnsafeRowLevelSecurityDataMovement");
            o.BackupDatabaseBeforeChanges = (string)element.Element("BackupDatabaseBeforeChanges");
            o.BlockOnPossibleDataLoss = (string)element.Element("BlockOnPossibleDataLoss");
            o.BlockWhenDriftDetected = (string)element.Element("BlockWhenDriftDetected");
            o.CommandTimeout = (string)element.Element("CommandTimeout");
            o.CommentOutSetVarDeclarations = (string)element.Element("CommentOutSetVarDeclarations");
            o.CompareUsingTargetCollation = (string)element.Element("CompareUsingTargetCollation");
            o.CreateNewDatabase = (string)element.Element("CreateNewDatabase");
            o.DatabaseLockTimeout = (string)element.Element("DatabaseLockTimeout");
            //o.DatabaseSpecification = (string)element.Element("DatabaseSpecification");
            o.DeployDatabaseInSingleUserMode = (string)element.Element("DeployDatabaseInSingleUserMode");
            o.DisableAndReenableDdlTriggers = (string)element.Element("DisableAndReenableDdlTriggers");
            o.DoNotAlterChangeDataCaptureObjects = (string)element.Element("DoNotAlterChangeDataCaptureObjects");
            o.DoNotAlterReplicatedObjects = (string)element.Element("DoNotAlterReplicatedObjects");
            //o.DoNotDropObjectTypes = (string)element.Element("DoNotDropObjectTypes");
            o.DropConstraintsNotInSource = (string)element.Element("DropConstraintsNotInSource");
            o.DropDmlTriggersNotInSource = (string)element.Element("DropDmlTriggersNotInSource");
            o.DropExtendedPropertiesNotInSource = (string)element.Element("DropExtendedPropertiesNotInSource");
            o.DropIndexesNotInSource = (string)element.Element("DropIndexesNotInSource");
            o.DropObjectsNotInSource = (string)element.Element("DropObjectsNotInSource");
            o.DropPermissionsNotInSource = (string)element.Element("DropPermissionsNotInSource");
            o.DropRoleMembersNotInSource = (string)element.Element("DropRoleMembersNotInSource");
            o.DropStatisticsNotInSource = (string)element.Element("DropStatisticsNotInSource");
            //o.ExcludeObjectTypes = (string)element.Element("ExcludeObjectTypes");
            o.GenerateSmartDefaults = (string)element.Element("GenerateSmartDefaults");
            o.IgnoreAnsiNulls = (string)element.Element("IgnoreAnsiNulls");
            o.IgnoreAuthorizer = (string)element.Element("IgnoreAuthorizer");
            o.IgnoreColumnCollation = (string)element.Element("IgnoreColumnCollation");
            o.IgnoreColumnOrder = (string)element.Element("IgnoreColumnOrder");
            o.IgnoreComments = (string)element.Element("IgnoreComments");
            o.IgnoreCryptographicProviderFilePath = (string)element.Element("IgnoreCryptographicProviderFilePath");
            o.IgnoreDdlTriggerOrder = (string)element.Element("IgnoreDdlTriggerOrder");
            o.IgnoreDdlTriggerState = (string)element.Element("IgnoreDdlTriggerState");
            o.IgnoreDefaultSchema = (string)element.Element("IgnoreDefaultSchema");
            o.IgnoreDmlTriggerOrder = (string)element.Element("IgnoreDmlTriggerOrder");
            o.IgnoreDmlTriggerState = (string)element.Element("IgnoreDmlTriggerState");
            o.IgnoreExtendedProperties = (string)element.Element("IgnoreExtendedProperties");
            o.IgnoreFileAndLogFilePath = (string)element.Element("IgnoreFileAndLogFilePath");
            o.IgnoreFilegroupPlacement = (string)element.Element("IgnoreFilegroupPlacement");
            o.IgnoreFileSize = (string)element.Element("IgnoreFileSize");
            o.IgnoreFillFactor = (string)element.Element("IgnoreFillFactor");
            o.IgnoreFullTextCatalogFilePath = (string)element.Element("IgnoreFullTextCatalogFilePath");
            o.IgnoreIdentitySeed = (string)element.Element("IgnoreIdentitySeed");
            o.IgnoreIncrement = (string)element.Element("IgnoreIncrement");
            o.IgnoreIndexOptions = (string)element.Element("IgnoreIndexOptions");
            o.IgnoreIndexPadding = (string)element.Element("IgnoreIndexPadding");
            o.IgnoreKeywordCasing = (string)element.Element("IgnoreKeywordCasing");
            o.IgnoreLockHintsOnIndexes = (string)element.Element("IgnoreLockHintsOnIndexes");
            o.IgnoreLoginSids = (string)element.Element("IgnoreLoginSids");
            o.IgnoreNotForReplication = (string)element.Element("IgnoreNotForReplication");
            o.IgnoreObjectPlacementOnPartitionScheme = (string)element.Element("IgnoreObjectPlacementOnPartitionScheme");
            o.IgnorePartitionSchemes = (string)element.Element("IgnorePartitionSchemes");
            o.IgnorePermissions = (string)element.Element("IgnorePermissions");
            o.IgnoreQuotedIdentifiers = (string)element.Element("IgnoreQuotedIdentifiers");
            o.IgnoreRoleMembership = (string)element.Element("IgnoreRoleMembership");
            o.IgnoreRouteLifetime = (string)element.Element("IgnoreRouteLifetime");
            o.IgnoreSemicolonBetweenStatements = (string)element.Element("IgnoreSemicolonBetweenStatements");
            o.IgnoreTableOptions = (string)element.Element("IgnoreTableOptions");
            o.IgnoreUserSettingsObjects = (string)element.Element("IgnoreUserSettingsObjects");
            o.IgnoreWhitespace = (string)element.Element("IgnoreWhitespace");
            o.IgnoreWithNocheckOnCheckConstraints = (string)element.Element("IgnoreWithNocheckOnCheckConstraints");
            o.IgnoreWithNocheckOnForeignKeys = (string)element.Element("IgnoreWithNocheckOnForeignKeys");
            o.IncludeCompositeObjects = (string)element.Element("IncludeCompositeObjects");
            o.IncludeTransactionalScripts = (string)element.Element("IncludeTransactionalScripts");
            o.LongRunningCommandTimeout = (string)element.Element("LongRunningCommandTimeout");
            o.NoAlterStatementsToChangeClrTypes = (string)element.Element("NoAlterStatementsToChangeClrTypes");
            o.PopulateFilesOnFileGroups = (string)element.Element("PopulateFilesOnFileGroups");
            o.RegisterDataTierApplication = (string)element.Element("RegisterDataTierApplication");
            o.RunDeploymentPlanExecutors = (string)element.Element("RunDeploymentPlanExecutors");
            o.ScriptDatabaseCollation = (string)element.Element("ScriptDatabaseCollation");
            o.ScriptDatabaseCompatibility = (string)element.Element("ScriptDatabaseCompatibility");
            o.ScriptDatabaseOptions = (string)element.Element("ScriptDatabaseOptions");
            o.ScriptDeployStateChecks = (string)element.Element("ScriptDeployStateChecks");
            o.ScriptFileSize = (string)element.Element("ScriptFileSize");
            o.ScriptNewConstraintValidation = (string)element.Element("ScriptNewConstraintValidation");
            o.ScriptRefreshModule = (string)element.Element("ScriptRefreshModule");

            if (element.Element(Xmlns + "SqlCommandVariableValues") is XElement sqlCommandVariableValuesElement)
                foreach (var sqlCommandVariableValue in sqlCommandVariableValuesElement.Elements(Xmlns + "SqlCommandVariableValue"))
                    o.SqlCommandVariableValues[(string)sqlCommandVariableValue.Attribute("Name")] = (string)sqlCommandVariableValue.Attribute("Value");

            o.TreatVerificationErrorsAsWarnings = (string)element.Element("TreatVerificationErrorsAsWarnings");
            o.UnmodifiableObjectWarnings = (string)element.Element("UnmodifiableObjectWarnings");
            o.VerifyCollationCompatibility = (string)element.Element("VerifyCollationCompatibility");
            o.VerifyDeployment = (string)element.Element("VerifyDeployment");
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
