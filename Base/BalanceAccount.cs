using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class BalanceAccount
    {
        public decimal Free { get; set; }
        public decimal Total { get; set; }
        public decimal Used => Total - Free;

        public override string ToString()
        {
            return $"{(decimal)Free} + {(decimal)Used} = {(decimal)Total}";
        }
    }
}
