using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AgentBit.Ccxt
{
    public class Huobi : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiV1Public = new Uri("https://api.huobi.com/");

        public Huobi(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://huobiglobal.zendesk.com/hc/en-us/articles/900001168066
            //Average 20 per 2s
            RateLimit = 2 * 1000 / 20;

            CommonCurrencies = new Dictionary<string, string>() {
                { "BIFI", "Bitcoin File" }, // conflict with Beefy.Finance https://github.com/ccxt/ccxt/issues/8706
                { "BULL", "Bullieverse" }, //Conflict with 3X Long Bitcoin Token
                { "GET", "Themis" }, // conflict with GET (Guaranteed Entrance Token, GET Protocol)
                { "GTC", "Game.com" }, // conflict with Gitcoin and Gastrocoin
                { "HIT", "HitChain" },
                { "HOT", "Hydro Protocol" }, // conflict with HOT (Holo) https://github.com/ccxt/ccxt/issues/4929
                { "NANO", "XNO" },
                { "PNT", "Penta" },
                { "QUICK", "quickswap-new" }, //Conflict with 
                { "SBTC", "Super Bitcoin" },
                { "SOUL", "Soulsaver" }, //Conflict with SOUL Phantasma
                { "STC", "satoshi-island" },
                { "XNO", "Xeno NFT Hub" } //Conflict with Nano XNO
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV1Public,
                    Path = "v1/common/symbols",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<HuobiCommonSymbols>(response.Text);
                var markets = responseJson.data;

                var result = new List<Market>();

                foreach (var market in markets)
                {
                    var newItem = new Market();
                    newItem.Id = market["symbol"].GetString();
                    newItem.BaseId = market["base-currency"].GetString();
                    newItem.QuoteId = market["quote-currency"].GetString();

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.PricePrecision = market["amount-precision"].GetInt32();
                    newItem.AmountPrecision = market["price-precision"].GetInt32();

                    newItem.PriceMin = (decimal)Math.Pow(10, -newItem.AmountPrecision);

                    newItem.AmountMin = market["min-order-amt"].GetDecimal();
                    newItem.AmountMax = market["max-order-amt"].GetDecimal();

                    newItem.CostMin = market["min-order-value"].GetDecimal();

                    newItem.Active = (market["state"].GetString() == "online");

                    newItem.FeeMaker = 0.2M / 100;
                    newItem.FeeTaker = 0.2M / 100;

                    newItem.Url = $"https://www.huobi.com/en-us/exchange/{@newItem.BaseId}_{@newItem.QuoteId}?invite_code=5p8x5";
                    newItem.Margin = market.ContainsKey("leverage-ratio");

                    newItem.Info = market;
                    result.Add(newItem);
                }

                _markets = result.ToArray();
            }
            return _markets;
        }


        public async Task<Ticker> FetchTicker(string symbol)
        {
            return (await FetchTickers(new string[] { symbol }).ConfigureAwait(false)).FirstOrDefault();
        }

        public async Task<Ticker[]> FetchTickers(string[] symbols = null)
        {
            var markets = await FetchMarkets().ConfigureAwait(false);

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiV1Public,
                Path = "market/tickers",
                ApiType = "public",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var responseJson = JsonSerializer.Deserialize<HuobiTickersResponse>(response.Text);

            var result = new List<Ticker>();
            foreach (var item in responseJson.data)
            {
                Ticker ticker = new Ticker();

                ticker.Timestamp = responseJson.ts;
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ticker.Timestamp);

                var market = markets.FirstOrDefault(m => m.Id == item.symbol);
                if (market == null)
                    continue;

                ticker.Symbol = market.Symbol;

                ticker.High = item.high;
                ticker.Low = item.low;

                ticker.Bid = item.bid;
                ticker.BidVolume = item.bidSize;
                ticker.Ask = item.ask;
                ticker.AskVolume = item.askSize;

                ticker.Open = item.open;
                ticker.Close = item.close;
                ticker.Last = item.close;
                ticker.Average = (ticker.Open + ticker.Close) / 2;

                ticker.BaseVolume = item.amount;
                ticker.QuoteVolume = item.vol;

                if (ticker.BaseVolume != 0)
                    ticker.Vwap = ticker.QuoteVolume / ticker.BaseVolume;
                ticker.Change = ticker.Close - ticker.Open;
                if (ticker.Open != 0)
                    ticker.Percentage = ticker.Change / ticker.Open * 100;

                ticker.Info = item;

                result.Add(ticker);
            }

            if (symbols == null)
                return result.ToArray();
            else
                return result.Where(m => symbols.Contains(m.Symbol)).ToArray();
        }

        public override void Sign(Request request)
        {
            if (request.ApiType != "private")
                return;
        }


        public class HuobiCommonSymbols
        {
            public string status { get; set; }
            public Dictionary<string, JsonElement>[] data { get; set; }
        }

        public class HuobiTickersResponse
        {
            public string status { get; set; }
            public ulong ts { get; set; }
            public HuobiTicker[] data { get; set; }
        }

        public class HuobiTicker
        {
            //{"symbol":"paybtc","open":6.08E-6,"high":6.33E-6,"low":5.77E-6,"close":5.81E-6,"amount":440075.7610779436,"vol":2.6533161464,
            //"count":2804,"bid":5.8E-6,"bidSize":125.9,"ask":5.84E-6,"askSize":4752.5}
            public string symbol { get; set; }
            public decimal open { get; set; }
            public decimal high { get; set; }
            public decimal low { get; set; }
            public decimal close { get; set; }
            public decimal amount { get; set; }
            public decimal vol { get; set; }
            public int count { get; set; }
            public decimal bid { get; set; }
            public decimal bidSize { get; set; }
            public decimal ask { get; set; }
            public decimal askSize { get; set; }
        }
    }
}
