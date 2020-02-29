using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;

namespace AgentBit.Ccxt
{
    public class Bitfinex : Exchange, IPublicAPI, IPrivateAPI, IFetchMarkets, IFetchTickers, IFetchTicker
    {
        public Bitfinex() : base()
        {
        }

        public async Task<TickerInfo> FetchTicker(string symbol, Dictionary<string, string> @params = null)
        {
            throw new NotImplementedException();
        }

        public async Task<Dictionary<string, TickerInfo>> FetchTickers(string[] symbols = null, Dictionary<string, string> @params = null)
        {
            throw new NotImplementedException();
        }
    }
}
