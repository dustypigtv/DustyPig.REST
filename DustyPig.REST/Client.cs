using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.REST
{
    public class Client : IDisposable
    {
        private readonly HttpClient _httpClient = new HttpClient();

        public void Dispose() => _httpClient.Dispose();

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



        private HttpRequestMessage CreateRequest(HttpMethod method, string url, IDictionary<string, string> headers, object data)
        {
            var request = new HttpRequestMessage(method, url);
            if (headers != null)
                foreach (var header in headers)
                    //request.Headers.Add(header.Key, header.Value);
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (data != null)
                request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

            return request;
        }

        private HttpRequestMessage CreateRequest(HttpMethod method, Uri uri, IDictionary<string, string> headers, object data)
        {
            var request = new HttpRequestMessage(method, uri);
            if (headers != null)
                foreach (var header in headers)
                    //request.Headers.Add(header.Key, header.Value);
                    request.Headers.TryAddWithoutValidation(header.Key, header.Value);

            if (data != null)
                request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");

            return request;
        }



        private async Task<Response> GetResponseAsync(HttpMethod method, string url, IDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            string content = null;
            HttpStatusCode statusCode = HttpStatusCode.BadRequest;
            string reasonPhrase = null;
            try
            {
                using var request = CreateRequest(method, url, headers, data);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return new Response 
                {
                    Success = true, 
                    StatusCode = response.StatusCode, 
                    ReasonPhrase = response.ReasonPhrase,
                    RawContent = IncludeRawContentInResponse ? content : null
                };
            }
            catch (Exception ex)
            {
                var ret = string.IsNullOrWhiteSpace(reasonPhrase) 
                    ? new Response { Error = ex } 
                    : new Response { Error = new Exception(reasonPhrase, ex) };

                ret.StatusCode = statusCode;
                ret.ReasonPhrase = reasonPhrase;
                if(IncludeRawContentInResponse)
                    ret.RawContent = content;

                if (AutoThrowIfError)
                    ret.ThrowIfError();

                return ret;
            }
        }

        private async Task<Response> GetResponseAsync(HttpMethod method, Uri uri, IDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            string content = null;
            HttpStatusCode statusCode = HttpStatusCode.BadRequest;
            string reasonPhrase = null;
            try
            {
                using var request = CreateRequest(method, uri, headers, data);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return new Response
                {
                    Success = true,
                    StatusCode = response.StatusCode,
                    ReasonPhrase = response.ReasonPhrase,
                    RawContent = IncludeRawContentInResponse ? content : null
                };
            }
            catch (Exception ex)
            {
                var ret = string.IsNullOrWhiteSpace(reasonPhrase)
                    ? new Response { Error = ex }
                    : new Response { Error = new Exception(reasonPhrase, ex) };

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
            string content = null;
            HttpStatusCode statusCode = HttpStatusCode.BadRequest;
            string reasonPhrase = null;
            try
            {
                using var request = CreateRequest(method, url, headers, data);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);             
                response.EnsureSuccessStatusCode();
                var ret = JsonConvert.DeserializeObject<T>(content);
                return new Response<T>
                {
                    Success = true, 
                    Data = ret, 
                    StatusCode = statusCode, 
                    ReasonPhrase = reasonPhrase, 
                    RawContent = IncludeRawContentInResponse ? content : null
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

        private async Task<Response<T>> GetResponseAsync<T>(HttpMethod method, Uri uri, IDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            string content = null;
            HttpStatusCode statusCode = HttpStatusCode.BadRequest;
            string reasonPhrase = null;
            try
            {
                using var request = CreateRequest(method, uri, headers, data);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                statusCode = response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var ret = JsonConvert.DeserializeObject<T>(content);
                return new Response<T>
                {
                    Success = true,
                    Data = ret,
                    StatusCode = statusCode,
                    ReasonPhrase = reasonPhrase,
                    RawContent = IncludeRawContentInResponse ? content : null
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




        public virtual Task<Response> GetAsync(string url, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Get, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response> GetAsync(Uri uri, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Get, uri, requestHeaders, null, cancellationToken);


        public virtual Task<Response<T>> GetAsync<T>(string url, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Get, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response<T>> GetAsync<T>(Uri uri, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
           GetResponseAsync<T>(HttpMethod.Get, uri, requestHeaders, null, cancellationToken);





        public virtual Task<Response> HeadAsync(string url, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Head, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response> HeadAsync(Uri uri, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Head, uri, requestHeaders, null, cancellationToken);


        public virtual Task<Response<T>> HeadAsync<T>(string url, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Head, url, requestHeaders, null, cancellationToken);

        public virtual Task<Response<T>> HeadAsync<T>(Uri uri, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Head, uri, requestHeaders, null, cancellationToken);





        public virtual Task<Response> PostAsync(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response> PostAsync(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Post, uri, requestHeaders, data, cancellationToken);


        public virtual Task<Response<T>> PostAsync<T>(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response<T>> PostAsync<T>(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
           GetResponseAsync<T>(HttpMethod.Post, uri, requestHeaders, data, cancellationToken);





        public virtual Task<Response> DeleteAsync(string url, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Delete, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response> DeleteAsync(Uri uri, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Delete, uri, requestHeaders, data, cancellationToken);


        public virtual Task<Response<T>> DeleteAsync<T>(string url, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Delete, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response<T>> DeleteAsync<T>(Uri uri, object data = null, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Delete, uri, requestHeaders, data, cancellationToken);





        public virtual Task<Response> PutAsync(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response> PutAsync(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Put, uri, requestHeaders, data, cancellationToken);


        public virtual Task<Response<T>> PutAsync<T>(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response<T>> PutAsync<T>(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Put, uri, requestHeaders, data, cancellationToken);

        

#if NET6_0_OR_GREATER

        public virtual Task<Response> PatchAsync(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

       public virtual Task<Response> PatchAsync(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Patch, uri, requestHeaders, data, cancellationToken);

       
        public virtual Task<Response<T>> PatchAsync<T>(string url, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

        public virtual Task<Response<T>> PatchAsync<T>(Uri uri, object data, IDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Patch, uri, requestHeaders, data, cancellationToken);

#endif
    }
}
