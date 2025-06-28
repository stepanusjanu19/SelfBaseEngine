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
