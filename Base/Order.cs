using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class Order
    {
        public string Id { get; set; }
        public ulong Timestamp { get; set; }
        public DateTime DateTime { get; set; }
        public DateTime LastTradeDateTime { get; set; }

        public string Symbol { get; set; }

        public OrderType Type { get; set; }
        public Side Side { get; set; }

        public OrderStatus Status { get; set; }

        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public decimal Cost { get; set; }

        public decimal Filled { get; set; }
        public decimal Remaining { get; set; }

        public decimal Average { get; set; }

        public decimal FeeCost { get; set; }
        public string FeeCurrency { get; set; }
        public decimal FeeRate { get; set; }

        public object Info { get; set; }
    }
}
