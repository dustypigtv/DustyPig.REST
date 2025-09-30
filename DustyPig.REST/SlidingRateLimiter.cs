using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.REST;

/// <summary>
/// A sliding window rate limiter that allows a maximum number of requests within a specified time window. If to many requests
/// are made withing the sliding window, it returns a 429 Too Many Requests response with a Retry-After header indicating when 
/// the next request can be made.
/// </summary>
public class SlidingRateLimiter : DelegatingHandler
{
#if NET9_0_OR_GREATER
    private readonly Lock _locker = new();
#else
    private readonly object _locker = new();
#endif

    private SemaphoreSlim _semaphore = new(1, 1);
    private readonly Queue<DateTime> _requestHistory = new();
    private readonly int _maxRequests;
    private readonly TimeSpan _timeWindow = TimeSpan.Zero;

    public SlidingRateLimiter(int maxRequests, TimeSpan timeWindow, HttpMessageHandler? innerHandler = null) : base(innerHandler ?? new HttpClientHandler())
    {
        if (maxRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "Must be greater than zero.");

        if (timeWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeWindow), "Must be greater than zero.");

        _maxRequests = maxRequests;
        _timeWindow = timeWindow;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_requestHistory.Count == _maxRequests)
            {
                var delta = DateTime.UtcNow - _requestHistory.Peek();
                if (delta > _timeWindow)
                {
                    _requestHistory.Dequeue();
                }
                else
                {
                    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
                    return response;
                }
            }

            var ret = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
            _requestHistory.Enqueue(DateTime.UtcNow);
            return ret;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_semaphore)
        {
            if (_requestHistory.Count == _maxRequests)
            {
                var delta = DateTime.UtcNow - _requestHistory.Peek();
                if (delta > _timeWindow)
                {
                    _requestHistory.Dequeue();
                }
                else
                {
                    var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                    response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
                    return response;
                }
            }

            var ret = base.Send(request, cancellationToken);
            _requestHistory.Enqueue(DateTime.UtcNow);
            return ret;
        }
    }
}
