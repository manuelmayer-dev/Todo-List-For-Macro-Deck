using Serilog;

namespace TodoList.Core.Scheduling;

/// <summary>
/// One shared timer that ticks at each wall-clock minute boundary while anyone listens, so relative due
/// labels, the widget and the minutes variable stay current without each running its own timer.
/// </summary>
public sealed class MinuteClock : IDisposable
{
	private readonly TimeProvider _clock;
	private readonly ILogger _logger;
	private readonly Lock _lock = new();
	private EventHandler<DateTimeOffset>? _tick;
	private ITimer? _timer;

	public MinuteClock(TimeProvider clock, ILogger logger)
	{
		_clock = clock;
		_logger = logger.ForContext<MinuteClock>();
	}

	public DateTimeOffset Now => _clock.GetUtcNow();

	public event EventHandler<DateTimeOffset>? Tick
	{
		add
		{
			lock (_lock)
			{
				_tick += value;
				if (_timer is null)
				{
					Arm();
				}
			}
		}
		remove
		{
			lock (_lock)
			{
				_tick -= value;
				if (_tick is null)
				{
					_timer?.Dispose();
					_timer = null;
				}
			}
		}
	}

	public void Dispose()
	{
		lock (_lock)
		{
			_tick = null;
			_timer?.Dispose();
			_timer = null;
		}
	}

	internal static TimeSpan DelayToNextMinute(DateTimeOffset now)
	{
		var next = new DateTimeOffset(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0, now.Offset).AddMinutes(1);
		return next - now;
	}

	private void Arm() =>
		_timer = _clock.CreateTimer(_ => OnTimer(), null, DelayToNextMinute(_clock.GetUtcNow()), Timeout.InfiniteTimeSpan);

	private void OnTimer()
	{
		EventHandler<DateTimeOffset>? handlers;
		lock (_lock)
		{
			handlers = _tick;
			_timer?.Dispose();
			_timer = null;
			if (handlers is not null)
			{
				Arm();
			}
		}

		if (handlers is null)
		{
			return;
		}

		var now = _clock.GetUtcNow();
		foreach (EventHandler<DateTimeOffset> handler in handlers.GetInvocationList())
		{
			try
			{
				handler(this, now);
			}
			catch (Exception exception)
			{
				// A listener's failure is its own; the clock keeps ticking for the others.
				_logger.Error(exception, "A minute tick subscriber failed");
			}
		}
	}
}
