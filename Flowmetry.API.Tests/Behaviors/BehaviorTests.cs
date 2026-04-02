using Flowmetry.Application.Behaviors;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Flowmetry.API.Tests.Behaviors;

/// <summary>
/// Tests for LoggingBehavior and PerformanceBehavior (Task 12.6).
/// </summary>
public class BehaviorTests
{
    // ── LoggingBehavior ───────────────────────────────────────────────────────

    [Fact]
    public async Task LoggingBehavior_LogsRequestTypeName()
    {
        var collector = new LogCollector<LoggingBehavior<TestRequest, TestResponse>>();
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(collector);

        var response = await behavior.Handle(
            new TestRequest(),
            _ => Task.FromResult(new TestResponse()),
            CancellationToken.None);

        Assert.NotNull(response);

        var infoLogs = collector.Entries
            .Where(e => e.Level == LogLevel.Information)
            .ToList();

        Assert.NotEmpty(infoLogs);
        Assert.Contains(infoLogs, e => e.Message.Contains(nameof(TestRequest)));
    }

    [Fact]
    public async Task LoggingBehavior_LogsSuccessOutcome()
    {
        var collector = new LogCollector<LoggingBehavior<TestRequest, TestResponse>>();
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(collector);

        await behavior.Handle(
            new TestRequest(),
            _ => Task.FromResult(new TestResponse()),
            CancellationToken.None);

        Assert.Contains(collector.Entries,
            e => e.Level == LogLevel.Information && e.Message.Contains("successfully"));
    }

    [Fact]
    public async Task LoggingBehavior_LogsError_WhenHandlerThrows()
    {
        var collector = new LogCollector<LoggingBehavior<TestRequest, TestResponse>>();
        var behavior = new LoggingBehavior<TestRequest, TestResponse>(collector);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            behavior.Handle(
                new TestRequest(),
                _ => throw new InvalidOperationException("boom"),
                CancellationToken.None));

        Assert.Contains(collector.Entries, e => e.Level == LogLevel.Error);
    }

    // ── PerformanceBehavior ───────────────────────────────────────────────────

    [Fact]
    public async Task PerformanceBehavior_FastHandler_DoesNotLogWarning()
    {
        var collector = new LogCollector<PerformanceBehavior<TestRequest, TestResponse>>();
        var behavior = new PerformanceBehavior<TestRequest, TestResponse>(collector);

        await behavior.Handle(
            new TestRequest(),
            _ => Task.FromResult(new TestResponse()),
            CancellationToken.None);

        Assert.DoesNotContain(collector.Entries, e => e.Level == LogLevel.Warning);
    }

    [Fact]
    public async Task PerformanceBehavior_SlowHandler_LogsWarning()
    {
        var collector = new LogCollector<PerformanceBehavior<TestRequest, TestResponse>>();
        var behavior = new PerformanceBehavior<TestRequest, TestResponse>(collector);

        // Simulate a slow handler (> 500ms threshold)
        await behavior.Handle(
            new TestRequest(),
            async _ =>
            {
                await Task.Delay(600);
                return new TestResponse();
            },
            CancellationToken.None);

        Assert.Contains(collector.Entries, e => e.Level == LogLevel.Warning);
    }
}

// ── Test doubles ─────────────────────────────────────────────────────────────

public record TestRequest : IRequest<TestResponse>;
public record TestResponse;

internal sealed class LogEntry
{
    public LogLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
}

/// <summary>Captures log entries for assertion.</summary>
internal sealed class LogCollector<T> : ILogger<T>
{
    public List<LogEntry> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        Entries.Add(new LogEntry
        {
            Level = logLevel,
            Message = formatter(state, exception)
        });
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();
        public void Dispose() { }
    }
}
