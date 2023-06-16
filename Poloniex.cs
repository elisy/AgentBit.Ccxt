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
                    BaseUri = ApiV1Public,
                    Path = "public?command=returnTicker",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<Dictionary<string, PoloniexTicker>>(response.Text);

                var result = new List<Market>();

                foreach (var market in responseJson)
                {
                    var newItem = new Market();
                    newItem.Id = market.Key;

                    var quoutes = market.Key.Split("_");
                    newItem.BaseId = quoutes[1];
                    newItem.QuoteId = quoutes[0];

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId.ToUpper());
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId.ToUpper());

                    newItem.AmountPrecision = 8;
                    newItem.PricePrecision = 8;

                    newItem.Active = market.Value.isFrozen != "1";

                    newItem.FeeMaker = 0.1450M / 100;
                    newItem.FeeTaker = 0.1550M / 100;

                    newItem.Url = $"https://poloniex.com/exchange/{market.Key}?c=YFGU6THS";
                    newItem.Margin = false;

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
                BaseUri = ApiV1Public,
                Path = "public?command=returnTicker",
                ApiType = "public",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var responseJson = JsonSerializer.Deserialize<Dictionary<string, PoloniexTicker>>(response.Text);

            var dateTime = DateTime.UtcNow;
            var timestamp = (uint)(dateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;

            var result = new List<Ticker>(responseJson.Keys.Count());
            foreach (var item in responseJson)
            {
                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.Key);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.DateTime = dateTime;
                ticker.Timestamp = timestamp;
                ticker.Symbol = market.Symbol;

                ticker.High = Convert.ToDecimal(item.Value.high24hr, CultureInfo.InvariantCulture);
                ticker.Low = Convert.ToDecimal(item.Value.low24hr, CultureInfo.InvariantCulture);
                ticker.Bid = Convert.ToDecimal(item.Value.highestBid, CultureInfo.InvariantCulture);
                ticker.Ask = Convert.ToDecimal(item.Value.lowestAsk, CultureInfo.InvariantCulture);
                ticker.Last = Convert.ToDecimal(item.Value.last, CultureInfo.InvariantCulture);
                ticker.Close = ticker.Last;
                ticker.Open = ticker.Last * (1 - Convert.ToDecimal(item.Value.percentChange, CultureInfo.InvariantCulture) / 100);
                ticker.BaseVolume = Convert.ToDecimal(item.Value.quoteVolume, CultureInfo.InvariantCulture);
                ticker.QuoteVolume = Convert.ToDecimal(item.Value.baseVolume, CultureInfo.InvariantCulture);
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
            //    "id": 14,
            //    "last": "0.00000090",
            //    "lowestAsk": "0.00000091",
            //    "highestBid": "0.00000089",
            //    "percentChange": "-0.02173913",
            //    "baseVolume": "0.28698296",
            //    "quoteVolume": "328356.84081156",
            //    "isFrozen": "0",
            //    "postOnly": "0",
            //    "high24hr": "0.00000093",
            //    "low24hr": "0.00000087"
            //  }
            public int id { get; set; }
            public string last { get; set; }
            public string lowestAsk { get; set; }
            public string highestBid { get; set; }
            public string percentChange { get; set; }
            public string baseVolume { get; set; }
            public string quoteVolume { get; set; }
            public string isFrozen { get; set; }
            public string postOnly { get; set; }
            public string high24hr { get; set; }
            public string low24hr { get; set; }
        }

    }
}
