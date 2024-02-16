using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.REST
{
    public class Client : IDisposable
    {
        private readonly HttpClient _httpClient = new HttpClient();

        private static readonly JsonSerializerOptions _jsonSerializerOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public Client() { }

        public Client(Uri baseAddress) => _httpClient.BaseAddress = baseAddress;

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
        /// When a network error occurs, how many times to retry the api call. Error results from api endopints are not retried. Default = 1
        /// </summary>
        public int RetryCount { get; set; } = 1;

        /// <summary>
        /// Number of milliseconds between retries. Default = 250
        /// </summary>
        public int RetryDelay { get; set; } = 250;



        private static HttpRequestMessage CreateRequest(HttpMethod method, string url, IDictionary<string, string> headers, object data)
        {
            var request = new HttpRequestMessage(method, url);
            AddHeadersAndContent(request, headers, data);
            return request;
        }

        private static HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, IDictionary<string, string> headers, object data)
        {
            var request = new HttpRequestMessage(method, uri);
            AddHeadersAndContent(request, headers, data);
            return request;
        }

        private static void AddHeadersAndContent(HttpRequestMessage request, IDictionary<string, string> headers, object data)
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
            while (true)
            {
                try
                {
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    statusCode = response.StatusCode;
                    reasonPhrase = response.ReasonPhrase;

                    if (IncludeRawContentInResponse)
                        content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    
                    response.EnsureSuccessStatusCode();
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
                    //If statusCode == null, there was a network error, retries are permitted
                    if (previousTries < RetryCount && statusCode == null)
                    {
                        try
                        {
                            if (RetryDelay > 0)
                                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
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
                var ret = string.IsNullOrWhiteSpace(reasonPhrase)
                    ? new Response { Error = ex }
                    : new Response { Error = new Exception(reasonPhrase, ex) };

                ret.StatusCode = statusCode;
                ret.ReasonPhrase = reasonPhrase;
                ret.RawContent = content;

                if (AutoThrowIfError)
                    ret.ThrowIfError();

                return ret;
            }
        }

        private async Task<Response> GetResponseAsync(HttpMethod method, string url, IDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(method, url, headers, data);
            return await GetResponseAsync(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Response> GetResponseAsync(HttpMethod method, Uri uri, IDictionary<string, string> headers, object data, CancellationToken cancellationToken)
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
            while (true)
            {
                try
                {
                    using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                    statusCode = response.StatusCode;
                    reasonPhrase = response.ReasonPhrase;

                    if (response.IsSuccessStatusCode || IncludeRawContentInResponse)
                        content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                    response.EnsureSuccessStatusCode();
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
                    //If statusCode == null, there was a network error, retries are permitted
                    if (previousTries < RetryCount && statusCode == null)
                    {
                        try
                        {
                            if (RetryDelay > 0)
                                await Task.Delay(RetryDelay, cancellationToken).ConfigureAwait(false);
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
                var ret = string.IsNullOrWhiteSpace(reasonPhrase)
                    ? new Response<T> { Error = ex }
                    : new Response<T> { Error = new Exception(reasonPhrase, ex) };

                ret.StatusCode = statusCode;
                ret.ReasonPhrase = reasonPhrase;
                if (IncludeRawContentInResponse)
                    ret.RawContent = content;

                if (AutoThrowIfError)
                    ret.ThrowIfError();

                return ret;
            }
        }


        private async Task<Response<T>> GetResponseAsync<T>(HttpMethod method, string url, IDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(method, url, headers, data);
            return await GetResponseAsync<T>(request, cancellationToken).ConfigureAwait(false);
        }

        private async Task<Response<T>> GetResponseAsync<T>(HttpMethod method, Uri uri, IDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            using var request = CreateRequest(method, uri, headers, data);
            return await GetResponseAsync<T>(request, cancellationToken).ConfigureAwait(false);
        }






        public virtual Task<Response> GetAsync(string url, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Get, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response> GetAsync(Uri uri, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Get, uri, requestHeaders, null, cancellationToken);


        public virtual Task<Response<T>> GetAsync<T>(string url, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Get, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response<T>> GetAsync<T>(Uri uri, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
           GetResponseAsync<T>(HttpMethod.Get, uri, requestHeaders, null, cancellationToken);





        public virtual Task<Response> HeadAsync(string url, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Head, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response> HeadAsync(Uri uri, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Head, uri, requestHeaders, null, cancellationToken);




        public virtual Task<Response> PostAsync(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response> PostAsync(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Post, uri, requestHeaders, data, cancellationToken);


        public virtual Task<Response<T>> PostAsync<T>(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response<T>> PostAsync<T>(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
           GetResponseAsync<T>(HttpMethod.Post, uri, requestHeaders, data, cancellationToken);





        public virtual Task<Response> DeleteAsync(string url, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Delete, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response> DeleteAsync(Uri uri, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Delete, uri, requestHeaders, null, cancellationToken);


        public virtual Task<Response<T>> DeleteAsync<T>(string url,IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Delete, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response<T>> DeleteAsync<T>(Uri uri, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Delete, uri, requestHeaders, null, cancellationToken);











        public virtual Task<Response> PutAsync(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response> PutAsync(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Put, uri, requestHeaders, data, cancellationToken);


        public virtual Task<Response<T>> PutAsync<T>(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response<T>> PutAsync<T>(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Put, uri, requestHeaders, data, cancellationToken);




        public virtual Task<Response> PatchAsync(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response> PatchAsync(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
             GetResponseAsync(HttpMethod.Patch, uri, requestHeaders, data, cancellationToken);


        public virtual Task<Response<T>> PatchAsync<T>(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response<T>> PatchAsync<T>(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Patch, uri, requestHeaders, data, cancellationToken);

    }
}
