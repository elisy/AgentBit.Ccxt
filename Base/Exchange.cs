using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
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
        public string ApiKey { get; set; }
        public string ApiSecret { get; set; }

        public Dictionary<string, string> CommonCurrencies { get; set; }

        protected HttpClient _httpClient;
        protected IMemoryCache _memoryCache;
        protected ILogger _logger;

        public Exchange(HttpClient httpClient, IMemoryCache memoryCache, ILogger logger)
        {
            _httpClient = httpClient;
            _memoryCache = memoryCache;
            _logger = logger;

            RateLimit = 2 * 1000;

            CommonCurrencies = new Dictionary<string, string>();
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
                await _throttleSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                finally
                {
                    _throttleSemaphore.Release();
                }
            }
        }

        public virtual async Task<Response> Request(Request request)
        {
            await Throttle().ConfigureAwait(false);

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

            try
            {
                _lastRequestTime = DateTime.Now;
                HttpResponseMessage response = await _httpClient.SendAsync(message).ConfigureAwait(false);
                return await HandleResponse(response, request).ConfigureAwait(false);
            }
            catch (WebException e)
            {
                throw e;
            }
            
        }

        public virtual async Task<Response> HandleResponse(HttpResponseMessage response, Request request)
        {
            Response result = new Response()
            {
                Request = request,
                HttpResponseMessage = response,
                Text = await response.Content.ReadAsStringAsync().ConfigureAwait(false)
            };
            HandleError(result);
            return result;
        }

        public virtual void HandleError(Response response)
        {
            if (response.HttpResponseMessage.IsSuccessStatusCode)
                return;
            throw new ExchangeError();
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
        }


        public virtual async Task<Market[]> FetchMarkets()
        {
            throw new NotImplementedException();
        }

        protected string GetCommonCurrencyCode(string code)
        {
            if (CommonCurrencies.ContainsKey(code))
                return CommonCurrencies[code];
            else
                return code;
        }
    }
}
