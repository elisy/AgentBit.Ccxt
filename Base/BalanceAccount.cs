using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{
    public class BalanceAccount
    {
        public double Free { get; set; }
        public double Total { get; set; }
        public double Used => Total - Free;

        public override string ToString()
        {
            return $"{(decimal)Free} + {(decimal)Used} = {(decimal)Total}";
        }
    }
}
