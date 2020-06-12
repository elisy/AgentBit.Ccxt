using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AgentBit.Ccxt.Base;
using Microsoft.Extensions.Logging;

namespace AgentBit.Ccxt
{
    public class Bitfinex : Exchange, IPublicAPI, IPrivateAPI, IFetchTickers, IFetchTicker
    {
        readonly Uri ApiPublicV1 = new Uri("https://api.bitfinex.com/v1/");
        readonly Uri ApiPublicV2 = new Uri("https://api-pub.bitfinex.com/v2/");

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
                { "DSH", "DASH" },
                { "DRK", "DRK" },
                { "GSD", "GUSD" },
                { "HOT", "Hydro Protocol" },
                { "IOS", "IOST" },
                { "IOT", "IOTA" },
                { "IQX", "IQ" },
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
                { "YYW", "YOYOW" },
                { "UDC", "USDC" },
                { "UST", "USDT" },
                { "UTN", "UTNP" },
                { "VSY", "VSYS" },
                { "WAX", "WAXP" },
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

                foreach(var market in details.Where(m=>ids.Contains(m.pair)))
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
                    newItem.AmountMin = Convert.ToDouble(market.minimum_order_size, CultureInfo.InvariantCulture);
                    newItem.AmountMax = Convert.ToDouble(market.maximum_order_size, CultureInfo.InvariantCulture);
                    newItem.PriceMin = Math.Pow(10, -newItem.PricePrecision);
                    newItem.PriceMax = Math.Pow(10, newItem.PricePrecision);
                    newItem.CostMin = newItem.AmountMin * newItem.PriceMin;

                    newItem.FeeMaker = 0.1 / 100;
                    newItem.FeeTaker = 0.2 / 100;

                    result.Add(newItem);
                }

                _markets = result.ToArray();
            }
            return _markets;
        }

        public override void Sign(Request request)
        {
            if (request.ApiType != "private")
                return;
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
                ticker.Timestamp = Convert.ToUInt64(JsonSerializer.Deserialize<double>(item.timestamp) * 1000);
                ticker.DateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddMilliseconds(ticker.Timestamp);

                var market = (await FetchMarkets().ConfigureAwait(false)).FirstOrDefault(m => m.Id == item.pair);
                if (market == null)
                    continue;
                
                ticker.Symbol = market.Symbol;
                ticker.High = JsonSerializer.Deserialize<double>(item.high);
                ticker.Low = JsonSerializer.Deserialize<double>(item.low);
                ticker.Bid = JsonSerializer.Deserialize<double>(item.bid);
                ticker.Ask = JsonSerializer.Deserialize<double>(item.ask);
                ticker.Last = JsonSerializer.Deserialize<double>(item.last_price);
                ticker.Close = ticker.Last;
                ticker.Average = JsonSerializer.Deserialize<double>(item.mid);
                ticker.BaseVolume = JsonSerializer.Deserialize<double>(item.volume);
                ticker.Info = item;

                result.Add(ticker);
            }
            if (symbols == null)
                return result.ToArray();
            else
                return result.Where(m => symbols.Contains(m.Symbol)).ToArray();
        }



        public class BitfinexSymbolDetails
        {
            //{"pair":"btcusd","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"2000.0","minimum_order_size":"0.0006","expiration":"NA","margin":true},{"pair":"ltcusd","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"5000.0","minimum_order_size":"0.1","expiration":"NA","margin":true},{"pair":"ltcbtc","price_precision":5,"initial_margin":"30.0","minimum_margin":"15.0","maximum_order_size":"5000.0","minimum_order_size":"0.1","expiration":"NA","margin":true},{"pair":"ethusd","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"5000.0","minimum_order_size":"0.02","expiration":"NA","margin":true},{"pair":"ethbtc","price_precision":5,"initial_margin":"20.0","minimum_margin":"10.0","maximum_order_size":"5000.0","minimum_order_size":"0.02","expiration":"NA","margin":true},{"pair":"etcbtc","price_precision":5,"initial_margin":"30.0","minimum_margin":"15.0","maximum_order_size":"100000.0","minimum_order_size":"0.6","expiration":"NA","margin":true}
            public string pair { get; set; }
            public int price_precision { get; set; }
            public string maximum_order_size { get; set; }
            public string minimum_order_size { get; set; }
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
