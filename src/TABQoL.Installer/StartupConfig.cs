using System;
using System.IO;
using System.Linq;
using System.Xml;

namespace TABQoL.Installer
{
    public static class StartupConfig
    {
        public static byte[] Prepare(string originalConfig)
        {
            var document = new XmlDocument { PreserveWhitespace = true, XmlResolver = null };
            if (originalConfig != null)
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
                using (var reader = XmlReader.Create(originalConfig, settings)) document.Load(reader);
            }
            else
            {
                document.AppendChild(document.CreateXmlDeclaration("1.0", "utf-8", null));
                document.AppendChild(document.CreateElement("configuration"));
            }

            var configuration = document.DocumentElement;
            if (configuration == null || configuration.Name != "configuration" || configuration.NamespaceURI.Length != 0)
                throw new InvalidDataException("The game startup config must have a configuration root without an XML namespace.");
            if (document.GetElementsByTagName("*").Cast<XmlElement>().Any(e =>
                e.LocalName == "appDomainManagerAssembly" || e.LocalName == "appDomainManagerType"))
                throw new InvalidDataException("The game startup config already selects an AppDomainManager; no files changed.");

            var runtimes = configuration.ChildNodes.OfType<XmlElement>().Where(e => e.LocalName == "runtime").ToArray();
            if (runtimes.Length > 1 || (runtimes.Length == 1 &&
                (runtimes[0].NamespaceURI.Length != 0 || runtimes[0].HasAttribute("configSource"))))
                throw new InvalidDataException("The game startup config has conflicting or external runtime sections; no files changed.");
            var runtime = runtimes.SingleOrDefault();
            if (runtime == null)
            {
                runtime = document.CreateElement("runtime");
                configuration.AppendChild(runtime);
            }
            var assembly = document.CreateElement("appDomainManagerAssembly");
            assembly.SetAttribute("value", typeof(Bridge).Assembly.FullName);
            runtime.AppendChild(assembly);
            var manager = document.CreateElement("appDomainManagerType");
            manager.SetAttribute("value", "TABQoL.SteamDomainManager");
            runtime.AppendChild(manager);

            using (var stream = new MemoryStream())
            {
                document.Save(stream);
                return stream.ToArray();
            }
        }
    }
}
