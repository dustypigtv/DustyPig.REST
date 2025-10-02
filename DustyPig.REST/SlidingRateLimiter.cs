using System;
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
    private readonly SemaphoreSlim[] _semaphores;
    private readonly DateTime[] _availableTimes;
    private readonly TimeSpan _timeWindow;

    public SlidingRateLimiter(int maxRequests, TimeSpan timeWindow, HttpMessageHandler? innerHandler = null) : base(innerHandler ?? new HttpClientHandler())
    {

        if (maxRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "Must be greater than zero.");

        if (timeWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeWindow), "Must be greater than zero.");

        _timeWindow = timeWindow;
        _semaphores = new SemaphoreSlim[maxRequests];
        _availableTimes = new DateTime[maxRequests];
        for(int i = 0; i < maxRequests; i++)
        {
            _semaphores[i] = new SemaphoreSlim(1, 1);
            _availableTimes[i] = DateTime.UtcNow.Add(-timeWindow).AddSeconds(-1);
        }
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.UtcNow;
        DateTime firstAvailable = DateTime.MaxValue;

        for (int i = 0; i < _semaphores.Length; i++)
        {
            if(await _semaphores[i].WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                try
                {
                    return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    new Thread(new ParameterizedThreadStart(WaitAndRelease)) { IsBackground = true }.Start(i);
                }
            }
            else
            {
                if (_availableTimes[i] < firstAvailable)
                    firstAvailable = _availableTimes[i];
            }
        }

        var delta = _timeWindow - (utcNow - firstAvailable);
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
        return response;
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        DateTime utcNow = DateTime.UtcNow;
        DateTime firstAvailable = DateTime.MaxValue;

        for (int i = 0; i < _semaphores.Length; i++)
        {
            if (_semaphores[i].Wait(0, cancellationToken))
            {
                try
                {
                    return base.Send(request, cancellationToken);
                }
                finally
                {
                    new Thread(new ParameterizedThreadStart(WaitAndRelease)) { IsBackground = true }.Start(i);
                }
            }
            else
            {
                if (_availableTimes[i] < firstAvailable)
                    firstAvailable = _availableTimes[i];
            }
        }

        var delta = _timeWindow - (utcNow - firstAvailable);
        var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(delta);
        return response;
    }


    private void WaitAndRelease(object? state)
    {
        if (state is int i)
        {
            Thread.Sleep(_timeWindow);
            _semaphores[i].Release();
        }
    }
}
