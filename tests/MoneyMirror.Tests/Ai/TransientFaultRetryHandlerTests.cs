using System.Net;
using MoneyMirror.Ai;

namespace MoneyMirror.Tests.Ai;

public class TransientFaultRetryHandlerTests
{
    /// <summary>
    /// Answers with a scripted sequence of statuses (the last repeats), and
    /// records every request body it saw so a retry can be shown to resend the
    /// original payload rather than an empty one.
    /// </summary>
    private sealed class ScriptedHandler(params HttpStatusCode[] statuses) : HttpMessageHandler
    {
        private readonly List<string> _bodies = [];

        public IReadOnlyList<string> Bodies => _bodies;
        public int Calls => _bodies.Count;
        public Exception? Throw { get; init; }
        public int ThrowTimes { get; init; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _bodies.Add(request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken));

            if (Throw is not null && _bodies.Count <= ThrowTimes)
            {
                throw Throw;
            }

            var status = statuses[Math.Min(_bodies.Count - 1, statuses.Length - 1)];
            return new HttpResponseMessage(status) { Content = new StringContent("body") };
        }
    }

    private static (HttpClient Client, List<TimeSpan> Delays) Build(
        ScriptedHandler inner, int maxRetries = 3)
    {
        var delays = new List<TimeSpan>();
        var handler = new TransientFaultRetryHandler(
            maxRetries,
            TimeSpan.FromSeconds(1),
            (wait, _) => { delays.Add(wait); return Task.CompletedTask; })
        {
            InnerHandler = inner,
        };

        return (new HttpClient(handler), delays);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client) =>
        client.PostAsync("https://example.test/v1/chat/completions", new StringContent("{\"model\":\"m\"}"));

    [Fact]
    public async Task Send_WhenFirstCallSucceeds_DoesNotRetry()
    {
        var inner = new ScriptedHandler(HttpStatusCode.OK);
        var (client, delays) = Build(inner);

        var response = await PostAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.Calls);
        Assert.Empty(delays);
    }

    // The observed NVIDIA failure: 503 ResourceExhausted, then a normal answer.
    [Fact]
    public async Task Send_WhenProviderIsSaturated_RetriesAndSucceeds()
    {
        var inner = new ScriptedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var (client, delays) = Build(inner);

        var response = await PostAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
        Assert.Equal([TimeSpan.FromSeconds(1)], delays);
    }

    [Fact]
    public async Task Send_BacksOffExponentially()
    {
        var inner = new ScriptedHandler(HttpStatusCode.ServiceUnavailable);
        var (client, delays) = Build(inner);

        await PostAsync(client);

        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)],
            delays);
        Assert.Equal(4, inner.Calls);
    }

    [Fact]
    public async Task Send_WhenRetriesAreExhausted_ReturnsTheLastResponse()
    {
        var inner = new ScriptedHandler(HttpStatusCode.ServiceUnavailable);
        var (client, _) = Build(inner);

        var response = await PostAsync(client);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    // A bad key or malformed body cannot be fixed by asking again.
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    public async Task Send_ClientErrorsAreNotRetried(HttpStatusCode status)
    {
        var inner = new ScriptedHandler(status);
        var (client, delays) = Build(inner);

        var response = await PostAsync(client);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(1, inner.Calls);
        Assert.Empty(delays);
    }

    [Theory]
    [InlineData(HttpStatusCode.RequestTimeout)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.GatewayTimeout)]
    public async Task Send_TransientStatusesAreRetried(HttpStatusCode status)
    {
        var inner = new ScriptedHandler(status, HttpStatusCode.OK);
        var (client, _) = Build(inner);

        var response = await PostAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    // The first attempt consumes the request's content stream, so without
    // buffering the retry would post an empty body.
    [Fact]
    public async Task Send_RetryResendsTheOriginalBody()
    {
        var inner = new ScriptedHandler(HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK);
        var (client, _) = Build(inner);

        await PostAsync(client);

        Assert.Equal(2, inner.Calls);
        Assert.All(inner.Bodies, body => Assert.Equal("{\"model\":\"m\"}", body));
    }

    [Fact]
    public async Task Send_DroppedConnectionIsRetried()
    {
        var inner = new ScriptedHandler(HttpStatusCode.OK)
        {
            Throw = new HttpRequestException("connection reset"),
            ThrowTimes = 1,
        };
        var (client, _) = Build(inner);

        var response = await PostAsync(client);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);
    }

    [Fact]
    public async Task Send_WhenEveryAttemptThrows_RethrowsTheLastException()
    {
        var inner = new ScriptedHandler(HttpStatusCode.OK)
        {
            Throw = new HttpRequestException("connection reset"),
            ThrowTimes = 99,
        };
        var (client, _) = Build(inner);

        await Assert.ThrowsAsync<HttpRequestException>(() => PostAsync(client));
        Assert.Equal(4, inner.Calls);
    }

    [Fact]
    public async Task Send_HonorsRetryAfterOverTheBackoffSchedule()
    {
        var inner = new RetryAfterHandler();
        var delays = new List<TimeSpan>();
        var handler = new TransientFaultRetryHandler(
            maxRetries: 3,
            TimeSpan.FromSeconds(1),
            (wait, _) => { delays.Add(wait); return Task.CompletedTask; })
        {
            InnerHandler = inner,
        };

        await PostAsync(new HttpClient(handler));

        Assert.Equal([TimeSpan.FromSeconds(30)], delays);
    }

    private sealed class RetryAfterHandler : HttpMessageHandler
    {
        private int _calls;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_calls++ == 0)
            {
                var busy = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
                busy.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
                    TimeSpan.FromSeconds(30));
                return Task.FromResult(busy);
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
