﻿using System;
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
    public class Cex : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker, IFetchBalance, IFetchMyTrades, IFetchOrders, IFetchOpenOrders, ICreateOrder
    {
        readonly Uri ApiPublicV1 = new Uri("https://cex.io/api/");
        readonly Uri ApiPrivateV1 = new Uri("https://cex.io/api/");

        public Cex(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://cex.io/cex-api
            //Please note that CEX.IO API is limited to 600 requests per 10 minutes
            RateLimit = (int)10 * 60 / 600 * 1000;
        }

        public async Task<CexCurrencyProfilesData> FetchCurrencies()
        {
            var detailsResponse = await Request(new Base.Request()
            {
                BaseUri = ApiPublicV1,
                Path = "currency_profile",
                ApiType = "public",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);
            var details = JsonSerializer.Deserialize<CexCurrencyProfiles>(detailsResponse.Text);

            return details.data;
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var currencies = await FetchCurrencies().ConfigureAwait(false);

                var limitsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV1,
                    Path = "currency_limits",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var limits = JsonSerializer.Deserialize<CexCurrencyLimits>(limitsResponse.Text);

                _markets = (from market in limits.data.First().Value
                            join pairRecord in currencies.pairs on $"{market.symbol1}/{market.symbol2}" equals $"{pairRecord.symbol1}/{pairRecord.symbol2}" into pairJoin
                            let pair = pairJoin.FirstOrDefault()
                            join baseCurrencyRecord in currencies.symbols on market.symbol1 equals baseCurrencyRecord.code into baseCurrencyJoin
                            let baseCurrency = baseCurrencyJoin.FirstOrDefault()
                            join quoteCurrencyRecord in currencies.symbols on market.symbol2 equals quoteCurrencyRecord.code into quoteCurrencyJoin
                            let quoteCurrency = quoteCurrencyJoin.FirstOrDefault()
                            where baseCurrency != null
                            where quoteCurrency != null
                            let amountPrecision = baseCurrency.precision - baseCurrency.scale
                            select new Market()
                            {
                                BaseId = market.symbol1,
                                QuoteId = market.symbol2,
                                Id = $"{market.symbol1}/{market.symbol2}",
                                Base = GetCommonCurrencyCode(market.symbol1),
                                Quote = GetCommonCurrencyCode(market.symbol2),

                                PricePrecision = pair == null ? quoteCurrency.precision : pair.pricePrecision,
                                AmountPrecision = amountPrecision,

                                AmountMin = market.minLotSize,
                                AmountMax = market.maxLotSize.HasValue ? market.maxLotSize.Value : decimal.MaxValue,

                                PriceMin = JsonSerializer.Deserialize<decimal>(market.minPrice),
                                PriceMax = JsonSerializer.Deserialize<decimal>(market.maxPrice),

                                CostMin = market.minLotSizeS2,

                                FeeMaker = 0.16M / 100,
                                FeeTaker = 0.25M / 100,

                                Url = $"https://cex.io/trade/{market.symbol1}-{market.symbol2}",

                                Info = new { market, pair, baseCurrency, quoteCurrency }
                            }).ToArray();
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

            var allCurrencies = from market in markets
                                group market by market.Quote into g
                                select g.Key;

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiPublicV1,
                Path = $"tickers/{String.Join('/', allCurrencies)}",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);
            var tickers = JsonSerializer.Deserialize<CexTickers>(response.Text);

            var result = new List<Ticker>();
            foreach (var item in tickers.data)
            {
                var market = markets.FirstOrDefault(m => m.Id == item.pair.Replace(':', '/'));
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.Timestamp = Convert.ToUInt64(JsonSerializer.Deserialize<decimal>(item.timestamp)); ;
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(ticker.Timestamp);

                ticker.Symbol = market.Symbol;
                ticker.High = JsonSerializer.Deserialize<decimal>(item.high);
                ticker.Low = JsonSerializer.Deserialize<decimal>(item.low);
                ticker.Bid = item.bid;
                ticker.Ask = item.ask;
                ticker.Last = JsonSerializer.Deserialize<decimal>(item.last);
                ticker.Close = ticker.Last;
                ticker.BaseVolume = JsonSerializer.Deserialize<decimal>(item.volume);
                ticker.QuoteVolume = ticker.BaseVolume * (ticker.High + ticker.Low) / 2;
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

            request.Params["key"] = ApiKey;
            request.Params["nonce"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            request.Params["signature"] = GetHmac<HMACSHA256>(request.Params["nonce"] + ApiUserId + ApiKey, ApiSecret);
        }


        public async Task<Dictionary<string, BalanceAccount>> FetchBalance()
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1,
                Path = "balance/",
                Method = HttpMethod.Post
            }).ConfigureAwait(false);

            var balances = JsonSerializer.Deserialize<Dictionary<string, object>>(response.Text);
            var result = (from balance in balances
                          let valueObject = (JsonElement)balance.Value
                          where valueObject.ValueKind == JsonValueKind.Object
                          select new { Asset = balance.Key, Balance = valueObject })
                          .ToDictionary(m => GetCommonCurrencyCode(m.Asset.ToUpper()),
                            m => new BalanceAccount()
                            {
                                Free = JsonSerializer.Deserialize<decimal>(m.Balance.GetProperty("available").GetString()),
                                Total = JsonSerializer.Deserialize<decimal>(m.Balance.GetProperty("available").GetString()) + JsonSerializer.Deserialize<decimal>(m.Balance.GetProperty("orders").GetString())
                            });
            return result;
        }

        public async Task<MyTrade[]> FetchMyTrades(DateTime since, IEnumerable<string> symbols = null, uint limit = 1000)
        {
            //Cex API does not contains get trades method
            var orders = await FetchOrders(since, symbols, limit);

            var result = (from order in orders
                          where order.Status == OrderStatus.Closed || (order.Status == OrderStatus.Open && order.Remaining != order.Amount)
                          let jsonOrder = (JsonElement)order.Info
                          let symbol2 = jsonOrder.GetProperty("symbol2").GetString()
                          //total amount in current currency (Maker)
                          let makerAmountCurrency2 = jsonOrder.TryGetProperty("ta:" + symbol2.ToUpper(), out var ta) ? Convert.ToDecimal(ta.GetString(), CultureInfo.InvariantCulture) : (decimal)0.0
                          //total amount in current currency (Taker)
                          let takerAmountCurrency2 = jsonOrder.TryGetProperty("tta:" + symbol2.ToUpper(), out var tta) ? Convert.ToDecimal(tta.GetString(), CultureInfo.InvariantCulture) : (decimal)0.0
                          let total = makerAmountCurrency2 + takerAmountCurrency2
                          let feeMaker = jsonOrder.TryGetProperty("fa:" + symbol2.ToUpper(), out var fa) ? Convert.ToDecimal(fa.GetString(), CultureInfo.InvariantCulture) : (decimal)0.0
                          let feeTaker = jsonOrder.TryGetProperty("tfa:" + symbol2.ToUpper(), out var tfa) ? Convert.ToDecimal(tfa.GetString(), CultureInfo.InvariantCulture) : (decimal)0.0
                          let feeCost = feeMaker + feeTaker
                          let feeRate = Math.Abs(Math.Round(feeCost / ((order.Amount - order.Remaining) * order.Price), 4))
                          select new MyTrade()
                          {
                              Id = order.Id,
                              OrderId = order.Id,
                              Timestamp = order.Timestamp,
                              DateTime = order.DateTime,
                              Symbol = order.Symbol,
                              Side = order.Side,
                              Amount = order.Amount - order.Remaining,
                              Price = total / (order.Amount - order.Remaining),
                              FeeCost = feeCost,
                              FeeCurrency = GetCommonCurrencyCode(symbol2),
                              FeeRate = feeRate,
                              Info = order.Info
                          }).ToArray();

            return result;
        }

        public async Task<Order[]> FetchOpenOrders(IEnumerable<string> symbols = null)
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1,
                Path = "open_orders/",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, object>() { }
            }).ConfigureAwait(false);

            var jsonResponse = JsonSerializer.Deserialize<CexOpenOrder[]>(response.Text);

            var markets = await FetchMarkets();

            var result = new List<Order>();
            foreach (var item in jsonResponse)
            {
                var market = markets.FirstOrDefault(m => m.Id == $"{item.symbol1}/{item.symbol2}");
                if (market == null)
                    continue;

                var order = new Order();
                order.Id = item.id;
                order.Timestamp = JsonSerializer.Deserialize<ulong>(item.time);
                order.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(order.Timestamp);
                order.Symbol = market.Symbol;
                order.Side = item.type == "buy" ? Side.Buy : Side.Sell;
                order.Amount = JsonSerializer.Deserialize<decimal>(item.amount);
                order.Price = JsonSerializer.Deserialize<decimal>(item.price);
                order.Cost = order.Amount * order.Price;
                order.Status = OrderStatus.Open;
                order.Type = OrderType.Limit;
                order.Remaining = JsonSerializer.Deserialize<decimal>(item.pending);
                order.Filled = order.Amount - order.Remaining;
                order.Info = item;

                result.Add(order);
            }
            return result.ToArray();
        }


        public async Task<Order[]> FetchOrders(DateTime since, IEnumerable<string> symbols = null, uint limit = 1000)
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1,
                Path = "archived_orders/",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, object>() { ["dateFrom"] = since.Subtract(new DateTime(1970, 1, 1)).TotalSeconds }
            }).ConfigureAwait(false);

            var markets = await FetchMarkets();

            var ordersJson = JsonSerializer.Deserialize<JsonElement[]>(response.Text);

            var result = new List<Order>();
            foreach (var item in ordersJson)
            {
                //Order status ('d' = done, fully executed OR 'c' = canceled, not executed OR 'cd' = cancel-done, partially executed OR 'a' = active, created)
                var statusProperty = item.GetProperty("status").GetString();
                var status = OrderStatus.Canceled;
                if (statusProperty == "d" || statusProperty == "cd")
                    status = OrderStatus.Closed;
                else if (statusProperty == "c")
                    status = OrderStatus.Canceled;
                else
                    status = OrderStatus.Open;

                var symbol1 = item.GetProperty("symbol1").GetString();
                var symbol2 = item.GetProperty("symbol2").GetString();

                var market = markets.FirstOrDefault(m => m.Id == $"{symbol1}/{symbol2}");
                if (market == null)
                    continue;

                var amount = Convert.ToDecimal(item.GetProperty("amount").GetString(), CultureInfo.InvariantCulture);
                //pending amount (if partially executed)
                var remains = Convert.ToDecimal(item.GetProperty("remains").GetString(), CultureInfo.InvariantCulture);
                var price = Convert.ToDecimal(item.GetProperty("price").GetString(), CultureInfo.InvariantCulture);

                var time = Convert.ToDateTime(item.GetProperty("time").GetString());

                var order = new Order()
                {
                    Id = item.GetProperty("id").GetString(),
                    Timestamp = (ulong)time.Subtract(new DateTime(1970, 1, 1)).TotalSeconds,
                    DateTime = time,
                    Symbol = market.Symbol,
                    Side = item.GetProperty("type").GetString() == "buy" ? Side.Buy : Side.Sell,
                    Status = status,
                    Amount = amount,
                    Price = price,
                    Cost = amount * price,
                    Remaining = remains,
                    Info = item
                };

                result.Add(order);
            }

            return result.ToArray();
        }

        public async Task<string> CreateOrder(string symbol, OrderType type, Side side, decimal amount, decimal price = 0)
        {
            var markets = await FetchMarkets();
            var market = markets.First(market => market.Symbol == symbol);

            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV1,
                Path = $"place_order/{market.BaseId}/{market.QuoteId}/",
                Method = HttpMethod.Post,
                Params = new Dictionary<string, object>() { 
                    ["type"] = side.ToString().ToLower(),
                    ["price"] = Math.Round(price, market.PricePrecision).ToString(CultureInfo.InvariantCulture),
                    ["amount"] = Math.Round(amount, 8).ToString(CultureInfo.InvariantCulture)
                }
            }).ConfigureAwait(false);

            if (response.Text.Contains("\"error\""))
                throw new ExchangeError(JsonSerializer.Deserialize<CexError>(response.Text).error);

            var createdOrder = JsonSerializer.Deserialize<CexPlaceOrder>(response.Text);
            return createdOrder.id;
        }


        public class CexError
        {
            public string error { get; set; }
        }


        public class CexPlaceOrder
        {
            public bool complete;
            public string id;
            public decimal time;
            public decimal pending;
            public decimal amount;
            public string type;
            public decimal price;
        }

        public class CexOpenOrder
        {
            public string id { get; set; }
            public string time { get; set; }
            public string type { get; set; }
            public string price { get; set; }
            public string amount { get; set; }
            public string pending { get; set; }
            public string symbol1 { get; set; }
            public string symbol2 { get; set; }
        }

        public class CexCurrencyProfiles
        {
            public string e { get; set; }
            public string ok { get; set; }
            public CexCurrencyProfilesData data { get; set; }
        }

        public class CexCurrencyProfilesData
        {
            public CexCurrencySymbol[] symbols { get; set; }
            public CexCurrencyPair[] pairs { get; set; }
        }


        public class CexCurrencyPair
        {
            //{"symbol1":"ETH","symbol2":"USD","pricePrecision":2,"priceScale":"/10000","minLotSize":0.1,"minLotSizeS2":20}
            public string symbol1 { get; set; }
            public string symbol2 { get; set; }
            public int pricePrecision { get; set; }
            public string priceScale { get; set; }
            public decimal minLotSize { get; set; }
            public decimal minLotSizeS2 { get; set; }
        }


        public class CexCurrencySymbol
        {
            //{"code":"GHS","contract":true,"commodity":true,"fiat":false,"description":"CEX.IO doesn't provide cloud mining services anymore.","precision":8,"scale":0,"minimumCurrencyAmount":"0.00000001","minimalWithdrawalAmount":-1}
            public string code { get; set; }
            public bool contract { get; set; }
            public bool commodity { get; set; }
            public bool fiat { get; set; }
            public string description { get; set; }
            public int precision { get; set; }
            public int scale { get; set; }
            public string minimumCurrencyAmount { get; set; }
            //Some values are string, some decimals -1
            //public string minimalWithdrawalAmount { get; set; }
        }


        public class CexCurrencyLimits
        {
            public string e { get; set; }
            public string ok { get; set; }
            public Dictionary<string, CexCurrencyLimit[]> data { get; set; }
        }

        public class CexCurrencyLimit
        {
            public string symbol1 { get; set; }
            public string symbol2 { get; set; }
            public decimal minLotSize { get; set; }
            public decimal minLotSizeS2 { get; set; }
            public decimal? maxLotSize { get; set; } // Null for some pairs
            public string minPrice { get; set; }
            public string maxPrice { get; set; }
        }

        public class CexTickers
        {
            public string e { get; set; }
            public string ok { get; set; }
            public CexTicker[] data { get; set; }
        }

        public class CexTicker
        {
            //{"timestamp":"1583924608","pair":"BTC:USD","low":"7745.3","high":"8167.9","last":"7854.1","volume":"214.93431824","volume30d":"4179.52263660","priceChange":"-225.4","priceChangePercentage":"-2.79","bid":7833.9,"ask":7853.8}
            public string timestamp { get; set; }
            public string pair { get; set; }
            public string low { get; set; }
            public string high { get; set; }
            public string last { get; set; }
            public string volume { get; set; }
            public string volume30d { get; set; }
            public string priceChange { get; set; }
            public string priceChangePercentage { get; set; }
            public decimal bid { get; set; }
            public decimal ask { get; set; }
        }

    }
}
