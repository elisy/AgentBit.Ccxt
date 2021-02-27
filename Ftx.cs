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
    public class Ftx : Exchange, IPublicAPI
    {
        readonly Uri ApiV1Public = new Uri("https://ftx.com/api/");

        public Ftx(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://docs.ftx.com/#rate-limits
            //Please do not send more than 30 requests per second
            RateLimit = 1 * 1000 / 30;

            CommonCurrencies = new Dictionary<string, string>() {
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV1Public,
                    Path = "markets",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<FtxMarketsResult<FtxMarket[]>>(response.Text, new JsonSerializerOptions() { IgnoreNullValues = true });
                var markets = responseJson.result;

                var result = new List<Market>();

                foreach (var market in markets)
                {
                    if (market.type != "spot")
                        continue;

                    var newItem = new Market();
                    newItem.Id = market.name;
                    newItem.BaseId = market.baseCurrency;
                    newItem.QuoteId = market.quoteCurrency;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.PricePrecision = -(int)Math.Ceiling(Math.Log10((double)market.priceIncrement));
                    newItem.AmountPrecision = -(int)Math.Ceiling(Math.Log10((double)market.sizeIncrement));

                    newItem.PriceMin = market.priceIncrement;

                    newItem.AmountMin = market.minProvideSize;

                    newItem.Active = market.enabled;

                    newItem.FeeMaker = 0.02M / 100;
                    newItem.FeeTaker = 0.07M / 100;

                    newItem.Url = $"https://ftx.com/trade/{@newItem.BaseId}/{@newItem.QuoteId}?a=6015897";

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

        public class FtxMarketsResult<T>
        {
            public bool success { get; set; }
            public T result { get; set; }
        }

        public class FtxMarket
        {
            public string type { get; set; }
            public string name { get; set; }
            public string underlying { get; set; }
            public string baseCurrency { get; set; }
            public string quoteCurrency { get; set; }
            public bool enabled { get; set; }
            public decimal ask { get; set; }
            public decimal bid { get; set; }
            public decimal? last { get; set; }
            public decimal minProvideSize { get; set; }
            public bool postOnly { get; set; }
            public decimal priceIncrement { get; set; }
            public decimal sizeIncrement { get; set; }
            public bool restricted { get; set; }
        }
    }
}
