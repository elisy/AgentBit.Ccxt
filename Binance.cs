using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Caching.Memory;

namespace AgentBit.Ccxt
{
    public class Binance : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiV3 = new Uri("https://api.binance.com/api/v3/");

        public Binance() : base()
        {
            RateLimit = 500;

            CommonCurrencies = new Dictionary<string, string>() {
                { "YOYO", "YOYOW" }           
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            return await Exchange.MemoryCache.GetOrCreateAsync<Market[]>("Bitfinex.FetchMarkets", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV3,
                    Path = "exchangeInfo",
                    ApiType = "public",
                    Method = HttpMethod.Get,
                    Timeout = TimeSpan.FromSeconds(30)
                });

                var result = new List<Market>();

                //TODO

                return result.ToArray();
            });
        }

        public override void Sign(Request request)
        {
            if (request.ApiType != "private")
                return;
        }

        public async Task<Ticker> FetchTicker(string symbol)
        {
            return (await FetchTickers(new string[] { symbol })).FirstOrDefault();
        }

        public async Task<Ticker[]> FetchTickers(string[] symbols = null)
        {
            var markets = await FetchMarkets();

            var symbolsString = symbols == null ? "ALL" : String.Join(',', markets.Where(m => symbols.Contains(m.Symbol)).Select(m => "t" + m.Id));

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiV3,
                Path = $"publicGetTicker24hr",
                Method = HttpMethod.Get,
                Timeout = TimeSpan.FromSeconds(30)
            });

            var result = new List<Ticker>();
            //TODO
            if (symbols == null)
                return result.ToArray();
            else
                return result.Where(m => symbols.Contains(m.Symbol)).ToArray();
        }
    }
}
