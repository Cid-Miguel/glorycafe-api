namespace GloryCafe.Application.Common.Interfaces;

public interface IClock
{
    DateTime UtcNow { get; }

    DateOnly TodayInBusinessTimeZone { get; }
}
