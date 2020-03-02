using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;

namespace AgentBit.Ccxt
{
    public class Bitfinex : Exchange, IPublicAPI, IPrivateAPI, IFetchMarkets, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiPublic = new Uri("https://api.bitfinex.com");

        public Bitfinex() : base()
        {

            CommonCurrencies = new Dictionary<string, string>() {
                { "ABS", "ABYSS" },
                { "AIO", "AION" },
                { "ALG", "ALGO" },
                { "AMP", "AMPL" },
                { "ATM", "ATMI" },
                { "ATO", "ATOM" },
                { "BAB", "BCH" },
                { "CTX", "CTXC" },
                { "DAD", "DADI" },
                { "DAT", "DATA" },
                { "DSH", "DASH" },
                { "DRK", "DRK" },
                { "GSD", "GUSD" },
                { "HOT", "Hydro Protocol" },
                { "IOS", "IOST" },
                { "IOT", "IOTA" },
                { "IQX", "IQ" },
                { "MIT", "MITH" },
                { "MNA", "MANA" },
                { "NCA", "NCASH" },
                { "ORS", "ORS Group" },
                { "POY", "POLY" },
                { "QSH", "QASH" },
                { "QTM", "QTUM" },
                { "SEE", "SEER" },
                { "SNG", "SNGLS" },
                { "SPK", "SPANK" },
                { "STJ", "STORJ" },
                { "TSD", "TUSD" },
                { "YYW", "YOYOW" },
                { "UDC", "USDC" },
                { "UST", "USDT" },
                { "UTN", "UTNP" },
                { "VSY", "VSYS" },
                { "WAX", "WAXP" },
                { "XCH", "XCHF" },
                { "ZBT", "ZB" }            
            };
        }

        public async Task<Ticker> FetchTicker(string symbol)
        {
            throw new NotImplementedException();
        }

        public async Task<Dictionary<string, Ticker>> FetchTickers(string[] symbols = null)
        {
            var markets = await FetchMarkets();
            var response = await Request(new Base.Request()
            {
                BaseUri = ApiPublic,
                Path = "tickers",
                Method = HttpMethod.Get,
                Timeout = TimeSpan.FromSeconds(30)
            });
            throw new NotImplementedException();
        }
    }
}
