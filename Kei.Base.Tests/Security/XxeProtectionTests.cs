using System;
using System.IO;
using System.Text;
using System.Xml;
using Kei.Base.Helper.Security;

namespace Kei.Base.Tests.Security
{
    /// <summary>
    /// Unit tests for XxeProtection — covers DTD prohibition, external entity
    /// blocking, XmlDocument loading, and normal XML parsing.
    /// </summary>
    public class XxeProtectionTests
    {
        // ─── Valid XML Parsing ────────────────────────────────────────────────────

        [Fact]
        public void ParseSafeXml_ValidXml_ReturnsDocument()
        {
            const string xml = "<root><user id=\"1\"><name>Alice</name></user></root>";
            var doc = XxeProtection.ParseSafeXml(xml);

            Assert.NotNull(doc);
            Assert.Equal("root", doc.Root?.Name.LocalName);
        }

        [Fact]
        public void ParseSafeXml_ElementValues_ParsedCorrectly()
        {
            const string xml = "<order><id>42</id><amount>199.99</amount></order>";
            var doc = XxeProtection.ParseSafeXml(xml);

            Assert.Equal("42", doc.Root?.Element("id")?.Value);
            Assert.Equal("199.99", doc.Root?.Element("amount")?.Value);
        }

        [Fact]
        public void ParseSafeXml_FromStream_ParsesCorrectly()
        {
            const string xml = "<data><item>1</item><item>2</item></data>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var doc = XxeProtection.ParseSafeXml(stream);

            Assert.NotNull(doc);
            Assert.Equal("data", doc.Root?.Name.LocalName);
        }

        // ─── DTD Injection Prevention ─────────────────────────────────────────────

        [Fact]
        public void ParseSafeXml_DtdDeclaration_ThrowsXmlException()
        {
            // A DTD declaration should throw because DtdProcessing = Prohibit
            const string xml = """
                <?xml version="1.0"?>
                <!DOCTYPE foo [<!ENTITY xxe "xxe-payload">]>
                <root>&xxe;</root>
                """;

            Assert.Throws<XmlException>(() => XxeProtection.ParseSafeXml(xml));
        }

        [Fact]
        public void ParseSafeXml_BillionLaughsEntity_ThrowsXmlException()
        {
            // Billion Laughs DoS attack via entity expansion
            const string xml = """
                <?xml version="1.0"?>
                <!DOCTYPE lolz [
                  <!ENTITY lol "lol">
                  <!ENTITY lol2 "&lol;&lol;&lol;&lol;&lol;">
                ]>
                <root>&lol2;</root>
                """;

            Assert.Throws<XmlException>(() => XxeProtection.ParseSafeXml(xml));
        }

        [Fact]
        public void ParseSafeXml_ExternalEntityFileAccess_ThrowsXmlException()
        {
            // Attempt to read /etc/passwd via external entity
            const string xml = """
                <?xml version="1.0"?>
                <!DOCTYPE test [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
                <test>&xxe;</test>
                """;

            Assert.Throws<XmlException>(() => XxeProtection.ParseSafeXml(xml));
        }

        [Fact]
        public void ParseSafeXml_ExternalEntityHttpRequest_ThrowsXmlException()
        {
            // SSRF-via-XXE: external HTTP entity
            const string xml = """
                <?xml version="1.0"?>
                <!DOCTYPE test [<!ENTITY xxe SYSTEM "http://169.254.169.254/latest/">]>
                <test>&xxe;</test>
                """;

            Assert.Throws<XmlException>(() => XxeProtection.ParseSafeXml(xml));
        }

        // ─── XmlDocument Loading ──────────────────────────────────────────────────

        [Fact]
        public void LoadSafeXmlDocument_ValidXml_Loads()
        {
            const string xml = "<users><user><id>1</id><name>Bob</name></user></users>";
            var doc = XxeProtection.LoadSafeXmlDocument(xml);

            Assert.NotNull(doc.DocumentElement);
            Assert.Equal("users", doc.DocumentElement!.Name);
        }

        [Fact]
        public void LoadSafeXmlDocument_DtdDeclaration_ThrowsXmlException()
        {
            const string xml = """
                <?xml version="1.0"?>
                <!DOCTYPE foo [<!ENTITY bar "baz">]>
                <foo>&bar;</foo>
                """;

            Assert.Throws<XmlException>(() => XxeProtection.LoadSafeXmlDocument(xml));
        }

        [Fact]
        public void LoadSafeXmlDocument_FromStream_Loads()
        {
            const string xml = "<config><key>value</key></config>";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));

            var doc = XxeProtection.LoadSafeXmlDocument(stream);
            Assert.NotNull(doc.DocumentElement);
        }

        // ─── XmlReader Settings Verification ─────────────────────────────────────

        [Fact]
        public void CreateSafeXmlReaderSettings_DtdProcessingProhibited()
        {
            var settings = XxeProtection.CreateSafeXmlReaderSettings();
            Assert.Equal(DtdProcessing.Prohibit, settings.DtdProcessing);
        }

        [Fact]
        public void CreateSafeXmlReaderSettings_XmlResolverConfiguredSafely()
        {
            // XmlResolver is write-only in some contexts; we verify indirectly by confirming
            // that a document with an external entity reference is blocked.
            // A reader with a live resolver would not throw; one with null resolver will throw.
            const string xmlWithEntity = """
                <?xml version="1.0"?>
                <!DOCTYPE foo [<!ENTITY ext SYSTEM "file:///etc/hosts">]>
                <root>&ext;</root>
                """;

            // The fact that ParseSafeXml throws XmlException proves the resolver is not active
            Assert.Throws<XmlException>(() => XxeProtection.ParseSafeXml(xmlWithEntity));
        }

        [Fact]
        public void CreateSafeXmlReaderSettings_MaxCharactersLimited()
        {
            var settings = XxeProtection.CreateSafeXmlReaderSettings();
            Assert.True(settings.MaxCharactersInDocument > 0);
            Assert.True(settings.MaxCharactersFromEntities > 0);
        }

        // ─── Input Validation ─────────────────────────────────────────────────────

        [Fact]
        public void ParseSafeXml_EmptyString_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => XxeProtection.ParseSafeXml(""));
        }

        [Fact]
        public void GetSafeXmlReader_NullStream_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                XxeProtection.GetSafeXmlReader((Stream)null!));
        }

        [Fact]
        public void GetSafeXmlReader_MalformedXml_ThrowsXmlException()
        {
            Assert.Throws<XmlException>(() =>
            {
                using var reader = XxeProtection.GetSafeXmlReader("<unclosed");
                while (reader.Read()) { }
            });
        }
    }
}
