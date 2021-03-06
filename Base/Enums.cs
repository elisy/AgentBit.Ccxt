﻿using System;
using System.Collections.Generic;
using System.Text;

namespace AgentBit.Ccxt.Base
{ 
    public enum TakerOrMaker
    {
        Taker,
        Maker
    }

    public enum Side
    {
        Buy,
        Sell
    }

    public enum OrderType
    {
        Limit,
        Market,
        Other
    }

    public enum OrderStatus
    {
        Open,
        Closed,
        Canceled
    }

}
