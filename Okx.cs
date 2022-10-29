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
    /// <summary>
    /// https://www.okx.com/docs-v5/en/
    /// </summary>
    public class Okx : Exchange, IPublicAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiV5 = new Uri("https://www.okx.com/api/v5/");

        public Okx(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            RateLimit = 2 * 1000 / 20; //Rate Limit: 20 requests per 2 seconds

            CommonCurrencies = new Dictionary<string, string>() {
                { "AE", "AET" },
                { "BOX", "DefiBox" },
                { "HOT", "Hydro Protocol" },
                { "HSR", "HC" },
                { "MAG", "Maggie" },
                { "SBTC", "Super Bitcoin" },
                { "STC", "Satoshi Island" },
                { "TRADE", "Unitrade" },
                { "YOYO", "YOYOW" },
                { "WIN", "WinToken" }
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV5,
                    Path = "public/instruments?instType=SPOT",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<OkxPublicResponse<OkxInstrument[]>>(response.Text);

                var result = new List<Market>();

                foreach (var market in responseJson.data)
                {
                    var newItem = new Market();
                    newItem.Id = market.instId;
                    newItem.BaseId = market.baseCcy;
                    newItem.QuoteId = market.quoteCcy;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId.ToUpper());
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId.ToUpper());

                    newItem.AmountMin = Convert.ToDecimal(market.minSz, CultureInfo.InvariantCulture);
                    newItem.AmountPrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.lotSz, CultureInfo.InvariantCulture)));

                    newItem.PriceMin = Convert.ToDecimal(market.tickSz, CultureInfo.InvariantCulture);
                    newItem.PricePrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.tickSz, CultureInfo.InvariantCulture)));

                    newItem.Active = true;

                    newItem.FeeMaker = 0.1M / 100;
                    newItem.FeeTaker = 0.15M / 100;

                    newItem.Url = $"https://www.okx.com/trade-spot/{market.instId.ToLower()}";
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
                BaseUri = ApiV5,
                Path = "market/tickers?instType=SPOT",
                ApiType = "public",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var responseJson = JsonSerializer.Deserialize<OkxPublicResponse<OkxTicker[]>>(response.Text);

            var result = new List<Ticker>(responseJson.data.Length);
            foreach (var item in responseJson.data)
            {
                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.instId);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.Timestamp = Convert.ToUInt64(item.ts);
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ticker.Timestamp);
                ticker.Symbol = market.Symbol;

                ticker.High = Convert.ToDecimal(item.high24h, CultureInfo.InvariantCulture);
                ticker.Low = Convert.ToDecimal(item.low24h, CultureInfo.InvariantCulture);
                ticker.Bid = Convert.ToDecimal(item.bidPx, CultureInfo.InvariantCulture);
                ticker.BidVolume = Convert.ToDecimal(item.bidSz, CultureInfo.InvariantCulture);
                ticker.Ask = Convert.ToDecimal(item.askPx, CultureInfo.InvariantCulture);
                ticker.AskVolume = Convert.ToDecimal(item.askSz, CultureInfo.InvariantCulture);
                ticker.Open = Convert.ToDecimal(item.open24h, CultureInfo.InvariantCulture);
                ticker.Last = Convert.ToDecimal(item.last, CultureInfo.InvariantCulture);
                ticker.Close = ticker.Last;
                ticker.BaseVolume = Convert.ToDecimal(item.vol24h, CultureInfo.InvariantCulture);
                ticker.QuoteVolume = Convert.ToDecimal(item.volCcy24h, CultureInfo.InvariantCulture);

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


        public class OkxPublicResponse<T>
        {
            public string code { get; set; }
            public string msg { get; set; }
            public T data { get; set; }
        }

        public class OkxInstrument
        {
            //{
            //    "instType":"SWAP",
            //    "instId":"LTC-USD-SWAP",
            //    "uly":"LTC-USD",
            //    "category":"1",
            //    "baseCcy":"",
            //    "quoteCcy":"",
            //    "settleCcy":"LTC",
            //    "ctVal":"10",
            //    "ctMult":"1",
            //    "ctValCcy":"USD",
            //    "optType":"C",
            //    "stk":"",
            //    "listTime":"1597026383085",
            //    "expTime":"1597026383085",
            //    "lever":"10",
            //    "tickSz":"0.01",
            //    "lotSz":"1",
            //    "minSz":"1",
            //    "ctType":"inverse",
            //    "alias":"this_week",
            //    "state":"live"
            //}            
            public string instType { get; set; }
            public string instId { get; set; }
            public string uly { get; set; }
            public string category { get; set; }
            public string baseCcy { get; set; }
            public string quoteCcy { get; set; }
            public string settleCcy { get; set; }
            public string ctVal { get; set; }
            public string ctMult { get; set; }
            public string ctValCcy { get; set; }
            public string optType { get; set; }
            public string stk { get; set; }
            public string listTime { get; set; }
            public string expTime { get; set; }
            public string lever { get; set; }
            public string tickSz { get; set; }
            public string lotSz { get; set; }
            public string minSz { get; set; }
            public string ctType { get; set; }
            public string alias { get; set; }
            public string state { get; set; }
        }

        public class OkxTicker
        {
            //{
            //   "instType":"SWAP",
            //   "instId":"LTC-USD-SWAP",
            //   "last":"9999.99",
            //   "lastSz":"0.1",
            //   "askPx":"9999.99",
            //   "askSz":"11",
            //   "bidPx":"8888.88",
            //   "bidSz":"5",
            //   "open24h":"9000",
            //   "high24h":"10000",
            //   "low24h":"8888.88",
            //   "volCcy24h":"2222",
            //   "vol24h":"2222",
            //   "sodUtc0":"0.1",
            //   "sodUtc8":"0.1",
            //   "ts":"1597026383085"
            //}
            public string instType { get; set; }
            public string instId { get; set; }
            public string last { get; set; }
            public string lastSz { get; set; }
            public string askPx { get; set; }
            public string askSz { get; set; }
            public string bidPx { get; set; }
            public string bidSz { get; set; }
            public string open24h { get; set; }
            public string high24h { get; set; }
            public string low24h { get; set; }
            public string volCcy24h { get; set; }
            public string vol24h { get; set; }
            public string sodUtc0 { get; set; }
            public string sodUtc8 { get; set; }
            public string ts { get; set; }
        }

    }
}
