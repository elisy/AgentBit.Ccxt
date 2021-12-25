using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    /// <summary>
    /// Corresponds to pro.coinbase.com
    /// https://docs.cloud.coinbase.com/exchange/docs
    /// </summary>
    public class Coinbase : Exchange, IPublicAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiV2 = new Uri("https://api.exchange.coinbase.com/");

        public Coinbase(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://docs.cloud.coinbase.com/exchange/docs/rate-limits
            RateLimit = 1 * 1000 / 10; // 10 requests per second

            CommonCurrencies = new Dictionary<string, string>() {
                { "CGLD", "CELO" }
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV2,
                    Path = "products",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<CoinbaseProduct[]>(response.Text);

                var result = new List<Market>();

                foreach (var market in responseJson)
                {
                    var newItem = new Market();
                    newItem.Id = market.id;
                    newItem.BaseId = market.base_currency;
                    newItem.QuoteId = market.quote_currency;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId.ToUpper());
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId.ToUpper());


                    newItem.AmountMin = Convert.ToDecimal(market.base_min_size, CultureInfo.InvariantCulture);
                    newItem.AmountMax = Convert.ToDecimal(market.base_max_size, CultureInfo.InvariantCulture);
                    newItem.AmountPrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.base_increment, CultureInfo.InvariantCulture)));

                    newItem.PriceMin = Convert.ToDecimal(market.quote_increment, CultureInfo.InvariantCulture);
                    newItem.PricePrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.quote_increment, CultureInfo.InvariantCulture)));

                    newItem.CostMin = Convert.ToDecimal(market.min_market_funds, CultureInfo.InvariantCulture);
                    newItem.CostMax = Convert.ToDecimal(market.max_market_funds, CultureInfo.InvariantCulture);

                    newItem.Active = (market.status == "online");

                    newItem.FeeMaker = 0.1M / 100;
                    newItem.FeeTaker = 0.1M / 100;

                    newItem.Url = $"https://pro.coinbase.com/trade/{@newItem.BaseId}-{@newItem.QuoteId}";
                    newItem.Margin = market.margin_enabled;

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
            throw new NotImplementedException();
        }

        public override void Sign(Request request)
        {
            //Error message User-Agent header is required.
            request.Headers.Add("User-Agent", typeof(Exchange).FullName);

            if (request.ApiType != "private")
                return;
        }



        public class CoinbaseProduct
        {
            //{
            //  "id": "ETC-EUR",
            //  "base_currency": "ETC",
            //  "quote_currency": "EUR",
            //  "base_min_size": "0.017",
            //  "base_max_size": "4900",
            //  "quote_increment": "0.01",
            //  "base_increment": "0.00000001",
            //  "display_name": "ETC/EUR",
            //  "min_market_funds": "0.84",
            //  "max_market_funds": "240000",
            //  "margin_enabled": false,
            //  "fx_stablecoin": false,
            //  "max_slippage_percentage": "0.03000000",
            //  "post_only": false,
            //  "limit_only": false,
            //  "cancel_only": false,
            //  "trading_disabled": false,
            //  "status": "online",
            //  "status_message": "Our ETC order books are now in full-trading mode. Limit, market and stop orders are all now available.",
            //  "auction_mode": false
            //}
            public string id { get; set; }
            public string base_currency { get; set; }
            public string quote_currency { get; set; }
            public string base_min_size { get; set; }
            public string base_max_size { get; set; }
            public string quote_increment { get; set; }
            public string base_increment { get; set; }
            public string display_name { get; set; }
            public string min_market_funds { get; set; }
            public string max_market_funds { get; set; }
            public bool margin_enabled { get; set; }
            public bool fx_stablecoin { get; set; }
            public string max_slippage_percentage { get; set; }
            public bool post_only { get; set; }
            public bool limit_only { get; set; }
            public bool cancel_only { get; set; }
            public bool trading_disabled { get; set; }
            public string status { get; set; }
            public string status_message { get; set; }
            public bool auction_mode { get; set; }
        }

    }
}
