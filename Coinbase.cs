using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
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
        readonly Uri WebSocketUri = new Uri("wss://ws-feed.exchange.coinbase.com");

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

                foreach (var market in responseJson
                    .Where(m => m.id == $"{m.base_currency}-{m.quote_currency}") //Exclude "BTCAUCTION-USD" with base = "BTC"
                    )
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
            //var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Symbol == symbol);
            //if (market == null)
            //    return null;

            //var response = await Request(new Base.Request()
            //{
            //    BaseUri = ApiV2,
            //    Path = $"products/{market.Id}/stats",
            //    ApiType = "public",
            //    Method = HttpMethod.Get
            //}).ConfigureAwait(false);

            //var responseJson = JsonSerializer.Deserialize<CoinbaseProduct[]>(response.Text);
            return (await FetchTickers(new string[] { symbol }).ConfigureAwait(false)).FirstOrDefault();
        }


        public async Task<Ticker[]> FetchTickers(string[] symbols = null)
        {
            var markets = (await FetchMarkets().ConfigureAwait(false)).Where(m => m.Active).ToArray();
            var a = markets.Where(m => m.Symbol == "BTC/USD").ToArray();
            var result = markets.ToDictionary(m => m.Symbol, m => (Ticker)null);

            var jsonSubscribe = new
            {
                type = "subscribe",
                product_ids = markets.Select(m => m.Id).ToArray(),
                channels = new object[]
                {
                    new
                    {
                        name = "ticker",
                        product_ids = markets.Select(m => m.Id).ToArray()
                    }
                }
            };

            ArraySegment<byte> bytesToSend = new ArraySegment<byte>(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(jsonSubscribe)));
            using (ClientWebSocket socket = new ClientWebSocket())
            {
                await socket.ConnectAsync(WebSocketUri, CancellationToken.None);
                await socket.SendAsync(bytesToSend, WebSocketMessageType.Text, true, CancellationToken.None);

                ArraySegment<byte> bufferToReceive = new ArraySegment<byte>(new byte[8192]);

                var totalIterations = 0;
                while (socket.State == WebSocketState.Open && totalIterations++ < markets.Length * 2)
                {
                    using (var ms = new MemoryStream())
                    {
                        WebSocketReceiveResult socketResult = null;
                        do
                        {
                            socketResult = await socket.ReceiveAsync(bufferToReceive, CancellationToken.None);
                            ms.Write(bufferToReceive.Array, bufferToReceive.Offset, socketResult.Count);
                        }
                        while (!socketResult.EndOfMessage);

                        ms.Seek(0, SeekOrigin.Begin);

                        var stringJson = Encoding.UTF8.GetString(ms.ToArray());
                        var resultJson = JsonSerializer.Deserialize<Dictionary<string, object>>(stringJson);

                        if (resultJson["type"].ToString() == "error")
                        {
                            _logger.LogError($"message: {resultJson["message"]} {stringJson}");
                        }
                        else if (resultJson["type"].ToString() == "ticker")
                        {
                            try
                            {
                                var jsonTicker = JsonSerializer.Deserialize<CoinbaseSocketTicker>(stringJson);
                                var market = markets.FirstOrDefault(m => m.Id == jsonTicker.product_id);
                                if (market == null)
                                    continue;

                                //Some of ticker string fields may be null
                                if (String.IsNullOrEmpty(jsonTicker.time))
                                    continue;

                                var ticker = new Ticker()
                                {
                                    Symbol = market.Symbol,
                                    DateTime = DateTime.Parse(jsonTicker.time, null, System.Globalization.DateTimeStyles.RoundtripKind),
                                    High = decimal.Parse(jsonTicker.high_24h, NumberStyles.Float, CultureInfo.InvariantCulture),
                                    Low = decimal.Parse(jsonTicker.low_24h, NumberStyles.Float, CultureInfo.InvariantCulture),
                                    Bid = decimal.Parse(jsonTicker.best_bid, NumberStyles.Float, CultureInfo.InvariantCulture),
                                    Ask = decimal.Parse(jsonTicker.best_ask, NumberStyles.Float, CultureInfo.InvariantCulture),
                                    Open = decimal.Parse(jsonTicker.open_24h, NumberStyles.Float, CultureInfo.InvariantCulture), //"open_24h":"7.3e-7"
                                    Close = decimal.Parse(jsonTicker.price, NumberStyles.Float, CultureInfo.InvariantCulture),
                                    Last = decimal.Parse(jsonTicker.price, NumberStyles.Float, CultureInfo.InvariantCulture),
                                    BaseVolume = decimal.Parse(jsonTicker.volume_24h, NumberStyles.Float, CultureInfo.InvariantCulture),
                                    Info = jsonTicker
                                };
                                ticker.Timestamp = (uint)(ticker.DateTime.Subtract(new DateTime(1970, 1, 1))).TotalSeconds;
                                ticker.QuoteVolume = ticker.BaseVolume * (ticker.Low + ticker.High) / 3;
                                result[market.Symbol] = ticker;
                            }
                            catch (Exception e)
                            {
                                _logger.LogError(e, $"Error parsint ticker {stringJson}");
                            }
                        }
                        else if (resultJson["type"].ToString() == "subscriptions")
                        {
                            //"subscriptions" Message ends subscribing process. By this time server sent all tickers
                            break;
                        }
                        else
                        {
                            _logger.LogError($"Unknown type: {resultJson["type"]} {stringJson}");
                        }
                    }
                }
            }

            if (symbols == null)
                return result.Values.Where(m => m != null).ToArray();
            else
                return result.Values.Where(m => m != null).Where(m => symbols.Contains(m.Symbol)).ToArray();
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

        public class CoinbaseSocketTicker
        {
            public string type { get; set; }
            public long sequence { get; set; }
            public string product_id { get; set; }
            public string price { get; set; }
            public string open_24h { get; set; }
            public string volume_24h { get; set; }
            public string low_24h { get; set; }
            public string high_24h { get; set; }
            public string volume_30d { get; set; }
            public string best_bid { get; set; }
            public string best_ask { get; set; }
            public string side { get; set; }
            public string time { get; set; }
            public int trade_id { get; set; }
            public string last_size { get; set; }

        }

    }
}
