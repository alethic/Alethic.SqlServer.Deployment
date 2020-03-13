using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using System.Xml.Linq;

using Cogito.Collections;

namespace Cogito.SqlServer.Deployment
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
        /// Loads a <see cref="SqlDeployment"/> from the specified <see cref="XmlReader"/>.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static SqlDeployment Load(XmlReader reader)
        {
            return Load(XDocument.Load(reader));
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
        /// <returns></returns>
        public static SqlDeployment Load(XElement xml)
        {
            if (xml.Name != Xmlns + "Deployment")
                throw new SqlDeploymentXmlException("Element must be a Deployment element.");

            var c = new ReaderContext();
            var d = new SqlDeployment();

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
            p.Setup = element.Element(Xmlns + "Setup") is XElement setup ? LocalSetup(setup) : null;

            foreach (var configurationElement in element.Elements(Xmlns + "Configuration"))
                p.Configuration[(string)configurationElement.Attribute("Name")] = (string)configurationElement.Attribute("Value");

            p.Databases.AddRange(element.Elements(Xmlns + "Database").Select(i => LoadDatabase(context, i)));
            p.LinkedServers.AddRange(element.Elements(Xmlns + "LinkedServer").Select(i => LoadLinkedServer(context, i)));
            p.Distributor = element.Element(Xmlns + "Distributor") is XElement distributor ? LoadDistributor(context, distributor) : null;
            p.Publisher = element.Element(Xmlns + "Publisher") is XElement publisher ? LoadPublisher(context, publisher) : null;
            return p;
        }

        static SqlDeploymentSetup LocalSetup(XElement element)
        {
            var p = new SqlDeploymentSetup();
            p.Exe = (string)element.Attribute("Exe");
            return p;
        }

        static SqlDeploymentDatabase LoadDatabase(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentDatabase();
            p.Name = (string)element.Attribute("Name");
            p.Owner = (string)element.Attribute("Owner");

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
            p.DataPath = (string)element.Attribute("DataPath");
            p.LogsPath = (string)element.Attribute("LogsPath");
            p.LogFileSize = (string)element.Attribute("LogFileSize");
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
            p.PublicationName = (string)element.Attribute("PublicationName");
            return p;
        }

        static SqlDeploymentPullSubscription LoadPullSubscription(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentPullSubscription();
            p.PublisherInstanceName = (string)element.Attribute("PublisherInstanceName");
            p.PublicationName = (string)element.Attribute("PublicationName");
            return p;
        }

    }

}
