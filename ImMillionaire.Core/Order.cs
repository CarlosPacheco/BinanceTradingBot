﻿using Binance.Net.Enums;
using Binance.Net.Objects.Futures.FuturesData;
using Binance.Net.Objects.Futures.UserStream;
using Binance.Net.Objects.Spot.SpotData;
using Binance.Net.Objects.Spot.UserStream;
using System;

namespace ImMillionaire.Core
{
    public class Order
    {
        /// <summary>
        /// The side of the order
        /// </summary>
        public OrderSide Side { get; set; }

        /// <summary>
        /// The type of the order
        /// </summary>
        public OrderType Type { get; set; }

        /// <summary>
        /// How long the order is active
        /// </summary>
        public TimeInForce TimeInForce { get; set; }

        /// <summary>
        /// The status of the order
        /// </summary>
        public OrderStatus Status { get; set; }

        /// <summary>
        /// The time the order was submitted
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// The stop price
        /// </summary>
        public decimal? StopPrice { get; set; }

        /// <summary>
        /// The original quote order quantity
        /// </summary>
        public decimal QuoteQuantity { get; set; }

        /// <summary>
        /// Cummulative amount
        /// </summary>
        public decimal QuoteQuantityFilled { get; set; }

        /// <summary>
        /// The currently executed quantity of the order
        /// </summary>
        public decimal QuantityFilled { get; set; }

        /// <summary>
        /// The original quantity of the order
        /// </summary>
        public decimal Quantity { get; set; }

        /// <summary>
        /// The price of the order
        /// </summary>
        public decimal Price { get; set; }

        /// <summary>
        /// Original order id
        /// </summary>
        public string OriginalClientOrderId { get; set; }

        /// <summary>
        /// The order id as assigned by the client
        /// </summary>
        public string ClientOrderId { get; set; }

        /// <summary>
        /// The order id generated by Binance
        /// </summary>
        public long OrderId { get; set; }

        /// <summary>
        /// The symbol the order is for
        /// </summary>
        public string Symbol { get; set; }

        /// <summary>
        /// The asset the commission was taken from
        /// </summary>
        public string CommissionAsset { get; set; }

        /// <summary>
        /// The commission payed
        /// </summary>
        public decimal Commission { get; set; }

        public Order()
        {
        }

        public Order(BinanceOrder binanceOrder)
        {
            CreateTime = binanceOrder.CreateTime;
            Side = binanceOrder.Side;
            StopPrice = binanceOrder.StopPrice;
            Type = binanceOrder.Type;
            TimeInForce = binanceOrder.TimeInForce;
            Status = binanceOrder.Status;
            QuoteQuantity = binanceOrder.QuoteQuantity;
            QuoteQuantityFilled = binanceOrder.QuoteQuantityFilled;
            QuantityFilled = binanceOrder.QuantityFilled;
            Quantity = binanceOrder.Quantity;
            Price = binanceOrder.Price;
            OriginalClientOrderId = binanceOrder.OriginalClientOrderId;
            ClientOrderId = binanceOrder.ClientOrderId;
            OrderId = binanceOrder.OrderId;
            Symbol = binanceOrder.Symbol;
        }

        public Order(BinancePlacedOrder binanceOrder)
        {
            CreateTime = binanceOrder.CreateTime;
            Side = binanceOrder.Side;
            StopPrice = binanceOrder.StopPrice;
            Type = binanceOrder.Type;
            TimeInForce = binanceOrder.TimeInForce;
            Status = binanceOrder.Status;
            QuoteQuantity = binanceOrder.QuoteQuantity;
            QuoteQuantityFilled = binanceOrder.QuoteQuantityFilled;
            QuantityFilled = binanceOrder.QuantityFilled;
            Quantity = binanceOrder.Quantity;
            Price = binanceOrder.Price;
            OriginalClientOrderId = binanceOrder.OriginalClientOrderId;
            ClientOrderId = binanceOrder.ClientOrderId;
            OrderId = binanceOrder.OrderId;
            Symbol = binanceOrder.Symbol;
        }

        public Order(BinanceStreamOrderUpdate binanceOrder)
        {
            CreateTime = binanceOrder.CreateTime;
            Side = binanceOrder.Side;
            StopPrice = binanceOrder.StopPrice;
            Type = binanceOrder.Type;
            TimeInForce = binanceOrder.TimeInForce;
            Status = binanceOrder.Status;
            QuoteQuantity = binanceOrder.QuoteQuantity;
            QuoteQuantityFilled = binanceOrder.QuoteQuantityFilled;
            QuantityFilled = binanceOrder.QuantityFilled;
            Quantity = binanceOrder.Quantity;
            Price = binanceOrder.Price;
            OriginalClientOrderId = binanceOrder.OriginalClientOrderId;
            ClientOrderId = binanceOrder.ClientOrderId;
            OrderId = binanceOrder.OrderId;
            Symbol = binanceOrder.Symbol;
            CommissionAsset = binanceOrder.CommissionAsset;
            Commission = binanceOrder.Commission;
        }

        public Order(BinanceFuturesStreamOrderUpdateData binanceOrder)
        {
            CreateTime = binanceOrder.UpdateTime;
            Side = binanceOrder.Side;
            StopPrice = binanceOrder.StopPrice;
            Type = binanceOrder.Type;
            TimeInForce = binanceOrder.TimeInForce;
            Status = binanceOrder.Status;
            QuantityFilled = binanceOrder.AccumulatedQuantityOfFilledTrades;
            Quantity = binanceOrder.Quantity;
            Price = binanceOrder.Price;
            ClientOrderId = binanceOrder.ClientOrderId;
            OrderId = binanceOrder.OrderId;
            Symbol = binanceOrder.Symbol;
            CommissionAsset = binanceOrder.CommissionAsset;
            Commission = binanceOrder.Commission;
        }

        public Order(BinanceFuturesOrder binanceOrder)
        {
            CreateTime = binanceOrder.CreatedTime;
            Side = binanceOrder.Side;
            StopPrice = binanceOrder.StopPrice;
            Type = binanceOrder.Type;
            TimeInForce = binanceOrder.TimeInForce;
            Status = binanceOrder.Status;
            QuoteQuantity = binanceOrder.LastFilledQuantity;
            QuoteQuantityFilled = binanceOrder.QuoteQuantityFilled;
            QuantityFilled = binanceOrder.QuantityFilled;
            Quantity = binanceOrder.Quantity;
            Price = binanceOrder.Price;
            OriginalClientOrderId = binanceOrder.ClientOrderId;
            ClientOrderId = binanceOrder.ClientOrderId;
            OrderId = binanceOrder.OrderId;
            Symbol = binanceOrder.Symbol;
        }

        public Order(BinanceFuturesPlacedOrder binanceOrder)
        {
            CreateTime = binanceOrder.UpdateTime;
            Side = binanceOrder.Side;
            StopPrice = binanceOrder.StopPrice;
            Type = binanceOrder.Type;
            TimeInForce = binanceOrder.TimeInForce;
            Status = binanceOrder.Status;
            QuoteQuantity = binanceOrder.LastFilledQuantity;
            QuoteQuantityFilled = binanceOrder.QuoteQuantityFilled;
            QuantityFilled = binanceOrder.QuantityFilled;
            Quantity = binanceOrder.Quantity;
            Price = binanceOrder.Price;
            OriginalClientOrderId = binanceOrder.ClientOrderId;
            ClientOrderId = binanceOrder.ClientOrderId;
            OrderId = binanceOrder.OrderId;
            Symbol = binanceOrder.Symbol;
        }
    }
}
