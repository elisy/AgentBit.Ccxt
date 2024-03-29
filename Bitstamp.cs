﻿using System;
using System.Collections.Concurrent;
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
    public class Bitstamp : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker, IFetchBalance
    {
        readonly Uri ApiPublicV2 = new Uri("https://www.bitstamp.net/api/v2/");
        readonly Uri ApiPrivateV2 = new Uri("https://www.bitstamp.net/api/v2/");

        public Bitstamp(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //https://www.bitstamp.net/api/
            //Do not make more than 8000 requests per 10 minutes or we will ban your IP address.
            RateLimit = 10 * 60 / 8000 * 1000;
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var pairsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV2,
                    Path = "trading-pairs-info",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var pairs = JsonSerializer.Deserialize<PairsInfo[]>(pairsResponse.Text);

                var result = new List<Market>();

                foreach(var market in pairs)
                {
                    var newItem = new Market();

                    var name = market.name.Split('/');
                    newItem.BaseId = name[0];
                    newItem.QuoteId = name[1];
                    newItem.Id = newItem.BaseId + "/" + newItem.QuoteId;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.AmountPrecision = market.base_decimals;
                    newItem.PricePrecision = market.counter_decimals;
                    newItem.Active = market.trading == "Enabled";

                    var parts = market.minimum_order.Split(' ');
                    newItem.CostMin = JsonSerializer.Deserialize<decimal>(parts[0]);

                    newItem.FeeMaker = 0.5M / 100;
                    newItem.FeeTaker = 0.5M / 100;

                    newItem.Url = $"https://www.bitstamp.net/markets/{newItem.BaseId}/{newItem.QuoteId}/";

                    result.Add(newItem);
                }

                _markets = result.ToArray();
            }
            return _markets;
        }


        public async Task<Ticker> FetchTicker(string symbol)
        {
            var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Symbol == symbol);
            if (market == null)
                return null;

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiPublicV2,
                Path = $"ticker/{market.BaseId.ToLower()}{market.QuoteId.ToLower()}/",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var ticker = JsonSerializer.Deserialize<BitstampTicker>(response.Text);
            
            var result = new Ticker();
            result.Symbol = market.Symbol;
            result.Timestamp = Convert.ToUInt64(JsonSerializer.Deserialize<decimal>(ticker.timestamp));
            result.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(result.Timestamp);
            result.High = JsonSerializer.Deserialize<decimal>(ticker.high);
            result.Low = JsonSerializer.Deserialize<decimal>(ticker.low);
            result.Bid = JsonSerializer.Deserialize<decimal>(ticker.bid);
            result.Ask = JsonSerializer.Deserialize<decimal>(ticker.ask);
            result.Vwap = JsonSerializer.Deserialize<decimal>(ticker.vwap);
            result.Open = JsonSerializer.Deserialize<decimal>(ticker.open);
            result.Last = JsonSerializer.Deserialize<decimal>(ticker.last);
            result.Close = result.Last;
            result.BaseVolume = JsonSerializer.Deserialize<decimal>(ticker.volume);
            result.QuoteVolume = result.BaseVolume * result.Vwap;
            result.Info = ticker;

            return result;
        }

        public async Task<Ticker[]> FetchTickers(string[] symbols = null)
        {
            if (symbols == null)
                symbols = (await FetchMarkets().ConfigureAwait(false)).Where(m => m.Active).Select(m => m.Symbol).ToArray();

            //var result = new List<Ticker>(symbols.Length);
            //foreach (var symbol in symbols)
            //    result.Add(await FetchTicker(symbol).ConfigureAwait(false));
            //return result.ToArray();
            var result = new ConcurrentBag<Ticker>();
            var tasks = symbols.Select(async symbol => {
                result.Add(await FetchTicker(symbol).ConfigureAwait(false));
            }).ToArray();
            await Task.WhenAll(tasks);
            return result.ToArray();
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
            var nonce = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
            request.Params["key"] = ApiKey;
            request.Params["signature"] = GetHmac<HMACSHA256>($"{nonce}{ApiUserId}{ApiKey}", ApiSecret).ToUpper();
            request.Params["nonce"] = nonce;
        }


        public async Task<Dictionary<string, BalanceAccount>> FetchBalance()
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiPrivateV2,
                Path = "balance/",
                Method = HttpMethod.Post
            }).ConfigureAwait(false);
            var balances = JsonSerializer.Deserialize<Dictionary<string, string>>(response.Text);

            var result = new Dictionary<string, BalanceAccount>();

            var markets = await FetchMarkets();
            var currencies = markets.Select(m => m.BaseId).Concat(markets.Select(m => m.QuoteId)).GroupBy(m => m).Select(m => m.Key.ToLower());

            foreach (var currencyId in currencies)
            {
                var account = new BalanceAccount();
                if (balances.ContainsKey(currencyId + "_available"))
                    account.Free = JsonSerializer.Deserialize<decimal>(balances[currencyId + "_available"]);
                if (balances.ContainsKey(currencyId + "_balance"))
                    account.Total = JsonSerializer.Deserialize<decimal>(balances[currencyId + "_balance"]);
                if (account.Free != 0 || account.Total != 0)
                    result[GetCommonCurrencyCode(currencyId.ToUpper())] = account;
            }

            return result;
        }

        public class PairsInfo
        {
            public string name { get; set; }
            public int base_decimals { get; set; }
            public string minimum_order { get; set; }
            public int counter_decimals { get; set; }
            public string trading { get; set; }
            public string url_symbol { get; set; }
            public string description { get; set; }
        }

        public class BitstampTicker
        {
            //{"high": "8158.42", "last": "8033.58", "timestamp": "1583853488", "bid": "8033.70", "vwap": "7901.42", "volume": "8157.55847320", "low": "7636.00", "ask": "8044.18", "open": "7937.20"}
            public string high { get; set; }
            public string last { get; set; }
            public string timestamp { get; set; }
            public string bid { get; set; }
            public string vwap { get; set; }
            public string volume { get; set; }
            public string low { get; set; }
            public string ask { get; set; }
            public string open { get; set; }
        }

    }
}
