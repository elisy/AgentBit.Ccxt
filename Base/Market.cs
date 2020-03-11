﻿using System;
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
        public double PriceMin { get; set; }
        public double PriceMax { get; set; }
        public int AmountPrecision { get; set; }
        public double AmountMin { get; set; }
        public double AmountMax { get; set; }
        public double CostMin { get; set; }
        public double CostMax { get; set; }
        public object Info { get; set; }

        public Market()
        {
            Active = true;
            PriceMax = double.MaxValue;
            AmountMax = double.MaxValue;
            CostMax = double.MaxValue;
        }
    }
}
