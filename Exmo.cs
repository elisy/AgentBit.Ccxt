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
    public class Exmo : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker, IFetchBalance, IFetchMyTrades, IFetchOpenOrders
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

                    newItem.Url = $"https://exmo.com/en/trade/{newItem.BaseId}_{newItem.QuoteId}?ref=931291";

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
                request.Body = new FormUrlEncodedContent(request.Params.ToDictionary(m => m.Key, m => Convert.ToString(m.Value, CultureInfo.InvariantCulture)));
            }
        }


        public override void Sign(Request request)
        {
            if (request.ApiType != "private")
                return;

            request.Params["nonce"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            //Get Post string identical to SetBody method
            var formContent = new FormUrlEncodedContent(request.Params.ToDictionary(m => m.Key, m => Convert.ToString(m.Value, CultureInfo.InvariantCulture)));
            var postData = formContent.ReadAsStringAsync().Result;

            request.Headers.Add("Sign", GetHmac<HMACSHA512>(postData, ApiSecret));
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

        public async Task<MyTrade[]> FetchMyTrades(DateTime since, IEnumerable<string> symbols = null, uint limit = 1000)
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
                Params = new Dictionary<string, object>()
                {
                    ["pair"] = String.Join(",", paramPairs),
                    ["offset"] = "0",
                    ["limit"] = limit.ToString(CultureInfo.InvariantCulture),
                }
            }).ConfigureAwait(false);

            var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, ExmoMyTrade[]>>(response.Text);

            var result = new List<MyTrade>();

            foreach (var kvp in jsonResponse.Where(m => m.Value.Length != 0))
            {
                var market = markets.FirstOrDefault(m => m.Id == kvp.Key);
                if (market == null)
                    continue;

                foreach (var item in kvp.Value)
                {
                    var myTrade = new MyTrade
                    {
                        Id = item.trade_id.ToString(CultureInfo.InvariantCulture),
                        Timestamp = item.date,
                        DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(item.date),
                        Symbol = market.Symbol,
                        OrderId = item.order_id.ToString(CultureInfo.InvariantCulture),
                        TakerOrMaker = item.exec_type == "maker" ? TakerOrMaker.Maker : TakerOrMaker.Taker,
                        Side = item.type == "buy" ? Side.Buy : Side.Sell,
                        Price = JsonSerializer.Deserialize<decimal>(item.price),
                        Amount = JsonSerializer.Deserialize<decimal>(item.quantity),
                        Info = item
                    };

                    if (!String.IsNullOrEmpty(item.commission_amount))
                        myTrade.FeeCost = JsonSerializer.Deserialize<decimal>(item.commission_amount);

                    if (!String.IsNullOrEmpty(item.commission_currency))
                        myTrade.FeeCurrency = GetCommonCurrencyCode(item.commission_currency);
                    if (String.IsNullOrEmpty(myTrade.FeeCurrency))
                        myTrade.FeeCurrency = myTrade.Side == Side.Buy ? market.Quote : market.Base;

                    if (!String.IsNullOrEmpty(item.commission_percent))
                        myTrade.FeeRate = JsonSerializer.Deserialize<decimal>(item.commission_percent) / 100;

                    result.Add(myTrade);
                }
            }

            return result.ToArray();
        }

        public async Task<Order[]> FetchOpenOrders(IEnumerable<string> symbols = null)
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1_1,
                Path = "user_open_orders",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, object>()
                {
                }
            }).ConfigureAwait(false);

            var jsonResponse = JsonSerializer.Deserialize<Dictionary<string, ExmoOpenOrder[]>>(response.Text);

            var markets = await FetchMarkets();

            var result = new List<Order>();
            foreach (var kvp in jsonResponse.Where(m => m.Value.Length != 0))
            {
                var market = markets.FirstOrDefault(m => m.Id == kvp.Key);
                if (market == null)
                    continue;

                foreach (var item in kvp.Value)
                {
                    var order = new Order();
                    order.Id = item.order_id;
                    order.Timestamp = JsonSerializer.Deserialize<ulong>(item.created);
                    order.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(order.Timestamp);
                    order.Symbol = market.Symbol;
                    order.Side = item.type == "buy" ? Side.Buy : Side.Sell;
                    order.Amount = JsonSerializer.Deserialize<decimal>(item.quantity);
                    order.Price = JsonSerializer.Deserialize<decimal>(item.price);
                    order.Cost = JsonSerializer.Deserialize<decimal>(item.amount);
                    order.Status = OrderStatus.Open;
                    order.Type = OrderType.Limit;
                    order.Info = item;

                    result.Add(order);
                }
            }

            return result.ToArray();
        }


        public class ExmoOpenOrder
        {
            public string order_id { get; set; }
            public string parent_order_id { get; set; }
            public string client_id { get; set; }
            public string created { get; set; }
            public string type { get; set; }
            public string pair { get; set; }
            public string quantity { get; set; }
            public string price { get; set; }
            public string trigger_price { get; set; }
            public string amount { get; set; }
        }


        public class ExmoMyTrade
        {
            //{"trade_id":183471457,"date":1593599974,"type":"buy","pair":"BTG_BTC","order_id":7127500537,
            //"quantity":"8.44695045","price":"0.001215","amount":"0.01026304","exec_type":"maker",
            //"commission_amount":"0.0337878","commission_currency":"BTG","commission_percent":"0.4"}
            public ulong trade_id { get; set; }
            public ulong date { get; set; }
            public string type { get; set; }
            public string pair { get; set; }
            public ulong order_id { get; set; }
            public string quantity { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string exec_type { get; set; }
            public string commission_amount { get; set; }
            public string commission_currency { get; set; }
            public string commission_percent { get; set; }
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
