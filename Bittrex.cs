using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    public class Bittrex : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiV3 = new Uri("https://api.bittrex.com/v3/");

        public Bittrex(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            // In general, API users are permitted to make a maximum of 60 API calls per minute
            RateLimit = 1 * 1000;

            CommonCurrencies = new Dictionary<string, string>() {
                { "BIFI", "Bifrost Finance" },
                { "GMT", "GMT Token" }, // conflict with STEPN
                { "MEME", "Memetic" }, // conflict with Meme Inu OkEX
                { "MER", "Mercury" }, // conflict with Mercurial Finance
                { "PLAY", "PlayChip" }, //Conflict with Kucoin PLAY HEROcoin
                { "PROS", "Pros.Finance" },
                { "REPV2", "REP" },
                { "TON", "Tokamak Network" },
                { "REAL", "REALLINK" } //Conflict with FTX Realy
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV3,
                    Path = "markets",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<BittrexMarket[]>(response.Text);

                var result = new List<Market>();

                foreach (var market in responseJson)
                {
                    var newItem = new Market();
                    newItem.Id = market.symbol;
                    newItem.BaseId = market.baseCurrencySymbol;
                    newItem.QuoteId = market.quoteCurrencySymbol;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.PricePrecision = market.precision;
                    newItem.PriceMin = (decimal)Math.Pow(10, -newItem.PricePrecision);
                    newItem.AmountPrecision = 8;
                    newItem.AmountMin = JsonSerializer.Deserialize<decimal>(market.minTradeSize);

                    newItem.Active = market.status == "ONLINE";

                    newItem.FeeMaker = 0.2M / 100;
                    newItem.FeeTaker = 0.2M / 100;

                    newItem.Url = $"https://global.bittrex.com/Market/Index?MarketName={newItem.QuoteId}-{newItem.BaseId}&referralCode=RKK-Z4E-YDB";

                    newItem.Info = market;

                    result.Add(newItem);
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
            var markets = await FetchMarkets().ConfigureAwait(false);

            var tickersTask = Request(new Base.Request()
            {
                BaseUri = ApiV3,
                Path = $"markets/tickers",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var summariesTask = Request(new Base.Request()
            {
                BaseUri = ApiV3,
                Path = $"markets/summaries",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var tickers = JsonSerializer.Deserialize<BittrexTicker[]>((await tickersTask).Text);
            var summaries = JsonSerializer.Deserialize<BittrexSummary[]>((await summariesTask).Text);

            var tickersSummaries = from ticker in tickers
                                   join summary in summaries on ticker.symbol equals summary.symbol
                                   select new { ticker, summary };

            var result = new List<Ticker>();

            foreach (var item in tickersSummaries)
            {
                Ticker ticker = new Ticker();

                ticker.DateTime = DateTime.UtcNow;
                ticker.Timestamp = (uint)(ticker.DateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.ticker.symbol);
                if (market == null)
                    continue;

                ticker.Symbol = market.Symbol;
                ticker.High = JsonSerializer.Deserialize<decimal>(item.summary.high);
                ticker.Low = JsonSerializer.Deserialize<decimal>(item.summary.low);
                ticker.Bid = JsonSerializer.Deserialize<decimal>(item.ticker.bidRate);
                ticker.Ask = JsonSerializer.Deserialize<decimal>(item.ticker.askRate);
                ticker.Last = JsonSerializer.Deserialize<decimal>(item.ticker.lastTradeRate);
                ticker.Close = ticker.Last;
                ticker.BaseVolume = JsonSerializer.Deserialize<decimal>(item.summary.volume);
                ticker.QuoteVolume = JsonSerializer.Deserialize<decimal>(item.summary.quoteVolume);
                ticker.Info = item;

                result.Add(ticker);
            }

            if (symbols == null)
                return result.ToArray();
            else
                return result.Where(m => symbols.Contains(m.Symbol)).ToArray();
        }

        public class BittrexSummary
        {
            //{"symbol":"1ST-BTC","high":"0.00000000","low":"0.00000000","volume":"0.00000000","quoteVolume":"0.00000000","updatedAt":"2020-06-27T11:40:04.507Z"}
            public string symbol { get; set; }
            public string high { get; set; }
            public string low { get; set; }
            public string volume { get; set; }
            public string quoteVolume { get; set; }
            public string updatedAt { get; set; }
        }

        public class BittrexTicker
        {
            //{"symbol":"1ST-BTC","lastTradeRate":"0.00001342","bidRate":"0.00000700","askRate":"0.00001345"}
            public string symbol { get; set; }
            public string lastTradeRate { get; set; }
            public string bidRate { get; set; }
            public string askRate { get; set; }
        }

        public class BittrexMarket
        {
            //{"symbol":"4ART-BTC","baseCurrencySymbol":"4ART","quoteCurrencySymbol":"BTC","minTradeSize":"10.00000000","precision":8,"status":"ONLINE",
            //"createdAt":"2020-06-10T15:05:29.833Z","notice":"","prohibitedIn":["US"]}
            public string symbol { get; set; }
            public string baseCurrencySymbol { get; set; }
            public string quoteCurrencySymbol { get; set; }
            public string minTradeSize { get; set; }
            public int precision { get; set; }
            public string status { get; set; }
            public string createdAt { get; set; }
            public string notice { get; set; }
        }
    }


}
