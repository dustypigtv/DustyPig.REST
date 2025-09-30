using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.REST;

/// <summary>
/// A simple throttling handler that ensures a minimum delay between requests. If a request is made before the delay has elapsed,
/// it returns a 429 Too Many Requests response with a Retry-After header indicating when the next request can be made.
/// </summary>
public class SimpleThrottle : DelegatingHandler
{
#if NET9_0_OR_GREATER
    private readonly Lock _locker = new();
#else
    private readonly object _locker = new();
#endif

    private readonly TimeSpan _delay;
    private DateTime _lastRequest = DateTime.MinValue;

    public SimpleThrottle(TimeSpan delay, HttpMessageHandler? innerHandler = null) : base(innerHandler ?? new HttpClientHandler())
    {
        if (delay <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(delay), "Must be greater than zero.");
        _delay = delay;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_locker)
        {
            var delta = _delay - (DateTime.UtcNow - _lastRequest);
            if (delta > TimeSpan.Zero)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
                return Task.FromResult(response);
            }

            _lastRequest = DateTime.UtcNow;
            return base.SendAsync(request, cancellationToken);
        }
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_locker)
        {
            var delta = _delay - (DateTime.UtcNow - _lastRequest);
            if (delta > TimeSpan.Zero)
            {
                var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
                return response;
            }

            _lastRequest = DateTime.UtcNow;
            return base.Send(request, cancellationToken);
        }
    }
}
