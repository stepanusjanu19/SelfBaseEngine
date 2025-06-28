using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Kei.Base.Extensions;

namespace Kei.Base.Helper
{
    public static class ZoneDate
    {
        public static string GetUserTimeZone(string cookie)
        {
            return string.IsNullOrWhiteSpace(cookie) ? TimeZoneAll.UTC : cookie;
        }

        public static DateTimeOffset ConvertToUserTimeZone(DateTimeOffset utcDate, string timeZoneId)
        {
            var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            var localTime = TimeZoneInfo.ConvertTimeFromUtc(utcDate.UtcDateTime, timeZone);
            return new DateTimeOffset(localTime, timeZone.BaseUtcOffset);
        }
    }
}
