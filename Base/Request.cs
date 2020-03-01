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

        public string BaseUrl { get; set; }
        public string Path { get; set; }
        public string ApiType { get; set; }
        public HttpMethod Method { get; set; }
        public HttpRequestHeaders Headers { get; set; }

        public TimeSpan Timeout { get; set; }
        public object Body { get; set; }
    }
}
