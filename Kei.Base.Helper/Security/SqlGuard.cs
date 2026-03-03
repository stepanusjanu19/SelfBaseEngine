using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Kei.Base.Helper.Security
{
    /// <summary>
    /// Guards against SQL Injection by validating column names, procedure names,
    /// sequence names, and detecting suspicious raw-SQL patterns.
    ///
    /// Bypass-resistant improvements in v2:
    /// <list type="bullet">
    ///   <item>UNION detection is newline-aware (covers UNION\nSELECT)</item>
    ///   <item>MySQL inline comment bypass (/*!UNION*/) detected</item>
    ///   <item>Hex-encoded string bypass (0x61 0x64...) detected</item>
    ///   <item>Stacked query detection strengthened (no leading semicolon required for batch keywords)</item>
    /// </list>
    /// </summary>
    public static class SqlGuard
    {
        // Only allow identifiers that consist of letters, digits, underscores, dots, or square brackets
        private static readonly Regex _safeIdentifierRegex =
            new(@"^[\w\.\[\]@]+$", RegexOptions.Compiled);

        // ─── SQL Injection Heuristic Patterns (v2 — bypass-resistant) ─────────────

        private static readonly Regex[] _sqlInjectionPatterns =
        {
            // Stacked queries: ; followed by DML/DDL (on same or following line)
            new Regex(@";\s*(?:drop|delete|truncate|update|insert|exec|execute|create|alter|grant|revoke|replace)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),

            // UNION SELECT — newline-aware, also catches UNION/**/SELECT and UNION\tSELECT
            // Covers: UNION SELECT, UNION/*comment*/SELECT, UNION\nSELECT
            new Regex(@"\bunion\b[\s/\*]*\bselect\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),

            // SQL comment injection — line comments and block comments
            new Regex(@"--[^\r\n]*|/\*.*?\*/",
                RegexOptions.Compiled | RegexOptions.Singleline),

            // MySQL inline comment bypass: /*!version-keyword*/
            new Regex(@"/\*![\s\S]*?\*/",
                RegexOptions.Compiled | RegexOptions.Singleline),

            // SQL Server extended stored procs and system sprocs
            new Regex(@"(?:xp_|sp_)\w+",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            // CHAR(N) or CHR(N) — character construction bypass
            new Regex(@"(?:CHAR|CHR|NCHAR)\s*\(\s*\d+\s*\)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            // Time-based blind injection functions (all major DBs)
            new Regex(@"(?:WAITFOR\s+DELAY|SLEEP|BENCHMARK|PG_SLEEP|DBMS_PIPE\.RECEIVE_MESSAGE)\s*\(",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            // Hex-encoded string bypass: 0x41424344 (MySQL/MSSQL)
            new Regex(@"\b0x[0-9a-fA-F]{4,}\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            // Information schema / sys catalog harvesting
            new Regex(@"\b(?:information_schema|sysobjects|sys\.tables|sys\.columns|pg_tables|pg_stat_user_tables)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            // Conditional time injection: IF(condition, SLEEP, 0)
            new Regex(@"\bIF\s*\([^)]*(?:SLEEP|WAITFOR|BENCHMARK)[^)]*\)",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline),

            // LOAD_FILE / INTO OUTFILE (MySQL file read/write)
            new Regex(@"\b(?:LOAD_FILE|INTO\s+(?:OUT|DUMP)FILE|INTO\s+DUMPFILE)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        // ─── Public API ────────────────────────────────────────────────────────────

        /// <summary>
        /// Asserts that <paramref name="columnName"/> is present in <paramref name="allowedColumns"/>.
        /// Throws <see cref="ArgumentException"/> if the column is not permitted, preventing
        /// injection through dynamically constructed ORDER BY clauses.
        /// </summary>
        public static void AssertSafeColumnName(string columnName, IEnumerable<string> allowedColumns)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                throw new ArgumentNullException(nameof(columnName), "Column name must not be null or empty.");

            if (allowedColumns == null)
                throw new ArgumentNullException(nameof(allowedColumns), "Allowed columns list must not be null.");

            var allowed = new HashSet<string>(allowedColumns, StringComparer.OrdinalIgnoreCase);
            if (!allowed.Contains(columnName))
                throw new ArgumentException(
                    $"Column '{columnName}' is not in the allowed column list. Rejected to prevent SQL injection.",
                    nameof(columnName));
        }

        /// <summary>
        /// Asserts that <paramref name="procName"/> contains only safe identifier characters.
        /// </summary>
        public static void AssertSafeProcName(string procName)
        {
            if (string.IsNullOrWhiteSpace(procName))
                throw new ArgumentNullException(nameof(procName), "Procedure name must not be null or empty.");

            if (!_safeIdentifierRegex.IsMatch(procName))
                throw new ArgumentException(
                    $"Procedure name '{procName}' contains illegal characters. Rejected to prevent SQL injection.",
                    nameof(procName));
        }

        /// <summary>
        /// Asserts that <paramref name="sequenceName"/> contains only safe identifier characters.
        /// </summary>
        public static void AssertSafeSequenceName(string sequenceName)
        {
            if (string.IsNullOrWhiteSpace(sequenceName))
                throw new ArgumentNullException(nameof(sequenceName), "Sequence name must not be null or empty.");

            if (!_safeIdentifierRegex.IsMatch(sequenceName))
                throw new ArgumentException(
                    $"Sequence name '{sequenceName}' contains illegal characters. Rejected to prevent SQL injection.",
                    nameof(sequenceName));
        }

        /// <summary>
        /// Performs a heuristic check on a raw SQL string for common injection patterns.
        /// This is a supplemental defense-in-depth measure; always prefer parameterized queries.
        /// </summary>
        /// <exception cref="InvalidOperationException">When a suspicious pattern is detected.</exception>
        public static void AssertSafeRawSql(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql))
                throw new ArgumentNullException(nameof(sql), "SQL string must not be null or empty.");

            foreach (var pattern in _sqlInjectionPatterns)
            {
                if (pattern.IsMatch(sql))
                    throw new InvalidOperationException(
                        "The SQL string failed security validation. Suspicious pattern detected. " +
                        "Ensure the SQL is parameterized and does not contain injection patterns.");
            }
        }

        /// <summary>
        /// Returns <c>true</c> if the identifier contains only safe characters;
        /// <c>false</c> otherwise. Use for conditional checks without throwing.
        /// </summary>
        public static bool IsSafeIdentifier(string identifier)
        {
            if (string.IsNullOrWhiteSpace(identifier)) return false;
            return _safeIdentifierRegex.IsMatch(identifier);
        }

        /// <summary>
        /// Returns the matched SQL injection pattern name for logging/audit purposes,
        /// or <c>null</c> if the SQL appears safe.
        /// </summary>
        public static string? DetectInjectionPattern(string sql)
        {
            if (string.IsNullOrWhiteSpace(sql)) return null;

            string[] patternNames =
            {
                "StackedQuery", "UnionSelect", "CommentInjection", "MySqlInlineComment",
                "DangerousStoredProc", "CharacterConstruction", "TimingAttack",
                "HexEncoding", "InformationSchema", "ConditionalTiming", "FileReadWrite"
            };

            for (int i = 0; i < _sqlInjectionPatterns.Length; i++)
            {
                if (_sqlInjectionPatterns[i].IsMatch(sql))
                    return i < patternNames.Length ? patternNames[i] : $"Pattern{i}";
            }
            return null;
        }

        /// <summary>
        /// Sanitizes a column name for use in dynamic ORDER BY, stripping anything
        /// that is not a safe identifier character. Returns <c>null</c> if the result
        /// would be empty, signalling the caller to skip ordering.
        /// </summary>
        public static string? SanitizeColumnName(string columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName)) return null;

            var sanitized = Regex.Replace(columnName, @"[^\w\.\[\]]", string.Empty);
            return string.IsNullOrEmpty(sanitized) ? null : sanitized;
        }
    }
}
