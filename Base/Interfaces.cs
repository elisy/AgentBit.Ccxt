﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AgentBit.Ccxt.Base
{
    public interface ICancelAllOrders
    {
    }

    public interface ICancelOrder
    {
    }

    public interface ICancelOrders
    {
    }

    public interface ICORS
    {
    }

    public interface ICreateDepositAddress
    {
    }

    //public interface ICreateLimitOrder
    //{
    //}

    //public interface ICreateMarketOrder
    //{
    //}

    public interface ICreateOrder
    {
        public Task<string> CreateOrder(string symbol, OrderType type, Side side, decimal amount, decimal price = 0);
    }

    public interface IDeposit
    {
    }

    public interface IEditOrder
    {
    }

    public interface IFetchBalance : IPrivateAPI
    {
        public Task<Dictionary<string, BalanceAccount>> FetchBalance();
    }

    public interface IFetchBidsAsks
    {
    }

    public interface IFetchClosedOrders
    {
    }

    public interface IFetchCurrencies
    {
    }

    public interface IFetchDepositAddress
    {
    }

    public interface IFetchDeposits
    {
    }

    public interface IFetchFundingFees
    {
    }

    public interface IFetchL2OrderBook
    {
    }

    public interface IFetchLedger
    {
    }

    public interface IFetchMarkets
    {
        public Task<Market[]> FetchMarkets();
    }

    /// <summary>
    /// Private method to get executed orders
    /// </summary>
    public interface IFetchMyTrades : IPrivateAPI
    {
        public Task<MyTrade[]> FetchMyTrades(DateTime since, IEnumerable<string> symbols = null, uint limit = 1000);
    }

    public interface IFetchOHLCV
    {
    }

    public interface IFetchOpenOrders
    {
        public Task<Order[]> FetchOpenOrders(IEnumerable<string> symbols = null);
    }

    public interface IFetchOrder
    {
    }

    public interface IFetchOrderBook
    {
    }

    public interface IFetchOrderBooks
    {
    }

    public interface IFetchOrders
    {
        public Task<Order[]> FetchOrders(DateTime since, IEnumerable<string> symbols = null, uint limit = 1000);
    }

    public interface IFetchOrderTrades
    {
    }

    public interface IFetchStatus
    {
    }

    public interface IFetchTicker
    {
        public Task<Ticker> FetchTicker(string symbol);
    }

    public interface IFetchTickers
    {
        public Task<Ticker[]> FetchTickers(string[] symbols = null);
    }

    public interface IFetchTime
    {
    }

    /// <summary>
    /// Public method to get trades
    /// </summary>
    public interface IFetchTrades
    {
    }

    public interface IFetchTradingFee
    {
    }

    public interface IFetchTradingFees
    {
    }

    public interface IFetchTradingLimits
    {
    }

    public interface IFetchTransactions
    {
    }

    public interface IFetchWithdrawals
    {
    }

    public interface IPrivateAPI
    {
    }

    public interface IPublicAPI
    {
    }

    public interface IWithdraw
    {
    }
}
