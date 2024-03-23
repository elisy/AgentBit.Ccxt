using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Web;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    public class Gate : Exchange, IPublicAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiPublicV4 = new Uri("https://api.gateio.ws/api/v4/");

        public Gate(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            RateLimit = 1000 * 10 / 200; //https://www.gate.io/ru/article/31282

            CommonCurrencies = new Dictionary<string, string>() {
                { "88MPH", "MPH" },
                { "AXIS", "Axis DeFi" },
                { "BIFI", "Bitcoin File" },
                { "BYN", "BeyondFi" },
                { "EGG", "Goose Finance" },
                { "GTC_HT", "Game.com HT" },
                { "GTC_BSC", "Game.com BSC" },
                { "HIT", "HitChain" },
                { "MM", "Million" },
                { "MPH", "Morpher" },
                { "POINT", "GatePoint" },
                { "RAI", "Rai Reflex Index" },
                { "SBTC", "Super Bitcoin" },
                { "TNC", "Trinity Network Credit" },
                { "VAI", "VAIOT" },
                { "TRAC", "TRACO" }
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var pairResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV4,
                    Path = "spot/currency_pairs",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var pairs = JsonSerializer.Deserialize<GatePair[]>(pairResponse.Text);

                var result = new List<Market>();

                foreach (var market in pairs)
                {
                    var newItem = new Market();
                    newItem.Id = market.id;

                    var baseQuote = market.id.Split('_');
                    newItem.BaseId = baseQuote[0];
                    newItem.QuoteId = baseQuote[1];

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.Active = market.trade_status == "tradable";

                    newItem.AmountMin = JsonSerializer.Deserialize<decimal>(market.min_base_amount);
                    if (!string.IsNullOrEmpty(market.max_base_amount))
                        newItem.AmountMax = JsonSerializer.Deserialize<decimal>(market.max_base_amount);
                    newItem.CostMin = JsonSerializer.Deserialize<decimal>(market.min_quote_amount);
                    if (!string.IsNullOrEmpty(market.max_quote_amount))
                        newItem.CostMax = JsonSerializer.Deserialize<decimal>(market.max_quote_amount);

                    newItem.PricePrecision = market.precision;
                    newItem.AmountPrecision = market.amount_precision;

                    newItem.FeeTaker = JsonSerializer.Deserialize<decimal>(market.fee) / 100;
                    newItem.FeeMaker = JsonSerializer.Deserialize<decimal>(market.fee) / 100;

                    newItem.Url = $"https://latoken.com/exchange/{newItem.Base.ToUpper()}_{newItem.Quote.ToUpper()}?r=pkkbsa37";

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
                BaseUri = ApiPublicV4,
                Path = "spot/tickers",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);
            var tickers = JsonSerializer.Deserialize<GateTicker[]>(response.Text);

            var result = new List<Ticker>();
            foreach (var item in tickers)
            {
                var market = markets.FirstOrDefault(m => m.Id == item.currency_pair);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.DateTime = DateTime.UtcNow;
                ticker.Timestamp = (ulong)((DateTimeOffset)ticker.DateTime).ToUnixTimeMilliseconds();

                ticker.Symbol = market.Symbol;
                if (!string.IsNullOrEmpty(item.highest_bid))
                    ticker.Bid = JsonSerializer.Deserialize<decimal>(item.highest_bid);
                if (!string.IsNullOrEmpty(item.lowest_ask))
                    ticker.Ask = JsonSerializer.Deserialize<decimal>(item.lowest_ask);
                ticker.Last = JsonSerializer.Deserialize<decimal>(item.last);
                ticker.Open = ticker.Last;
                ticker.Close = ticker.Last;
                ticker.Average = (ticker.Bid + ticker.Ask) / 2;
                ticker.BaseVolume = JsonSerializer.Deserialize<decimal>(item.base_volume);
                ticker.QuoteVolume = JsonSerializer.Deserialize<decimal>(item.quote_volume);

                ticker.High = JsonSerializer.Deserialize<decimal>(item.high_24h);
                ticker.Low = JsonSerializer.Deserialize<decimal>(item.low_24h);

                ticker.Percentage = JsonSerializer.Deserialize<decimal>(item.change_percentage);

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


        public class GatePair
        {
            //{
            //    "id": "ETH_USDT",
            //    "base": "ETH",
            //    "quote": "USDT",
            //    "fee": "0.2",
            //    "min_base_amount": "0.001",
            //    "min_quote_amount": "1.0",
            //    "max_base_amount": "10000",
            //    "max_quote_amount": "10000000",
            //    "amount_precision": 3,
            //    "precision": 6,
            //    "trade_status": "tradable",
            //    "sell_start": 1516378650,
            //    "buy_start": 1516378650
            //}

            public string id { get; set; }
            public string @base { get; set; }
            public string quote { get; set; }
            public string fee { get; set; }
            public string min_base_amount { get; set; }
            public string min_quote_amount { get; set; }
            public string max_base_amount { get; set; }
            public string max_quote_amount { get; set; }
            public int amount_precision { get; set; }
            public int precision { get; set; }
            public string trade_status { get; set; }
            public long sell_start { get; set; }
            public long buy_start { get; set; }
        }

        public class GateTicker
        {
            //{
            //  "currency_pair": "BTC3L_USDT",
            //  "last": "2.46140352",
            //  "lowest_ask": "2.477",
            //  "highest_bid": "2.4606821",
            //  "change_percentage": "-8.91",
            //  "change_utc0": "-8.91",
            //  "change_utc8": "-8.91",
            //  "base_volume": "656614.0845820589",
            //  "quote_volume": "1602221.66468375534639404191",
            //  "high_24h": "2.7431",
            //  "low_24h": "1.9863",
            //  "etf_net_value": "2.46316141",
            //  "etf_pre_net_value": "2.43201848",
            //  "etf_pre_timestamp": 1611244800,
            //  "etf_leverage": "2.2803019447281203"
            //}
            public string currency_pair { get; set; }
            public string last { get; set; }
            public string lowest_ask { get; set; }
            public string highest_bid { get; set; }
            public string change_percentage { get; set; }
            public string change_utc0 { get; set; }
            public string change_utc8 { get; set; }
            public string base_volume { get; set; }
            public string quote_volume { get; set; }
            public string high_24h { get; set; }
            public string low_24h { get; set; }
            public string etf_net_value { get; set; }
            public string etf_pre_net_value { get; set; }
            public ulong etf_pre_timestamp { get; set; }
            public string etf_leverage { get; set; }
        }
    }
}
