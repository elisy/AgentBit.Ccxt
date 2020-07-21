using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class Market
    {
        /// <summary>
        /// Exchange specific symbol
        /// </summary>
        public string Id { get; set; }
        /// <summary>
        /// Exchange specific base
        /// </summary>
        public string BaseId { get; set; }
        /// <summary>
        /// Exchange specific quote
        /// </summary>
        public string QuoteId { get; set; }

        /// <summary>
        /// Common symbol for all exchanges
        /// Symbol = $"{Base}/{Quote}"
        /// </summary>
        public string Symbol { get; private set; }

        private string _base = String.Empty;
        /// <summary>
        /// Common currency code for all excnahges
        /// </summary>
        public string Base { get { return _base; } set { _base = value.ToUpper(); Symbol = $"{_base}/{_quote}"; } }
        private string _quote = String.Empty;
        /// <summary>
        /// Common currency code for all excnahges
        /// </summary>
        public string Quote { get { return _quote; } set { _quote = value.ToUpper(); Symbol = $"{_base}/{_quote}"; } }

        public bool Active { get; set; }
        public int PricePrecision { get; set; }
        public decimal PriceMin { get; set; }
        public decimal PriceMax { get; set; }
        public int AmountPrecision { get; set; }
        public decimal AmountMin { get; set; }
        public decimal AmountMax { get; set; }
        public decimal CostMin { get; set; }
        public decimal CostMax { get; set; }

        /// <summary>
        /// Default max maker fee rate, 0.0016 = 0.16%
        /// </summary>
        public decimal FeeMaker { get; set; }
        /// <summary>
        /// Default max taker fee rate, 0.002 = 0.2%
        /// </summary>
        public decimal FeeTaker { get; set; }

        public object Info { get; set; }
        public string Url { get; set; }

        public Market()
        {
            Active = true;
            PriceMax = decimal.MaxValue;
            AmountMax = decimal.MaxValue;
            CostMax = decimal.MaxValue;
        }
    }
}
