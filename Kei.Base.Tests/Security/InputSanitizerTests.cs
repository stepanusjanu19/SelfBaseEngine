using System;
using Kei.Base.Helper.Security;

namespace Kei.Base.Tests.Security
{
    /// <summary>
    /// Unit tests for InputSanitizer — covers XSS bypass vectors,
    /// Reflected XSS context-encoding, and HTTP header injection.
    /// </summary>
    public class InputSanitizerTests
    {
        // ─── SanitizeHtml: Basic XSS ─────────────────────────────────────────────

        [Fact]
        public void SanitizeHtml_CleanInput_ReturnsSame()
        {
            var result = InputSanitizer.SanitizeHtml("Hello, World!");
            Assert.Equal("Hello, World!", result);
        }

        [Fact]
        public void SanitizeHtml_ScriptTag_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<script>alert('xss')</script>");
            Assert.DoesNotContain("<script>", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("alert", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_IframeTag_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<iframe src='evil.com'></iframe>");
            Assert.DoesNotContain("<iframe", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_OnClickAttribute_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<a href='/' onclick=\"alert(1)\">click</a>");
            Assert.DoesNotContain("onclick", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_OnClickAtTagStart_Removed()
        {
            // Bypass attempt: no leading whitespace before onclick
            var result = InputSanitizer.SanitizeHtml("<p onerror=\"evil()\">text</p>");
            Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_JavascriptHrefProtocol_Neutralized()
        {
            var result = InputSanitizer.SanitizeHtml("<a href=\"javascript:alert(1)\">click</a>");
            Assert.DoesNotContain("javascript:", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_VbscriptProtocol_Neutralized()
        {
            var result = InputSanitizer.SanitizeHtml("<a href=\"vbscript:MsgBox(1)\">click</a>");
            Assert.DoesNotContain("vbscript:", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_DataProtocol_Neutralized()
        {
            var result = InputSanitizer.SanitizeHtml("<a href=\"data:text/html,<script>alert(1)</script>\">x</a>");
            Assert.DoesNotContain("<script>", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("data:", result, StringComparison.OrdinalIgnoreCase);
        }

        // ─── SanitizeHtml: SVG / MathML XSS ─────────────────────────────────────

        [Fact]
        public void SanitizeHtml_SvgOnLoad_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<svg onload=\"alert(1)\"></svg>");
            Assert.DoesNotContain("onload", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_SvgTag_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<svg><script>alert(1)</script></svg>");
            Assert.DoesNotContain("<svg", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("<script", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_ImgOnerror_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<img src=x onerror=alert(1)>");
            Assert.DoesNotContain("onerror", result, StringComparison.OrdinalIgnoreCase);
        }

        // ─── SanitizeHtml: Obfuscation / Bypass Attacks ──────────────────────────

        [Fact]
        public void SanitizeHtml_HtmlEntityObfuscation_ScriptRemoved()
        {
            // &#x3C;script&#x3E;  →  <script>
            var input = "&#x3C;script&#x3E;alert(1)&#x3C;/script&#x3E;";
            var result = InputSanitizer.SanitizeHtml(input);
            Assert.DoesNotContain("alert", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_DecimalEntityObfuscation_ScriptRemoved()
        {
            // &#60;script&#62;
            var input = "&#60;script&#62;evil()&#60;/script&#62;";
            var result = InputSanitizer.SanitizeHtml(input);
            Assert.DoesNotContain("evil()", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_MixedCaseTagBypass_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<ScRiPt>alert(1)</ScRiPt>");
            Assert.DoesNotContain("alert", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_CssExpressionBypass_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("<p style=\"top: expression(alert(1))\">x</p>");
            Assert.DoesNotContain("expression(", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void SanitizeHtml_TemplateLiteralBypass_Removed()
        {
            var result = InputSanitizer.SanitizeHtml("Hello ${evil}");
            Assert.DoesNotContain("${evil}", result);
        }

        [Fact]
        public void SanitizeHtml_ControlChars_Stripped()
        {
            // Use char code to insert real control chars into the string
            var input = "safe" + (char)0x00 + "text" + (char)0x0B + "more";
            var result = InputSanitizer.SanitizeHtml(input);
            // After sanitization all control chars should be gone
            Assert.True(result.Length < input.Length, "Control chars should be removed, shortening the string");
            Assert.Contains("safe", result);
            Assert.Contains("text", result);
            foreach (char c in result)
                Assert.True(c >= 0x09 && c != 0x0B && c != 0x0C || c > 0x1F, $"Unexpected control char 0x{(int)c:X2} found in result");
        }

        [Fact]
        public void SanitizeHtml_EmptyInput_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, InputSanitizer.SanitizeHtml(""));
        }

        [Fact]
        public void SanitizeHtml_NullInput_ReturnsEmpty()
        {
            Assert.Equal(string.Empty, InputSanitizer.SanitizeHtml(null!));
        }

        // ─── Reflected XSS: Context Encoders ────────────────────────────────────

        [Fact]
        public void EncodeForHtmlContext_AngleBrackets_Encoded()
        {
            var result = InputSanitizer.EncodeForHtmlContext("<script>alert(1)</script>");
            Assert.Equal("&lt;script&gt;alert(1)&lt;&#x2F;script&gt;", result);
        }

        [Fact]
        public void EncodeForHtmlContext_Ampersand_Encoded()
        {
            var result = InputSanitizer.EncodeForHtmlContext("foo & bar");
            Assert.DoesNotContain("&", result.Replace("&amp;", ""));
        }

        [Fact]
        public void EncodeForHtmlAttributeContext_SpecialChars_HexEncoded()
        {
            // The encoder encodes non-alphanumeric chars like " = space
            // It does NOT remove words like 'onmouseover' — it ensures they can't break
            // out of the containing attribute by encoding the surrounding quotes
            var result = InputSanitizer.EncodeForHtmlAttributeContext("\" onmouseover=\"evil");
            // The double-quote characters must be encoded, not raw
            Assert.DoesNotContain("\"", result);
            // The encoded form should contain the &#x22; entity for the double quote
            Assert.Contains("&#x22;", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EncodeForJavaScriptContext_ScriptChars_Escaped()
        {
            var result = InputSanitizer.EncodeForJavaScriptContext("</script>");
            Assert.DoesNotContain("<", result);
            Assert.DoesNotContain(">", result);
        }

        [Fact]
        public void EncodeForUrlContext_SpecialChars_PercentEncoded()
        {
            var result = InputSanitizer.EncodeForUrlContext("<script>alert(1)</script>");
            Assert.DoesNotContain("<", result);
            Assert.Contains("%3C", result, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void EncodeForCssContext_InjectionChars_Escaped()
        {
            var result = InputSanitizer.EncodeForCssContext("expression(alert(1))");
            Assert.DoesNotContain("expression(", result, StringComparison.OrdinalIgnoreCase);
        }

        // ─── HTTP Header Injection ─────────────────────────────────────────────

        [Fact]
        public void SanitizeForHeaderContext_CRLFRemoved()
        {
            var result = InputSanitizer.SanitizeForHeaderContext("legit\r\nSet-Cookie: evil=val");
            Assert.DoesNotContain("\r", result);
            Assert.DoesNotContain("\n", result);
            Assert.Contains("legit", result);
        }

        [Fact]
        public void AssertSafeHeaderValue_WithCRLF_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                InputSanitizer.AssertSafeHeaderValue("value\r\nevil-header: injected"));
        }

        [Fact]
        public void AssertSafeHeaderValue_CleanValue_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                InputSanitizer.AssertSafeHeaderValue("application/json"));
            Assert.Null(ex);
        }

        // ─── MaxLength ───────────────────────────────────────────────────────────

        [Fact]
        public void MaxLength_LongInput_Truncated()
        {
            var result = InputSanitizer.MaxLength("Hello World", 5);
            Assert.Equal("Hello", result);
        }

        [Fact]
        public void MaxLength_ExactLength_ReturnsSame()
        {
            var result = InputSanitizer.MaxLength("Hello", 5);
            Assert.Equal("Hello", result);
        }

        // ─── IsAlphanumericSafe ─────────────────────────────────────────────────

        [Theory]
        [InlineData("user_name-123", true)]
        [InlineData("safe-id_99", true)]
        [InlineData("../etc/passwd", false)]
        [InlineData("id; DROP TABLE", false)]
        [InlineData("", false)]
        public void IsAlphanumericSafe_Variants(string input, bool expected)
        {
            Assert.Equal(expected, InputSanitizer.IsAlphanumericSafe(input));
        }
    }
}
