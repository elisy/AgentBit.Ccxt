using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    public class Poloniex : Exchange, IPublicAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiV1Public = new Uri("https://poloniex.com/");
        readonly Uri ApiV2Public = new Uri("https://api.poloniex.com/");

        public Poloniex(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://docs.poloniex.com/#http-api
            RateLimit = 1 * 1000 / 6; // Making more than 6 calls per second to the API, or repeatedly and needlessly fetching excessive amounts of data, can result in rate limit

            CommonCurrencies = new Dictionary<string, string>() {
                { "AIR", "AirCoin" },
                { "APH", "AphroditeCoin" },
                { "BCC", "BTCtalkcoin" },
                { "BCHABC", "BCHABC" },
                { "BDG", "Badgercoin" },
                { "BTM", "Bitmark" },
                { "CON", "Coino" },
                { "FREE", "freerossdao" },
                { "GOLD", "GoldEagles" },
                { "GPUC", "GPU" },
                { "HOT", "Hotcoin" },
                { "ITC", "Information Coin" },
                { "KEY", "KEYCoin" },
                { "MASK", "NFTX Hashmasks Index" },
                { "MEME", "Degenerator Meme" },
                { "PLX", "ParallaxCoin" },
                { "REPV2", "REP" },
                { "STR", "XLM" },
                { "TRADE", "Unitrade" },
                { "XAP", "API Coin" }
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV2Public,
                    Path = "markets",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<PoloniexMarket[]>(response.Text);

                var result = new List<Market>();

                foreach (var market in responseJson)
                {
                    var newItem = new Market();
                    newItem.Id = market.symbol;

                    newItem.BaseId = market.baseCurrencyName;
                    newItem.QuoteId = market.quoteCurrencyName;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId.ToUpper());
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId.ToUpper());


                    newItem.Active = market.state == "NORMAL";

                    if (market.symbolTradeLimit is not null)
                    {
                        newItem.AmountPrecision = market.symbolTradeLimit.quantityScale;
                        newItem.PricePrecision = market.symbolTradeLimit.priceScale;

                        newItem.AmountMin = Convert.ToDecimal(market.symbolTradeLimit.minQuantity, CultureInfo.InvariantCulture);
                        newItem.CostMin = Convert.ToDecimal(market.symbolTradeLimit.minAmount, CultureInfo.InvariantCulture);
                    }

                    newItem.FeeMaker = 0.1450M / 100;
                    newItem.FeeTaker = 0.1550M / 100;

                    newItem.Url = $"https://poloniex.com/trade/{market.symbol}?c=YFGU6THS";
                    newItem.Margin = market.crossMargin is not null && market.crossMargin.supportCrossMargin;

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
            var markets = (await FetchMarkets().ConfigureAwait(false)).Where(m => m.Active).ToArray();

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiV2Public,
                Path = "markets/ticker24h",
                ApiType = "public",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var responseJson = JsonSerializer.Deserialize<PoloniexTicker[]>(response.Text);

            var dateTime = DateTime.UtcNow;
            var timestamp = (uint)(dateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            var result = new List<Ticker>(responseJson.Length);
            foreach (var item in responseJson)
            {
                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.symbol);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.Timestamp = item.ts;
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ticker.Timestamp);
                ticker.Symbol = market.Symbol;

                ticker.High = Convert.ToDecimal(item.high, CultureInfo.InvariantCulture);
                ticker.Low = Convert.ToDecimal(item.low, CultureInfo.InvariantCulture);
                ticker.Bid = Convert.ToDecimal(item.bid, CultureInfo.InvariantCulture);
                ticker.BidVolume = Convert.ToDecimal(item.bidQuantity, CultureInfo.InvariantCulture);
                ticker.Ask = Convert.ToDecimal(item.ask, CultureInfo.InvariantCulture);
                ticker.AskVolume = Convert.ToDecimal(item.askQuantity, CultureInfo.InvariantCulture);
                ticker.Last = Convert.ToDecimal(item.close, CultureInfo.InvariantCulture);
                ticker.Close = Convert.ToDecimal(item.close, CultureInfo.InvariantCulture);
                ticker.Open = Convert.ToDecimal(item.open, CultureInfo.InvariantCulture);
                ticker.BaseVolume = Convert.ToDecimal(item.quantity, CultureInfo.InvariantCulture);
                ticker.QuoteVolume = Convert.ToDecimal(item.amount, CultureInfo.InvariantCulture);
                ticker.Average = (ticker.Close + ticker.Open) / 2;

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
            //Error message User-Agent header is required.
            request.Headers.Add("User-Agent", typeof(Exchange).FullName);

            if (request.ApiType != "private")
                return;
        }


        public class PoloniexTicker
        {
            //{
            //  "symbol" : "BTS_BTC",
            //  "open" : "0.0000003243",
            //  "low" : "0.0000003243",
            //  "high" : "0.00000035",
            //  "close" : "0.0000003452",
            //  "quantity" : "4411",
            //  "amount" : "0.0015364543",
            //  "tradeCount" : 4,
            //  "startTime" : 1692923340000,
            //  "closeTime" : 1693009782250,
            //  "displayName" : "BTS/BTC",
            //  "dailyChange" : "0.0644",
            //  "bid" : "0.0000003325",
            //  "bidQuantity" : "2706",
            //  "ask" : "0.0000003559",
            //  "askQuantity" : "207",
            //  "ts" : 1693009785446,
            //  "markPrice" : "0.000000335"
            //}
            public string symbol { get; set; }
            public string open { get; set; }
            public string low { get; set; }
            public string high { get; set; }
            public string close { get; set; }
            public string quantity { get; set; }
            public string amount { get; set; }
            public uint tradeCount { get; set; }
            public ulong startTime { get; set; }
            public ulong closeTime { get; set; }
            public string displayName { get; set; }
            public string dailyChange { get; set; }
            public string bid { get; set; }
            public string bidQuantity { get; set; }
            public string ask { get; set; }
            public string askQuantity { get; set; }
            public ulong ts { get; set; }
            public string markPrice { get; set; }
        }

        public class PoloniexMarketTradeLimit
        {
            public string symbol { get; set; }
            public int priceScale { get; set; }
            public int quantityScale { get; set; }
            public int amountScale { get; set; }
            public string minQuantity { get; set; }
            public string minAmount { get; set; }
            public string highestBid { get; set; }
            public string lowestAsk { get; set; }
        }

        public class PoloniexMarketCrossMargin
        {
            public bool supportCrossMargin { get; set; }
            public int maxLeverage { get; set; }
        }

        public class PoloniexMarket
        {
            //{
            //  "symbol" : "BTS_BTC",
            //  "baseCurrencyName" : "BTS",
            //  "quoteCurrencyName" : "BTC",
            //  "displayName" : "BTS/BTC",
            //  "state" : "NORMAL",
            //  "visibleStartTime" : 1659018816626,
            //  "tradableStartTime" : 1659018816626,
            //  "symbolTradeLimit" : {
            //    "symbol" : "BTS_BTC",
            //    "priceScale" : 10,
            //    "quantityScale" : 0,
            //    "amountScale" : 8,
            //    "minQuantity" : "100",
            //    "minAmount" : "0.00001",
            //    "highestBid" : "0",
            //    "lowestAsk" : "0"
            //  },
            //  "crossMargin" : {
            //    "supportCrossMargin" : false,
            //    "maxLeverage" : 1
            //  }
            //}
            public string symbol { get; set; }
            public string baseCurrencyName { get; set; }
            public string quoteCurrencyName { get; set; }
            public string displayName { get; set; }
            public string state { get; set; }
            public ulong visibleStartTime { get; set; }
            public ulong tradableStartTime { get; set; }
            public PoloniexMarketTradeLimit symbolTradeLimit { get; set; }
            public PoloniexMarketCrossMargin crossMargin { get; set; }
        }


    }
}
