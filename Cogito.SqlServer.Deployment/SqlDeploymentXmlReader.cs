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
            p.Distribution = element.Element(Xmlns + "Distribution") is XElement distribution ? LoadDistribution(context, distribution) : null;
            p.Publications.AddRange(element.Elements(Xmlns + "Publication").Select(i => LoadPublication(context, i)));
            p.Subscriptions.AddRange(element.Elements(Xmlns + "Subscription").Select(i => LoadSubscription(context, i)));
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
            p.DataPath = (string)element.Attribute("DataPath");
            p.LogsPath = (string)element.Attribute("LogsPath");
            p.LogFileSize = (string)element.Attribute("LogFileSize");
            p.MinimumRetention = (string)element.Attribute("MinimumRetention");
            p.MaximumRetention = (string)element.Attribute("MaximumRetention");
            p.HistoryRetention = (string)element.Attribute("HistoryRetention");
            p.SnapshotPath = (string)element.Attribute("SnapshotPath");
            return p;
        }

        static SqlDeploymentDistribution LoadDistribution(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentDistribution();
            p.InstanceName = (string)element.Attribute("InstanceName");
            return p;
        }

        static SqlDeploymentPublication LoadPublication(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentPublication();
            p.Name = (string)element.Attribute("Name");
            p.Type = (string)element.Attribute("Type") switch
            {
                "Snapshot" => SqlDeploymentPublicationType.Snapshot,
                "Transactional" => SqlDeploymentPublicationType.Transactional,
                "Merge" => SqlDeploymentPublicationType.Merge,
                _ => throw new SqlDeploymentXmlException($"Unknown Publication Type '{element.Attribute("Type")}'."),
            };

            if (element.Element(Xmlns + "Articles") is XElement articlesElement)
                p.Articles.AddRange(LoadArticles(context, articlesElement));

            return p;
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

        static SqlDeploymentSubscription LoadSubscription(ReaderContext context, XElement element)
        {
            var p = new SqlDeploymentSubscription();
            p.Name = (string)element.Attribute("Name");
            return p;
        }

    }

}
