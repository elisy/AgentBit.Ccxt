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
        public double High { get; set; }
        public double Low { get; set; }
        public double Bid { get; set; }
        public double BidVolume { get; set; }
        public double Ask { get; set; }
        public double AskVolume { get; set; }
        /// <summary>
        /// Weighted Average Price
        /// </summary>
        public double Vwap { get; set; }
        public double Open { get; set; }
        public double Close { get; set; }
        public double Last { get; set; }

        /// <summary>
        /// Previous Day Close
        /// </summary>
        public double PreviousClose { get; set; }
        public double Change { get; set; }
        public double Percentage { get; set; }
        public double Average { get; set; }
        public double BaseVolume { get; set; }
        public double QuoteVolume { get; set; }
        public object Info { get; set; }
    }
}
