using System.Net;

namespace MoneyMirror.Ai;

/// <summary>
/// Retries transient provider failures with exponential backoff.
/// <para>
/// NVIDIA's shared inference endpoint answers in about 200ms with
/// <c>503 ResourceExhausted: Worker local total request limit reached (16/16)</c>
/// whenever every worker is busy. Measured against the Nemotron model, roughly
/// one call in six is turned away this way while the next succeeds - so a
/// failure surfaced to the user is almost always a queue that was briefly full,
/// not a model that could not answer.
/// </para>
/// <para>
/// Only failures that are safe and sensible to repeat are retried: the request
/// never reached a model (429/503/504), the server failed before producing one
/// (500/502), or the connection broke. A 4xx such as a bad key or a malformed
/// body is returned immediately, since repeating it cannot change the answer.
/// </para>
/// </summary>
public class TransientFaultRetryHandler : DelegatingHandler
{
    private readonly int _maxRetries;
    private readonly TimeSpan _baseDelay;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public TransientFaultRetryHandler()
        : this(maxRetries: 3, baseDelay: TimeSpan.FromSeconds(1))
    {
    }

    public TransientFaultRetryHandler(
        int maxRetries,
        TimeSpan baseDelay,
        Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _maxRetries = maxRetries;
        _baseDelay = baseDelay;
        _delay = delay ?? Task.Delay;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        // A sent HttpRequestMessage cannot be sent again, and its content stream
        // is consumed on the first attempt, so buffer the body up front and build
        // a fresh message per attempt.
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsByteArrayAsync(cancellationToken);

        HttpResponseMessage? response = null;
        HttpRequestException? lastException = null;

        for (var attempt = 0; ; attempt++)
        {
            response?.Dispose();
            response = null;
            lastException = null;

            using var attemptRequest = CloneRequest(request, body);

            try
            {
                response = await base.SendAsync(attemptRequest, cancellationToken);
                if (!IsTransient(response.StatusCode))
                {
                    return response;
                }
            }
            catch (HttpRequestException ex)
            {
                lastException = ex;
            }

            if (attempt >= _maxRetries)
            {
                break;
            }

            // 1s, 2s, 4s, ... unless the provider named its own wait.
            var wait = RetryAfter(response) ?? _baseDelay * Math.Pow(2, attempt);
            await _delay(wait, cancellationToken);
        }

        if (response is not null)
        {
            return response;
        }

        throw lastException!;
    }

    private static bool IsTransient(HttpStatusCode status) =>
        status is HttpStatusCode.RequestTimeout          // 408
            or HttpStatusCode.TooManyRequests            // 429
            or HttpStatusCode.InternalServerError        // 500
            or HttpStatusCode.BadGateway                 // 502
            or HttpStatusCode.ServiceUnavailable         // 503
            or HttpStatusCode.GatewayTimeout;            // 504

    private static TimeSpan? RetryAfter(HttpResponseMessage? response)
    {
        var retryAfter = response?.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }

        return null;
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in (IDictionary<string, object?>)request.Options)
        {
            clone.Options.TryAdd(option.Key, option.Value);
        }

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content!.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
    }
}
