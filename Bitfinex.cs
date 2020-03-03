using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Caching.Memory;

namespace AgentBit.Ccxt
{
    public class Bitfinex : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker
    {
        MemoryCache _memoryCache = new MemoryCache(new MemoryCacheOptions());

        readonly Uri ApiPublicV1 = new Uri("https://api.bitfinex.com/v1/");
        readonly Uri ApiPublicV2 = new Uri("https://api-pub.bitfinex.com/v2/");

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

        public override async Task<Market[]> FetchMarkets()
        {
            return await _memoryCache.GetOrCreateAsync<Market[]>("Bitfinex.FetchMarkets", async entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);

                var idsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = "symbols",
                    ApiType = "public",
                    Method = HttpMethod.Get,
                    Timeout = TimeSpan.FromSeconds(30)
                });
                var ids = JsonSerializer.Deserialize<string[]>(idsResponse.Text);

                var detailsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = "symbols_details",
                    ApiType = "public",
                    Method = HttpMethod.Get,
                    Timeout = TimeSpan.FromSeconds(30)
                });
                var details = JsonSerializer.Deserialize<SymbolDetailsJson[]>(idsResponse.Text);

                var result = new List<Market>();

                foreach(var market in details.Where(m=>ids.Contains(m.pair)))
                {
                    var newItem = new Market();
                    newItem.Id = market.pair.ToUpper();
                    if (newItem.Id.IndexOf(':') > 0) 
                    {
                        var parts = newItem.Id.Split(new char[] { ':' });
                        newItem.BaseId = parts[0];
                        newItem.QuoteId = parts[1];
                    }
                    else
                    {
                        newItem.BaseId = newItem.Id.Substring(0, 3);
                        newItem.QuoteId = newItem.Id.Substring(3, 3);
                    }
                    newItem.Base = CommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = CommonCurrencyCode(newItem.QuoteId);
                    newItem.Symbol = newItem.Base + '/' + newItem.Quote;

                    newItem.PricePrecision = market.price_precision;
                    newItem.AmountMin = market.minimum_order_size;
                    newItem.AmountMax = market.maximum_order_size;
                    newItem.PriceMin = Math.Pow(10, -newItem.PricePrecision);
                    newItem.PriceMax = Math.Pow(10, newItem.PricePrecision);
                    newItem.CostMin = newItem.AmountMin * newItem.PriceMin;

                    result.Add(newItem);
                }

                return result.ToArray();
            });
        }

        public async Task<Ticker> FetchTicker(string symbol)
        {
            return (await FetchTickers(new string[] { symbol })).First().Value;
        }

        public async Task<Dictionary<string, Ticker>> FetchTickers(string[] symbols = null)
        {
            var markets = await FetchMarkets();

            var symbolsString = symbols == null ? "ALL" : String.Join(',', markets.Where(m => symbols.Contains(m.Symbol)).Select(m => m.Id));

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiPublicV1,
                Path = $"tickers?symbols={symbolsString}",
                Method = HttpMethod.Get,
                Timeout = TimeSpan.FromSeconds(30)
            });
            var tickers = JsonSerializer.Deserialize<TickerJson[]>(response.Text);

            var result = new Dictionary<string, Ticker>();
            foreach (var item in tickers)
            {
                //Ticker ticker = this.parseTicker(item);
                Ticker ticker = new Ticker();
                ticker.DateTime = DateTime.UtcNow;
                ticker.Timestamp = (uint)(ticker.DateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                var market = (await FetchMarkets()).FirstOrDefault(m => m.Id == item.SYMBOL);
                if (market == null)
                    continue;

                ticker.Symbol = market.Symbol;
                ticker.High = item.HIGH;
                ticker.Low = item.LOW;
                ticker.Bid = item.BID;
                ticker.BidVolume = item.BID_SIZE;
                ticker.Ask = item.ASK;
                ticker.AskVolume = item.ASK_SIZE;
                ticker.Last = item.LAST_PRICE;
                ticker.Close = ticker.Last;
                ticker.Average = (ticker.Ask + ticker.Bid) / 2;
                ticker.BaseVolume = item.VOLUME;
                ticker.Info = item;

                result[ticker.Symbol] = ticker;
            }
            return result;
        }

        public class SymbolDetailsJson
        {
            public string pair;
            public int price_precision;
            public double maximum_order_size;
            public double minimum_order_size;
        }

        public class TickerJson
        {
            public string SYMBOL;
            public double BID;
            public double BID_SIZE;
            public double ASK;
            public double ASK_SIZE;
            public double DAILY_CHANGE;
            public double DAILY_CHANGE_RELATIVE;
            public double LAST_PRICE;
            public double VOLUME;
            public double HIGH;
            public double LOW;
        }

    }
}
