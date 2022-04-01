using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DustyPig.REST
{
    public class Client : IDisposable
    {
        private readonly HttpClient _httpClient = new HttpClient();

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    _httpClient.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposedValue = true;
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

        public System.Net.Http.Headers.HttpRequestHeaders DefaultRequestHeaders => _httpClient.DefaultRequestHeaders;


        #region GET

        public virtual async Task<Response> GetAsync(string url, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
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

        public virtual async Task<Response<T>> GetWithResponseDataAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
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

        #endregion



        #region POST

        public virtual async Task<Response> PostAsync(string url, object data, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                using var response = await _httpClient.PostAsync(url, new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
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

        public virtual async Task<Response<T>> PostWithResponseDataAsync<T>(string url, object data, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                using var response = await _httpClient.PostAsync(url, new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json"), cancellationToken).ConfigureAwait(false);
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

        #endregion


        #region DELETE

        public virtual async Task<Response> DeleteAsync(string url, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                using var response = await _httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
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

        public virtual async Task<Response> DeleteAsync(string url, object data, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, url)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json")
                };
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

        public virtual async Task<Response<T>> DeleteWithResponseDataAsync<T>(string url, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                using var response = await _httpClient.DeleteAsync(url, cancellationToken).ConfigureAwait(false);
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

        public virtual async Task<Response<T>> DeleteWithResponseDataAsync<T>(string url, object data, CancellationToken cancellationToken = default)
        {
            string content = null;
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Delete, url)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(data), Encoding.UTF8, "application/json")
                };
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

        #endregion

    }
}
