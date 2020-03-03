using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace AgentBit.Ccxt.Base
{

    /// <summary>
    /// Base class for all exchanges
    /// </summary>
    public class Exchange : IDisposable, IFetchMarkets
    {
        //thread safe
        public static MemoryCache MemoryCache = new MemoryCache(new MemoryCacheOptions());

        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }

        public TimeSpan Timeout { get; set; }

        public SocketsHttpHandler SocketsHttpHandler { get; set; }

        protected HttpClient _httpClient = null;
        public HttpClient HttpClient
        {
            get
            {
                if (_httpClient == null)
                {
                    _httpClient = new HttpClient(SocketsHttpHandler);
                    _httpClient.Timeout = Timeout;

                    _httpClient.DefaultRequestHeaders.Connection.Clear();
                    _httpClient.DefaultRequestHeaders.ConnectionClose = false;
                    _httpClient.DefaultRequestHeaders.Connection.Add("Keep-Alive");
                }

                return _httpClient;
            }
        }

        public Dictionary<string, string> CommonCurrencies { get; set; }

        public Exchange()
        {
            SocketsHttpHandler = new SocketsHttpHandler();
            //SocketsHttpHandler.MaxConnectionsPerServer = 5;
            SocketsHttpHandler.UseCookies = false;
            SocketsHttpHandler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;

            Timeout = TimeSpan.FromSeconds(5);

            RateLimit = 2 * 1000;
        }

        /// <summary>
        /// Request rate limit in milliseconds = seconds * 1000
        /// </summary>
        public int RateLimit { get; set; }
        protected DateTime _lastRequestTime = DateTime.Now;
        protected SemaphoreSlim _throttleSemaphore = new SemaphoreSlim(1, 1);
        /// <summary>
        /// Request rate limiter
        /// </summary>
        public virtual async Task Throttle()
        {
            var delay = (int)(RateLimit - (DateTime.Now - _lastRequestTime).TotalMilliseconds);
            if (delay > 0)
            {
                await _throttleSemaphore.WaitAsync();
                try
                {
                    await Task.Delay(delay);
                }
                finally
                {
                    _throttleSemaphore.Release();
                }
            }
        }

        public virtual async Task<Response> Request(Request request)
        {
            await Throttle();

            Sign(request);
            SetBody(request);

            HttpRequestMessage message = new HttpRequestMessage()
            {
                RequestUri = new Uri(request.BaseUri, request.Path),
                Method = request.Method,
                Content = request.Body
            };
            if (request.Headers != null)
            {
                foreach (var header in request.Headers)
                    message.Headers.Add(header.Key, header.Value);
            }

            HttpResponseMessage response = await HttpClient.SendAsync(message);
            return await HandleRestResponse(response, request);
        }

        public virtual async Task<Response> HandleRestResponse(HttpResponseMessage response, Request request)
        {
            Response result = new Response()
            {
                Request = request,
                HttpResponseMessage = response,
                Text = await response.Content.ReadAsStringAsync()
            };
            return result;
        }


        public virtual void Sign(Request request)
        {
            throw new NotImplementedException("Sign method should be overriden");
        }


        /// <summary>
        /// Fills request.Body. Default is Json
        /// </summary>
        /// <param name="request"></param>
        public virtual void SetBody(Request request)
        {
            if (request.Params != null && request.Params.Count != 0)
            {
                var json = JsonSerializer.Serialize<Dictionary<string, string>>(request.Params);
                request.Body = new StringContent(json, Encoding.UTF8, "application/json");
            }
        }

        public void Dispose()
        {
            if (_httpClient != null)
                _httpClient.Dispose();
        }


        public virtual async Task<Market[]> FetchMarkets()
        {
            throw new NotImplementedException();
        }

        protected string CommonCurrencyCode(string code)
        {
            if (CommonCurrencies.ContainsKey(code))
                return CommonCurrencies[code];
            else
                return code;
        }
    }
}
