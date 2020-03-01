using System;
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

    public interface ICreateLimitOrder
    {
    }

    public interface ICreateMarketOrder
    {
    }

    public interface ICreateOrder
    {
    }

    public interface IDeposit
    {
    }

    public interface IEditOrder
    {
    }

    public interface IFetchBalance
    {
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
    }

    public interface IFetchMyTrades
    {
    }

    public interface IFetchOHLCV
    {
    }

    public interface IFetchOpenOrders
    {
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
        public Task<Dictionary<string, Ticker>> FetchTickers(string[] symbols = null);
    }

    public interface IFetchTime
    {
    }

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
