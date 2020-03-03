using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class Response
    {
        public Request Request { get; set; }
        public HttpResponseMessage HttpResponseMessage { get; set; }
        public string Text { get; set; }
        public object Json { get; set; }
    }
}
