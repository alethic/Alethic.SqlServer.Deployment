namespace Alethic.SqlServer.Deployment.Tasks
{

    using System;
    using System.IO;
    using System.IO.Compression;
    using System.IO.Packaging;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Xml;
    using System.Xml.Linq;

    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;

    public class UpdateDacPacModelReferencePathsToRelative : Task
    {

        [Required]
        [Output]
        public string? Target { get; set; }

        public override bool Execute()
        {
            if (Target == null)
                throw new Exception();

            var xns = XNamespace.Get("http://schemas.microsoft.com/sqlserver/dac/Serialization/2012/02");
            var pkg = Package.Open(Target);

            var xmlWriterSettings = new XmlWriterSettings()
            {
                Indent = true,
                IndentChars = "\t",
                CloseOutput = true,
            };

            var originUri = new Uri("/Origin.xml", UriKind.Relative);
            var originPart = pkg.GetPart(originUri);
            var originStream = originPart.GetStream();
            var origin = XDocument.Load(originStream);
            originStream.Close();

            var modelUri = new Uri("/model.xml", UriKind.Relative);
            var modelPart = pkg.GetPart(modelUri);
            var modelStream = modelPart.GetStream(FileMode.Open);
            var model = XDocument.Load(modelStream);
            modelStream.Close();

            foreach (XElement fileName in model
                .Elements(xns + "DataSchemaModel")
                .Elements(xns + "Header")
                .Elements(xns + "CustomData")
                .Where(i => (string)i.Attribute("Category") == "Reference")
                .Elements(xns + "Metadata")
                .Where(i => (string)i.Attribute("Name") == "FileName" || (string)i.Attribute("Name") == "AssemblySymbolsName"))
                if ((string)fileName.Attribute("Value") != null)
                    if (Path.IsPathRooted((string)fileName.Attribute("Value")))
                        fileName.SetAttributeValue("Value", Path.GetFileName((string)fileName.Attribute("Value")));

            pkg.DeletePart(modelUri);
            modelPart = pkg.CreatePart(modelUri, "text/xml", CompressionOption.Maximum);
            using (var wrt = XmlWriter.Create(modelPart.GetStream(), xmlWriterSettings))
                model.Save(wrt);

            pkg.Flush();

            // compute hash of model file
            var data = SHA256.Create().ComputeHash(pkg.GetPart(modelUri).GetStream());
            var hash = BitConverter.ToString(data).Replace("-", "");

            var modelChecksum = origin
                .Elements(xns + "DacOrigin")
                .Elements(xns + "Checksums")
                .Elements(xns + "Checksum")
                .FirstOrDefault(i => (string)i.Attribute("Uri") == modelUri.ToString());
            if (modelChecksum != null)
                modelChecksum.Value = hash;

            var identity = origin
                .Elements(xns + "DacOrigin")
                .Elements(xns + "Operation")
                .Elements(xns + "Identity")
                .FirstOrDefault();
            if (identity != null)
            {
                var g = new byte[16];
                Array.Copy(data, 0, g, 0, 16);
                identity.Value = new Guid(g).ToString();
            }

            var start = origin
                .Elements(xns + "DacOrigin")
                .Elements(xns + "Operation")
                .Elements(xns + "Start")
                .FirstOrDefault();
            if (start != null)
                start.Value = new DateTimeOffset(new DateTime(2000, 1, 1), TimeSpan.Zero).ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz");

            var end = origin
                .Elements(xns + "DacOrigin")
                .Elements(xns + "Operation")
                .Elements(xns + "End")
                .FirstOrDefault();
            if (end != null)
                end.Value = new DateTimeOffset(new DateTime(2000, 1, 1), TimeSpan.Zero).ToString("yyyy-MM-ddTHH\\:mm\\:ss.fffffffzzz");

            pkg.DeletePart(originUri);
            originPart = pkg.CreatePart(originUri, "text/xml", CompressionOption.Maximum);
            using (var wrt = XmlWriter.Create(originPart.GetStream(), xmlWriterSettings))
                origin.Save(wrt);

            pkg.Flush();
            pkg.Close();
            pkg = null;

            File.Delete(Target + ".tmp");

            using (var srcZip = ZipFile.OpenRead(Target))
            using (var tmpZip = ZipFile.Open(Target + ".tmp", ZipArchiveMode.Create))
            {
                foreach (var srcEntry in srcZip.Entries)
                {
                    var tmpEntry = tmpZip.CreateEntry(srcEntry.Name, CompressionLevel.Optimal);
                    tmpEntry.LastWriteTime = new DateTimeOffset(new DateTime(2000, 1, 1), TimeSpan.Zero);

                    using (var srcStream = srcEntry.Open())
                    using (var tmpStream = tmpEntry.Open())
                        srcStream.CopyTo(tmpStream);
                }
            }

            File.Delete(Target);
            File.Move(Target + ".tmp", Target);

            return true;
        }

    }

}
