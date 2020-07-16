using System;
using System.Collections.Generic;
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
    public class Binance : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker, IFetchBalance
    {
        readonly Uri ApiV3 = new Uri("https://api.binance.com/api/v3/");

        private int _xMbxUsedWeight = 0;

        public Binance(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            //Binance overrides Throttle method to support weighted rate limit
            RateLimit = 1 * 1000;

            CommonCurrencies = new Dictionary<string, string>() {
                { "YOYO", "YOYOW" }
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV3,
                    Path = "exchangeInfo",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<BinanceExchangeInfo>(response.Text);
                var markets = responseJson.symbols;

                var result = new List<Market>();

                foreach (var market in markets)
                {
                    var newItem = new Market();
                    newItem.Id = market.symbol;
                    newItem.BaseId = market.baseAsset;
                    newItem.QuoteId = market.quoteAsset;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId);
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId);

                    newItem.PricePrecision = market.quotePrecision;
                    newItem.AmountPrecision = market.baseAssetPrecision;

                    var priceFilter = market.filters.FirstOrDefault(m => m.ContainsKey("filterType") && m["filterType"].ToString() == "PRICE_FILTER");
                    if (priceFilter != null)
                    {
                        newItem.PriceMin = JsonSerializer.Deserialize<double>(priceFilter["minPrice"].ToString());
                        newItem.PriceMax = JsonSerializer.Deserialize<double>(priceFilter["maxPrice"].ToString());
                    }

                    var lotFilter = market.filters.FirstOrDefault(m => m.ContainsKey("filterType") && m["filterType"].ToString() == "LOT_SIZE");
                    if (lotFilter != null)
                    {
                        newItem.AmountPrecision = (int)Math.Abs(Math.Log10(JsonSerializer.Deserialize<double>(lotFilter["stepSize"].ToString())));
                        newItem.AmountMin = JsonSerializer.Deserialize<double>(lotFilter["minQty"].ToString());
                        newItem.AmountMax = JsonSerializer.Deserialize<double>(lotFilter["maxQty"].ToString());
                    }

                    var minNotional = market.filters.FirstOrDefault(m => m.ContainsKey("filterType") && m["filterType"].ToString() == "MIN_NOTIONAL");
                    if (minNotional != null)
                    {
                        newItem.CostMin = JsonSerializer.Deserialize<double>(minNotional["minNotional"].ToString());
                    }

                    newItem.Active = (market.status == "TRADING");

                    newItem.FeeMaker = 0.1 / 100;
                    newItem.FeeTaker = 0.1 / 100;

                    newItem.Url = $"https://www.binance.com/en/trade/{@newItem.BaseId}_{@newItem.QuoteId}?ref=28257151";

                    result.Add(newItem);
                }

                _markets = result.ToArray();
            }
            return _markets;
        }


        public override async Task Throttle()
        {
            //https://github.com/binance-exchange/binance-official-api-docs/blob/master/rest-api.md
            //{
            //  "rateLimitType": "REQUEST_WEIGHT",
            //  "interval": "MINUTE",
            //  "intervalNum": 1,
            //  "limit": 1200
            //}
            var delay = RateLimit;
            if (_xMbxUsedWeight > 1000)
            {
                await _throttleSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    await Task.Delay(delay).ConfigureAwait(false);
                }
                finally
                {
                    _throttleSemaphore.Release();
                }
            }
        }


        public override async Task<Response> Request(Request request)
        {
            var result = await base.Request(request).ConfigureAwait(false);

            IEnumerable<string> values;
            if (result.HttpResponseMessage.Headers.TryGetValues("X-MBX-USED-WEIGHT", out values))
                _xMbxUsedWeight = Convert.ToInt32(values.First());

            return result;
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
                BaseUri = ApiV3,
                Path = $"ticker/24hr",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var tickers = JsonSerializer.Deserialize<BinanceTicker[]>(response.Text);
            var result = new List<Ticker>();

            foreach (var item in tickers)
            {
                Ticker ticker = new Ticker();

                //ticker.Timestamp = (uint)(ticker.DateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds
                ticker.Timestamp = item.closeTime;
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ticker.Timestamp);

                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.symbol);
                if (market == null)
                    continue;

                ticker.Symbol = market.Symbol;
                ticker.High = JsonSerializer.Deserialize<double>(item.highPrice);
                ticker.Low = JsonSerializer.Deserialize<double>(item.lowPrice);
                ticker.Bid = JsonSerializer.Deserialize<double>(item.bidPrice);
                ticker.BidVolume = JsonSerializer.Deserialize<double>(item.bidQty);
                ticker.Ask = JsonSerializer.Deserialize<double>(item.askPrice);
                ticker.AskVolume = JsonSerializer.Deserialize<double>(item.askQty);
                ticker.Vwap = JsonSerializer.Deserialize<double>(item.weightedAvgPrice);
                ticker.Last = JsonSerializer.Deserialize<double>(item.lastPrice);
                ticker.Close = ticker.Last;
                ticker.PreviousClose = JsonSerializer.Deserialize<double>(item.prevClosePrice);
                ticker.Change = JsonSerializer.Deserialize<double>(item.priceChange);
                ticker.Percentage = JsonSerializer.Deserialize<double>(item.priceChangePercent);

                ticker.BaseVolume = JsonSerializer.Deserialize<double>(item.volume);
                ticker.QuoteVolume = JsonSerializer.Deserialize<double>(item.quoteVolume);
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

            request.Headers.Add("X-MBX-APIKEY", ApiKey);

            var uri = new Uri(request.BaseUri, request.Path);
            var query = HttpUtility.ParseQueryString(uri.Query);
            query.Add("timestamp", DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
            query.Add("recvWindow", "5000");
            query.Add("signature", HmacSha256(query.ToString(), ApiSecret));
            request.Path = $"{uri.LocalPath}?{query.ToString()}";
        }



        public async Task<Dictionary<string, BalanceAccount>> FetchBalance()
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiV3,
                Path = "account",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var json = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response.Text);

            var result = new Dictionary<string, BalanceAccount>();
            foreach (var item in json["balances"].EnumerateArray())
            {
                //{"asset":"LTC","free":"0.01975000","locked":"0.00000000"}
                result[GetCommonCurrencyCode(item.GetProperty("asset").GetString().ToUpper())] = new BalanceAccount()
                {
                    Free = JsonSerializer.Deserialize<double>(item.GetProperty("free").ToString()),
                    Total = JsonSerializer.Deserialize<double>(item.GetProperty("free").ToString()) + JsonSerializer.Deserialize<double>(item.GetProperty("locked").ToString())
                };
            }
            return result;
        }

        public class BinanceExchangeInfoSymbol
        {
            public string symbol { get; set; }
            public string status { get; set; }
            public string maintMarginPercent { get; set; }
            public string requiredMarginPercent { get; set; }
            public string baseAsset { get; set; }
            public string quoteAsset { get; set; }
            public int pricePrecision { get; set; }
            public int quantityPrecision { get; set; }
            public int baseAssetPrecision { get; set; }
            public int quotePrecision { get; set; }
            public Dictionary<string, object>[] filters { get; set; }
            public string[] orderTypes { get; set; }
            public string[] timeInForce { get; set; }
        }

        public class BinanceExchangeInfo
        {
            public BinanceExchangeInfoSymbol[] symbols { get; set; }
        }

        public class BinanceTicker
        {
            //{"symbol":"ETHBTC","priceChange":"-0.00023700","priceChangePercent":"-0.916","weightedAvgPrice":"0.02573788","prevClosePrice":"0.02587900","lastPrice":"0.02564200",
            //"lastQty":"0.36900000","bidPrice":"0.02564100","bidQty":"7.50300000","askPrice":"0.02564300","askQty":"1.39100000","openPrice":"0.02587900","highPrice":"0.02614000","lowPrice":"0.02540900",
            //"volume":"160929.50800000","quoteVolume":"4141.98478695","openTime":1583226298907,"closeTime":1583312698907,"firstId":165962731,"lastId":166074512,"count":111782},{"symbol":"LTCBTC","priceChange":"-0.00002100","priceChangePercent":"-0.303","weightedAvgPrice":"0.00694754","prevClosePrice":"0.00692000","lastPrice":"0.00690000","lastQty":"7.87000000","bidPrice":"0.00690000","bidQty":"61.16000000","askPrice":"0.00690200","askQty":"10.08000000","openPrice":"0.00692100","highPrice":"0.00702400","lowPrice":"0.00685800","volume":"139522.40000000","quoteVolume":"969.33730248","openTime":1583227593814,"closeTime":1583313993814,"firstId":39565521,"lastId":39594387,"count":28867}
            public string symbol { get; set; }
            public string priceChange { get; set; }
            public string priceChangePercent { get; set; }
            public string weightedAvgPrice { get; set; }
            public string prevClosePrice { get; set; }
            public string lastPrice { get; set; }
            public string lastQty { get; set; }
            public string bidPrice { get; set; }
            public string bidQty { get; set; }
            public string askPrice { get; set; }
            public string askQty { get; set; }
            public string openPrice { get; set; }
            public string highPrice { get; set; }
            public string lowPrice { get; set; }
            public string volume { get; set; }
            public string quoteVolume { get; set; }
            public ulong openTime { get; set; }
            public ulong closeTime { get; set; }
            public long firstId { get; set; }
            public long lastId { get; set; }
            public long count { get; set; }
        }
    }


}
