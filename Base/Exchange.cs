using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace AgentBit.Ccxt.Base
{

    /// <summary>
    /// Base class for all exchanges
    /// </summary>
    public class Exchange: IDisposable
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
                }

                return _httpClient;
            }
        }

        public Dictionary<string, string> CommonCurrencies { get; set; }

        public Exchange()
        {
            SocketsHttpHandler = new SocketsHttpHandler();
            SocketsHttpHandler.MaxConnectionsPerServer = 5;
            SocketsHttpHandler.UseCookies = false;

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

        public virtual async Task Request(Request request)
        {
            await Throttle();

            Sign(request);


            throw new NotImplementedException();
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
    }
}
