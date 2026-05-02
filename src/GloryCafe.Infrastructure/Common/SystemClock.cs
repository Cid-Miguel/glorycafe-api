using GloryCafe.Application.Common.Interfaces;

namespace GloryCafe.Infrastructure.Common;

public class SystemClock : IClock
{
    private static readonly TimeZoneInfo BusinessTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Australia/Brisbane");

    public DateTime UtcNow => DateTime.UtcNow;

    public DateOnly TodayInBusinessTimeZone =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, BusinessTimeZone));
}
