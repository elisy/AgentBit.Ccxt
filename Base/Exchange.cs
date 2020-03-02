using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AgentBit.Ccxt.Base
{

    /// <summary>
    /// Base class for all exchanges
    /// </summary>
    public class Exchange: IDisposable, IFetchMarkets
    {
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
        }


        /// <summary>
        /// Request rate limiter
        /// </summary>
        public virtual async Task Throttle()
        {
            throw new NotImplementedException();
            //await mySemaphoreSlim.WaitAsync();
            //try
            //{
            //    await Stuff();
            //}
            //finally
            //{
            //    mySemaphoreSlim.Release();
            //}
        }

        public virtual async Task<HttpResponseMessage> Request(Request request)
        {
            await Throttle();

            Sign(request);

            HttpRequestMessage message = new HttpRequestMessage()
            {
                RequestUri = new Uri(request.BaseUri, request.Path),
                Method = request.Method
            };
            foreach (var header in request.Headers)
                message.Headers.Add(header.Key, header.Value);

            HttpResponseMessage response = await _httpClient.SendAsync(message);
            return await HandleRestResponse(response, request);
        }

        public virtual async Task<HttpResponseMessage> HandleRestResponse(HttpResponseMessage response, Request request)
        {
            var text = await response.Content.ReadAsStringAsync();
            return response;
        }


        public virtual void Sign(Request request)
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            if (_httpClient != null)
                _httpClient.Dispose();
        }

        public virtual async Task<Dictionary<string, Market>> FetchMarkets()
        {
            throw new NotImplementedException();
        }
    }
}
