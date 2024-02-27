using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.REST;

public class Client : IDisposable
{
    private readonly HttpClient _httpClient = new();

    private int _retryCount = 1;
    private int _retryDelay = 250;
    private int _throttle = 0;
    private DateTime _nextCall = DateTime.MinValue;

    private static readonly JsonSerializerOptions _jsonSerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public Client() { }

    public Client(Uri baseAddress) => _httpClient.BaseAddress = baseAddress;

    public Client(string baseAddress) => _httpClient.BaseAddress = new Uri(baseAddress);

    public void Dispose()
    {
        _httpClient.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Accesses the underlying <see cref="System.Net.Http.HttpClient"/> for configurating options
    /// </summary>
    public HttpClient HttpClient => _httpClient;

    public bool AutoThrowIfError { get; set; }

    public bool IncludeRawContentInResponse { get; set; }



    /// <summary>
    /// When an error occurs, how many times to retry the api call.
    /// <br />
    /// Default = 1
    /// </summary>
    /// <remarks>
    /// <para>
    /// There are 2 events that can trigger a retry:
    /// </para>
    /// <para>
    /// 1. There is an error connecting to the server (such as a network layer error).
    /// </para>
    /// <para>
    /// 2. The connection succeeded, but the server sent HttpStatusCode.TooManyRequests (429). 
    ///    In this case, the client will attempt to get the RetryAfter header, and if found, 
    ///    the delay will be the maximum of the header and the <see cref="RetryDelay"/>. 
    ///    Otherwise, the retry delay will just be <see cref="RetryDelay"/>.
    /// </para>
    /// </remarks>
    public int RetryCount 
    {
        get => _retryCount;
        set
        {
            ThrowIfNegative(value);
            _retryCount = value;
        }
    }
    

    /// <summary>
    /// Number of milliseconds between retries.
    /// <br />
    /// Default = 250
    /// </summary>
    public int RetryDelay
    {
        get => _retryDelay;
        set
        {
            
            ThrowIfNegative(value);
            _retryDelay = value;
        }
    }
    

    /// <summary>
    /// Minimum number of milliseconds between api calls.
    /// <br />
    /// Default = 0
    /// </summary>
    public int Throttle
    {
        get => _throttle;
        set
        {
            ThrowIfNegative(value);
            _throttle = value;
        }
    }
    


    private static void ThrowIfNegative(int value)
    {
#if NET8_0_OR_GREATER
        ArgumentOutOfRangeException.ThrowIfNegative(value);
#else
        if(value < 0)
            throw new ArgumentOutOfRangeException(nameof(value), value, $"{nameof(value)} ('{value}') must be a non-negative value.");
#endif
    }


    private Task WaitForThrottle(CancellationToken cancellationToken)
    {
        if (_throttle > 0)
        {
            var wait = (_nextCall - DateTime.Now).Milliseconds;
            if (wait > 0)
                return Task.Delay(wait, cancellationToken);
        }
        return Task.CompletedTask;
    }

    private void SetNextCall()
    {
        if (_throttle > 0)
            _nextCall = DateTime.Now.AddMilliseconds(_throttle);
    }


    private static HttpRequestMessage CreateRequest(HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, object data)
    {
        var request = new HttpRequestMessage(method, url);
        AddHeadersAndContent(request, headers, data);
        return request;
    }

    private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, IReadOnlyDictionary<string, string> headers, object data)
    {
        var request = new HttpRequestMessage(method, uri);
        AddHeadersAndContent(request, headers, data);
        return request;
    }

    private static void AddHeadersAndContent(HttpRequestMessage request, IReadOnlyDictionary<string, string> headers, object data)
    {
        if (headers != null)
            foreach (var header in headers)
                request.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (data != null)
            request.Content = new StringContent(JsonSerializer.Serialize(data, _jsonSerializerOptions), Encoding.UTF8, "application/json");
    }

    private static HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        //Can't send the same request twice, so just build a copy for retries
        var ret = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Content = request.Content,
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        foreach (var header in request.Headers)
            ret.Headers.TryAddWithoutValidation(header.Key, header.Value);

        foreach (var option in request.Options)
            ret.Options.TryAdd(option.Key, option.Value);

        //Deprecated
        //foreach (var property in request.Properties)
        //    ret.Properties.Add(property.Key, property.Value);

        return ret;
    }

    


    public async Task<Response> GetResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        HttpStatusCode? statusCode = null;
        string reasonPhrase = null;
        string content = null;
        int previousTries = 0;
        var retryAfter = TimeSpan.Zero;
        while (true)
        {
            try
            {
                if(_throttle > 0)
                    await WaitForThrottle(cancellationToken).ConfigureAwait(false);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.Zero;

                if (IncludeRawContentInResponse)
                    content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                SetNextCall();

                return new Response
                {
                    Success = true,
                    StatusCode = response.StatusCode,
                    ReasonPhrase = response.ReasonPhrase,
                    RawContent = content
                };
            }
            catch (Exception ex)
            {
                SetNextCall();

                //If statusCode == null, there was a network error, retries are permitted
                //If statusCode == HttpStatusCode.TooManyRequests, retries are also permitted
                if (previousTries < RetryCount && (statusCode == null || statusCode == HttpStatusCode.TooManyRequests))
                {
                    try
                    {
                        int delay = Math.Max(0, Math.Max(RetryDelay, retryAfter.Milliseconds));
                        if (delay > 0)
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        return BuildErrorResponse(ex);
                    }
                    request = CloneRequest(request);
                    previousTries++;
                }
                else
                {
                    return BuildErrorResponse(ex);
                }
            }
        }

        Response BuildErrorResponse(Exception ex)
        {
            var ret = new Response
            {
                Error = ex,
                StatusCode = statusCode,
                ReasonPhrase = reasonPhrase,
                RawContent = content
            };

            if (AutoThrowIfError)
                ret.ThrowIfError();

            return ret;
        }
    }

    private async Task<Response> GetResponseAsync(HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, object data, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, url, headers, data);
        return await GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Response> GetResponseAsync(HttpMethod method, Uri uri, IReadOnlyDictionary<string, string> headers, object data, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, headers, data);
        return await GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
    }






    public async Task<Response<T>> GetResponseAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        string content = null;
        HttpStatusCode? statusCode = null;
        string reasonPhrase = null;
        int previousTries = 0;
        var retryAfter = TimeSpan.Zero;
        while (true)
        {
            try
            {
                if (_throttle > 0)
                    await WaitForThrottle(cancellationToken).ConfigureAwait(false);
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.Zero;

                if (response.IsSuccessStatusCode || IncludeRawContentInResponse)
                    content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                response.EnsureSuccessStatusCode();
                SetNextCall();

                return new Response<T>
                {
                    Success = true,
                    StatusCode = statusCode,
                    ReasonPhrase = reasonPhrase,
                    RawContent = IncludeRawContentInResponse ? content : null,
                    Data = JsonSerializer.Deserialize<T>(content, _jsonSerializerOptions)
                };
            }
            catch (Exception ex)
            {
                SetNextCall();

                //If statusCode == null, there was a network error, retries are permitted
                //If statusCode == HttpStatusCode.TooManyRequests, retries are also permitted
                if (previousTries < RetryCount && (statusCode == null || statusCode == HttpStatusCode.TooManyRequests))
                {
                    try
                    {
                        int delay = Math.Max(0, Math.Max(RetryDelay, retryAfter.Milliseconds));
                        if (delay > 0)
                            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        return BuildErrorResponse(ex);
                    }
                    request = CloneRequest(request);
                    previousTries++;
                }
                else
                {
                    return BuildErrorResponse(ex);
                }
            }
        }

        Response<T> BuildErrorResponse(Exception ex)
        {
            var ret = new Response<T>
            {
                Error = ex,
                StatusCode = statusCode,
                ReasonPhrase = reasonPhrase,
                RawContent = IncludeRawContentInResponse ? content : null
            };

            if (AutoThrowIfError)
                ret.ThrowIfError();

            return ret;
        }
    }

    private async Task<Response<T>> GetResponseAsync<T>(HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, object data, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, url, headers, data);
        return await GetResponseAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Response<T>> GetResponseAsync<T>(HttpMethod method, Uri uri, IReadOnlyDictionary<string, string> headers, object data, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, uri, headers, data);
        return await GetResponseAsync<T>(request, cancellationToken).ConfigureAwait(false);
    }






    public virtual Task<Response> GetAsync(string url, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Get, url, requestHeaders, null, cancellationToken);

    public virtual Task<Response> GetAsync(Uri uri, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Get, uri, requestHeaders, null, cancellationToken);


    public virtual Task<Response<T>> GetAsync<T>(string url, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Get, url, requestHeaders, null, cancellationToken);

    public virtual Task<Response<T>> GetAsync<T>(Uri uri, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
       GetResponseAsync<T>(HttpMethod.Get, uri, requestHeaders, null, cancellationToken);





    public virtual Task<Response> HeadAsync(string url, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Head, url, requestHeaders, null, cancellationToken);

    public virtual Task<Response> HeadAsync(Uri uri, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Head, uri, requestHeaders, null, cancellationToken);




    public virtual Task<Response> PostAsync(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

    public virtual Task<Response> PostAsync(Uri uri, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Post, uri, requestHeaders, data, cancellationToken);


    public virtual Task<Response<T>> PostAsync<T>(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

    public virtual Task<Response<T>> PostAsync<T>(Uri uri, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
       GetResponseAsync<T>(HttpMethod.Post, uri, requestHeaders, data, cancellationToken);





    /// <param name="data">While the HTTP DELETE specification does not allow a request body, there is at least 1 API I know of where a body is required! So this optional parameter is included</param>
    public virtual Task<Response> DeleteAsync(string url, IReadOnlyDictionary<string, string> requestHeaders = null, object data = null, CancellationToken cancellationToken = default) =>
       GetResponseAsync(HttpMethod.Delete, url, requestHeaders, data, cancellationToken);

    /// <param name="data">While the HTTP DELETE specification does not allow a request body, there is at least 1 API I know of where a body is required! So this optional parameter is included</param>
    public virtual Task<Response> DeleteAsync(Uri uri, IReadOnlyDictionary<string, string> requestHeaders = null, object data = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Delete, uri, requestHeaders, data, cancellationToken);


    /// <param name="data">While the HTTP DELETE specification does not allow a request body, there is at least 1 API I know of where a body is required! So this optional parameter is included</param>
    public virtual Task<Response<T>> DeleteAsync<T>(string url, IReadOnlyDictionary<string, string> requestHeaders = null, object data = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Delete, url, requestHeaders, data, cancellationToken);

    /// <param name="data">While the HTTP DELETE specification does not allow a request body, there is at least 1 API I know of where a body is required! So this optional parameter is included</param>
    public virtual Task<Response<T>> DeleteAsync<T>(Uri uri, IReadOnlyDictionary<string, string> requestHeaders = null, object data = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Delete, uri, requestHeaders, data, cancellationToken);












    public virtual Task<Response> PutAsync(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

    public virtual Task<Response> PutAsync(Uri uri, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Put, uri, requestHeaders, data, cancellationToken);


    public virtual Task<Response<T>> PutAsync<T>(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

    public virtual Task<Response<T>> PutAsync<T>(Uri uri, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Put, uri, requestHeaders, data, cancellationToken);




    public virtual Task<Response> PatchAsync(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

    public virtual Task<Response> PatchAsync(Uri uri, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
         GetResponseAsync(HttpMethod.Patch, uri, requestHeaders, data, cancellationToken);


    public virtual Task<Response<T>> PatchAsync<T>(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

    public virtual Task<Response<T>> PatchAsync<T>(Uri uri, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
        GetResponseAsync<T>(HttpMethod.Patch, uri, requestHeaders, data, cancellationToken);

}
