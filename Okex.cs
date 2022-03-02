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
    [Obsolete("Okex was rebrended to Okx")]
    public class Okex : Exchange, IPublicAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiV3 = new Uri("https://www.okex.com/api/spot/v3/");

        public Okex(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://www.okex.com/docs/en/#summary-limit
            RateLimit = 1 * 1000 / 6; // If not specified, the limit is 6 requests per second.

            CommonCurrencies = new Dictionary<string, string>() {
                { "AE", "AET" },
                { "BOX", "DefiBox" },
                { "HOT", "Hydro Protocol" },
                { "HSR", "HC" },
                { "MAG", "Maggie" },
                { "SBTC", "Super Bitcoin" },
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
                    BaseUri = ApiV3,
                    Path = "instruments",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<OkexInstrument[]>(response.Text);

                var result = new List<Market>();

                foreach (var market in responseJson)
                {
                    var newItem = new Market();
                    newItem.Id = market.instrument_id;
                    newItem.BaseId = market.base_currency;
                    newItem.QuoteId = market.quote_currency;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId.ToUpper());
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId.ToUpper());


                    newItem.AmountMin = Convert.ToDecimal(market.min_size, CultureInfo.InvariantCulture);
                    newItem.AmountPrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.size_increment, CultureInfo.InvariantCulture)));

                    newItem.PriceMin = Convert.ToDecimal(market.tick_size, CultureInfo.InvariantCulture);
                    newItem.PricePrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.tick_size, CultureInfo.InvariantCulture)));

                    newItem.CostMin = newItem.PriceMin * newItem.AmountMin;

                    newItem.Active = true;

                    newItem.FeeMaker = 0.1M / 100;
                    newItem.FeeTaker = 0.15M / 100;

                    newItem.Url = $"https://www.okex.com/trade-spot/{market.instrument_id.ToLower()}";
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
                BaseUri = ApiV3,
                Path = "instruments/ticker",
                ApiType = "public",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var responseJson = JsonSerializer.Deserialize<OkexTicker[]>(response.Text);

            var result = new List<Ticker>(responseJson.Length);
            foreach (var item in responseJson)
            {
                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.instrument_id);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.DateTime = DateTime.Parse(item.timestamp, null, DateTimeStyles.RoundtripKind);
                ticker.Timestamp = (uint)(ticker.DateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                ticker.Symbol = market.Symbol;

                ticker.High = Convert.ToDecimal(item.high_24h, CultureInfo.InvariantCulture);
                ticker.Low = Convert.ToDecimal(item.low_24h, CultureInfo.InvariantCulture);
                ticker.Bid = Convert.ToDecimal(item.best_bid, CultureInfo.InvariantCulture);
                ticker.BidVolume = Convert.ToDecimal(item.best_bid_size, CultureInfo.InvariantCulture);
                ticker.Ask = Convert.ToDecimal(item.best_ask, CultureInfo.InvariantCulture);
                ticker.AskVolume = Convert.ToDecimal(item.best_ask_size, CultureInfo.InvariantCulture);
                ticker.Open = Convert.ToDecimal(item.open_24h, CultureInfo.InvariantCulture);
                ticker.Close = Convert.ToDecimal(item.last, CultureInfo.InvariantCulture);
                ticker.Last = Convert.ToDecimal(item.last, CultureInfo.InvariantCulture);
                ticker.BaseVolume = Convert.ToDecimal(item.base_volume_24h, CultureInfo.InvariantCulture);
                ticker.QuoteVolume = Convert.ToDecimal(item.quote_volume_24h, CultureInfo.InvariantCulture);

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



        public class OkexInstrument
        {
            //{"base_currency":"BTC","category":"1","instrument_id":"BTC-USDT","min_size":"0.00001","quote_currency":"USDT","size_increment":"0.00000001","tick_size":"0.1"}
            public string base_currency { get; set; }
            public string category { get; set; }
            public string instrument_id { get; set; }
            public string min_size { get; set; }
            public string quote_currency { get; set; }
            public string size_increment { get; set; }
            public string tick_size { get; set; }
        }

        public class OkexTicker
        {
            //{"best_ask":"0.003172","best_bid":"0.003171","instrument_id":"LTC-BTC","open_utc0":"0.003159","open_utc8":"0.003162",
            //"product_id":"LTC-BTC","last":"0.003172","last_qty":"0","ask":"0.003172","best_ask_size":"41.1988","bid":"0.003171",
            //"best_bid_size":"29.25","open_24h":"0.003154","high_24h":"0.003176","low_24h":"0.003127","base_volume_24h":"20743.395145",
            //"timestamp":"2022-01-02T04:12:03.356Z","quote_volume_24h":"65.475616"}
            public string best_ask { get; set; }
            public string best_bid { get; set; }
            public string instrument_id { get; set; }
            public string open_utc0 { get; set; }
            public string open_utc8 { get; set; }
            public string product_id { get; set; }
            public string last { get; set; }
            public string last_qty { get; set; }
            public string ask { get; set; }
            public string best_ask_size { get; set; }
            public string bid { get; set; }
            public string best_bid_size { get; set; }
            public string open_24h { get; set; }
            public string high_24h { get; set; }
            public string low_24h { get; set; }
            public string base_volume_24h { get; set; }
            public string timestamp { get; set; }
            public string quote_volume_24h { get; set; }
        }

    }
}
