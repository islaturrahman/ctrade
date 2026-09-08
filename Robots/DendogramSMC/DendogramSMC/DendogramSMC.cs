using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class DendogramSMC : Robot
    {
        [Parameter("Risk % Per Trade", DefaultValue = 1.0, MinValue = 0.1, Step = 0.1, Group = "Risk Management")]
        public double RiskPercent { get; set; }

        [Parameter("SMC Swing Length", DefaultValue = 5, MinValue = 2, Group = "SMC")]
        public int SwingLength { get; set; }

        [Parameter("Cluster Window Size", DefaultValue = 50, MinValue = 10, Group = "Machine Learning")]
        public int ClusterWindow { get; set; }

        [Parameter("Num Regimes (K)", DefaultValue = 3, MinValue = 2, MaxValue = 5, Group = "Machine Learning")]
        public int NumRegimes { get; set; }

        [Parameter("Regime Threshold (Trend)", DefaultValue = 0.001, MinValue = 0.0, Step = 0.0005, Group = "Machine Learning")]
        public double RegimeThreshold { get; set; }

        [Parameter("Confirm Bars", DefaultValue = 2, MinValue = 1, MaxValue = 5, Group = "Machine Learning")]
        public int ConfirmBars { get; set; }

        [Parameter("Target Risk-Reward Ratio", DefaultValue = 3.0, MinValue = 1.0, Step = 0.5, Group = "Risk Management")]
        public double TargetRR { get; set; }

        [Parameter("Use Trend Filter (EMA 200)", DefaultValue = true, Group = "Risk Management")]
        public bool UseTrendFilter { get; set; }

        [Parameter("Move to Break-Even", DefaultValue = true, Group = "Risk Management")]
        public bool MoveToBreakEven { get; set; }

        [Parameter("Break-Even Trigger RR", DefaultValue = 1.0, MinValue = 0.5, Step = 0.1, Group = "Risk Management")]
        public double BreakEvenTriggerRR { get; set; }

        [Parameter("Max Active Trades", DefaultValue = 1, MinValue = 1, MaxValue = 3, Group = "Risk Management")]
        public int MaxActiveTrades { get; set; }

        [Parameter("Enable Visuals", DefaultValue = true, Group = "Visuals")]
        public bool EnableVisuals { get; set; }

        private AverageTrueRange _atr;
        private ExponentialMovingAverage _ema200;
        private SimpleMovingAverage _sma50;

        private int _currentRegime = -1; // 1 = Bullish, -1 = Bearish, 0 = Ranging/Sideways
        private int _pendingRegime = -2;
        private int _regimeStreak = 0;
        
        private bool _isRegimeShift = false;

        private List<FeatureVector> _historicalFeatures = new List<FeatureVector>();

        // SMC State
        private OrderBlock _lastBullishOB = null;
        private OrderBlock _lastBearishOB = null;

        protected override void OnStart()
        {
            _atr = Indicators.AverageTrueRange(14, MovingAverageType.Simple);
            _ema200 = Indicators.ExponentialMovingAverage(Bars.ClosePrices, 200);
            _sma50 = Indicators.SimpleMovingAverage(Bars.ClosePrices, 50);

            Print("SMC + Dendrogram ML Strategy Started with Bugfixes and Trend Alignment.");
            
            if (EnableVisuals)
            {
                UpdateChartInfo("Initializing...");
            }
        }

        protected override void OnTick()
        {
            ManageActivePositions();
        }

        protected override void OnBar()
        {
            InvalidateOBs();
            UpdateSMC();
            UpdateRegime();

            if (_isRegimeShift)
            {
                ManagePendingOrders();
                
                // Check if we are already at max positions
                if (Positions.Count(p => p.Label == "SMC_Dendrogram_Opt") < MaxActiveTrades)
                {
                    if (_currentRegime == 1) // Bullish Shift
                    {
                        // Trend Filter Check
                        if (!UseTrendFilter || Bars.ClosePrices.Last(1) > _ema200.Result.Last(1))
                        {
                            if (_lastBullishOB != null)
                            {
                                PlaceLimitOrMarketOrder(TradeType.Buy, _lastBullishOB.Top, _lastBullishOB.Bottom);
                            }
                        }
                    }
                    else if (_currentRegime == -1) // Bearish Shift
                    {
                        // Trend Filter Check
                        if (!UseTrendFilter || Bars.ClosePrices.Last(1) < _ema200.Result.Last(1))
                        {
                            if (_lastBearishOB != null)
                            {
                                PlaceLimitOrMarketOrder(TradeType.Sell, _lastBearishOB.Bottom, _lastBearishOB.Top);
                            }
                        }
                    }
                }
                
                _isRegimeShift = false;
            }

            if (EnableVisuals)
            {
                DrawSMCVisuals();
            }
        }

        private void InvalidateOBs()
        {
            double close = Bars.ClosePrices.Last(1);
            
            if (_lastBullishOB != null && close < _lastBullishOB.Bottom)
            {
                _lastBullishOB = null;
                if (EnableVisuals) Chart.RemoveObject("BullishOB");
                Print("Bullish OB broken/mitigated.");
            }
            
            if (_lastBearishOB != null && close > _lastBearishOB.Top)
            {
                _lastBearishOB = null;
                if (EnableVisuals) Chart.RemoveObject("BearishOB");
                Print("Bearish OB broken/mitigated.");
            }
        }

        private void UpdateSMC()
        {
            // BUGFIX: candidate index must be offset by SwingLength to prevent out-of-bounds future checks
            int index = Bars.ClosePrices.Count - 1 - SwingLength;
            if (index < SwingLength) return;

            bool isSwingHigh = true;
            bool isSwingLow = true;

            for (int i = 1; i <= SwingLength; i++)
            {
                if (Bars.HighPrices[index] <= Bars.HighPrices[index - i] || Bars.HighPrices[index] <= Bars.HighPrices[index + i])
                    isSwingHigh = false;
                
                if (Bars.LowPrices[index] >= Bars.LowPrices[index - i] || Bars.LowPrices[index] >= Bars.LowPrices[index + i])
                    isSwingLow = false;
            }

            if (isSwingHigh)
            {
                _lastBearishOB = new OrderBlock 
                { 
                    Top = Bars.HighPrices[index], 
                    Bottom = Math.Min(Bars.OpenPrices[index], Bars.ClosePrices[index]), 
                    BarIndex = index 
                };
            }
            if (isSwingLow)
            {
                _lastBullishOB = new OrderBlock 
                { 
                    Top = Math.Max(Bars.OpenPrices[index], Bars.ClosePrices[index]), 
                    Bottom = Bars.LowPrices[index], 
                    BarIndex = index 
                };
            }
        }

        private void UpdateRegime()
        {
            if (Bars.ClosePrices.Count < 50) return;

            // STABILITY FIX: Use SMA relative distance as Trend feature and ATR/Close as Volatility feature
            double currentSMA = _sma50.Result.Last(1);
            double currentTrendDist = (Bars.ClosePrices.Last(1) - currentSMA) / currentSMA;
            double currentVolatility = _atr.Result.Last(1) / Bars.ClosePrices.Last(1);

            _historicalFeatures.Add(new FeatureVector { TrendDist = currentTrendDist, Volatility = currentVolatility, Price = Bars.ClosePrices.Last(1) });
            if (_historicalFeatures.Count > ClusterWindow)
            {
                _historicalFeatures.RemoveAt(0);
            }

            if (_historicalFeatures.Count == ClusterWindow)
            {
                int newRegime = CalculateDendrogramRegime(_historicalFeatures);
                
                if (newRegime != _pendingRegime)
                {
                    _pendingRegime = newRegime;
                    _regimeStreak = 1;
                }
                else
                {
                    _regimeStreak++;
                }

                if (_regimeStreak >= ConfirmBars && _currentRegime != _pendingRegime)
                {
                    _isRegimeShift = true;
                    Print($"Regime Shift Confirmed: {GetRegimeName(_currentRegime)} -> {GetRegimeName(_pendingRegime)}");
                    _currentRegime = _pendingRegime;
                }

                if (EnableVisuals)
                {
                    UpdateChartInfo($"Regime: {GetRegimeName(_currentRegime)}");
                }
            }
        }

        private int CalculateDendrogramRegime(List<FeatureVector> data)
        {
            double minTrend = data.Min(f => f.TrendDist);
            double maxTrend = data.Max(f => f.TrendDist);
            double minVol = data.Min(f => f.Volatility);
            double maxVol = data.Max(f => f.Volatility);

            double trendRange = Math.Abs(maxTrend - minTrend) < 1e-9 ? 1.0 : (maxTrend - minTrend);
            double volRange = Math.Abs(maxVol - minVol) < 1e-9 ? 1.0 : (maxVol - minVol);

            List<NormalizedFeature> normalizedList = data.Select(f => new NormalizedFeature
            {
                NormTrendDist = (f.TrendDist - minTrend) / trendRange,
                NormVolatility = (f.Volatility - minVol) / volRange,
                Original = f
            }).ToList();

            List<Cluster> clusters = normalizedList.Select((nf, i) => new Cluster { Id = i, Features = new List<NormalizedFeature> { nf } }).ToList();

            while (clusters.Count > NumRegimes)
            {
                double minDistance = double.MaxValue;
                int mergeA = -1;
                int mergeB = -1;

                for (int i = 0; i < clusters.Count; i++)
                {
                    for (int j = i + 1; j < clusters.Count; j++)
                    {
                        double dist = CalculateDistance(clusters[i], clusters[j]);
                        if (dist < minDistance)
                        {
                            minDistance = dist;
                            mergeA = i;
                            mergeB = j;
                        }
                    }
                }

                clusters[mergeA].Features.AddRange(clusters[mergeB].Features);
                clusters.RemoveAt(mergeB);
            }

            NormalizedFeature latestNorm = normalizedList.Last();
            Cluster activeCluster = clusters.First(c => c.Features.Contains(latestNorm));

            double avgTrendDist = activeCluster.Features.Average(f => f.Original.TrendDist);
            
            if (avgTrendDist > RegimeThreshold)
            {
                return 1; // Bullish
            }
            else if (avgTrendDist < -RegimeThreshold)
            {
                return -1; // Bearish
            }
            
            return 0; // Neutral/Ranging
        }

        private double CalculateDistance(Cluster a, Cluster b)
        {
            double sum = 0;
            int count = 0;
            foreach (var p1 in a.Features)
            {
                foreach (var p2 in b.Features)
                {
                    sum += Math.Pow(p1.NormTrendDist - p2.NormTrendDist, 2) + Math.Pow(p1.NormVolatility - p2.NormVolatility, 2);
                    count++;
                }
            }
            return sum / count;
        }

        private void PlaceLimitOrMarketOrder(TradeType tradeType, double targetPrice, double fallbackSL)
        {
            double currentClose = Bars.ClosePrices.Last(1);
            
            // Tight Structural Invalidation Stop Loss (OB Bound + 0.5 * ATR buffer) instead of wide cluster SL
            double atrVal = _atr.Result.Last(1);
            double slPrice = tradeType == TradeType.Buy ? (fallbackSL - atrVal * 0.5) : (fallbackSL + atrVal * 0.5);

            // Limit order direction check
            if (tradeType == TradeType.Buy && targetPrice >= Symbol.Ask)
            {
                if (currentClose > slPrice)
                {
                    ExecuteMarketOrderOptimized(TradeType.Buy, slPrice);
                }
                return;
            }
            else if (tradeType == TradeType.Sell && targetPrice <= Symbol.Bid)
            {
                if (currentClose < slPrice)
                {
                    ExecuteMarketOrderOptimized(TradeType.Sell, slPrice);
                }
                return;
            }

            double riskAmount = Account.Balance * (RiskPercent / 100);
            double slDistance = Math.Abs(targetPrice - slPrice);
            
            if (slDistance <= 0) return;

            double volume = Symbol.NormalizeVolumeInUnits(riskAmount / (slDistance * Symbol.TickValue / Symbol.TickSize), RoundingMode.Down);

            if (volume >= Symbol.VolumeInUnitsMin)
            {
                double stopLossPips = slDistance / Symbol.PipSize;
                double takeProfitPips = stopLossPips * TargetRR;

                PlaceLimitOrder(tradeType, SymbolName, volume, targetPrice, "SMC_Dendrogram_Opt", stopLossPips, takeProfitPips);
                Print($"Placed {tradeType} Limit at {targetPrice}. SL: {slPrice} ({stopLossPips:F1} pips). TP: {takeProfitPips:F1} pips");
            }
        }

        private void ExecuteMarketOrderOptimized(TradeType tradeType, double slPrice)
        {
            double entryPrice = Bars.ClosePrices.Last(1);
            double slDistance = Math.Abs(entryPrice - slPrice);
            if (slDistance <= 0) return;

            double riskAmount = Account.Balance * (RiskPercent / 100);
            double volume = Symbol.NormalizeVolumeInUnits(riskAmount / (slDistance * Symbol.TickValue / Symbol.TickSize), RoundingMode.Down);

            if (volume >= Symbol.VolumeInUnitsMin)
            {
                double stopLossPips = slDistance / Symbol.PipSize;
                double takeProfitPips = stopLossPips * TargetRR;

                ExecuteMarketOrder(tradeType, SymbolName, volume, "SMC_Dendrogram_Opt", stopLossPips, takeProfitPips);
                Print($"Executed Market {tradeType} at {entryPrice}. SL: {slPrice} ({stopLossPips:F1} pips). TP: {takeProfitPips:F1} pips");
            }
        }

        private void ManagePendingOrders()
        {
            foreach (var order in PendingOrders)
            {
                if (order.Label == "SMC_Dendrogram_Opt")
                {
                    CancelPendingOrder(order);
                }
            }
        }

        private void ManageActivePositions()
        {
            foreach (var position in Positions)
            {
                if (position.Label == "SMC_Dendrogram_Opt")
                {
                    if (MoveToBreakEven)
                    {
                        double entryPrice = position.EntryPrice;
                        double currentPrice = position.TradeType == TradeType.Buy ? Symbol.Bid : Symbol.Ask;
                        double stopLoss = position.StopLoss ?? 0;
                        
                        if (position.TradeType == TradeType.Buy)
                        {
                            double targetDistance = (position.TakeProfit ?? 0) - entryPrice;
                            double currentProfitDistance = currentPrice - entryPrice;
                            if (currentProfitDistance >= targetDistance * (BreakEvenTriggerRR / TargetRR) && stopLoss < entryPrice)
                            {
                                ModifyPosition(position, entryPrice, position.TakeProfit);
                                Print($"Position {position.Id} moved to Break-Even at {entryPrice}");
                            }
                        }
                        else
                        {
                            double targetDistance = entryPrice - (position.TakeProfit ?? 0);
                            double currentProfitDistance = entryPrice - currentPrice;
                            if (currentProfitDistance >= targetDistance * (BreakEvenTriggerRR / TargetRR) && stopLoss > entryPrice)
                            {
                                ModifyPosition(position, entryPrice, position.TakeProfit);
                                Print($"Position {position.Id} moved to Break-Even at {entryPrice}");
                            }
                        }
                    }
                }
            }
        }

        private void DrawSMCVisuals()
        {
            if (_lastBullishOB != null)
            {
                Chart.DrawRectangle("BullishOB", _lastBullishOB.BarIndex, _lastBullishOB.Top, Bars.ClosePrices.Count, _lastBullishOB.Bottom, Color.FromArgb(40, 38, 166, 154));
            }
            if (_lastBearishOB != null)
            {
                Chart.DrawRectangle("BearishOB", _lastBearishOB.BarIndex, _lastBearishOB.Top, Bars.ClosePrices.Count, _lastBearishOB.Bottom, Color.FromArgb(40, 239, 83, 80));
            }
        }

        private void UpdateChartInfo(string regimeStatus)
        {
            Color displayColor = Color.White;
            if (_currentRegime == 1) displayColor = Color.LimeGreen;
            if (_currentRegime == -1) displayColor = Color.Tomato;
            if (_currentRegime == 0) displayColor = Color.Gold;

            string text = $"SMC + Dendrogram ML Strategy\n{regimeStatus}\nK={NumRegimes} | Win={ClusterWindow}";
            Chart.DrawStaticText("ML_Status", text, VerticalAlignment.Top, HorizontalAlignment.Left, displayColor);
        }

        private string GetRegimeName(int regime)
        {
            switch (regime)
            {
                case 1: return "BULLISH (Up-Trend)";
                case -1: return "BEARISH (Down-Trend)";
                case 0: return "RANGING (Sideways)";
                default: return "UNKNOWN";
            }
        }

        private class FeatureVector
        {
            public double TrendDist { get; set; }
            public double Volatility { get; set; }
            public double Price { get; set; }
        }

        private class NormalizedFeature
        {
            public double NormTrendDist { get; set; }
            public double NormVolatility { get; set; }
            public FeatureVector Original { get; set; }
        }

        private class Cluster
        {
            public int Id { get; set; }
            public List<NormalizedFeature> Features { get; set; }
        }

        private class OrderBlock
        {
            public double Top { get; set; }
            public double Bottom { get; set; }
            public int BarIndex { get; set; }
        }
    }
}