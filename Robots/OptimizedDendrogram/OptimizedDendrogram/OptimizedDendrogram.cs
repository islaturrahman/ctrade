using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class OptimizedDendrogram : Robot
    {
        // ═══════════════════════════════════════
        //  STRATEGY PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("Lookback Period", Group = "Strategy", DefaultValue = 20, MinValue = 5)]
        public int Lookback { get; set; }

        [Parameter("Number of Bins", Group = "Strategy", DefaultValue = 10, MinValue = 2)]
        public int NumBins { get; set; }

        [Parameter("Bias Threshold", Group = "Strategy", DefaultValue = 0.01, MinValue = 0.0)]
        public double BiasThreshold { get; set; }

        [Parameter("Use ATR for SL/TP", Group = "Strategy", DefaultValue = true)]
        public bool UseAtr { get; set; }

        [Parameter("Volume Filter", Group = "Strategy", DefaultValue = true)]
        public bool VolFilter { get; set; }

        [Parameter("Volume Multiplier", Group = "Strategy", DefaultValue = 1.0, MinValue = 0.0)]
        public double VolMult { get; set; }

        [Parameter("SL Multiplier", Group = "Strategy", DefaultValue = 2.0, MinValue = 0.1)]
        public double SlMult { get; set; }

        [Parameter("TP Multiplier", Group = "Strategy", DefaultValue = 1.5, MinValue = 0.1)]
        public double TpMult { get; set; }

        // ═══════════════════════════════════════
        //  RISK MANAGEMENT
        // ═══════════════════════════════════════

        [Parameter("Volume (Lots)", Group = "Risk Management", DefaultValue = 0.1, MinValue = 0.01)]
        public double LotSize { get; set; }

        // ═══════════════════════════════════════
        //  VISUALS
        // ═══════════════════════════════════════

        [Parameter("Show Chart Visuals", Group = "Visuals", DefaultValue = true)]
        public bool ShowVisuals { get; set; }

        // ═══════════════════════════════════════
        //  PRIVATE FIELDS
        // ═══════════════════════════════════════

        private const string BotLabel = "OptimizedDendrogram";
        private AverageTrueRange _atr;

        private int _buySignals = 0;
        private int _sellSignals = 0;
        private int _stepsEvaluated = 0;

        protected override void OnStart()
        {
            // Initializing ATR indicator matching the Python setup (14 period, simple moving average)
            _atr = Indicators.AverageTrueRange(14, MovingAverageType.Simple);

            // Smart Volume Filter Check: Disable filter if data does not contain volume
            double totalVol = 0;
            for (int i = 0; i < Bars.Count; i++)
            {
                totalVol += Bars.TickVolumes[i];
            }

            if (totalVol == 0 || double.IsNaN(totalVol))
            {
                Print($"[{BotLabel}] WARNING: Volume is 0/NaN. Auto-disabling VolFilter.");
                VolFilter = false;
            }

            Print($"🤖 {BotLabel} Started | Lookback={Lookback} | NumBins={NumBins} | Bias={BiasThreshold} | UseAtr={UseAtr}");
        }

        protected override void OnBar()
        {
            // Ensure we have enough bars to process lookback and calculate indicators safely
            if (Bars.Count < Lookback + 1)
                return;

            _stepsEvaluated++;

            // Extract window data for closed bars (from index endIndex - Lookback + 1 to endIndex)
            int endIndex = Bars.Count - 2;
            int startIndex = endIndex - Lookback + 1;

            double[] windowOpen = new double[Lookback];
            double[] windowClose = new double[Lookback];
            double[] windowVol = new double[Lookback];

            for (int i = 0; i < Lookback; i++)
            {
                int idx = startIndex + i;
                windowOpen[i] = Bars.OpenPrices[idx];
                windowClose[i] = Bars.ClosePrices[idx];
                windowVol[i] = Bars.TickVolumes[idx];
            }

            double currentPrice = windowClose[Lookback - 1]; // Close of last completed bar
            double pMin = windowClose.Min();
            double pMax = windowClose.Max();

            if (pMax == pMin)
                return;

            // Calculate bins spacing
            double binWidth = (pMax - pMin) / (NumBins - 1);
            double[] bins = new double[NumBins];
            for (int j = 0; j < NumBins; j++)
            {
                bins[j] = pMin + j * binWidth;
            }

            // Calculate positive volume mean to serve as default volume
            List<double> posVols = new List<double>();
            for (int i = 0; i < Lookback; i++)
            {
                if (windowVol[i] > 0)
                    posVols.Add(windowVol[i]);
            }
            double meanVol = posVols.Count > 0 ? posVols.Average() : 100.0;

            // Volume filter validation
            if (VolFilter)
            {
                double currentVol = windowVol[Lookback - 1];
                if (currentVol < meanVol * VolMult)
                {
                    if (ShowVisuals)
                    {
                        UpdateDashboard(currentPrice, double.NaN, 0, false, meanVol, currentVol);
                    }
                    return;
                }
            }

            // Initialize bins volume profile arrays
            double[] binVolumes = new double[NumBins];
            double[] binDeltas = new double[NumBins];

            // Build Dendrogram volume profile from historical lookback window
            for (int i = 0; i < Lookback; i++)
            {
                double p = windowClose[i];
                double v = windowVol[i] <= 0 ? meanVol : windowVol[i];
                double op = windowOpen[i];
                double cl = windowClose[i];
                double d = cl >= op ? v : -v;

                int binIdx = (int)((p - pMin) / binWidth);
                if (binIdx < 0) binIdx = 0;
                if (binIdx >= NumBins) binIdx = NumBins - 1;

                binVolumes[binIdx] += v;
                binDeltas[binIdx] += d;
            }

            // Identify the Point of Control (POC) bin index
            int pocBinIdx = 0;
            double maxVol = -1;
            for (int j = 0; j < NumBins; j++)
            {
                if (binVolumes[j] > maxVol)
                {
                    maxVol = binVolumes[j];
                    pocBinIdx = j;
                }
            }

            double pocPrice = bins[pocBinIdx];
            double pocDelta = binDeltas[pocBinIdx];
            double pocVol = binVolumes[pocBinIdx];

            // Calculate bias from Point of Control imbalance
            int pocBias = 0;
            if (pocVol > 0)
            {
                double imbalance = pocDelta / pocVol;
                if (imbalance > BiasThreshold)
                    pocBias = 1;
                else if (imbalance < -BiasThreshold)
                    pocBias = -1;
            }

            // Determine the risk step size (ATR or Bin Width)
            double atrVal = _atr.Result[endIndex];
            if (double.IsNaN(atrVal) || atrVal <= 0)
            {
                atrVal = Symbol.PipSize * 10; // Simple fallback if ATR is not calculated yet
            }

            double step = UseAtr ? atrVal : binWidth;

            // Signal trigger conditions
            bool buySignal = currentPrice > pocPrice && pocBias == 1;
            bool sellSignal = currentPrice < pocPrice && pocBias == -1;

            var position = Positions.Find(BotLabel, SymbolName);

            if (buySignal)
            {
                _buySignals++;
                if (position == null)
                {
                    ExecuteOrder(TradeType.Buy, step);
                }
                else if (position.TradeType == TradeType.Sell)
                {
                    ClosePosition(position);
                    ExecuteOrder(TradeType.Buy, step);
                }
            }
            else if (sellSignal)
            {
                _sellSignals++;
                if (position == null)
                {
                    ExecuteOrder(TradeType.Sell, step);
                }
                else if (position.TradeType == TradeType.Buy)
                {
                    ClosePosition(position);
                    ExecuteOrder(TradeType.Sell, step);
                }
            }

            // Handle UI drawings
            if (ShowVisuals)
            {
                DrawVisuals(startIndex, endIndex, pocPrice, pocBias);
                UpdateDashboard(currentPrice, pocPrice, pocBias, true, meanVol, windowVol[Lookback - 1]);
            }
        }

        private void ExecuteOrder(TradeType tradeType, double step)
        {
            double slPips = (SlMult * step) / Symbol.PipSize;
            double tpPips = (TpMult * step) / Symbol.PipSize;

            double volume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(LotSize));
            if (volume < Symbol.VolumeInUnitsMin) volume = Symbol.VolumeInUnitsMin;

            var result = ExecuteMarketOrder(tradeType, SymbolName, volume, BotLabel, slPips, tpPips);
            if (result.IsSuccessful)
            {
                Print($"✅ {tradeType} Position Opened | SL: {slPips:F1} pips | TP: {tpPips:F1} pips | Step: {step:F5}");
            }
            else
            {
                Print($"❌ Failed to open {tradeType} position: {result.Error}");
            }
        }

        private void DrawVisuals(int startIndex, int endIndex, double pocPrice, int pocBias)
        {
            Color pocColor = pocBias == 1 ? Color.LimeGreen : (pocBias == -1 ? Color.Red : Color.Gray);
            string biasText = pocBias == 1 ? "BULLISH BIAS" : (pocBias == -1 ? "BEARISH BIAS" : "NEUTRAL");

            // Draw current POC level as a visual line spanning across the lookback window
            Chart.DrawTrendLine("Dendrogram_POC", startIndex, pocPrice, endIndex, pocPrice, pocColor, 2, LineStyle.Solid);
            Chart.DrawText("Dendrogram_POC_Txt", $" POC: {pocPrice:F4} ({biasText})", endIndex + 1, pocPrice, pocColor);
        }

        private void UpdateDashboard(double currentPrice, double pocPrice, int pocBias, bool volPassed, double meanVol, double currentVol)
        {
            string biasStr = pocBias == 1 ? "Bullish (Buy)" : (pocBias == -1 ? "Bearish (Sell)" : "Neutral");
            string volStr = volPassed ? "PASSED" : "FAILED";
            var activePos = Positions.Find(BotLabel, SymbolName);
            string posStr = activePos != null ? $"{activePos.TradeType} (SL: {activePos.StopLoss:F4}, TP: {activePos.TakeProfit:F4})" : "None";

            var text = $"═════════════════════════════════════\n" +
                       $"  OPTIMIZED DENDROGRAM STRATEGY\n" +
                       $"═════════════════════════════════════\n" +
                       $" Current Price  : {currentPrice:F4}\n" +
                       $" POC Price      : {(double.IsNaN(pocPrice) ? "N/A" : pocPrice.ToString("F4"))}\n" +
                       $" POC Bias       : {biasStr}\n" +
                       $" Volume Filter  : {volStr} (Current: {currentVol:F0} | Threshold: {(meanVol * VolMult):F0})\n" +
                       $" Steps Evaluated: {_stepsEvaluated}\n" +
                       $" Signals Trigger: BUY={_buySignals} | SELL={_sellSignals}\n" +
                       $" Active Position: {posStr}\n" +
                       $"═════════════════════════════════════";

            Color panelColor = pocBias == 1 ? Color.LimeGreen : (pocBias == -1 ? Color.Tomato : Color.LightSteelBlue);
            Chart.DrawStaticText("Dendrogram_Dashboard", text, VerticalAlignment.Top, HorizontalAlignment.Left, panelColor);
        }
    }
}
