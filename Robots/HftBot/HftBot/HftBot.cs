using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;
using cAlgo.Indicators;

namespace cAlgo
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class HftBot : Robot
    {
        [Parameter("Trade Volume (Lots)", DefaultValue = 0.1, MinValue = 0.01, Step = 0.01)]
        public double LotSize { get; set; }

        [Parameter("Transition Threshold (pips)", DefaultValue = 0.2, MinValue = 0.01, Step = 0.01)]
        public double TransitionThresholdPips { get; set; }

        [Parameter("Min Training Samples (Transitions)", DefaultValue = 50, MinValue = 10)]
        public int MinSamplesToPredict { get; set; }

        [Parameter("Take Profit (pips)", DefaultValue = 2.0, MinValue = 0.5, Step = 0.1)]
        public double TakeProfitPips { get; set; }

        [Parameter("Stop Loss (pips)", DefaultValue = 4.0, MinValue = 0.5, Step = 0.1)]
        public double StopLossPips { get; set; }



        [Parameter("Enable Opposite Candle Filter", DefaultValue = true)]
        public bool EnableOppositeFilter { get; set; }

        [Parameter("Opposite Candle Time Limit (sec)", DefaultValue = 30, MinValue = 1)]
        public int OppositeThresholdSeconds { get; set; }

        [Parameter("Limit Order Offset (pips)", DefaultValue = 0.0, MinValue = -10.0, Step = 0.1)]
        public double LimitOffsetPips { get; set; }

        [Parameter("Enable Depth Volume Spike Filter", DefaultValue = true)]
        public bool EnableDepthFilter { get; set; }

        [Parameter("Depth Levels to Sum", DefaultValue = 3, MinValue = 1)]
        public int DepthLevelsToSum { get; set; }

        [Parameter("Depth Window Period", DefaultValue = 50, MinValue = 5)]
        public int DepthPeriod { get; set; }

        [Parameter("Depth Spike Multiplier", DefaultValue = 2.0, MinValue = 1.0, Step = 0.1)]
        public double DepthSpikeMultiplier { get; set; }

        [Parameter("Enable Wick Ratio Filter", DefaultValue = true)]
        public bool EnableWickRatioFilter { get; set; }

        [Parameter("Wick Ratio Limit (e.g. 0.70)", DefaultValue = 0.70, MinValue = 0.10, MaxValue = 1.00, Step = 0.01)]
        public double WickRatioLimit { get; set; }

        [Parameter("Enable HFT Microstructure Timing", DefaultValue = true)]
        public bool EnableHftTiming { get; set; }

        [Parameter("HFT Entry Score Threshold", DefaultValue = 30.0, MinValue = -100.0, MaxValue = 100.0, Step = 1.0)]
        public double HftThreshold { get; set; }

        [Parameter("Enable Dynamic Order Book Pricing", DefaultValue = true)]
        public bool EnableDynamicPricing { get; set; }

        [Parameter("Log DOM Updates", DefaultValue = true)]
        public bool LogDomUpdates { get; set; }

        private double _volumeInUnits;
        private double _lastPrice;
        private int _lastState = 1; // Starts as Sideways (1)
        
        // 3x3 Transition Matrix
        // State 0 = Bearish, State 1 = Sideways, State 2 = Bullish
        private int[,] _transitionMatrix = new int[3, 3];
        private int _totalTransitions = 0;

        // Loss Protection State Tracker
        private int _consecutiveLosses = 0;

        // Market Depth rolling averages
        private double _averageBidVolume;
        private double _averageAskVolume;
        private MarketDepth _marketDepth;
        private readonly HashSet<int> _pendingCancelOrderIds = new HashSet<int>();
        private int _predictedState = 1; // HMM prediction (0 = Sell, 1 = Hold, 2 = Buy)
        private int _lastPrintedTransition = -1;
        private bool _trainingCompleteLogged = false;

        protected override void OnStart()
        {
            _lastPrice = Symbol.Bid;
            _volumeInUnits = Symbol.QuantityToVolumeInUnits(LotSize);
            
            Positions.Closed += OnPositionsClosed;
            
            PendingOrders.Cancelled += OnPendingOrdersCancelled;
            PendingOrders.Filled += OnPendingOrdersFilled;

            // Timer for stable HMM transition tracking (ticks every 100ms)
            Timer.Start(TimeSpan.FromMilliseconds(100));

            _marketDepth = MarketData.GetMarketDepth(SymbolName);
            _marketDepth.Updated += OnMarketDepthUpdated;

            Print("HFT HMM Bot Started. Min samples: {0}.", MinSamplesToPredict);
        }

        protected override void OnTick()
        {
            if (Bars.Count < 2)
                return;

            // DOM Level 2 validation check: if not supported, throw an exception
            if (_marketDepth == null || _marketDepth.BidEntries.Count == 0 || _marketDepth.AskEntries.Count == 0)
            {
                throw new InvalidOperationException("CRITICAL ERROR: Market Depth (Level 2 / DOM) data is not available for this symbol or account. HftBot requires Level 2 data to run.");
            }

            double currentPrice = Symbol.Bid;

            // Update Market Depth rolling averages and evaluate spikes
            double currentBidVolume = 0;
            double currentAskVolume = 0;
            bool depthBuyAllowed = true;
            bool depthSellAllowed = true;

            if (_marketDepth != null && _marketDepth.BidEntries.Count > 0 && _marketDepth.AskEntries.Count > 0)
            {
                int bidCount = Math.Min(DepthLevelsToSum, _marketDepth.BidEntries.Count);
                int askCount = Math.Min(DepthLevelsToSum, _marketDepth.AskEntries.Count);

                for (int i = 0; i < bidCount; i++)
                    currentBidVolume += _marketDepth.BidEntries[i].VolumeInUnits;

                for (int i = 0; i < askCount; i++)
                    currentAskVolume += _marketDepth.AskEntries[i].VolumeInUnits;

                if (_averageBidVolume == 0)
                    _averageBidVolume = currentBidVolume;
                else
                    _averageBidVolume = (_averageBidVolume * (DepthPeriod - 1) + currentBidVolume) / DepthPeriod;

                if (_averageAskVolume == 0)
                    _averageAskVolume = currentAskVolume;
                else
                    _averageAskVolume = (_averageAskVolume * (DepthPeriod - 1) + currentAskVolume) / DepthPeriod;

                if (EnableDepthFilter)
                {
                    depthBuyAllowed = currentBidVolume > _averageBidVolume * DepthSpikeMultiplier;
                    depthSellAllowed = currentAskVolume > _averageAskVolume * DepthSpikeMultiplier;
                }
            }

            // HFT Microstructure OBI and Micro-Price Pressure Calculations
            double obi = 0;
            double microPrice = currentPrice;
            double midPrice = currentPrice;
            double mpp = 0; // Micro-Price Pressure in pips

            if (currentBidVolume + currentAskVolume > 0)
            {
                obi = (currentBidVolume - currentAskVolume) / (currentBidVolume + currentAskVolume);
                
                double bestBid = Symbol.Bid;
                double bestAsk = Symbol.Ask;
                
                midPrice = (bestBid + bestAsk) / 2.0;
                
                // Micro-Price is the volume-weighted mid-price
                microPrice = (currentBidVolume * bestAsk + currentAskVolume * bestBid) / (currentBidVolume + currentAskVolume);
                
                mpp = (microPrice - midPrice) / Symbol.PipSize;
            }

            // Calculate HFT Entry Score
            double hesBuy = obi * 50.0 + mpp * 50.0;
            double hesSell = -obi * 50.0 - mpp * 50.0;

            bool hftTimingBuyAllowed = true;
            bool hftTimingSellAllowed = true;

            if (EnableHftTiming)
            {
                hftTimingBuyAllowed = hesBuy >= HftThreshold;
                hftTimingSellAllowed = hesSell >= HftThreshold;
            }

            // 3. Trade Entry Logic (Only if we have collected enough sample data)
            if (_totalTransitions >= MinSamplesToPredict)
            {
                // Cancel pending limit orders that no longer match the prediction
                foreach (var order in PendingOrders)
                {
                    if (order.Label == "HMM_Buy" && _predictedState != 2)
                    {
                        if (!_pendingCancelOrderIds.Contains(order.Id))
                        {
                            _pendingCancelOrderIds.Add(order.Id);
                            CancelPendingOrder(order);
                        }
                    }
                    else if (order.Label == "HMM_Sell" && _predictedState != 0)
                    {
                        if (!_pendingCancelOrderIds.Contains(order.Id))
                        {
                            _pendingCancelOrderIds.Add(order.Id);
                            CancelPendingOrder(order);
                        }
                    }
                }

                // Strict single active position & pending order check (no hedging, max 1 active or pending)
                if (Positions.Count == 0 && PendingOrders.Count == 0)
                {
                    var curr = Bars.Last(0);
                    double secondsElapsed = (Server.Time - curr.OpenTime).TotalSeconds;

                    // 3b. Evaluate Wick Ratio Filter (Avoid entry if current candle opposite wick is too long)
                    double barLength = curr.High - curr.Low;
                    double lowerWickRatio = 0;
                    double upperWickRatio = 0;

                    if (barLength > 0)
                    {
                        double lowerWick = Math.Min(curr.Open, currentPrice) - curr.Low;
                        double upperWick = curr.High - Math.Max(curr.Open, currentPrice);
                        lowerWickRatio = lowerWick / barLength;
                        upperWickRatio = upperWick / barLength;
                    }

                    bool wickRatioBuyAllowed = true;
                    bool wickRatioSellAllowed = true;

                    if (EnableWickRatioFilter)
                    {
                        // If upper wick (selling pressure) is >= threshold, block BUY
                        if (upperWickRatio >= WickRatioLimit)
                        {
                            wickRatioBuyAllowed = false;
                        }
                        // If lower wick (buying pressure) is >= threshold, block SELL
                        if (lowerWickRatio >= WickRatioLimit)
                        {
                            wickRatioSellAllowed = false;
                        }
                    }

                    // 3c. Evaluate Opposite Candle Filter (Avoid entry if candle is strong in the opposite direction)
                    bool oppositeBuyAllowed = true;
                    bool oppositeSellAllowed = true;
                    if (EnableOppositeFilter)
                    {
                        if (secondsElapsed > OppositeThresholdSeconds)
                        {
                            // If current forming bar is bearish, block Buy entries
                            if (currentPrice < curr.Open)
                            {
                                oppositeBuyAllowed = false;
                            }
                            // If current forming bar is bullish, block Sell entries
                            if (currentPrice > curr.Open)
                            {
                                oppositeSellAllowed = false;
                            }
                        }
                    }

                    // 3d. Execute Sinyal via Limit Orders
                    double spread = Symbol.Ask - Symbol.Bid;
                    double dynamicMidPrice = (Symbol.Bid + Symbol.Ask) / 2.0;

                    if (_predictedState == 2)
                    {
                        if (oppositeBuyAllowed && depthBuyAllowed && wickRatioBuyAllowed && hftTimingBuyAllowed)
                        {
                            double targetPrice;
                            if (EnableDynamicPricing)
                            {
                                targetPrice = dynamicMidPrice - (0.5 - 0.5 * obi) * spread - LimitOffsetPips * Symbol.PipSize;
                            }
                            else
                            {
                                targetPrice = Symbol.Bid - LimitOffsetPips * Symbol.PipSize;
                            }

                            Print("Markov Predicts BULLISH. Depth Spike, Opposite, Wick Ratio & HFT timing passed. Placing BUY Limit at {0} (Dynamic: {1}).", targetPrice, EnableDynamicPricing);
                            PlaceLimitOrder(TradeType.Buy, SymbolName, _volumeInUnits, targetPrice, "HMM_Buy", StopLossPips, TakeProfitPips, ProtectionType.Relative);
                        }
                    }
                    else if (_predictedState == 0)
                    {
                        if (oppositeSellAllowed && depthSellAllowed && wickRatioSellAllowed && hftTimingSellAllowed)
                        {
                            double targetPrice;
                            if (EnableDynamicPricing)
                            {
                                targetPrice = dynamicMidPrice + (0.5 + 0.5 * obi) * spread + LimitOffsetPips * Symbol.PipSize;
                            }
                            else
                            {
                                targetPrice = Symbol.Ask + LimitOffsetPips * Symbol.PipSize;
                            }

                            Print("Markov Predicts BEARISH. Depth Spike, Opposite, Wick Ratio & HFT timing passed. Placing SELL Limit at {0} (Dynamic: {1}).", targetPrice, EnableDynamicPricing);
                            PlaceLimitOrder(TradeType.Sell, SymbolName, _volumeInUnits, targetPrice, "HMM_Sell", StopLossPips, TakeProfitPips, ProtectionType.Relative);
                        }
                    }

                    // Identify candlestick pattern in real-time
                    string activePattern = IdentifyCandlestickPattern(curr, Bars.Last(1), currentPrice);

                    // Log training completion once
                    if (_totalTransitions >= MinSamplesToPredict && !_trainingCompleteLogged)
                    {
                        _trainingCompleteLogged = true;
                        Print("Min training samples ({0}) reached. HMM predictions are now active.", MinSamplesToPredict);
                    }

                    // 3e. Periodic Log (Prints status once every 2 seconds / 20 transitions to avoid spam)
                    if (_totalTransitions % 20 == 0 && _totalTransitions != _lastPrintedTransition)
                    {
                        _lastPrintedTransition = _totalTransitions;
                        string predictStr = _predictedState == 2 ? "BUY" : (_predictedState == 0 ? "SELL" : "HOLD/SIDEWAYS");
                        
                        Print("Real-time Status -> HMM Prediction: {0} | Candle Pattern: {1} | Opposite Filter (B/S): {2}/{3} | Depth Spike Filter (B/S): {4}/{5} (Cur Vol: {6:F0}/{7:F0}, Avg Vol: {8:F0}/{9:F0}) | HFT Entry Score (B/S): {10:F1}/{11:F1} (OBI: {12:F2}, MPP: {13:F2} pips) | Wick Ratio (B/S): {14:P0}/{15:P0} (Limit: {16:P0}, Status: {17}/{18}) | Consecutive Losses: {19}", 
                            predictStr, activePattern, oppositeBuyAllowed, oppositeSellAllowed, depthBuyAllowed, depthSellAllowed, currentBidVolume, currentAskVolume, _averageBidVolume, _averageAskVolume, hesBuy, hesSell, obi, mpp, upperWickRatio, lowerWickRatio, WickRatioLimit, wickRatioBuyAllowed, wickRatioSellAllowed, _consecutiveLosses);

                        // Print DOM Level 2 debug info
                        string domDebugStr = "DOM Level 2 (Bids/Asks): ";
                        int printLevels = Math.Min(3, _marketDepth.BidEntries.Count);
                        for (int i = 0; i < printLevels; i++)
                        {
                            domDebugStr += $"[Bid{i+1}: Price={_marketDepth.BidEntries[i].Price}, Vol={_marketDepth.BidEntries[i].VolumeInUnits}] ";
                        }
                        for (int i = 0; i < printLevels; i++)
                        {
                            domDebugStr += $"[Ask{i+1}: Price={_marketDepth.AskEntries[i].Price}, Vol={_marketDepth.AskEntries[i].VolumeInUnits}] ";
                        }
                        Print(domDebugStr);
                    }
                }
            }
        }

        protected override void OnTimer()
        {
            if (Bars.Count < 2)
                return;

            double currentPrice = Symbol.Bid;
            double diff = currentPrice - _lastPrice;
            double threshold = TransitionThresholdPips * Symbol.PipSize;

            // 1. Determine Current State
            int currentState = 1; // Sideways
            if (diff > threshold)
            {
                currentState = 2; // Bullish
            }
            else if (diff < -threshold)
            {
                currentState = 0; // Bearish
            }

            // 2. Record State Transition
            _transitionMatrix[_lastState, currentState]++;
            _totalTransitions++;

            // 3. Update Prediction
            _predictedState = PredictNextState(currentState);

            // Update variables for the next timer cycle
            _lastState = currentState;
            _lastPrice = currentPrice;
        }

        private int PredictNextState(int currentState)
        {
            int bullishCount = _transitionMatrix[currentState, 2];
            int bearishCount = _transitionMatrix[currentState, 0];

            if (bullishCount > bearishCount)
            {
                return 2; // Predicts Bullish
            }
            else if (bearishCount > bullishCount)
            {
                return 0; // Predicts Bearish
            }

            return 1; // Sideways / Tie (No Action)
        }

        private string IdentifyCandlestickPattern(Bar curr, Bar c1, double currentPrice)
        {
            double open = curr.Open;
            double close = currentPrice;
            double high = Math.Max(curr.High, currentPrice);
            double low = Math.Min(curr.Low, currentPrice);
            double range = high - low;
            double body = Math.Abs(close - open);
            
            if (range <= 0)
                return "None";

            double upperWick = high - Math.Max(open, close);
            double lowerWick = Math.Min(open, close) - low;

            // 1. Marubozu
            if (body >= 0.9 * range)
            {
                return close > open ? "Bullish Marubozu" : "Bearish Marubozu";
            }

            // 2. Doji
            if (body <= 0.1 * range)
            {
                return "Doji";
            }

            // 3. Hammer / Hanging Man (Small body near top, long lower shadow)
            if (body <= 0.3 * range && lowerWick >= 2 * body && upperWick <= 0.1 * range)
            {
                return close > open ? "Hammer (Bullish)" : "Hanging Man (Bearish)";
            }

            // 4. Inverted Hammer / Shooting Star (Small body near bottom, long upper shadow)
            if (body <= 0.3 * range && upperWick >= 2 * body && lowerWick <= 0.1 * range)
            {
                return close > open ? "Inverted Hammer (Bullish)" : "Shooting Star (Bearish)";
            }

            // Engulfing and Harami patterns (Requires comparison with previous closed bar c1)
            double c1_open = c1.Open;
            double c1_close = c1.Close;
            double c1_body = Math.Abs(c1_close - c1_open);

            // 5. Engulfing
            if (c1_close < c1_open && close > open && open <= c1_close && close >= c1_open)
            {
                return "Bullish Engulfing";
            }
            if (c1_close > c1_open && close < open && open >= c1_close && close <= c1_open)
            {
                return "Bearish Engulfing";
            }

            // 6. Harami
            if (c1_close < c1_open && close > open && open >= c1_close && close <= c1_open && body < c1_body)
            {
                return "Bullish Harami";
            }
            if (c1_close > c1_open && close < open && open <= c1_close && close >= c1_open && body < c1_body)
            {
                return "Bearish Harami";
            }

            // Trend classification
            if (close > open)
                return "Standard Bullish";
            else if (close < open)
                return "Standard Bearish";

            return "None";
        }

        private void OnPositionsClosed(PositionClosedEventArgs args)
        {
            if (args.Position.NetProfit < 0)
            {
                _consecutiveLosses++;
                Print("Position closed in LOSS (Net: {0:F2}). Consecutive losses: {1}.", 
                    args.Position.NetProfit, _consecutiveLosses);
            }
            else
            {
                _consecutiveLosses = 0;
                Print("Position closed in PROFIT (Net: {0:F2}). Resetting consecutive losses.", args.Position.NetProfit);
            }
        }

        private void OnPendingOrdersCancelled(PendingOrderCancelledEventArgs args)
        {
            _pendingCancelOrderIds.Remove(args.PendingOrder.Id);
        }

        private void OnPendingOrdersFilled(PendingOrderFilledEventArgs args)
        {
            _pendingCancelOrderIds.Remove(args.PendingOrder.Id);
        }

        private void OnMarketDepthUpdated()
        {
            if (LogDomUpdates)
            {
                if (_marketDepth == null || _marketDepth.BidEntries.Count == 0 || _marketDepth.AskEntries.Count == 0)
                    return;

                string domUpdateStr = "DOM Level 2 Update -> ";
                int printLevels = Math.Min(3, _marketDepth.BidEntries.Count);
                for (int i = 0; i < printLevels; i++)
                {
                    domUpdateStr += $"[Bid{i+1}: Price={_marketDepth.BidEntries[i].Price}, Vol={_marketDepth.BidEntries[i].VolumeInUnits}] ";
                }
                for (int i = 0; i < printLevels; i++)
                {
                    domUpdateStr += $"[Ask{i+1}: Price={_marketDepth.AskEntries[i].Price}, Vol={_marketDepth.AskEntries[i].VolumeInUnits}] ";
                }
                Print(domUpdateStr);
            }
        }

        protected override void OnStop()
        {
            if (_marketDepth != null)
            {
                _marketDepth.Updated -= OnMarketDepthUpdated;
            }
            Print("HFT Markov Bot Stopped. Total recorded state transitions: {0}", _totalTransitions);
        }
    }
}
