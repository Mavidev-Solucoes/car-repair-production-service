using Xunit;
using System.Diagnostics;
using System.Text;
using Infrastructure.Messaging;

namespace InfrastructureTests;

public sealed class MessageTracingTests
{
    [Fact]
    public void Inject_ShouldWriteTraceparentOfCurrentActivity()
    {
        using var activity = new Activity("request").Start();
        activity.TraceStateString = "vendor=1";
        var headers = new Dictionary<string, object>();

        MessageTracing.Inject(headers);

        Assert.Equal(activity.Id, headers[MessageTracing.TraceParentHeader]);
        Assert.Equal("vendor=1", headers[MessageTracing.TraceStateHeader]);
    }

    [Fact]
    public void Inject_WithoutActivity_ShouldNotWriteHeaders()
    {
        Activity.Current = null;
        var headers = new Dictionary<string, object>();

        MessageTracing.Inject(headers);

        Assert.Empty(headers);
    }

    [Fact]
    public async Task ConsumeAsync_ShouldContinueTraceFromHeaders()
    {
        Activity.Current = null;
        var traceId = ActivityTraceId.CreateRandom();
        var headers = new Dictionary<string, object>
        {
            [MessageTracing.TraceParentHeader] = Encoding.UTF8.GetBytes($"00-{traceId}-{ActivitySpanId.CreateRandom()}-01"),
            [MessageTracing.TraceStateHeader] = "vendor=2"
        };
        Activity? inside = null;
        var published = new Dictionary<string, object>();

        await MessageTracing.ConsumeAsync("SomeCommand", "os-1", headers, () =>
        {
            inside = Activity.Current;
            MessageTracing.Inject(published);
            return Task.CompletedTask;
        });

        Assert.NotNull(inside);
        Assert.Equal(traceId, inside!.TraceId);
        Assert.Equal("vendor=2", inside.TraceStateString);
        Assert.Contains(traceId.ToString(), (string)published[MessageTracing.TraceParentHeader]);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task ConsumeAsync_WithoutOrInvalidHeaders_ShouldStartNewTrace()
    {
        Activity.Current = null;
        var traces = new List<ActivityTraceId>();

        await MessageTracing.ConsumeAsync("SomeCommand", null, null, () =>
        {
            traces.Add(Activity.Current!.TraceId);
            return Task.CompletedTask;
        });
        await MessageTracing.ConsumeAsync("SomeCommand", "os-2", new Dictionary<string, object> { [MessageTracing.TraceParentHeader] = 42 }, () =>
        {
            traces.Add(Activity.Current!.TraceId);
            return Task.CompletedTask;
        });

        Assert.Equal(2, traces.Distinct().Count());
        Assert.Null(MessageTracing.Read(new Dictionary<string, object> { ["x"] = 1 }, "x"));
        Assert.Equal("a", MessageTracing.Read(new Dictionary<string, object> { ["x"] = "a" }, "x"));
    }
}
