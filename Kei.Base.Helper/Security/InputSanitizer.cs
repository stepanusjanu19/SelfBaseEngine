using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Provides comprehensive XSS protection including:
    /// <list type="bullet">
    ///   <item>Stored XSS — sanitization of HTML fragments</item>
    ///   <item>Reflected XSS — context-aware output encoding for URL parameters, HTTP headers, JSON contexts</item>
    ///   <item>DOM XSS — stripping of dangerous protocols and event handlers</item>
    /// </list>
    /// </summary>
    public static class InputSanitizer
    {
        // ─── Regex Definitions ────────────────────────────────────────────────────

        // Matches any HTML open/close/self-closing tag
        private static readonly Regex _htmlTagRegex =
            new(@"<[^>]*(>|$)", RegexOptions.Compiled | RegexOptions.Singleline);

        // Matches on* event attributes — fixed: no leading whitespace requirement,
        // handles attribute at position 0 in a tag or after a space/tab/newline.
        private static readonly Regex _eventHandlerRegex =
            new(@"(?:^|\s|/)on\w+\s*=\s*(?:""[^""]*""|'[^']*'|\S+)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        // Matches href/src/action and similar attributes containing dangerous protocols
        private static readonly Regex _jsProtocolRegex =
            new(@"(?:href|src|action|formaction|xlink:href|data|background|poster|srcdoc)\s*=\s*[""']?\s*(?:javascript|vbscript|data|livescript|mocha)\s*:",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Dangerous tags that must be removed entirely with their contents
        // Expanded to include svg, math, video, audio, picture, canvas, animate
        private static readonly Regex _dangerousTagsWithContent =
            new(@"<\s*(?:script|iframe|object|embed|applet|base|link|meta|style|form|svg|math|video|audio|canvas|animate|set|use)[^>]*>.*?<\s*/\s*(?:script|iframe|object|embed|applet|base|link|meta|style|form|svg|math|video|audio|canvas|animate|set|use)\s*>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Matches potentially dangerous self-closing or single tags
        private static readonly Regex _dangerousSelfClosingTags =
            new(@"<\s*(?:script|iframe|object|embed|applet|base|link|meta|style|form|svg|math|input|img|video|audio|source|track|area)[^>]*/?>",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Unicode entity bypass: &#x3C; &#60; \u003c etc.
        private static readonly Regex _htmlEntityDecodeRegex =
            new(@"&#[xX]?[0-9a-fA-F]+;?|\\u[0-9a-fA-F]{4}|\\[0-7]{1,3}",
                RegexOptions.Compiled);

        // Control characters (except \t, \n, \r) + zero-width + BOM
        private static readonly Regex _controlCharsRegex =
            new(@"[\x00-\x08\x0B\x0C\x0E-\x1F\x7F\uFEFF\u200B\u200C\u200D\u200E\u200F\u2028\u2029]",
                RegexOptions.Compiled);

        // CSS expression() injection
        private static readonly Regex _cssExpressionRegex =
            new(@"expression\s*\(",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Template literal injection (${...}) and backtick XSS
        private static readonly Regex _templateLiteralRegex =
            new(@"\$\{[^}]*\}|`[^`]*`",
                RegexOptions.Compiled);

        // Detects attempts to break out of HTML attributes via quotes + angle brackets
        private static readonly Regex _attributeBreakoutRegex =
            new(@"[""'][^""']*[<>]",
                RegexOptions.Compiled);

        // HTTP response header injection (CR/LF injection)
        private static readonly Regex _headerInjectionRegex =
            new(@"[\r\n\x00]",
                RegexOptions.Compiled);

        // ─── Stored XSS — HTML Sanitization ──────────────────────────────────────

        /// <summary>
        /// Sanitizes an untrusted HTML fragment to prevent Stored and DOM XSS.
        /// Performs multi-pass normalization to defeat obfuscation:
        /// <list type="number">
        ///   <item>Strips control chars and zero-width Unicode</item>
        ///   <item>Decodes HTML entities (prevents &#x3C;script&#x3E; bypass)</item>
        ///   <item>Removes dangerous tag content (script, svg, iframe, etc.)</item>
        ///   <item>Removes event handler attributes (on* at any position)</item>
        ///   <item>Neutralizes dangerous URL protocols (javascript:, data:, vbscript:)</item>
        ///   <item>Removes CSS expression() and template literals</item>
        /// </list>
        /// </summary>
        public static string SanitizeHtml(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            // Step 1: strip control chars
            var result = _controlCharsRegex.Replace(input, string.Empty);

            // Step 2: decode HTML entities to catch obfuscated payloads like &#x3C;script>
            // We do this before pattern matching so bypasses via encoding are caught
            result = DecodeHtmlEntitiesForScan(result);

            // Step 3: remove content-bearing dangerous tags (with contents)
            result = _dangerousTagsWithContent.Replace(result, string.Empty);

            // Step 4: remove self-closing dangerous tags
            result = _dangerousSelfClosingTags.Replace(result, string.Empty);

            // Step 5: remove event handler attributes at any position in a tag
            result = _eventHandlerRegex.Replace(result, string.Empty);

            // Step 6: neutralize dangerous protocols in attributes
            result = _jsProtocolRegex.Replace(result, match =>
            {
                return Regex.Replace(match.Value,
                    @"(?:javascript|vbscript|data|livescript|mocha)\s*:",
                    "blocked:",
                    RegexOptions.IgnoreCase);
            });

            // Step 7: remove CSS expression() patterns (IE-specific XSS)
            result = _cssExpressionRegex.Replace(result, "blocked(");

            // Step 8: remove template literal injections
            result = _templateLiteralRegex.Replace(result, string.Empty);

            // Step 9: second pass — re-check after decoding in case of nested obfuscation
            if (ContainsSuspiciousPattern(result))
            {
                result = _dangerousTagsWithContent.Replace(result, string.Empty);
                result = _dangerousSelfClosingTags.Replace(result, string.Empty);
                result = _eventHandlerRegex.Replace(result, string.Empty);
            }

            return result;
        }

        // ─── Reflected XSS — Context-Aware Output Encoding ───────────────────────

        /// <summary>
        /// Encodes a value for safe output in an HTML body context (Reflected XSS prevention).
        /// Encodes: &amp; &lt; &gt; &quot; &#x27; &#x2F; and backtick.
        /// Use this for values that come from URL parameters, form fields, or any untrusted source
        /// being reflected into HTML output.
        /// </summary>
        public static string EncodeForHtmlContext(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new StringBuilder(input.Length * 2);
            foreach (var c in input)
            {
                switch (c)
                {
                    case '&': sb.Append("&amp;"); break;
                    case '<': sb.Append("&lt;"); break;
                    case '>': sb.Append("&gt;"); break;
                    case '"': sb.Append("&quot;"); break;
                    case '\'': sb.Append("&#x27;"); break;
                    case '/': sb.Append("&#x2F;"); break;
                    case '`': sb.Append("&#x60;"); break;
                    case '=': sb.Append("&#x3D;"); break;
                    default: sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encodes a value for safe insertion into an HTML attribute value (Reflected XSS prevention).
        /// Applies full attribute-context encoding. The encoded string must be placed inside a
        /// quoted attribute: <c>attr="[encoded]"</c>.
        /// </summary>
        public static string EncodeForHtmlAttributeContext(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            // HTML attribute encoding: all chars outside [a-zA-Z0-9] get &#xHH; encoding
            var sb = new StringBuilder(input.Length * 4);
            foreach (var c in input)
            {
                if (char.IsLetterOrDigit(c) || c == '.' || c == '-' || c == '_' || c == ' ')
                    sb.Append(c);
                else
                    sb.Append($"&#x{(int)c:X};");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encodes a value for safe insertion inside a JavaScript string literal (DOM XSS prevention).
        /// The output must be placed inside a quoted JS string: <c>var x = '[encoded]'</c>.
        /// </summary>
        public static string EncodeForJavaScriptContext(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new StringBuilder(input.Length * 4);
            foreach (var c in input)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(c);
                else if (c < 256)
                    sb.Append($"\\x{(int)c:X2}");
                else
                    sb.Append($"\\u{(int)c:X4}");
            }
            return sb.ToString();
        }

        /// <summary>
        /// Encodes a value for safe insertion in a URL parameter (Reflected XSS via URL prevention).
        /// Applies percent-encoding to all characters except unreserved URI characters.
        /// </summary>
        public static string EncodeForUrlContext(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return Uri.EscapeDataString(input);
        }

        /// <summary>
        /// Encodes a value for safe insertion in a CSS property value (CSS injection prevention).
        /// </summary>
        public static string EncodeForCssContext(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;

            var sb = new StringBuilder(input.Length * 4);
            foreach (var c in input)
            {
                if (char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_' || c == '.' || c == '#')
                    sb.Append(c);
                else
                    sb.Append($"\\{(int)c:X6} ");
            }
            return sb.ToString();
        }

        // ─── HTTP Header Injection Prevention ─────────────────────────────────────

        /// <summary>
        /// Sanitizes a value intended for use in an HTTP response header by stripping
        /// carriage return (CR), line feed (LF), and null bytes that enable header injection.
        /// </summary>
        public static string SanitizeForHeaderContext(string input)
        {
            if (string.IsNullOrEmpty(input)) return string.Empty;
            return _headerInjectionRegex.Replace(input, string.Empty);
        }

        /// <summary>
        /// Validates that an HTTP header value is safe (no CR/LF/null bytes).
        /// Throws <see cref="ArgumentException"/> if the value would enable header injection.
        /// </summary>
        public static void AssertSafeHeaderValue(string headerValue, string headerName = "header")
        {
            if (_headerInjectionRegex.IsMatch(headerValue ?? string.Empty))
                throw new ArgumentException(
                    $"The value for '{headerName}' contains CR/LF/null characters which could enable HTTP response splitting.",
                    headerName);
        }

        // ─── General Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Strips all HTML tags from the input, returning plain text.
        /// </summary>
        public static string StripHtmlTags(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            var result = _controlCharsRegex.Replace(input, string.Empty);
            result = DecodeHtmlEntitiesForScan(result);
            return _htmlTagRegex.Replace(result, string.Empty);
        }

        /// <summary>
        /// Removes null bytes, zero-width characters, and control characters
        /// that can confuse parsers or bypass WAF rules.
        /// </summary>
        public static string StripControlChars(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input ?? string.Empty;

            return _controlCharsRegex.Replace(input, string.Empty);
        }

        /// <summary>
        /// Truncates the string to <paramref name="maxLength"/> characters,
        /// respecting surrogate pairs.
        /// </summary>
        public static string MaxLength(string input, int maxLength)
        {
            if (string.IsNullOrEmpty(input) || input.Length <= maxLength)
                return input ?? string.Empty;

            if (maxLength > 0 && char.IsHighSurrogate(input[maxLength - 1]))
                maxLength--;

            return input.Substring(0, maxLength);
        }

        /// <summary>
        /// HTML-encodes a plain-text string for embedding in an HTML document (HTML body context).
        /// </summary>
        public static string HtmlEncode(string input)
            => EncodeForHtmlContext(input);

        /// <summary>
        /// Returns <c>true</c> if the string contains only alphanumeric characters,
        /// underscores, or hyphens — safe pattern for IDs and slugs.
        /// </summary>
        public static bool IsAlphanumericSafe(string input)
        {
            if (string.IsNullOrEmpty(input)) return false;
            foreach (var c in input)
            {
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-')
                    return false;
            }
            return true;
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        /// <summary>
        /// Performs a lightweight HTML entity + unicode escape decode ONLY for the purpose
        /// of scanning. Does NOT produce HTML output — only used to normalize before regex scanning.
        /// </summary>
        private static string DecodeHtmlEntitiesForScan(string input)
        {
            if (string.IsNullOrEmpty(input)) return input;

            // Decode numeric entities: &#60; &#x3C; → < >
            var result = _htmlEntityDecodeRegex.Replace(input, match =>
            {
                var val = match.Value;
                try
                {
                    if (val.StartsWith("\\u", StringComparison.OrdinalIgnoreCase))
                    {
                        var code = Convert.ToInt32(val.Substring(2), 16);
                        return ((char)code).ToString();
                    }
                    if (val.StartsWith("&#x", StringComparison.OrdinalIgnoreCase)
                     || val.StartsWith("&#X", StringComparison.OrdinalIgnoreCase))
                    {
                        var hex = val.Substring(3).TrimEnd(';');
                        var code = Convert.ToInt32(hex, 16);
                        return ((char)code).ToString();
                    }
                    if (val.StartsWith("&#"))
                    {
                        var dec = val.Substring(2).TrimEnd(';');
                        var code = int.Parse(dec);
                        return ((char)code).ToString();
                    }
                }
                catch { /* leave as-is on parse failure */ }
                return val;
            });

            // Also decode named HTML entities via WebUtility
            try { result = WebUtility.HtmlDecode(result); }
            catch { /* swallow */ }

            return result;
        }

        private static readonly string[] _suspiciousTokens = { "<script", "javascript:", "onerror", "onload", "onclick", "onmouseover", "<svg", "<iframe", "<img" };

        private static bool ContainsSuspiciousPattern(string input)
        {
            var lower = input.ToLowerInvariant();
            foreach (var token in _suspiciousTokens)
                if (lower.Contains(token)) return true;
            return false;
        }
    }
}
