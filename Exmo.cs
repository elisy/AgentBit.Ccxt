using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    public class Exmo : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker, IFetchBalance, IFetchMyTrades
    {
        readonly Uri ApiPublicV1 = new Uri("https://api.exmo.com/v1/");
        readonly Uri ApiPrivateV1 = new Uri("https://api.exmo.com/v1/");
        readonly Uri ApiPrivateV1_1 = new Uri("https://api.exmo.com/v1.1/");

        public Exmo(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://exmo.com/en/news_view?id=1472
            //The maximum number of API requests from one user or one IP address can reach 180 per minute
            RateLimit = (int)60 / 180 * 1000;
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var detailsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = "pair_settings",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var details = JsonSerializer.Deserialize<Dictionary<string, ExmoPairSettings>>(detailsResponse.Text);

                var result = new List<Market>();

                foreach (var market in details)
                {
                    var newItem = new Market();
                    newItem.Id = market.Key;
                    var parts = market.Key.Split('_');
                    newItem.BaseId = parts[0];
                    newItem.QuoteId = parts[1];

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.CostMin = JsonSerializer.Deserialize<decimal>(market.Value.min_amount);
                    newItem.CostMax = JsonSerializer.Deserialize<decimal>(market.Value.max_amount);
                    newItem.PriceMin = JsonSerializer.Deserialize<decimal>(market.Value.min_price);
                    newItem.PriceMax = JsonSerializer.Deserialize<decimal>(market.Value.max_price);
                    newItem.AmountMin = JsonSerializer.Deserialize<decimal>(market.Value.min_quantity);
                    newItem.AmountMax = JsonSerializer.Deserialize<decimal>(market.Value.max_quantity);

                    newItem.PricePrecision = 8;
                    newItem.AmountPrecision = 8;

                    newItem.FeeTaker = 0.4M / 100;
                    newItem.FeeMaker = 0.4M / 100;

                    newItem.Url = $"https://exmo.com/en/trade/{newItem.BaseId}_{newItem.QuoteId}";

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
                BaseUri = ApiPublicV1,
                Path = "ticker",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);
            var tickers = JsonSerializer.Deserialize<Dictionary<string, ExmoTicker>>(response.Text);

            var result = new List<Ticker>();
            foreach (var item in tickers)
            {
                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.Key);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.Timestamp = item.Value.updated;
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ticker.Timestamp);

                ticker.Symbol = market.Symbol;
                ticker.High = JsonSerializer.Deserialize<decimal>(item.Value.high);
                ticker.Low = JsonSerializer.Deserialize<decimal>(item.Value.low);
                ticker.Bid = JsonSerializer.Deserialize<decimal>(item.Value.buy_price);
                ticker.Ask = JsonSerializer.Deserialize<decimal>(item.Value.sell_price);
                ticker.Last = JsonSerializer.Deserialize<decimal>(item.Value.last_trade);
                ticker.Close = ticker.Last;
                ticker.Average = JsonSerializer.Deserialize<decimal>(item.Value.avg);
                ticker.BaseVolume = JsonSerializer.Deserialize<decimal>(item.Value.vol);
                ticker.QuoteVolume = JsonSerializer.Deserialize<decimal>(item.Value.vol_curr);
                ticker.Info = item;

                result.Add(ticker);
            }
            if (symbols == null)
                return result.ToArray();
            else
                return result.Where(m => symbols.Contains(m.Symbol)).ToArray();
        }

        public override void SetBody(Request request)
        {
            if (request.Params != null && request.Params.Count != 0)
            {
                request.Body = new FormUrlEncodedContent(request.Params);
            }
        }

        protected string BuildPostData(Dictionary<string, string> data)
        {
            var sb = new StringBuilder();
            foreach (var key in data)
                sb.AppendFormat("{0}={1}&", key.Key, key.Value);
            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }

        public override void Sign(Request request)
        {
            if (request.ApiType != "private")
                return;

            request.Params["nonce"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            request.Headers.Add("Sign", GetHmac<HMACSHA512>(BuildPostData(request.Params), ApiSecret));
            request.Headers.Add("Key", ApiKey);
        }


        public async Task<Dictionary<string, BalanceAccount>> FetchBalance()
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1_1,
                Path = "user_info",
                Method = HttpMethod.Post
            }).ConfigureAwait(false);

            var jsonResponse = JsonSerializer.Deserialize<ExmoBalance>(response.Text);

            var result = (from balance in jsonResponse.balances
                          join reserve in jsonResponse.reserved on balance.Key equals reserve.Key
                          select new
                          {
                              Asset = GetCommonCurrencyCode(balance.Key.ToUpper()),
                              Total = JsonSerializer.Deserialize<decimal>(balance.Value) + JsonSerializer.Deserialize<decimal>(reserve.Value),
                              Free = JsonSerializer.Deserialize<decimal>(balance.Value)
                          }).ToDictionary(m => m.Asset, m => new BalanceAccount() { Free = m.Free, Total = m.Total });
            return result;
        }

        public async Task<Trade[]> FetchMyTrades(DateTime since, IEnumerable<string> symbols = null, uint limit = 1000)
        {
            var markets = await FetchMarkets();

            if (symbols == null)
                symbols = markets.Select(m => m.Symbol);
            var symbolsHashSet = symbols.ToHashSet();

            var paramPairs = markets.Where(m => symbolsHashSet.Contains(m.Symbol)).Select(m => m.Id);

            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1_1,
                Path = "user_trades",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, string>()
                {
                    ["pair"] = String.Join(",", paramPairs),
                    ["offset"] = "0",
                    ["limit"] = limit.ToString(CultureInfo.InvariantCulture),
                }
            }).ConfigureAwait(false);

            throw new NotImplementedException();
        }

        public class ExmoBalance
        {
            public ulong uid { get; set; }
            public ulong server_date { get; set; }
            public Dictionary<string, string> balances { get; set; }
            public Dictionary<string, string> reserved { get; set; }
        }

        public class ExmoPairSettings
        {
            //{"min_quantity":"1","max_quantity":"100000000","min_price":"0.00000001","max_price":"1000","max_amount":"100000","min_amount":"0.01"}
            public string min_quantity { get; set; }
            public string max_quantity { get; set; }
            public string min_price { get; set; }
            public string max_price { get; set; }
            public string min_amount { get; set; }
            public string max_amount { get; set; }
        }

        public class ExmoTicker
        {
            //{"buy_price":"0.00321945","sell_price":"0.00328169","last_trade":"0.00325557","high":"0.00335492","low":"0.00321945","avg":"0.0032834","vol":"919448.73424402","vol_curr":"2993.32971574","updated":1583917213}
            public string buy_price { get; set; }
            public string sell_price { get; set; }
            public string last_trade { get; set; }
            public string high { get; set; }
            public string low { get; set; }
            public string avg { get; set; }
            public string vol { get; set; }
            public string vol_curr { get; set; }
            public ulong updated { get; set; }
        }

    }
}
