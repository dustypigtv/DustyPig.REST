using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.REST;

public class SlidingRateThrottle : DelegatingHandler
{
    private readonly SemaphoreSlim _semaphore;
    private readonly TimeSpan _timeWindow;

    /// <summary>
    /// Like the <see cref="SlidingRateLimiter"/>, but instead of returning a 429 response when the limit is reached, it waits until a slot is available.
    /// </summary>
    public SlidingRateThrottle(int maxRequests, TimeSpan timeWindow, HttpMessageHandler? innerHandler = null) : base(innerHandler ?? new HttpClientHandler())
    {
        
        if (maxRequests <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxRequests), "Must be greater than zero.");
    
        if (timeWindow <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeWindow), "Must be greater than zero.");
        
        _semaphore = new SemaphoreSlim(maxRequests, maxRequests);
        _timeWindow = timeWindow;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            new Thread(WaitAndRelease) { IsBackground = true }.Start();
        }
    }

    protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _semaphore.Wait(cancellationToken);
        try
        {
            return base.Send(request, cancellationToken);
        }
        finally
        {
            new Thread(WaitAndRelease) { IsBackground = true }.Start();
        }
    }

    private void WaitAndRelease()
    {
        Thread.Sleep(_timeWindow);
        _semaphore.Release();
    }
}
