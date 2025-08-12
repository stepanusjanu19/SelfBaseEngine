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
    }
}

