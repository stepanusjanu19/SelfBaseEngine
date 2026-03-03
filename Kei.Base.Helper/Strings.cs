using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Helper
{
    public static class StringExtensions
    {
        public static DateTime ToDate(this string value)
        {
            DateTime returnedDate = DateTime.Now;
            value = value.Replace('/', '-');
            value = value.Replace('.', '-');
            List<string> format = new List<string>();
            format.Add("ddMMyyyy");
            format.Add("MMddyyyy");
            format.Add("dd-MM-yyyy");
            format.Add("MM-dd-yyyy");
            format.Add("ddMMyy");
            format.Add("ddMMyyyy");
            format.Add("dd/MM/yyyy");
            format.Add("MM/dd/yyyy");
            try
            {
                returnedDate = DateTime.ParseExact(value, format.ToArray(), System.Globalization.CultureInfo.InvariantCulture, DateTimeStyles.None);
            }
            catch
            {
                try
                {
                    returnedDate = DateTime.ParseExact(value, "MM-dd-yyyy", System.Globalization.CultureInfo.InvariantCulture);
                }
                catch
                {
                    try
                    {
                        returnedDate = DateTime.ParseExact(value, "dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture);
                    }
                    catch
                    {
                        try
                        {
                            returnedDate = DateTime.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
                        }
                        catch
                        {
                            try
                            {
                                returnedDate = DateTime.ParseExact(value, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
                            }
                            catch
                            {
                                try
                                {
                                    returnedDate = DateTime.ParseExact(value, "dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
                                }
                                catch
                                {
                                    DateTime.TryParse(value, out returnedDate);
                                }
                            }
                        }
                    }
                }
            }

            return returnedDate;
        }

        public static DateTime ToTimeStamp(this DateTime datetime)
        {
            return datetime.Kind == DateTimeKind.Utc
                    ? datetime
                    : (datetime.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(datetime, DateTimeKind.Local).ToUniversalTime()
                        : datetime.ToUniversalTime());
        }

        public static DateTime ToTimestampWithoutZone(this DateTime datetime)
        {
            var utc = datetime.ToTimeStamp();
            return DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        }

        public static DateTime ConvertToUnspecified(this string dateStr)
        {
            if (string.IsNullOrWhiteSpace(dateStr))
                throw new ArgumentException("Date string cannot be null or empty.", nameof(dateStr));

            var parsed = DateTime.ParseExact(
                dateStr,
                "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None
            );

            return DateTime.SpecifyKind(parsed, DateTimeKind.Unspecified);
        }

        public static byte[] BuildCsv<T>(IEnumerable<T> data, string delimiter = "|")
        {
            if (data == null || !data.Any())
                return Array.Empty<byte>();

            var sb = new StringBuilder();
            var properties = typeof(T).GetProperties();

            sb.AppendLine(string.Join(delimiter, properties.Select(p => p.Name)));

            foreach (var item in data)
            {
                var row = string.Join(delimiter, properties.Select(p =>
                {
                    var value = p.GetValue(item);
                    return value?.ToString()?.Replace(delimiter, " ") ?? "";
                }));
                sb.AppendLine(row);
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }


        public static byte[] BuildCsv<T>(IEnumerable<T> data, IEnumerable<(string Header, Func<T, object> Selector)>? columns, string delimiter = "|")
        {
            if (columns == null || !columns.Any())
            {
                return BuildCsv(data, delimiter);
            }

            var sb = new StringBuilder();
            sb.AppendLine(string.Join(delimiter, columns.Select(c => c.Header)));

            foreach (var item in data)
            {
                var row = string.Join(delimiter, columns.Select(c =>
                {
                    var value = c.Selector(item);
                    return value?.ToString()?.Replace(delimiter, " ") ?? "";
                }));
                sb.AppendLine(row);
            }

            return Encoding.UTF8.GetBytes(sb.ToString());
        }

#nullable disable
        public static string DecodeOrNull(this string input, string ignoreValue = null)
        {
            if (string.IsNullOrEmpty(input)) return null;
            var decoded = DataEncrypt.base64Decode(input);
            return decoded == ignoreValue ? null : decoded;
        }

        public static string EncodeOrNull(this string input, string ignoreValue = null)
        {
            if (string.IsNullOrEmpty(input)) return null;
            var encoded = DataEncrypt.base64Encode(input);
            return encoded == ignoreValue ? null : encoded;
        }
#nullable restore

        public static string ToUpperSnakeCase(string input) =>
                string.Concat(input.Select((c, i) =>
                    i > 0 && char.IsUpper(c) ? "_" + c : c.ToString())).ToUpper();

        // ─── Security-Oriented String Extensions ──────────────────────────────────

        /// <summary>
        /// Sanitizes an HTML string to prevent XSS injection.
        /// Removes script/iframe/object/style tags, event handler attributes,
        /// and javascript: protocol links. Delegates to <see cref="Security.InputSanitizer.SanitizeHtml"/>.
        /// </summary>
        public static string SanitizeHtml(this string input)
            => Security.InputSanitizer.SanitizeHtml(input);

        /// <summary>
        /// Strips all HTML tags from the string and returns plain text.
        /// Delegates to <see cref="Security.InputSanitizer.StripHtmlTags"/>.
        /// </summary>
        public static string StripHtmlTags(this string input)
            => Security.InputSanitizer.StripHtmlTags(input);

        /// <summary>
        /// Truncates the string to <paramref name="maxLength"/> characters,
        /// respecting Unicode surrogate pairs. No exception is thrown for null input.
        /// </summary>
        public static string TruncateSafe(this string input, int maxLength)
            => Security.InputSanitizer.MaxLength(input, maxLength);

        /// <summary>
        /// Returns <c>true</c> if the string contains only alphanumeric characters,
        /// underscores, or hyphens — safe pattern for IDs and slugs.
        /// Delegates to <see cref="Security.InputSanitizer.IsAlphanumericSafe"/>.
        /// </summary>
        public static bool IsAlphanumericSafe(this string input)
            => Security.InputSanitizer.IsAlphanumericSafe(input);
    }
}
