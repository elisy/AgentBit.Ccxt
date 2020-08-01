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
        readonly Uri ApiV1Public = new Uri("https://api.huobi.com/v1/");

        public Huobi(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://huobiglobal.zendesk.com/hc/en-us/articles/900001168066
            //Average 20 per 2s
            RateLimit = 2 * 1000 / 20;

            CommonCurrencies = new Dictionary<string, string>() {
                { "GET", "Themis" }, // conflict with GET (Guaranteed Entrance Token, GET Protocol)
                { "HOT", "Hydro Protocol" } // conflict with HOT (Holo) https://github.com/ccxt/ccxt/issues/4929
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV1Public,
                    Path = "common/symbols",
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

            throw new NotImplementedException();
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
    }
}
