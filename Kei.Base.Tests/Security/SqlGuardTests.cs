using System;
using System.Collections.Generic;
using Kei.Base.Helper.Security;

namespace Kei.Base.Tests.Security
{
    /// <summary>
    /// Unit tests for SqlGuard — covers column/proc/sequence allowlists and
    /// SQL injection heuristic detection including bypass variants.
    /// </summary>
    public class SqlGuardTests
    {
        // ─── AssertSafeColumnName ─────────────────────────────────────────────────

        [Fact]
        public void AssertSafeColumnName_AllowedColumn_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                SqlGuard.AssertSafeColumnName("FirstName", new[] { "FirstName", "LastName" }));
            Assert.Null(ex);
        }

        [Fact]
        public void AssertSafeColumnName_CaseInsensitiveMatch_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                SqlGuard.AssertSafeColumnName("firstname", new[] { "FirstName" }));
            Assert.Null(ex);
        }

        [Fact]
        public void AssertSafeColumnName_NotAllowed_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                SqlGuard.AssertSafeColumnName("malicious; DROP TABLE users--", new[] { "Name" }));
        }

        [Fact]
        public void AssertSafeColumnName_EmptyColumn_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SqlGuard.AssertSafeColumnName("", new[] { "Name" }));
        }

        [Fact]
        public void AssertSafeColumnName_NullAllowedList_Throws()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SqlGuard.AssertSafeColumnName("Name", null!));
        }

        // ─── AssertSafeProcName ────────────────────────────────────────────────────

        [Theory]
        [InlineData("dbo.sp_GetUsers")]
        [InlineData("proc_createOrder")]
        [InlineData("schema.MyProc")]
        public void AssertSafeProcName_SafeNames_DoNotThrow(string name)
        {
            var ex = Record.Exception(() => SqlGuard.AssertSafeProcName(name));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData("dbo.sp'; DROP TABLE users--")]
        [InlineData("proc <injection>")]
        [InlineData("' OR 1=1--")]
        public void AssertSafeProcName_MaliciousNames_Throw(string name)
        {
            Assert.Throws<ArgumentException>(() => SqlGuard.AssertSafeProcName(name));
        }

        [Fact]
        public void AssertSafeProcName_Empty_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => SqlGuard.AssertSafeProcName(""));
        }

        // ─── AssertSafeSequenceName ────────────────────────────────────────────────

        [Fact]
        public void AssertSafeSequenceName_Safe_DoesNotThrow()
        {
            var ex = Record.Exception(() => SqlGuard.AssertSafeSequenceName("seq_user_id"));
            Assert.Null(ex);
        }

        [Fact]
        public void AssertSafeSequenceName_WithInjection_Throws()
        {
            Assert.Throws<ArgumentException>(() =>
                SqlGuard.AssertSafeSequenceName("seq'; DELETE FROM users--"));
        }

        // ─── AssertSafeRawSql ─────────────────────────────────────────────────────

        [Fact]
        public void AssertSafeRawSql_CleanSelect_DoesNotThrow()
        {
            var ex = Record.Exception(() =>
                SqlGuard.AssertSafeRawSql("SELECT id, name FROM Users WHERE id = @id"));
            Assert.Null(ex);
        }

        [Theory]
        [InlineData("; DROP TABLE users")]
        [InlineData("; DELETE FROM orders")]
        [InlineData("; EXEC xp_cmdshell('dir')")]
        public void AssertSafeRawSql_StackedQuery_Throws(string sql)
        {
            Assert.Throws<InvalidOperationException>(() => SqlGuard.AssertSafeRawSql(sql));
        }

        [Fact]
        public void AssertSafeRawSql_UnionSelect_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("x UNION SELECT username, password FROM users"));
        }

        [Fact]
        public void AssertSafeRawSql_UnionSelectWithNewline_Throws()
        {
            // Bypass attempt: UNION\nSELECT
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("x UNION\nSELECT password FROM users"));
        }

        [Fact]
        public void AssertSafeRawSql_UnionSelectWithComment_Throws()
        {
            // Bypass attempt: UNION/**/SELECT
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("x UNION/**/SELECT password FROM users"));
        }

        [Fact]
        public void AssertSafeRawSql_MySqlInlineComment_Throws()
        {
            // MySQL conditional comment bypass
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("x /*!UNION*/ SELECT 1"));
        }

        [Fact]
        public void AssertSafeRawSql_LineCommentInjection_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("x -- admin comment bypass"));
        }

        [Fact]
        public void AssertSafeRawSql_BlockCommentInjection_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("x /* malicious */ y"));
        }

        [Fact]
        public void AssertSafeRawSql_XpCmdshell_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("x; EXEC xp_cmdshell('whoami')"));
        }

        [Fact]
        public void AssertSafeRawSql_TimingAttack_Sleep_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("1 AND SLEEP(5)--"));
        }

        [Fact]
        public void AssertSafeRawSql_TimingAttack_WaitFor_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("'; WAITFOR DELAY '0:0:5'--"));
        }

        [Fact]
        public void AssertSafeRawSql_HexEncoding_Throws()
        {
            // Hex bypass: 0x61646d696e = "admin"
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("WHERE name = 0x61646d696e704173737764"));
        }

        [Fact]
        public void AssertSafeRawSql_CharFunction_Throws()
        {
            // CHAR(65) bypass
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("SELECT CHAR(65)+CHAR(100)"));
        }

        [Fact]
        public void AssertSafeRawSql_InformationSchema_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("SELECT * FROM information_schema.tables"));
        }

        [Fact]
        public void AssertSafeRawSql_LoadFile_Throws()
        {
            Assert.Throws<InvalidOperationException>(() =>
                SqlGuard.AssertSafeRawSql("SELECT LOAD_FILE('/etc/passwd')"));
        }

        // ─── DetectInjectionPattern ────────────────────────────────────────────────

        [Fact]
        public void DetectInjectionPattern_UnionSelect_ReturnsUnionSelect()
        {
            var name = SqlGuard.DetectInjectionPattern("x UNION SELECT 1");
            Assert.Equal("UnionSelect", name);
        }

        [Fact]
        public void DetectInjectionPattern_CleanSql_ReturnsNull()
        {
            var name = SqlGuard.DetectInjectionPattern("SELECT id FROM Users WHERE id = @id");
            Assert.Null(name);
        }

        // ─── IsSafeIdentifier ─────────────────────────────────────────────────────

        [Theory]
        [InlineData("UserId", true)]
        [InlineData("schema.TableName", true)]
        [InlineData("[ColumnName]", true)]
        [InlineData("'; DROP--", false)]
        [InlineData("", false)]
        public void IsSafeIdentifier_Variants(string input, bool expected)
        {
            Assert.Equal(expected, SqlGuard.IsSafeIdentifier(input));
        }

        // ─── SanitizeColumnName ────────────────────────────────────────────────────

        [Fact]
        public void SanitizeColumnName_SafeName_ReturnedUnchanged()
        {
            Assert.Equal("UserId", SqlGuard.SanitizeColumnName("UserId"));
        }

        [Fact]
        public void SanitizeColumnName_InjectionChars_Stripped()
        {
            // SanitizeColumnName strips chars outside [\w.\[\]] — so ; and spaces are removed
            // The remaining safe chars include letters
            var result = SqlGuard.SanitizeColumnName("UserId; DROP TABLE users");
            Assert.NotNull(result);
            // Semicolons and spaces must be stripped
            Assert.DoesNotContain(";", result!);
            Assert.DoesNotContain(" ", result!);
        }

        [Fact]
        public void SanitizeColumnName_AllBadChars_ReturnsNull()
        {
            var result = SqlGuard.SanitizeColumnName("!!! ###");
            Assert.Null(result);
        }
    }
}
