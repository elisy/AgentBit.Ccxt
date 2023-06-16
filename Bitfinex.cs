using System;
using System.Collections.Generic;
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
    public class Bitfinex : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker, IFetchBalance, IFetchMyTrades, IFetchOpenOrders, ICreateOrder
    {
        readonly Uri ApiPublicV1 = new Uri("https://api.bitfinex.com/v1/");
        readonly Uri ApiPublicV2 = new Uri("https://api-pub.bitfinex.com/v2/");

        readonly Uri ApiPrivateV1 = new Uri("https://api.bitfinex.com");
        readonly Uri ApiPrivateV2 = new Uri("https://api.bitfinex.com/");

        public Bitfinex(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://bitcoin.stackexchange.com/questions/36952/bitfinex-api-limit
            //The limit is measured per IP address and per account. So for one account (regardless of the number of key/secret pairs) 
            //60 requests per minute can be made via our API.
            RateLimit = 60 / 60 * 1000;

            CommonCurrencies = new Dictionary<string, string>() {
                { "ABS", "ABYSS" },
                { "AIO", "AION" },
                { "ALG", "ALGO" },
                { "AMP", "AMPL" },
                { "ATM", "ATMI" },
                { "ATO", "ATOM" },
                { "BAB", "BCH" },
                { "CTX", "CTXC" },
                { "DAD", "DADI" },
                { "DAT", "DATA" },
                { "DOG", "MDOGE" },
                { "DSH", "DASH" },
                { "DRK", "DRK" },
                { "EDO", "PNT" },
                { "EUT", "EURT" },
                { "EUS", "EURS" },
                { "GSD", "GUSD" },
                { "HOT", "Hydro Protocol" },
                { "IOS", "IOST" },
                { "IOT", "IOTA" },
                { "IQX", "IQ" },
                { "LUNA", "LUNC" },
                { "LUNA2", "LUNA" },
                { "MIT", "MITH" },
                { "MNA", "MANA" },
                { "NCA", "NCASH" },
                { "ORS", "ORS Group" },
                { "POY", "POLY" },
                { "QSH", "QASH" },
                { "QTM", "QTUM" },
                { "SEE", "SEER" },
                { "SNG", "SNGLS" },
                { "SPK", "SPANK" },
                { "STJ", "STORJ" },
                { "TSD", "TUSD" },
                { "TERRAUST", "USTC" },
                { "YGG", "YEED" }, // conflict with Yield Guild Games
                { "YYW", "YOYOW" },
                { "UDC", "USDC" },
                { "UST", "USDT" },
                { "UTN", "UTNP" },
                { "VSY", "VSYS" },
                { "WAX", "WAXP" },
                { "WBT", "WBTC" },
                { "WHBT", "WBT" },
                { "XCH", "XCHF" },
                { "ZBT", "ZB" }
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var idsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = "symbols",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var ids = JsonSerializer.Deserialize<string[]>(idsResponse.Text);

                var detailsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = "symbols_details",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var details = JsonSerializer.Deserialize<BitfinexSymbolDetails[]>(detailsResponse.Text);

                var result = new List<Market>();

                foreach (var market in details.Where(m => ids.Contains(m.pair)))
                {
                    var newItem = new Market();
                    newItem.Id = market.pair.ToUpper();
                    if (newItem.Id.IndexOf(':') > 0)
                    {
                        var parts = newItem.Id.Split(new char[] { ':' });
                        newItem.BaseId = parts[0];
                        newItem.QuoteId = parts[1];
                    }
                    else
                    {
                        newItem.BaseId = newItem.Id.Substring(0, 3);
                        newItem.QuoteId = newItem.Id.Substring(3, 3);
                    }
                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.PricePrecision = market.price_precision;
                    newItem.AmountMin = JsonSerializer.Deserialize<decimal>(market.minimum_order_size);
                    newItem.AmountMax = JsonSerializer.Deserialize<decimal>(market.maximum_order_size);
                    newItem.PriceMin = (decimal)Math.Pow(10, -newItem.PricePrecision);
                    newItem.PriceMax = (decimal)Math.Pow(10, newItem.PricePrecision);
                    newItem.CostMin = newItem.AmountMin * newItem.PriceMin;

                    newItem.FeeMaker = 0.1M / 100;
                    newItem.FeeTaker = 0.2M / 100;

                    newItem.Url = $"https://trading.bitfinex.com/t/{newItem.BaseId}:{newItem.QuoteId}?refcode=BbA2Zpxdo";
                    newItem.Margin = market.margin;

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

            var symbolsString = symbols == null ? "ALL" : String.Join(',', markets.Where(m => symbols.Contains(m.Symbol)).Select(m => "t" + m.Id));

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiPublicV1,
                Path = $"tickers?symbols={symbolsString}",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);
            var tickers = JsonSerializer.Deserialize<BitfinexTicker[]>(response.Text);

            var result = new List<Ticker>();
            foreach (var item in tickers)
            {
                Ticker ticker = new Ticker();

                //ticker.Timestamp = (uint)(ticker.DateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
                ticker.Timestamp = Convert.ToUInt64(JsonSerializer.Deserialize<decimal>(item.timestamp) * 1000);
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ticker.Timestamp);

                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.pair);
                if (market == null)
                    continue;

                ticker.Symbol = market.Symbol;
                ticker.High = JsonSerializer.Deserialize<decimal>(item.high);
                ticker.Low = JsonSerializer.Deserialize<decimal>(item.low);
                ticker.Bid = JsonSerializer.Deserialize<decimal>(item.bid);
                ticker.Ask = JsonSerializer.Deserialize<decimal>(item.ask);
                ticker.Last = JsonSerializer.Deserialize<decimal>(item.last_price);
                ticker.Close = ticker.Last;
                ticker.Average = JsonSerializer.Deserialize<decimal>(item.mid);
                ticker.BaseVolume = JsonSerializer.Deserialize<decimal>(item.volume);
                ticker.QuoteVolume = ticker.BaseVolume * JsonSerializer.Deserialize<decimal>(item.mid);
                ticker.Info = item;

                result.Add(ticker);
            }
            if (symbols == null)
                return result.ToArray();
            else
                return result.Where(m => symbols.Contains(m.Symbol)).ToArray();
        }

        /// <summary>
        /// FetchBalanse uses API v1
        /// </summary>
        /// <param name="request"></param>
        public void SignV1(Request request)
        {
            request.Params["request"] = request.Path;
            request.Params["nonce"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            string jsonString = JsonSerializer.Serialize(request.Params);
            string json64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(jsonString));

            request.Headers.Add("X-BFX-APIKEY", ApiKey);
            request.Headers.Add("X-BFX-PAYLOAD", json64);
            request.Headers.Add("X-BFX-SIGNATURE", GetHmac<HMACSHA384>(json64, ApiSecret));
        }

        public void SignV2(Request request)
        {
            var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            var data = $"/api/{request.Path}{nonce}";
            if (request.Params.Count != 0)
                data += JsonSerializer.Serialize(request.Params);
            var signature = GetHmac<HMACSHA384>(data, ApiSecret);
            request.Headers.Add("bfx-nonce", nonce);
            request.Headers.Add("bfx-apikey", ApiKey);
            request.Headers.Add("bfx-signature", signature);
        }


        public override void Sign(Request request)
        {
            if (request.ApiType != "private")
                return;

            if (request.Path.StartsWith("/v1/"))
                SignV1(request);
            else if (request.Path.StartsWith("v2/"))
                SignV2(request);
        }


        public async Task<Dictionary<string, BalanceAccount>> FetchBalance()
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1,
                Path = "/v1/balances",
                Method = HttpMethod.Post
            }).ConfigureAwait(false);
            var balances = JsonSerializer.Deserialize<BitfinexBalance[]>(response.Text);

            var result = new Dictionary<string, BalanceAccount>();
            foreach (var balance in balances.Where(m => m.type == "exchange"))
            {
                result[GetCommonCurrencyCode(balance.currency.ToUpper())] = new BalanceAccount()
                {
                    Free = JsonSerializer.Deserialize<decimal>(balance.available),
                    Total = JsonSerializer.Deserialize<decimal>(balance.amount)
                };
            }
            return result;
        }

        public async Task<MyTrade[]> FetchMyTrades(DateTime since, IEnumerable<string> symbols = null, uint limit = 100)
        {
            //https://docs.bitfinex.com/reference#rest-auth-trades
            //Number of records (Max: 2500)
            if (limit > 2500)
                limit = 2500;

            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV2,
                Path = "v2/auth/r/trades/hist",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, object>()
                {
                    ["start"] = since.Subtract(new DateTime(1970, 1, 1)).TotalMilliseconds.ToString(CultureInfo.InvariantCulture),
                    ["limit"] = limit
                }
            }).ConfigureAwait(false);

            var markets = await FetchMarkets();

            var ordersJson = JsonSerializer.Deserialize<JsonElement[][]>(response.Text);

            var result = new List<MyTrade>();
            foreach (var item in ordersJson)
            {
                var market = markets.FirstOrDefault(m => "t" + m.Id == item[1].GetString());
                if (market == null)
                    continue;

                var myTrade = new MyTrade
                {
                    Id = item[0].GetUInt64().ToString(CultureInfo.InvariantCulture),
                    Timestamp = item[2].GetUInt64(),
                    DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(item[2].GetUInt64()),
                    Symbol = market.Symbol,
                    OrderId = item[3].GetUInt64().ToString(CultureInfo.InvariantCulture),
                    Side = item[4].GetDecimal() > 0 ? Side.Buy : Side.Sell,
                    Amount = Math.Abs(item[4].GetDecimal()),
                    Price = item[5].GetDecimal(),
                    FeeCost = Math.Abs(item[9].GetDecimal()),
                    FeeCurrency = GetCommonCurrencyCode(item[10].GetString()),
                    Info = item
                };

                if (myTrade.FeeCurrency == market.Base)
                    myTrade.FeeRate = Math.Abs(Math.Round(myTrade.FeeCost / myTrade.Amount, 4));
                else
                    myTrade.FeeRate = Math.Abs(Math.Round(myTrade.FeeCost / (myTrade.Amount * myTrade.Price), 4));

                result.Add(myTrade);
            }

            return result.ToArray();
        }

        public async Task<Order[]> FetchOpenOrders(IEnumerable<string> symbols = null)
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV2,
                Path = "v2/auth/r/orders/",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, object>()
                {
                }
            }).ConfigureAwait(false);

            var markets = await FetchMarkets();

            var ordersJson = JsonSerializer.Deserialize<JsonElement[][]>(response.Text);

            var result = new List<Order>();
            foreach (var item in ordersJson)
            {
                var market = markets.FirstOrDefault(m => "t" + m.Id == item[3].GetString());
                if (market == null)
                    continue;

                var order = new Order();
                order.Id = item[0].GetUInt64().ToString(CultureInfo.InvariantCulture);
                order.ClientId = item[2].GetInt64().ToString(CultureInfo.InvariantCulture);
                order.Timestamp = item[5].GetUInt64();
                order.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(order.Timestamp);
                order.Symbol = market.Symbol;
                order.Side = item[7].GetDecimal() > 0 ? Side.Buy : Side.Sell;
                order.Remaining = Math.Abs(item[6].GetDecimal());
                order.Amount = Math.Abs(item[7].GetDecimal());
                order.Filled = order.Amount - order.Remaining;
                order.Price = item[16].GetDecimal();
                order.Cost = order.Price * order.Amount;

                var status = item[13].GetString();
                if (status == "ACTIVE")
                    order.Status = OrderStatus.Open;
                else if (status.Contains("PARTIALLY FILLED", StringComparison.InvariantCulture))
                    order.Status = OrderStatus.Open;
                else if (status == "EXECUTED")
                    order.Status = OrderStatus.Closed;
                else
                    order.Status = OrderStatus.Canceled;

                var type = item[8].GetString();
                if (type == "LIMIT")
                    order.Type = OrderType.Limit;
                else if (type == "MARKET")
                    order.Type = OrderType.Market;
                else
                    order.Type = OrderType.Other;

                order.Info = item;

                result.Add(order);
            }

            return result.Where(m => m.Status == OrderStatus.Open).ToArray();
        }

        public async Task<string> CreateOrder(string symbol, OrderType type, Side side, decimal amount, decimal price = 0)
        {
            var markets = await FetchMarkets();
            var market = markets.First(market => market.Symbol == symbol);

            var amountParameter = side == Side.Buy ? amount : -amount;

            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV2,
                Path = "v2/auth/w/order/submit",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, object>()
                {
                    ["pair"] = market.Id,
                    ["type"] = $"EXCHANGE {type.ToString().ToUpper()}",
                    ["price"] = price.ToString(CultureInfo.InvariantCulture),
                    ["amount"] = amountParameter.ToString(CultureInfo.InvariantCulture),
                    ["meta"] = new Dictionary<string, string>()
                    {
                        ["aff_code"] = "BbA2Zpxdo"
                    }
                }
            }).ConfigureAwait(false);

            var ordersJson = JsonSerializer.Deserialize<JsonElement[]>(response.Text);
            var orderJson = ordersJson[4].EnumerateArray().First().EnumerateArray().ToArray();

            return orderJson[0].GetUInt64().ToString(CultureInfo.InvariantCulture);
        }

        public class BitfinexBalance
        {
            //{"type":"deposit","currency":"usd","amount":"7831.30410987","available":"0.000001"}
            public string type { get; set; }
            public string currency { get; set; }
            public string amount { get; set; }
            public string available { get; set; }
        }

        public class BitfinexSymbolDetails
        {
            //{"pair":"btcusd","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"2000.0","minimum_order_size":"0.0006","expiration":"NA","margin":true},{"pair":"ltcusd","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"5000.0","minimum_order_size":"0.1","expiration":"NA","margin":true},{"pair":"ltcbtc","price_precision":5,"initial_margin":"30.0","minimum_margin":"15.0","maximum_order_size":"5000.0","minimum_order_size":"0.1","expiration":"NA","margin":true},{"pair":"ethusd","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"5000.0","minimum_order_size":"0.02","expiration":"NA","margin":true},{"pair":"ethbtc","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"5000.0","minimum_order_size":"0.02","expiration":"NA","margin":true},{"pair":"etcbtc","price_precision":5,"initial_margin":"30.0","minimum_margin":"15.0","maximum_order_size":"100000.0","minimum_order_size":"0.6","expiration":"NA","margin":true}
            public string pair { get; set; }
            public int price_precision { get; set; }
            public string maximum_order_size { get; set; }
            public string minimum_order_size { get; set; }
            public bool margin { get; set; }
        }

        public class BitfinexTicker
        {
            //{"mid":"8871.95","bid":"8871.9","ask":"8872.0","last_price":"8872.0","low":"8739.1","high":"8990.8","volume":"5951.993828259999","timestamp":"1583233141.23957302","pair":"BTCUSD"},{"mid":"62.0715","bid":"62.058","ask":"62.085","last_price":"62.023","low":"59.382","high":"62.474","volume":"51998.729606729999","timestamp":"1583233141.239823921","pair":"LTCUSD"},{"mid":"0.00699215","bid":"0.0069866","ask":"0.0069977","last_price":"0.0069966","low":"0.0067904","high":"0.0070141","volume":"4081.41011916","timestamp":"1583233141.24008307","pair":"LTCBTC"},{"mid":"230.57","bid":"230.54","ask":"230.6","last_price":"230.48","low":"223.29","high":"235.34","volume":"79020.87426416","timestamp":"1583233141.240407872","pair":"ETHUSD"},{"mid":"0.02599","bid":"0.02598","ask":"0.026","last_price":"0.025997","low":"0.025535","high":"0.026298","volume":"4437.472426569999","timestamp":"1583233141.24069981","pair":"ETHBTC"},{"mid":"0.00094471","bid":"0.00094347","ask":"0.00094595","last_price":"0.0009446","low":"0.00092219","high":"0.00097385","volume":"10351.683375469999","timestamp":"1583233141.240970383","pair":"ETCBTC"},{"mid":"8.3829","bid":"8.3776","ask":"8.3882","last_price":"8.3961","low":"8.1062","high":"8.68","volume":"130705.13379812","timestamp":"1583233141.241201122","pair":"ETCUSD"},{"mid":"0.0234735","bid":"0.022948","ask":"0.023999","last_price":"0.023987","low":"0.022476","high":"0.023987","volume":"572.267","timestamp":"1583233141.241507786","pair":"RRTUSD"},{"mid":"0.00000291","bid":"0.00000248","ask":"0.00000334","last_price":"0.00000247","low":"0.00000247","high":"0.00000319","volume":"614.92","timestamp":"1583233141.241728096","pair":"RRTBTC"},{"mid":"51.6825","bid":"51.614","ask":"51.751","last_price":"51.673","low":"50.539","high":"53.076","volume":"4963.188879729999","timestamp":"1583233141.242095041","pair":"ZECUSD"},{"mid":"0.0058247","bid":"0.0058181","ask":"0.0058313","last_price":"0.0058365","low":"0.0057622","high":"0.005937","volume":"1414.512698769999","timestamp":"1583233141.242405659","pair":"ZECBTC"}
            public string mid { get; set; }
            public string bid { get; set; }
            public string ask { get; set; }
            public string last_price { get; set; }
            public string low { get; set; }
            public string high { get; set; }
            public string volume { get; set; }
            public string timestamp { get; set; }
            public string pair { get; set; }
        }

    }
}
