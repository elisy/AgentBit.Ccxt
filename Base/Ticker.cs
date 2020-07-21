using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class Ticker
    {
        public string Symbol { get; set; }
        public ulong Timestamp { get; set; }
        public DateTime DateTime { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Bid { get; set; }
        public decimal BidVolume { get; set; }
        public decimal Ask { get; set; }
        public decimal AskVolume { get; set; }
        /// <summary>
        /// Weighted Average Price
        /// </summary>
        public decimal Vwap { get; set; }
        public decimal Open { get; set; }
        public decimal Close { get; set; }
        public decimal Last { get; set; }

        /// <summary>
        /// Previous Day Close
        /// </summary>
        public decimal PreviousClose { get; set; }
        public decimal Change { get; set; }
        public decimal Percentage { get; set; }
        public decimal Average { get; set; }
        public decimal BaseVolume { get; set; }
        public decimal QuoteVolume { get; set; }
        public object Info { get; set; }
    }
}
