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
    public class Latoken : Exchange, IPublicAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiPublicV2 = new Uri("https://api.latoken.com/v2/");
        readonly Uri ApiPublicV1 = new Uri("https://api.latoken.com/v1/");

        public Latoken(HttpClient httpClient, ILogger logger) : base(httpClient, logger)
        {
            RateLimit = 1000;

            CommonCurrencies = new Dictionary<string, string>() {
                { "BUX", "Buxcoin" },
                { "CBT", "Community Business Token" },
                { "CTC", "CyberTronchain" },
                { "DMD", "Diamond Coin" },
                { "FREN", "Frenchie" },
                { "GDX", "GoldenX" },
                { "GEC", "Geco One" },
                { "GEM", "NFTmall" },
                { "GMT", "GMT Token" },
                { "IMC", "IMCoin" },
                { "MT", "Monarch" },
                { "TPAY", "Tetra Pay" },
                { "TRADE", "Smart Trade Coin" },
                { "TSL", "Treasure SL" },
                { "UNO", "Unobtanium" },
                { "WAR", "Warrior Token" }
            };
        }

        public override async Task<Market[]> FetchMarkets()
        {
            if (_markets == null)
            {
                var currenciesResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV2,
                    Path = "currency",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var currencies = JsonSerializer.Deserialize<LatokenCurrency[]>(currenciesResponse.Text).ToDictionary(m => m.id, m => m);

                var pairsResponse = await Request(new Base.Request()
                {
                    BaseUri = ApiPublicV2,
                    Path = "pair",
                    ApiType = "public",
                    Method = HttpMethod.Get
                }).ConfigureAwait(false);
                var pairs = JsonSerializer.Deserialize<LatokenPair[]>(pairsResponse.Text);

                var result = new List<Market>();

                foreach (var market in pairs)
                {
                    if (!currencies.ContainsKey(market.baseCurrency) || !currencies.ContainsKey(market.quoteCurrency))
                        continue;

                    var newItem = new Market();
                    newItem.Id = market.id;
                    newItem.BaseId = market.baseCurrency;
                    newItem.QuoteId = market.quoteCurrency;

                    newItem.Base = GetCommonCurrencyCode(currencies[market.baseCurrency].tag);
                    newItem.Quote = GetCommonCurrencyCode(currencies[market.quoteCurrency].tag);

                    newItem.Active = market.status == "PAIR_STATUS_ACTIVE";

                    if (newItem.Quote == "USD")
                    {
                        newItem.CostMin = JsonSerializer.Deserialize<decimal>(market.minOrderCostUsd);
                        newItem.CostMax = JsonSerializer.Deserialize<decimal>(market.maxOrderCostUsd);
                    }

                    newItem.AmountMin = JsonSerializer.Deserialize<decimal>(market.minOrderQuantity);

                    newItem.PricePrecision = market.priceDecimals;
                    newItem.AmountPrecision = market.quantityDecimals;

                    newItem.FeeTaker = 0.49M / 100;
                    newItem.FeeMaker = 0.49M / 100;

                    newItem.Url = $"https://latoken.com/exchange/{newItem.Base.ToUpper()}_{newItem.Quote.ToUpper()}?r=pkkbsa37";

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

            var response = await Request(new Base.Request()
            {
                BaseUri = ApiPublicV2,
                Path = "ticker",
                Method = HttpMethod.Get
            }).ConfigureAwait(false);
            var tickers = JsonSerializer.Deserialize<LatokenTickerV2[]>(response.Text);

            var result = new List<Ticker>();
            foreach (var item in tickers)
            {
                var market = markets.FirstOrDefault(m => m.Symbol == item.symbol);
                if (market == null)
                    continue;

                Ticker ticker = new Ticker();

                ticker.Timestamp = item.updateTimestamp;
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ticker.Timestamp);

                ticker.Symbol = market.Symbol;
                ticker.Bid = JsonSerializer.Deserialize<decimal>(item.bestBid);
                ticker.BidVolume = JsonSerializer.Deserialize<decimal>(item.bestBidQuantity);
                ticker.Ask = JsonSerializer.Deserialize<decimal>(item.bestAsk);
                ticker.AskVolume = JsonSerializer.Deserialize<decimal>(item.bestAskQuantity);
                ticker.Last = JsonSerializer.Deserialize<decimal>(item.lastPrice);
                ticker.Open = ticker.Last;
                ticker.Close = ticker.Last;
                ticker.Average = (ticker.Bid + ticker.Ask) / 2;
                ticker.BaseVolume = JsonSerializer.Deserialize<decimal>(item.amount24h);
                ticker.QuoteVolume = JsonSerializer.Deserialize<decimal>(item.volume24h);

                decimal change24 = JsonSerializer.Deserialize<decimal>(item.change24h);
                if (change24 > 0)
                {
                    ticker.High = ticker.Last;
                    ticker.Low = Math.Round(ticker.Last / (1 + change24 / 100), market.PricePrecision);
                }
                else
                {
                    ticker.High = Math.Round(ticker.Last * (1 + Math.Abs(change24) / 100), market.PricePrecision);
                    ticker.Low = ticker.Last;
                }

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
        }


        public class LatokenCurrency
        {
            //{
            //"id": "d663138b-3ec1-436c-9275-b3a161761523",
            //"status": "CURRENCY_STATUS_ACTIVE",
            //"type": "CURRENCY_TYPE_CRYPTO",
            //"name": "Latoken",
            //"tag": "LA",
            //"description": "LATOKEN is a cutting edge exchange which makes investing and payments easy and safe worldwide.",
            //"logo": "https://static.dev-mid.nekotal.tech/icons/color/la.svg",
            //"decimals": 9,
            //"created": 1571333563712,
            //"tier": 1,
            //"assetClass": "ASSET_CLASS_UNKNOWN",
            //"minTransferAmount": 0
            //}
            public string id { get; set; }
            public string status { get; set; }
            public string type { get; set; }
            public string name { get; set; }
            public string tag { get; set; }
            public string description { get; set; }
            public string logo { get; set; }
            public int decimals { get; set; }
            public long created { get; set; }
            public int tier { get; set; }
            public string assetClass { get; set; }
            public decimal minTransferAmount { get; set; } //May be 0E-18
        }

        public class LatokenPair
        {
            //{
            //"id": "263d5e99-1413-47e4-9215-ce4f5dec3556",
            //"status": "PAIR_STATUS_ACTIVE",
            //"baseCurrency": "6ae140a9-8e75-4413-b157-8dd95c711b23",
            //"quoteCurrency": "23fa548b-f887-4f48-9b9b-7dd2c7de5ed0",
            //"priceTick": "0.010000000",
            //"priceDecimals": 2,
            //"quantityTick": "0.010000000",
            //"quantityDecimals": 2,
            //"costDisplayDecimals": 3,
            //"created": 1571333313871,
            //"minOrderQuantity": "0",
            //"maxOrderCostUsd": "999999999999999999",
            //"minOrderCostUsd": "0",
            //"externalSymbol": ""
            //}
            public string id { get; set; }
            public string status { get; set; }
            public string baseCurrency { get; set; }
            public string quoteCurrency { get; set; }
            public string priceTick { get; set; }
            public int priceDecimals { get; set; }
            public string quantityTick { get; set; }
            public int quantityDecimals { get; set; }
            public int costDisplayDecimals { get; set; }
            public long created { get; set; }
            public string minOrderQuantity { get; set; }
            public string maxOrderCostUsd { get; set; }
            public string minOrderCostUsd { get; set; }
            public string externalSymbol { get; set; }
        }


        public class LatokenTickerV2
        {
            //{
            //"symbol": "ETH/USDT",
            //"baseCurrency": "23fa548b-f887-4f48-9b9b-7dd2c7de5ed0",
            //"quoteCurrency": "d721fcf2-cf87-4626-916a-da50548fe5b3",
            //"volume24h": "450.29",
            //"volume7d": "3410.23",
            //"change24h": "-5.2100",
            //"change7d": "1.1491",
            //"amount24h": "25.2100",
            //"amount7d": "111.1491",
            //"lastPrice": "10034.14",
            //"lastQuantity": "10034.14",
            //"bestBid": "105.1445",
            //"bestBidQuantity": "198789.14",
            //"bestAsk": "10021.14",
            //"bestAskQuantity": "1054034.14",
            //"updateTimestamp": 100341454655423
            //}
            public string symbol { get; set; }
            public string baseCurrency { get; set; }
            public string quoteCurrency { get; set; }
            public string volume24h { get; set; }
            public string volume7d { get; set; }
            public string change24h { get; set; }
            public string change7d { get; set; }
            public string amount24h { get; set; }
            public string amount7d { get; set; }
            public string lastPrice { get; set; }
            public string lastQuantity { get; set; }
            public string bestBid { get; set; }
            public string bestBidQuantity { get; set; }
            public string bestAsk { get; set; }
            public string bestAskQuantity { get; set; }
            public ulong updateTimestamp { get; set; }
        }

        public class LatokenTickerV1
        {
            //{
            //"pairId": 502,
            //"symbol": "LAETH",
            //"volume": 1023314.3202,
            //"open": 134.82,
            //"low": 133.95,
            //"high": 136.22,
            //"close": 135.12,
            //"priceChange": 0.22
            //}
            public int pairId { get; set; }
            public string symbol { get; set; }
            public decimal volume { get; set; }
            public decimal open { get; set; }
            public decimal low { get; set; }
            public decimal high { get; set; }
            public decimal close { get; set; }
        }

    }
}
