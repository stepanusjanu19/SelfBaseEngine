using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Extensions
{
    public struct StatusMessage
    {
        public const string Success = "success";
        public const string Error = "error";
    }

    public struct EnvironmentCrypto
    {
        public const string PASSPHRASE = "Pas5pr@se";
        public const string SALTVALUE = "s@1tValue";
        public const string HASHALGORITHM = "SHA1";
        public const int PASSWORDITERATIONS = 2;
        public const string INITVECTOR = "@1B2c3D4e5F6g7H8";
        public const int KEYSIZE = 128;
    }

    /// <summary>
    /// Validates that <see cref="EnvironmentCrypto"/> constants have been overridden
    /// from their insecure default values. Call <see cref="WarnIfUsingDefaults"/> at
    /// application startup to surface this risk in logs.
    ///
    /// The existing <see cref="EnvironmentCrypto"/> constants are intentionally left
    /// unchanged; this class only reports when defaults are still in use.
    /// </summary>
    public static class EnvironmentCryptoValidator
    {
        private const string DefaultPassphrase = "Pas5pr@se";
        private const string DefaultSaltValue = "s@1tValue";
        private const string DefaultInitVector = "@1B2c3D4e5F6g7H8";
        private const int DefaultIterations = 2;
        private const string DefaultHashAlgorithm = "SHA1";

        /// <summary>
        /// Writes a warning via <see cref="Console.Error" /> when any
        /// <see cref="EnvironmentCrypto"/> constant still equals its insecure default.
        /// Override these values in your application's configuration
        /// (environment variables, secrets manager, etc.) before deploying to production.
        /// </summary>
        public static void WarnIfUsingDefaults()
        {
            var warnings = new System.Collections.Generic.List<string>();

            if (EnvironmentCrypto.PASSPHRASE == DefaultPassphrase)
                warnings.Add("PASSPHRASE is using the insecure default value.");

            if (EnvironmentCrypto.SALTVALUE == DefaultSaltValue)
                warnings.Add("SALTVALUE is using the insecure default value.");

            if (EnvironmentCrypto.INITVECTOR == DefaultInitVector)
                warnings.Add("INITVECTOR is using the insecure default value.");

            if (EnvironmentCrypto.PASSWORDITERATIONS == DefaultIterations)
                warnings.Add("PASSWORDITERATIONS is set to 2, which is dangerously low. Use at least 100,000 for PBKDF2.");

            if (string.Equals(EnvironmentCrypto.HASHALGORITHM, DefaultHashAlgorithm, StringComparison.OrdinalIgnoreCase))
                warnings.Add("HASHALGORITHM is SHA1, which is weak. Consider upgrading to SHA256 or SHA512.");

            if (warnings.Count > 0)
            {
                Console.Error.WriteLine(
                    $"[SECURITY WARNING] EnvironmentCrypto has {warnings.Count} insecure default value(s):");
                foreach (var w in warnings)
                    Console.Error.WriteLine($"  • {w}");
                Console.Error.WriteLine(
                    "  Override these values with secrets from your deployment environment before running in production.");
            }
        }

        /// <summary>
        /// Returns <c>true</c> when all crypto constants have been changed from their defaults.
        /// </summary>
        public static bool AllOverridden =>
            EnvironmentCrypto.PASSPHRASE != DefaultPassphrase &&
            EnvironmentCrypto.SALTVALUE != DefaultSaltValue &&
            EnvironmentCrypto.INITVECTOR != DefaultInitVector &&
            EnvironmentCrypto.PASSWORDITERATIONS > DefaultIterations;
    }

    public struct TimeZoneAll
    {
        // UTC
        public const string UTC = "UTC";

        // America
        public const string AMERICA_NEW_YORK = "America/New_York";
        public const string AMERICA_CHICAGO = "America/Chicago";
        public const string AMERICA_DENVER = "America/Denver";
        public const string AMERICA_LOS_ANGELES = "America/Los_Angeles";
        public const string AMERICA_TORONTO = "America/Toronto";
        public const string AMERICA_SAO_PAULO = "America/Sao_Paulo";

        // Europe
        public const string EUROPE_LONDON = "Europe/London";
        public const string EUROPE_PARIS = "Europe/Paris";
        public const string EUROPE_BERLIN = "Europe/Berlin";
        public const string EUROPE_MADRID = "Europe/Madrid";
        public const string EUROPE_ROME = "Europe/Rome";
        public const string EUROPE_MOSCOW = "Europe/Moscow";

        // Asia
        public const string ASIA_JKT = "Asia/Jakarta";
        public const string ASIA_BANGKOK = "Asia/Bangkok";
        public const string ASIA_KUALA_LUMPUR = "Asia/Kuala_Lumpur";
        public const string ASIA_SINGAPORE = "Asia/Singapore";
        public const string ASIA_MANILA = "Asia/Manila";
        public const string ASIA_TOKYO = "Asia/Tokyo";
        public const string ASIA_SEOUL = "Asia/Seoul";
        public const string ASIA_SHANGHAI = "Asia/Shanghai";
        public const string ASIA_DUBAI = "Asia/Dubai";
        public const string ASIA_KOLKATA = "Asia/Kolkata";
        public const string ASIA_HONG_KONG = "Asia/Hong_Kong";

        // Australia
        public const string AUSTRALIA_SYDNEY = "Australia/Sydney";
        public const string AUSTRALIA_MELBOURNE = "Australia/Melbourne";
        public const string AUSTRALIA_PERTH = "Australia/Perth";
        public const string AUSTRALIA_BRISBANE = "Australia/Brisbane";

        // Africa
        public const string AFRICA_JOHANNESBURG = "Africa/Johannesburg";
        public const string AFRICA_CAIRO = "Africa/Cairo";
        public const string AFRICA_LAGOS = "Africa/Lagos";

        // Middle East
        public const string ASIA_RIYADH = "Asia/Riyadh";
        public const string ASIA_TEHRAN = "Asia/Tehran";

        // Others
        public const string PACIFIC_AUCKLAND = "Pacific/Auckland";
        public const string PACIFIC_FIJI = "Pacific/Fiji";
    }
}
