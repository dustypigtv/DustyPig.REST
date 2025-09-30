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

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_locker)
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
                    return Task.FromResult(response);
                }
            }

            try
            {
                return base.SendAsync(request, cancellationToken);
            }
            finally
            {
                _requestHistory.Enqueue(DateTime.UtcNow);
            }
        }
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        lock (_locker)
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

            try
            {
                return base.Send(request, cancellationToken);
            }
            finally
            {
                _requestHistory.Enqueue(DateTime.UtcNow);
            }
        }
    }
}
