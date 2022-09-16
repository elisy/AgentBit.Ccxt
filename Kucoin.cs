using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    public class Kucoin : Exchange, IPublicAPI, IFetchTickers, IFetchTicker, IFetchBalance
    {
        readonly Uri ApiV1 = new Uri("https://api.kucoin.com/api/v1/");

        public Kucoin(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            RateLimit = 334;

            CommonCurrencies = new Dictionary<string, string>() {
                { "BIFI", "Bifrost" }, //Conflict with Beefy Finance
                { "BULL", "Bullieverse" }, //Conflict with 3X Long Bitcoin Token
                { "HOT", "HOTNOW" },
                { "EDGE", "DADI" },
                { "WAX", "WAXP" },
                { "TRY", "TRIAS" },
                { "VAI", "VAIOT" }
            };
        }

        public async Task<Dictionary<string, BalanceAccount>> FetchBalance()
        {
            var response = await Request(new Base.Request()
            {
                ApiType = "private",
                BaseUri = ApiV1,
                Path = "accounts",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);
            var serverResponse = JsonSerializer.Deserialize<KucoinResponse<KucoinBalance[]>>(response.Text);

            var result = new Dictionary<string, BalanceAccount>();
            foreach (var balance in serverResponse.data.Where(m => m.type == "trade"))
            {
                result[GetCommonCurrencyCode(balance.currency.ToUpper())] = new BalanceAccount()
                {
                    Free = JsonSerializer.Deserialize<decimal>(balance.available),
                    Total = JsonSerializer.Deserialize<decimal>(balance.balance),
                    Info = balance
                };
            }
            return result;
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var response = await Request(new Base.Request()
                {
                    BaseUri = ApiV1,
                    Path = "symbols",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);

                var responseJson = JsonSerializer.Deserialize<KucoinResponse<KucoinSymbol[]>>(response.Text);

                var result = new List<Market>();

                foreach (var market in responseJson.data)
                {
                    var newItem = new Market();
                    newItem.Id = market.symbol;
                    newItem.BaseId = market.baseCurrency;
                    newItem.QuoteId = market.quoteCurrency;

                    newItem.Base = GetCommonCurrencyCode(newItem.BaseId.ToUpper());
                    newItem.Quote = GetCommonCurrencyCode(newItem.QuoteId.ToUpper());

                    newItem.AmountMin = Convert.ToDecimal(market.baseMinSize, CultureInfo.InvariantCulture);
                    newItem.AmountMax = Convert.ToDecimal(market.baseMaxSize, CultureInfo.InvariantCulture);
                    newItem.AmountPrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.quoteIncrement, CultureInfo.InvariantCulture)));

                    newItem.PriceMin = Convert.ToDecimal(market.baseIncrement, CultureInfo.InvariantCulture);
                    newItem.PricePrecision = (int)Math.Abs(Math.Log10(Convert.ToDouble(market.priceIncrement, CultureInfo.InvariantCulture)));

                    newItem.CostMin = Convert.ToDecimal(market.quoteMinSize, CultureInfo.InvariantCulture);
                    newItem.CostMax = Convert.ToDecimal(market.quoteMaxSize, CultureInfo.InvariantCulture);

                    newItem.Active = market.enableTrading;

                    newItem.FeeMaker = 0.1M / 100;
                    newItem.FeeTaker = 0.1M / 100;

                    newItem.Url = $"https://trade.kucoin.com/trade/{market.symbol}?rcode=vu4MKr";
                    newItem.Margin = market.isMarginEnabled;

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
            var markets = (await FetchMarkets().ConfigureAwait(false)).Where(m => m.Active).ToArray();

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiV1,
                Path = "market/allTickers",
                ApiType = "public",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);

            var responseJson = JsonSerializer.Deserialize<KucoinResponse<KucoinTickers>>(response.Text);

            var timestamp = responseJson.data.time;
            var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(timestamp);

            var result = new List<Ticker>(responseJson.data.ticker.Length);
            foreach (var item in responseJson.data.ticker)
            {
                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.symbol);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.DateTime = dateTime;
                ticker.Timestamp = timestamp;
                ticker.Symbol = market.Symbol;

                ticker.High = Convert.ToDecimal(item.high, CultureInfo.InvariantCulture);
                ticker.Low = Convert.ToDecimal(item.low, CultureInfo.InvariantCulture);
                ticker.Bid = Convert.ToDecimal(item.buy, CultureInfo.InvariantCulture);
                ticker.Ask = Convert.ToDecimal(item.sell, CultureInfo.InvariantCulture);
                ticker.Open = Convert.ToDecimal(item.last, CultureInfo.InvariantCulture) * (1 - Convert.ToDecimal(item.changePrice, CultureInfo.InvariantCulture));
                ticker.Close = Convert.ToDecimal(item.last, CultureInfo.InvariantCulture);
                ticker.Last = Convert.ToDecimal(item.last, CultureInfo.InvariantCulture);
                ticker.BaseVolume = Convert.ToDecimal(item.vol, CultureInfo.InvariantCulture);
                ticker.QuoteVolume = Convert.ToDecimal(item.volValue, CultureInfo.InvariantCulture);
                ticker.Average = Convert.ToDecimal(item.averagePrice, CultureInfo.InvariantCulture);

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
            //Error message User-Agent header is required.
            request.Headers.Add("User-Agent", typeof(Exchange).FullName);

            if (request.ApiType != "private")
                return;

            var api_key = ApiKey;
            var api_secret = ApiSecret;
            var api_passphrase = ApiPassword;
            var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

            var str_to_sign = now + request.Method + "/api/v1/" + request.Path;

            var signature  = GetHmacBase64<HMACSHA256>(str_to_sign, api_secret);
            var passphrase = GetHmacBase64<HMACSHA256>(api_passphrase, api_secret);
            request.Headers.Add("KC-API-SIGN", signature);
            request.Headers.Add("KC-API-TIMESTAMP", now);
            request.Headers.Add("KC-API-KEY", api_key);
            request.Headers.Add("KC-API-PASSPHRASE", passphrase);
            request.Headers.Add("KC-API-KEY-VERSION", "2");
        }


        public class KucoinResponse<T>
        {
            public string code { get; set; }
            public T data { get; set; }
        }


        public class KucoinBalance
        {
            //https://docs.kucoin.com/#list-accounts
            //{
            //  "id": "5bd6e9286d99522a52e458de",  //accountId
            //  "currency": "BTC",  //Currency
            //  "type": "main",     //Account type, including main and trade
            //  "balance": "237582.04299",  //Total assets of a currency
            //  "available": "237582.032",  //Available assets of a currency
            //  "holds": "0.01099" //Hold assets of a currency
            //}
            public string id { get; set; }
            public string currency { get; set; }
            public string type { get; set; }
            public string balance { get; set; }
            public string available { get; set; }
            public string holds { get; set; }
        }


        public class KucoinSymbol
        {
            public string symbol { get; set; }
            public string name { get; set; }
            public string baseCurrency { get; set; }
            public string quoteCurrency { get; set; }
            public string feeCurrency { get; set; }
            public string market { get; set; }
            public string baseMinSize { get; set; }
            public string quoteMinSize { get; set; }
            public string baseMaxSize { get; set; }
            public string quoteMaxSize { get; set; }
            public string baseIncrement { get; set; }
            public string quoteIncrement { get; set; }
            public string priceIncrement { get; set; }
            public string priceLimitRate { get; set; }
            public bool isMarginEnabled { get; set; }
            public bool enableTrading { get; set; }
        }

        public class KucoinTickers
        {
            public ulong time { get; set; }
            public KucoinTicker[] ticker { get; set; }
        }

        public class KucoinTicker
        {
            //{"symbol":"NKN-USDT","symbolName":"NKN-USDT","buy":"0.3783","sell":"0.3788","changeRate":"0.0068","changePrice":"0.0026",
            //"high":"0.3867","low":"0.3737","vol":"217147.31734871","volValue":"82816.707508883833","last":"0.3799","averagePrice":"0.37734421",
            //"takerFeeRate":"0.001","makerFeeRate":"0.001","takerCoefficient":"1","makerCoefficient":"1"}
            public string symbol { get; set; }
            public string symbolName { get; set; }
            public string buy { get; set; }
            public string sell { get; set; }
            public string changeRate { get; set; }
            public string changePrice { get; set; }
            public string high { get; set; }
            public string low { get; set; }
            public string vol { get; set; }
            public string volValue { get; set; }
            public string last { get; set; }
            public string averagePrice { get; set; }
            public string takerFeeRate { get; set; }
            public string makerFeeRate { get; set; }
            public string takerCoefficient { get; set; }
            public string makerCoefficient { get; set; }
        }

    }
}
