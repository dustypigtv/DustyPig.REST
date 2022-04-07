using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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

        private bool _disposed = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _httpClient.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                _disposed = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
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


        public HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;


        
        
        private HttpRequestMessage CreateRequest(HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, object data)
        {
            var request = new HttpRequestMessage(method, url);
            if (headers != null)
                foreach (var header in headers)
                   request.Headers.Add(header.Key, header.Value);

            if (data != null)
                request.Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json");
           
            return request;
        }

        private async Task<Response> GetResponseAsync(HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            string content = null;
            try
            {
                using var request = CreateRequest(method, url, headers, data);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);                
                content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                return new Response { Success = true };
            }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(content))
                    return new Response { Error = ex };
                return new Response { Error = new Exception(content, ex) };
            }
        }

        private async Task<Response<T>> GetResponseAsync<T>(HttpMethod method, string url, IReadOnlyDictionary<string, string> headers, object data, CancellationToken cancellationToken)
        {
            string content = null;
            try
            {
                using var request = CreateRequest(method, url, headers, data);
                using var response = await _httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                var ret = JsonConvert.DeserializeObject<T>(content);
                return new Response<T> { Success = true, Data = ret };
            }
            catch (Exception ex)
            {
                if (string.IsNullOrWhiteSpace(content))
                    return new Response<T> { Error = ex };
                return new Response<T> { Error = new Exception(content, ex) };
            }
        }






        public virtual Task<Response> GetAsync(string url, object data = null, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Get, url, requestHeaders, null, cancellationToken);


        public virtual Task<Response<T>> GetAsync<T>(string url, object data = null, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Get, url, requestHeaders, null, cancellationToken);





        public virtual Task<Response> HeadAsync(string url, object data = null, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Head, url, requestHeaders, null, cancellationToken);


        public virtual Task<Response<T>> HeadAsync<T>(string url, object data = null, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Head, url, requestHeaders, null, cancellationToken);





        public virtual Task<Response> PostAsync(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

        
        public virtual Task<Response<T>> PostAsync<T>(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Post, url, requestHeaders, data, cancellationToken);

        



        public virtual Task<Response> DeleteAsync(string url, object data = null, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Delete, url, requestHeaders, data, cancellationToken);

        
        public virtual Task<Response<T>> DeleteAsync<T>(string url, object data = null, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Delete, url, requestHeaders, data, cancellationToken);

        



        public virtual Task<Response> PutAsync(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

        
        public virtual Task<Response<T>> PutAsync<T>(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Put, url, requestHeaders, data, cancellationToken);

        



#if NET6_0_OR_GREATER

        public virtual Task<Response> PatchAsync(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

       
        public virtual Task<Response<T>> PatchAsync<T>(string url, object data, IReadOnlyDictionary<string, string> requestHeaders = null, CancellationToken cancellationToken = default) =>
            GetResponseAsync<T>(HttpMethod.Patch, url, requestHeaders, data, cancellationToken);

#endif
    }
}
