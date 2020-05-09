using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class Request
    {
        public Request()
        {
            Method = HttpMethod.Get;
        }

        public Uri BaseUri { get; set; }
        public string Path { get; set; }
        public string ApiType { get; set; }
        public HttpMethod Method { get; set; }
        public HttpRequestHeaders Headers { get; set; }

        public Dictionary<string, string> Params { get; set; }

        /// <summary>
        /// StringContent or FormUrlEncodedContent
        /// </summary>
        public ByteArrayContent Body { get; set; }
    }
}
