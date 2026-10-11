using System;
using System.Globalization;

namespace MemoTask.Helpers
{
    /// <summary>
    /// 时间的人话写法。列表卡片要的是「3 分钟前」这种可扫读的相对时间，
    /// 而不是绝对时间戳；超过一周才退回日期。跨阈值靠定时刷新，不做自轮询绑定。
    /// </summary>
    internal static class TimeText
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static string Relative(DateTime moment, DateTime now)
        {
            TimeSpan d = now - moment;
            if (d.TotalSeconds < 45) return "刚刚";
            if (d.TotalMinutes < 60) return (int)d.TotalMinutes + " 分钟前";
            if (IsSameDay(moment, now)) return "今天 " + moment.ToString("HH:mm", Inv);
            DateTime yesterday = now.Date.AddDays(-1);
            if (IsSameDay(moment, yesterday)) return "昨天 " + moment.ToString("HH:mm", Inv);
            if (d.TotalDays < 7) return (int)d.TotalDays + " 天前";
            if (moment.Year == now.Year) return moment.ToString("M 月 d 日", Inv);
            return moment.ToString("yyyy 年 M 月 d 日", Inv);
        }

        /// <summary>待办截止日的人话写法，含逾期天数。</summary>
        public static string Due(DateTime? due, DateTime now)
        {
            if (!due.HasValue) return "无期限";
            DateTime d = due.Value.Date;
            int days = (d - now.Date).Days;
            string clock = due.Value.TimeOfDay == TimeSpan.Zero ? "" : " " + due.Value.ToString("HH:mm", Inv);
            if (days < 0) return "逾期 " + (-days) + " 天";
            if (days == 0) return "今天到期" + clock;
            if (days == 1) return "明天" + clock;
            if (days < 7) return "周" + WeekdayCn(d.DayOfWeek) + " · " + days + " 天后";
            if (d.Year == now.Year) return d.ToString("M 月 d 日", Inv);
            return d.ToString("yyyy 年 M 月 d 日", Inv);
        }

        /// <summary>宽容解析：支持 今天/明天/后天、+3 / 3天后、yyyy-M-d、M-d、d 以及 / . 分隔。</summary>
        public static bool TryParseDate(string text, DateTime today, out DateTime result)
        {
            result = DateTime.MinValue;
            if (string.IsNullOrWhiteSpace(text)) return false;
            string s = text.Trim();

            int offset = KeywordOffset(s);
            if (offset >= 0)
            {
                result = today.Date.AddDays(offset);
                return true;
            }
            if (TryRelativeDays(s, out offset))
            {
                result = today.Date.AddDays(offset);
                return true;
            }

            string numeric = s.Replace('/', '-').Replace('.', '-');
            DateTime parsed;
            if (DateTime.TryParse(numeric, Inv, DateTimeStyles.None, out parsed))
            {
                result = parsed.Date;
                return true;
            }

            string[] parts = numeric.Split('-');
            int m, d, y;
            if (parts.Length == 3 && TryInt(parts[0], out y) && TryInt(parts[1], out m) && TryInt(parts[2], out d))
                return TryBuild(y, m, d, out result);
            if (parts.Length == 2 && TryInt(parts[0], out m) && TryInt(parts[1], out d))
                return TryBuild(today.Year, m, d, out result);
            if (parts.Length == 1 && TryInt(parts[0], out d) && d >= 1 && d <= 31)
                return TryBuild(today.Year, today.Month, d, out result);
            return false;
        }

        /// <summary>认得「无 / 清除」= 取消期限，UI 与解析都要用，单独摊开。</summary>
        public static bool IsClearDateWord(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            string s = text.Trim();
            return s == "无" || s == "清除" || s == "取消" || s == "-";
        }

        /// <summary>返回 -1 表示不是关键词。</summary>
        private static int KeywordOffset(string s)
        {
            switch (s)
            {
                case "今天":
                case "今日": return 0;
                case "明天":
                case "明日": return 1;
                case "后天": return 2;
                case "大后天": return 3;
                case "昨天": return -1;
                default: return -1;
            }
        }

        /// <summary>「+5」「5 天后」「-2 天前」这类相对写法。要求带 +/- 或「天」，否则裸数字按当月几号解释。</summary>
        private static bool TryRelativeDays(string s, out int days)
        {
            days = 0;
            bool marked = s.StartsWith("+") || s.StartsWith("-") || s.IndexOf('天') >= 0;
            if (!marked) return false;

            var kept = new System.Text.StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (c >= '0' && c <= '9') kept.Append(c);
                else if (c == '+' || c == '-') kept.Append(c);
            }
            if (kept.Length == 0) return false;

            int v;
            if (!int.TryParse(kept.ToString(), NumberStyles.AllowLeadingSign, Inv, out v)) return false;
            if (v < -3650 || v > 3650) return false;
            days = v;
            return true;
        }

        public static string ToStorage(DateTime value)
        {
            return value.ToString("yyyy-MM-dd HH:mm:ss", Inv);
        }

        private static bool TryInt(string s, out int value)
        {
            return int.TryParse(s, NumberStyles.Integer, Inv, out value);
        }

        private static bool TryBuild(int y, int m, int d, out DateTime result)
        {
            result = DateTime.MinValue;
            if (y < 2000 || y > 2999 || m < 1 || m > 12 || d < 1 || d > 31) return false;
            try
            {
                result = new DateTime(y, m, d);
                return true;
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
        }

        private static string WeekdayCn(DayOfWeek w)
        {
            switch (w)
            {
                case DayOfWeek.Monday: return "一";
                case DayOfWeek.Tuesday: return "二";
                case DayOfWeek.Wednesday: return "三";
                case DayOfWeek.Thursday: return "四";
                case DayOfWeek.Friday: return "五";
                case DayOfWeek.Saturday: return "六";
                default: return "日";
            }
        }

        private static bool IsSameDay(DateTime a, DateTime b)
        {
            return a.Year == b.Year && a.Month == b.Month && a.Day == b.Day;
        }
    }
}
