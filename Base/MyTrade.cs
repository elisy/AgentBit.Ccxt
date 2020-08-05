using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class MyTrade
    {
        public string Id { get; set; }
        public ulong Timestamp { get; set; }
        public DateTime DateTime { get; set; }
        public string Symbol { get; set; }
        public string Type { get; set; }
        public string OrderId { get; set; }
        public TakerOrMaker TakerOrMaker { get; set; }
        public Side Side { get; set; }
        public decimal Price { get; set; }
        public decimal Amount { get; set; }
        public decimal Cost => Price * Amount;
        public decimal FeeCost { get; set; }
        public string FeeCurrency { get; set; }
        public decimal FeeRate { get; set; }
        public object Info { get; set; }
    }
}
