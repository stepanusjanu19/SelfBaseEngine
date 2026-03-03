using System;
using System.IO;
using System.Xml;
using System.Xml.Linq;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Provides XXE (XML External Entity) protection by enforcing safe
    /// <see cref="XmlReaderSettings"/> on all XML parsing operations.
    /// </summary>
    public static class XxeProtection
    {
        /// <summary>
        /// Creates <see cref="XmlReaderSettings"/> configured to prevent XXE attacks:
        /// <list type="bullet">
        ///   <item>DTD processing is prohibited.</item>
        ///   <item>The XML resolver is set to <c>null</c>, blocking external resource resolution.</item>
        ///   <item>Maximum characters limits are enforced to mitigate billion-laughs DoS.</item>
        /// </list>
        /// </summary>
        public static XmlReaderSettings CreateSafeXmlReaderSettings()
        {
            return new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null,
                IgnoreComments = true,
                IgnoreWhitespace = false,
                MaxCharactersInDocument = 10_000_000,   // 10 MB cap
                MaxCharactersFromEntities = 1_024,       // prevent entity-expansion DoS
            };
        }

        /// <summary>
        /// Creates an <see cref="XmlReader"/> from a <see cref="Stream"/> using safe settings.
        /// </summary>
        /// <param name="stream">Source XML stream.</param>
        public static XmlReader GetSafeXmlReader(Stream stream)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            return XmlReader.Create(stream, CreateSafeXmlReaderSettings());
        }

        /// <summary>
        /// Creates an <see cref="XmlReader"/> from an XML string using safe settings.
        /// </summary>
        /// <param name="xml">Raw XML string.</param>
        public static XmlReader GetSafeXmlReader(string xml)
        {
            if (string.IsNullOrWhiteSpace(xml))
                throw new ArgumentNullException(nameof(xml), "XML input must not be null or empty.");

            var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(xml));
            return XmlReader.Create(stream, CreateSafeXmlReaderSettings());
        }

        /// <summary>
        /// Parses an XML string into an <see cref="XDocument"/> with XXE protection enabled.
        /// </summary>
        /// <param name="xml">Raw XML string.</param>
        /// <returns>Parsed <see cref="XDocument"/>.</returns>
        /// <exception cref="XmlException">Thrown when the XML is invalid or DTD is detected.</exception>
        public static XDocument ParseSafeXml(string xml)
        {
            using var reader = GetSafeXmlReader(xml);
            return XDocument.Load(reader, LoadOptions.None);
        }

        /// <summary>
        /// Parses an XML <see cref="Stream"/> into an <see cref="XDocument"/> with XXE protection enabled.
        /// </summary>
        /// <param name="stream">Source XML stream.</param>
        /// <returns>Parsed <see cref="XDocument"/>.</returns>
        public static XDocument ParseSafeXml(Stream stream)
        {
            using var reader = GetSafeXmlReader(stream);
            return XDocument.Load(reader, LoadOptions.None);
        }

        /// <summary>
        /// Creates an <see cref="XmlDocument"/> and loads XML from a string with XXE protection.
        /// </summary>
        /// <param name="xml">Raw XML string.</param>
        /// <returns>Loaded <see cref="XmlDocument"/>.</returns>
        public static XmlDocument LoadSafeXmlDocument(string xml)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using var reader = GetSafeXmlReader(xml);
            doc.Load(reader);
            return doc;
        }

        /// <summary>
        /// Creates an <see cref="XmlDocument"/> and loads XML from a <see cref="Stream"/> with XXE protection.
        /// </summary>
        /// <param name="stream">Source XML stream.</param>
        /// <returns>Loaded <see cref="XmlDocument"/>.</returns>
        public static XmlDocument LoadSafeXmlDocument(Stream stream)
        {
            var doc = new XmlDocument { XmlResolver = null };
            using var reader = GetSafeXmlReader(stream);
            doc.Load(reader);
            return doc;
        }
    }
}
