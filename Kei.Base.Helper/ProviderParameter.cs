using System.Data.Common;

using MySqlParams = MySql.Data.MySqlClient.MySqlParameter;
using PostgreParams = Npgsql.NpgsqlParameter;
using SqlServerParams = Microsoft.Data.SqlClient.SqlParameter;
using OracleParams = Oracle.ManagedDataAccess.Client.OracleParameter;

namespace Kei.Base.Helper
{
    public static class ProviderParameter
    {
        public static DbParameter Create(string providerName, string name, object? value)
        {
            var val = value ?? DBNull.Value;

            if (providerName.Contains("Npgsql"))
                return new PostgreParams(name, val);
            if (providerName.Contains("SqlServer"))
                return new SqlServerParams(name, val);
            if (providerName.Contains("MySql"))
                return new MySqlParams(name, val);
            if (providerName.Contains("Oracle"))
                return new OracleParams(name, val);

            throw new NotSupportedException($"Provider {providerName} not supported.");
        }
        
        public static string ProcedureCallSyntax(string provider, string procName, IList<string> paramNames)
        {
            string placeholders = string.Join(", ", paramNames.Select(p => $"@{p}"));

            if (provider.Contains("SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                return paramNames.Count > 0
                    ? $"EXEC {procName} {placeholders}"
                    : $"EXEC {procName}";
            }
            else if (provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) ||
                     provider.Contains("MySql", StringComparison.OrdinalIgnoreCase) ||
                     provider.Contains("MariaDb", StringComparison.OrdinalIgnoreCase))
            {
                return paramNames.Count > 0
                    ? $"CALL {procName}({placeholders})"
                    : $"CALL {procName}()";
            }
            else if (provider.Contains("Oracle", StringComparison.OrdinalIgnoreCase))
            {
                return paramNames.Count > 0
                    ? $"BEGIN {procName}({placeholders}); END;"
                    : $"BEGIN {procName}; END;";
            }

            throw new NotSupportedException($"Unsupported provider: {provider}");
        }
    }
}

