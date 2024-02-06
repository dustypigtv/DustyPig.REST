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

        public void Dispose()
        {
            _httpClient.Dispose();
            GC.SuppressFinalize(this);
        }

        public Uri BaseAddress
        {
            get => _httpClient.BaseAddress;
            set => _httpClient.BaseAddress = value;
        }

        public TimeSpan Timeout
        {
            get => _httpClient.Timeout;
            set => _httpClient.Timeout = value;
        }

        public bool AutoThrowIfError { get; set; }

        public bool IncludeRawContentInResponse { get; set; }

        public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;





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





        public async Task<Response> GetResponseAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
        {
            HttpStatusCode? statusCode = null;
            string reasonPhrase = null;
            try
            {
                using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                response.EnsureSuccessStatusCode();
                return new Response
                {
                    Success = true,
                    StatusCode = response.StatusCode,
                    ReasonPhrase = response.ReasonPhrase
                };
            }
            catch (Exception ex)
            {
                var ret = string.IsNullOrWhiteSpace(reasonPhrase)
                    ? new Response { Error = ex }
                    : new Response { Error = new Exception(reasonPhrase, ex) };

                ret.StatusCode = statusCode;
                ret.ReasonPhrase = reasonPhrase;

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
