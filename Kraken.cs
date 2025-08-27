using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    public class Kraken : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiPublicV1 = new Uri("https://api.kraken.com/0/public/");

        //https://support.kraken.com/hc/en-us/articles/360022635612-Request-Limits-REST-API-
        //Private average cost is 20
        public const int PrivateRateLimit = (int)500 / 20 / 10 * 1000;
        //https://support.kraken.com/hc/en-us/articles/206548367-What-are-the-REST-API-rate-limits-
        //Calling the public endpoints at a frequency of 1 per second (or less) would remain within the rate limits, but exceeding this frequency could cause the calls to be rate limited.
        public const int PublicRateLimit = 1 * 1000;

        public Kraken(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            RateLimit = PublicRateLimit; //See Throttle overriden method

            CommonCurrencies = new Dictionary<string, string>() {
                { "LUNA", "LUNC" },
                { "UST", "USTC" },
                { "XBT", "BTC" },
                { "XDG", "DOGE" }
            };
        }

        public override async Task Throttle()
        {
            var delay = RateLimit;
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

        public override async Task<Response> Request(Request request)
        {
            if (request.ApiType == "public")
                RateLimit = PublicRateLimit;
            else
                RateLimit = PrivateRateLimit;

            return await base.Request(request).ConfigureAwait(false);
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = "AssetPairs",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var result = new List<Market>();

                using (var document = JsonDocument.Parse(response.Text))
                {
                    var resultProperty = document.RootElement.GetProperty("result");
                    var pairs = JsonSerializer.Deserialize<Dictionary<string, KrakenAssetPair>>(resultProperty.GetRawText());

                    foreach (var market in pairs)
                    {
                        var darkpool = market.Key.EndsWith(".d");
                        if (darkpool)
                            continue;

                        var newItem = new Market();

                        newItem.Id = market.Key;
                        newItem.BaseId = market.Value.@base;
                        newItem.QuoteId = market.Value.quote;

                        if (String.IsNullOrEmpty(market.Value.wsname))
                        {
                            newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                            newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);
                        }
                        else
                        {
                            var wsname = market.Value.wsname.Split('/');
                            newItem.Base = GetCommonCurrencyCode(wsname[0]);
                            newItem.Quote = GetCommonCurrencyCode(wsname[1]);
                        }

                        newItem.AmountPrecision = market.Value.lot_decimals;
                        newItem.PricePrecision = market.Value.pair_decimals;

                        newItem.AmountMin = (decimal)Math.Pow(10, -newItem.AmountPrecision);

                        newItem.Info = market;

                        newItem.PricePrecision = 8;
                        newItem.AmountPrecision = 8;

                        newItem.FeeTaker = 0.26M / 100;
                        newItem.FeeMaker = 0.16M / 100;

                        newItem.Url = $"https://trade.kraken.com/ru-ru/charts/KRAKEN:{newItem.BaseId}-{newItem.QuoteId}";

                        result.Add(newItem);
                    }
                }

                _markets = result.ToArray();
            }
            return _markets;
        }

        public override void Sign(Request request)
        {
            if (request.ApiType != "private")
                return;
        }

        public async Task<Ticker> FetchTicker(string symbol)
        {
            return (await FetchTickers(new string[] { symbol }).ConfigureAwait(false)).FirstOrDefault();
        }

        public async Task<Ticker[]> FetchTickers(string[] symbols = null)
        {
            var allMarkets = await FetchMarkets().ConfigureAwait(false);

            var marketsChunks = allMarkets.Chunk(1000); //Error 520 from CoudFlare if too many markets in one request

            var result = new List<Ticker>();
            foreach (var markets in marketsChunks)
            {
                var argument = String.Join(',', markets.Where(market => symbols == null || symbols.Contains(market.Symbol)).Select(market => market.Id));

                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = $"Ticker?pair={argument}",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                using (var document = JsonDocument.Parse(response.Text))
                {
                    var resultProperty = document.RootElement.GetProperty("result");
                    var tickers = JsonSerializer.Deserialize<Dictionary<string, KrakenTicker>>(resultProperty.GetRawText());

                    foreach (var item in tickers)
                    {
                        var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.Key);
                        if (market == null)
                            continue;

                        Ticker ticker = new Ticker();

                        ticker.DateTime = DateTime.UtcNow;
                        ticker.Timestamp = (uint)(ticker.DateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                        ticker.Symbol = market.Symbol;
                        ticker.High = JsonSerializer.Deserialize<decimal>(item.Value.h[1]);
                        ticker.Low = JsonSerializer.Deserialize<decimal>(item.Value.l[1]);
                        ticker.Bid = JsonSerializer.Deserialize<decimal>(item.Value.b[0]);
                        ticker.Ask = JsonSerializer.Deserialize<decimal>(item.Value.a[0]);
                        ticker.Vwap = JsonSerializer.Deserialize<decimal>(item.Value.p[1]);
                        ticker.Open = JsonSerializer.Deserialize<decimal>(item.Value.o);
                        ticker.Last = JsonSerializer.Deserialize<decimal>(item.Value.c[0]);
                        ticker.Close = ticker.Last;
                        ticker.BaseVolume = JsonSerializer.Deserialize<decimal>(item.Value.v[1]);
                        ticker.QuoteVolume = ticker.BaseVolume * ticker.Vwap;
                        ticker.Info = item;

                        result.Add(ticker);
                    }
                }
            }

            if (symbols == null)
                    return result.ToArray();
                else
                    return result.Where(m => symbols.Contains(m.Symbol)).ToArray();
        }



        public class KrakenAssetPair
        {
            //{"altname":"ADAETH","wsname":"ADA\/ETH","aclass_base":"currency","base":"ADA","aclass_quote":"currency","quote":"XETH","lot":"unit","pair_decimals":7,"lot_decimals":8,"lot_multiplier":1,
            //"leverage_buy":[],"leverage_sell":[],
            //"fees":[[0,0.26],[50000,0.24],[100000,0.22],[250000,0.2],[500000,0.18],[1000000,0.16],[2500000,0.14],[5000000,0.12],[10000000,0.1]],
            //"fees_maker":[[0,0.16],[50000,0.14],[100000,0.12],[250000,0.1],[500000,0.08],[1000000,0.06],[2500000,0.04],[5000000,0.02],[10000000,0]],
            //"fee_volume_currency":"ZUSD","margin_call":80,"margin_stop":40}
            public string wsname { get; set; }
            public string altname { get; set; }
            public string @base { get; set; }
            public string quote { get; set; }
            public int pair_decimals { get; set; }
            public int lot_decimals { get; set; }
        }

        public class KrakenTicker
        {
            //{"a":["0.000212400","9893","9893.000"],"b":["0.000211800","20538","20538.000"],"c":["0.000211700","7.19717713"],"v":["55102.28982945","1917962.23950851"],
            //"p":["0.000212359","0.000209285"],"t":[32,487],"l":["0.000209800","0.000201800"],"h":["0.000215300","0.000220000"],"o":"0.000212900"}
            public string[] a { get; set; }
            public string[] b { get; set; }
            public string[] c { get; set; }
            public string[] v { get; set; }
            public string[] p { get; set; }
            public int[] t { get; set; }
            public string[] l { get; set; }
            public string[] h { get; set; }
            public string o { get; set; }
        }

    }
}
