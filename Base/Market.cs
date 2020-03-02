using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class Market
    {
        /// <summary>
        /// Exchange specific Id
        /// </summary>
        public string Id { get; set; }
        public string BaseId { get; set; }
        public string QuoteId { get; set; }
        public string Symbol { get; set; }
        public string Base { get; set; }
        public string Quote { get; set; }
        public bool Active { get; set; }
        public double PricePrecision { get; set; }
        public double PriceMin { get; set; }
        public double PriceMax { get; set; }
        public double AmountPrecision { get; set; }
        public double AmountMin { get; set; }
        public double AmountMax { get; set; }
        public double CostMin { get; set; }
        public double CostMax { get; set; }
        public string Info { get; set; }

        public Market()
        {
            Active = true;
        }
    }
}
