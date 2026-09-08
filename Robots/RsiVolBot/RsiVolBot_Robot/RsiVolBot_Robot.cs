using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

/*
 * RsiVolBot_Robot – Signal Display + Order Entry with MA Filter
 *
 * Entry : RSI 30/70 crossover + Volume Profile filter + optional SMC/Delta/Gamma/Volume filters
 * SL    : Swing Low − buffer (Buy) | Swing High + buffer (Sell)
 * TP1   : POC  (50% volume, adjustable by Tp1Ratio)
 * TP2   : VAH  (Buy) | VAL (Sell)  (50% volume)
 * Mode  : Single entry – hanya 1 posisi aktif per arah, sinyal berlawanan menutup posisi lama
 * Trend Filter: MA 100 + VP Expansion Guard (mencegah entry counter-trend saat pasar breakout trending)
 */

namespace cAlgo
{
    public enum VpFilterMode2
    {
        Rolling,
        PreviousBlock,
        DevelopingBlock
    }

    public enum SmcFilterMode2
    {
        Both,
        OrderBlock,
        FairValueGap
    }

    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class RsiVolBot_Robot : Robot
    {
        // ── RSI ───────────────────────────────────────────────────────────────
        [Parameter("Source", Group = "RSI")]
        public DataSeries Source { get; set; }

        [Parameter("Periods", DefaultValue = 14, Group = "RSI")]
        public int Periods { get; set; }

        // ── Volume Profile ────────────────────────────────────────────────────
        [Parameter("Filter Signals with VP", DefaultValue = true, Group = "Volume Profile")]
        public bool FilterSignalsWithVp { get; set; }

        [Parameter("VP Filter Mode", DefaultValue = VpFilterMode2.PreviousBlock, Group = "Volume Profile")]
        public VpFilterMode2 FilterMode { get; set; }

        [Parameter("Use POC Magnet Filter", DefaultValue = false, Group = "Volume Profile")]
        public bool UsePocMagnetFilter { get; set; }

        [Parameter("Show Signal Labels", DefaultValue = true, Group = "Volume Profile")]
        public bool ShowSignalLabels { get; set; }

        [Parameter("Lookback (Bars)", DefaultValue = 100, MinValue = 10, Group = "Volume Profile")]
        public int VpLookback { get; set; }

        [Parameter("Bins", DefaultValue = 50, MinValue = 5, Group = "Volume Profile")]
        public int VpBins { get; set; }

        [Parameter("Value Area %", DefaultValue = 70.0, MinValue = 10.0, MaxValue = 100.0, Group = "Volume Profile")]
        public double ValueAreaPercentage { get; set; }

        // ── Strict Confirmation ───────────────────────────────────────────────
        [Parameter("Use Volume Filter", DefaultValue = false, Group = "Strict Confirmation")]
        public bool UseVolumeFilter { get; set; }

        [Parameter("Volume SMA Periods", DefaultValue = 20, Group = "Strict Confirmation")]
        public int VolumeSmaPeriods { get; set; }

        [Parameter("Min Volume Multiplier", DefaultValue = 1.0, Group = "Strict Confirmation")]
        public double MinVolumeMultiplier { get; set; }

        [Parameter("Confirm with Delta", DefaultValue = false, Group = "Strict Confirmation")]
        public bool ConfirmWithDelta { get; set; }

        [Parameter("Confirm with Gamma", DefaultValue = false, Group = "Strict Confirmation")]
        public bool ConfirmWithGamma { get; set; }

        [Parameter("Use Trend Filter (MA)", DefaultValue = true, Group = "Strict Confirmation")]
        public bool UseTrendFilter { get; set; }

        [Parameter("Trend MA Period", DefaultValue = 100, Group = "Strict Confirmation")]
        public int TrendMaPeriod { get; set; }

        // ── SMC ───────────────────────────────────────────────────────────────
        [Parameter("Use SMC Filter", DefaultValue = false, Group = "Smart Money Concepts (SMC)")]
        public bool UseSmcFilter { get; set; }

        [Parameter("SMC Swing Period", DefaultValue = 5, MinValue = 2, MaxValue = 20, Group = "Smart Money Concepts (SMC)")]
        public int SmcSwingPeriod { get; set; }

        [Parameter("SMC Filter Mode", DefaultValue = SmcFilterMode2.Both, Group = "Smart Money Concepts (SMC)")]
        public SmcFilterMode2 FilterModeSMC { get; set; }

        [Parameter("Show OB Zones", DefaultValue = true, Group = "Smart Money Concepts (SMC)")]
        public bool ShowObZones { get; set; }

        [Parameter("Show FVG Zones", DefaultValue = true, Group = "Smart Money Concepts (SMC)")]
        public bool ShowFvgZones { get; set; }

        // ── Trading ───────────────────────────────────────────────────────────
        [Parameter("Label", DefaultValue = "RsiVolBot", Group = "Trading")]
        public string TradeLabel { get; set; }

        [Parameter("Risk Percent", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10.0, Group = "Trading")]
        public double RiskPercent { get; set; }

        [Parameter("Min Volume (Lots)", DefaultValue = 0.01, MinValue = 0.01, Group = "Trading")]
        public double MinVolumeLots { get; set; }

        [Parameter("Max Volume (Lots)", DefaultValue = 10.0, MinValue = 0.01, Group = "Trading")]
        public double MaxVolumeLots { get; set; }

        [Parameter("SL Buffer (Pips)", DefaultValue = 5.0, MinValue = 0.0, Group = "Trading")]
        public double SlBufferPips { get; set; }

        [Parameter("TP1 Ratio to POC (0.1-1.0)", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 1.0, Group = "Trading")]
        public double Tp1Ratio { get; set; }

        [Parameter("Fallback SL Pips (if no swing)", DefaultValue = 20.0, MinValue = 1.0, Group = "Trading")]
        public double FallbackSlPips { get; set; }

        // ── Private State ─────────────────────────────────────────────────────
        private RelativeStrengthIndex _rsi;
        private SimpleMovingAverage _volumeSma;
        private SimpleMovingAverage _trendMa;

        private double _lastSwingHigh;
        private double _lastSwingLow;
        private bool _isBullishStructure;

        private class Zone
        {
            public string Id { get; set; }
            public double TopPrice { get; set; }
            public double BottomPrice { get; set; }
            public int StartIndex { get; set; }
            public bool IsOB { get; set; }
            public bool IsBullish { get; set; }
            public bool IsMitigated { get; set; }
        }

        private List<Zone> _activeZones;

        // ── Lifecycle ─────────────────────────────────────────────────────────

        protected override void OnStart()
        {
            _rsi = Indicators.RelativeStrengthIndex(Source, Periods);
            _volumeSma = Indicators.SimpleMovingAverage(Bars.TickVolumes, VolumeSmaPeriods);
            _trendMa = Indicators.SimpleMovingAverage(Source, TrendMaPeriod);

            _lastSwingHigh = double.NaN;
            _lastSwingLow  = double.NaN;
            _isBullishStructure = true;
            _activeZones = new List<Zone>();

            Print("RsiVolBot started | Label={0} | Risk={1}%", TradeLabel, RiskPercent);
        }

        protected override void OnBar()
        {
            int index = Bars.Count - 2; // bar terakhir yang sudah tutup
            if (index < 1) return;
            ProcessBar(index);
        }

        protected override void OnTick()
        {
            // Ambil data harga running dan level indikator terbaru
            double runningClose = Symbol.Bid;
            int lastClosedBarIndex = Bars.Count - 2;

            double vah = 0, val = 0, poc = 0;
            bool hasVp = TryGetVpLevels(lastClosedBarIndex, out vah, out val, out poc);

            // Cek kondisi emergency close berdasarkan tren kuat berlawanan
            if (UseTrendFilter && hasVp)
            {
                double maVal = _trendMa.Result[lastClosedBarIndex];

                // 1. Tren Bullish Kuat (Harga > MA100 dan menembus ke atas VAH) -> Tutup posisi SELL jika ada
                if (runningClose > maVal && runningClose > vah)
                {
                    if (HasActivePosition(TradeType.Sell))
                    {
                        Print("Strong Bullish Trend detected (Price > MA100 & Price > VAH). Closing all SELL positions.");
                        ClosePositions(TradeType.Sell);
                    }
                }
                // 2. Tren Bearish Kuat (Harga < MA100 dan menembus ke bawah VAL) -> Tutup posisi BUY jika ada
                else if (runningClose < maVal && runningClose < val)
                {
                    if (HasActivePosition(TradeType.Buy))
                    {
                        Print("Strong Bearish Trend detected (Price < MA100 & Price < VAL). Closing all BUY positions.");
                        ClosePositions(TradeType.Buy);
                    }
                }
            }

            // Periksa posisi TP2 yang aktif (absorpsi setelah TP1 kena)
            foreach (var pos in Positions)
            {
                if (pos.SymbolName == SymbolName && pos.Label == TradeLabel + "_TP2")
                {
                    // Cari apakah posisi TP1 yang berpasangan masih ada
                    bool tp1StillExists = false;
                    foreach (var p1 in Positions)
                    {
                        if (p1.SymbolName == SymbolName && p1.Label == TradeLabel + "_TP1" && p1.TradeType == pos.TradeType)
                        {
                            tp1StillExists = true;
                            break;
                        }
                    }

                    // Jika TP1 sudah tidak ada (berarti sudah terkena Take Profit)
                    // dan posisi TP2 berbalik arah menjadi negatif/rugi (GrossProfit < 0)
                    if (!tp1StillExists && pos.GrossProfit <= 0)
                    {
                        Print("TP1 has been hit. TP2 is starting to turn negative ({0} profit). Closing TP2 to prevent loss.", pos.GrossProfit);
                        ClosePosition(pos);
                    }
                }
            }
        }

        protected override void OnStop() { }

        // ── Core Logic ────────────────────────────────────────────────────────

        private void ProcessBar(int index)
        {
            // 1. SMC Market Structure Tracking
            if (IsSwingHigh(index, SmcSwingPeriod))
                _lastSwingHigh = Bars.HighPrices[index - SmcSwingPeriod];

            if (IsSwingLow(index, SmcSwingPeriod))
                _lastSwingLow = Bars.LowPrices[index - SmcSwingPeriod];

            double currentClose = Bars.ClosePrices[index];

            // BOS / CHoCH + OB zone creation
            if (!double.IsNaN(_lastSwingHigh) && currentClose > _lastSwingHigh)
            {
                bool isChoch = !_isBullishStructure;
                _isBullishStructure = true;
                Chart.DrawText("SMC_Break_" + index, "  " + (isChoch ? "CHoCH" : "BOS"), index,
                    Bars.HighPrices[index] + 5 * Symbol.PipSize, Color.LimeGreen);

                int obIdx = FindBullishObCandle(index);
                _activeZones.Add(new Zone
                {
                    Id = "SMC_OB_Bull_" + index, TopPrice = Bars.HighPrices[obIdx],
                    BottomPrice = Bars.LowPrices[obIdx], StartIndex = obIdx,
                    IsOB = true, IsBullish = true, IsMitigated = false
                });
                _lastSwingHigh = double.NaN;
            }
            else if (!double.IsNaN(_lastSwingLow) && currentClose < _lastSwingLow)
            {
                bool isChoch = _isBullishStructure;
                _isBullishStructure = false;
                Chart.DrawText("SMC_Break_" + index, "  " + (isChoch ? "CHoCH" : "BOS"), index,
                    Bars.LowPrices[index] - 5 * Symbol.PipSize, Color.Tomato);

                int obIdx = FindBearishObCandle(index);
                _activeZones.Add(new Zone
                {
                    Id = "SMC_OB_Bear_" + index, TopPrice = Bars.HighPrices[obIdx],
                    BottomPrice = Bars.LowPrices[obIdx], StartIndex = obIdx,
                    IsOB = true, IsBullish = false, IsMitigated = false
                });
                _lastSwingLow = double.NaN;
            }

            // Detect Fair Value Gaps
            if (index >= 2)
            {
                if (Bars.LowPrices[index - 2] > Bars.HighPrices[index])
                    _activeZones.Add(new Zone
                    {
                        Id = "SMC_FVG_Bull_" + index, TopPrice = Bars.LowPrices[index - 2],
                        BottomPrice = Bars.HighPrices[index], StartIndex = index - 2,
                        IsOB = false, IsBullish = true, IsMitigated = false
                    });
                else if (Bars.HighPrices[index - 2] < Bars.LowPrices[index])
                    _activeZones.Add(new Zone
                    {
                        Id = "SMC_FVG_Bear_" + index, TopPrice = Bars.LowPrices[index],
                        BottomPrice = Bars.HighPrices[index - 2], StartIndex = index - 2,
                        IsOB = false, IsBullish = false, IsMitigated = false
                    });
            }

            // 2. RSI Crossover check
            bool potentialBuy  = _rsi.Result[index] > 30 && _rsi.Result[index - 1] <= 30;
            bool potentialSell = _rsi.Result[index] < 70 && _rsi.Result[index - 1] >= 70;

            // 3. Get VP levels (always needed for SL/TP even if VP filter is off)
            double vah = 0, val = 0, poc = 0, minVpPrice = 0, maxVpPrice = 0;
            bool hasVp = TryGetVpLevels(index, out vah, out val, out poc, out minVpPrice, out maxVpPrice);

            bool triggerBuy  = false;
            bool triggerSell = false;

            if (potentialBuy || potentialSell)
            {
                triggerBuy  = potentialBuy;
                triggerSell = potentialSell;

                // Mencegah Entry jika harga berada di luar jangkauan (min/max price) historis lookback VP
                if (hasVp)
                {
                    if (currentClose < minVpPrice || currentClose > maxVpPrice)
                    {
                        Print("Entry blocked: Price {0} is outside VP absolute lookback range [{1} - {2}]", currentClose, minVpPrice, maxVpPrice);
                        triggerBuy = false;
                        triggerSell = false;
                    }
                }

                // VP signal filter
                if (FilterSignalsWithVp)
                {
                    if (!hasVp)
                    {
                        triggerBuy = triggerSell = false;
                    }
                    else
                    {
                        double currentOpen = Bars.OpenPrices[index];
                        double volume      = Bars.TickVolumes[index];
                        double barDelta    = GetBarDelta(index);
                        double gamma       = barDelta - GetBarDelta(index - 1);
                        bool volumeCond    = !UseVolumeFilter || (volume >= MinVolumeMultiplier * _volumeSma.Result[index]);

                        if (potentialBuy)
                        {
                            bool priceBelowVal  = val > 0 && currentClose < val;
                            bool isBullCandle   = currentClose > currentOpen;
                            bool pocMagnet      = !UsePocMagnetFilter || (poc > 0 && currentClose < poc);
                            bool deltaCond      = !ConfirmWithDelta || (barDelta > 0);
                            bool gammaCond      = !ConfirmWithGamma || (gamma > 0);
                            bool smcCond        = EvaluateSmcCondition(index, true);
                            triggerBuy = priceBelowVal && isBullCandle && pocMagnet && volumeCond && deltaCond && gammaCond && smcCond;
                        }
                        else if (potentialSell)
                        {
                            bool priceAboveVah  = vah > 0 && currentClose > vah;
                            bool isBearCandle   = currentClose < currentOpen;
                            bool pocMagnet      = !UsePocMagnetFilter || (poc > 0 && currentClose > poc);
                            bool deltaCond      = !ConfirmWithDelta || (barDelta < 0);
                            bool gammaCond      = !ConfirmWithGamma || (gamma < 0);
                            bool smcCond        = EvaluateSmcCondition(index, false);
                            triggerSell = priceAboveVah && isBearCandle && pocMagnet && volumeCond && deltaCond && gammaCond && smcCond;
                        }
                    }
                }

                // Trend Filter (MA 100 + VP Expansion Guard)
                if (UseTrendFilter && hasVp)
                {
                    double maVal = _trendMa.Result[index];
                    
                    // Kondisi 1: Bullish Trend (Harga di atas MA)
                    if (currentClose > maVal)
                    {
                        // VP Ranging: harga di dalam Value Area (VAL <= close <= VAH)
                        // VP Trending/Expansion: harga breakout ke atas VAH -> HANYA Buy yang boleh (Sell diblok)
                        bool isVpTrendingUp = currentClose > vah;
                        
                        if (isVpTrendingUp && triggerSell)
                        {
                            Print("Sell signal BLOCKED: Strong Bullish Trend (Close > MA100 & Close > VAH)");
                            triggerSell = false;
                        }
                    }
                    // Kondisi 2: Bearish Trend (Harga di bawah MA)
                    else if (currentClose < maVal)
                    {
                        // VP Trending/Expansion: harga breakout ke bawah VAL -> HANYA Sell yang boleh (Buy diblok)
                        bool isVpTrendingDown = currentClose < val;
                        
                        if (isVpTrendingDown && triggerBuy)
                        {
                            Print("Buy signal BLOCKED: Strong Bearish Trend (Close < MA100 & Close < VAL)");
                            triggerBuy = false;
                        }
                    }
                }

                // 4. Draw visual signals
                string chartIcon  = "ChartRsiSignal_" + index;
                string textLabel  = "TextRsiSignal_" + index;

                if (triggerBuy)
                {
                    double volume   = Bars.TickVolumes[index];
                    double cumDelta = GetCumulativeDelta(index);
                    double gamma    = GetBarDelta(index) - GetBarDelta(index - 1);
                    string lbl = string.Format("IDX:{0}\nVol:{1:N0}\nCD:{2:N0}\nG:{3:N0}", index, volume, cumDelta, gamma);

                    Chart.DrawIcon(chartIcon, ChartIconType.UpArrow, index,
                        Bars.LowPrices[index] - 5 * Symbol.PipSize, Color.LimeGreen);

                    if (ShowSignalLabels)
                        Chart.DrawText(textLabel, lbl, index,
                            Bars.LowPrices[index] - 15 * Symbol.PipSize, Color.LimeGreen);
                    else
                        Chart.RemoveObject(textLabel);

                    // Draw TP/SL lines pada chart
                    if (hasVp)
                    {
                        double slPx = !double.IsNaN(_lastSwingLow)
                            ? _lastSwingLow - SlBufferPips * Symbol.PipSize
                            : currentClose - FallbackSlPips * Symbol.PipSize;
                        double tp1Px = currentClose + (poc - currentClose) * Tp1Ratio;
                        DrawTpSlLines(index, slPx, tp1Px, vah, isBuy: true);
                    }

                    Print("BUY Signal | IDX:{0} | VP: poc={1:F5} vah={2:F5} val={3:F5}", index, poc, vah, val);

                    // 5. Execute order
                    if (hasVp)
                        TryOpenBuy(index, poc, vah, val);
                }
                else if (triggerSell)
                {
                    double volume   = Bars.TickVolumes[index];
                    double cumDelta = GetCumulativeDelta(index);
                    double gamma    = GetBarDelta(index) - GetBarDelta(index - 1);
                    string lbl = string.Format("IDX:{0}\nVol:{1:N0}\nCD:{2:N0}\nG:{3:N0}", index, volume, cumDelta, gamma);

                    Chart.DrawIcon(chartIcon, ChartIconType.DownArrow, index,
                        Bars.HighPrices[index] + 5 * Symbol.PipSize, Color.Tomato);

                    if (ShowSignalLabels)
                        Chart.DrawText(textLabel, lbl, index,
                            Bars.HighPrices[index] + 15 * Symbol.PipSize, Color.Tomato);
                    else
                        Chart.RemoveObject(textLabel);

                    if (hasVp)
                    {
                        double slPx = !double.IsNaN(_lastSwingHigh)
                            ? _lastSwingHigh + SlBufferPips * Symbol.PipSize
                            : currentClose + FallbackSlPips * Symbol.PipSize;
                        double tp1Px = currentClose - (currentClose - poc) * Tp1Ratio;
                        DrawTpSlLines(index, slPx, tp1Px, val, isBuy: false);
                    }

                    Print("SELL Signal | IDX:{0} | VP: poc={1:F5} vah={2:F5} val={3:F5}", index, poc, vah, val);

                    if (hasVp)
                        TryOpenSell(index, poc, vah, val);
                }
                else
                {
                    Chart.RemoveObject("ChartRsiSignal_" + index);
                    Chart.RemoveObject("TextRsiSignal_"  + index);
                }
            }

            // 6. Zone mitigation
            for (int i = _activeZones.Count - 1; i >= 0; i--)
            {
                var zone = _activeZones[i];
                if (zone.IsBullish)
                {
                    if (zone.IsOB)
                    { if (currentClose < zone.BottomPrice) { zone.IsMitigated = true; Chart.RemoveObject(zone.Id); } }
                    else
                    { if (Bars.LowPrices[index] <= zone.BottomPrice) { zone.IsMitigated = true; Chart.RemoveObject(zone.Id); } }
                }
                else
                {
                    if (zone.IsOB)
                    { if (currentClose > zone.TopPrice) { zone.IsMitigated = true; Chart.RemoveObject(zone.Id); } }
                    else
                    { if (Bars.HighPrices[index] >= zone.TopPrice) { zone.IsMitigated = true; Chart.RemoveObject(zone.Id); } }
                }
            }
            _activeZones.RemoveAll(z => z.IsMitigated);

            // 7. Draw swing lines & zones (hanya di bar terkini)
            if (index == Bars.Count - 2)
            {
                if (!double.IsNaN(_lastSwingHigh))
                {
                    Chart.DrawTrendLine("SMC_SwingHigh", index - 30, _lastSwingHigh, index, _lastSwingHigh, Color.Orange, 1, LineStyle.Dots);
                    Chart.DrawText("SMC_SwingHigh_Label", "  SH", index, _lastSwingHigh, Color.Orange);
                }
                else { Chart.RemoveObject("SMC_SwingHigh"); Chart.RemoveObject("SMC_SwingHigh_Label"); }

                if (!double.IsNaN(_lastSwingLow))
                {
                    Chart.DrawTrendLine("SMC_SwingLow", index - 30, _lastSwingLow, index, _lastSwingLow, Color.RoyalBlue, 1, LineStyle.Dots);
                    Chart.DrawText("SMC_SwingLow_Label", "  SL", index, _lastSwingLow, Color.RoyalBlue);
                }
                else { Chart.RemoveObject("SMC_SwingLow"); Chart.RemoveObject("SMC_SwingLow_Label"); }

                foreach (var zone in _activeZones)
                {
                    if (zone.IsOB && ShowObZones)
                    {
                        Color c = zone.IsBullish ? Color.FromArgb(40, 0, 255, 0) : Color.FromArgb(40, 255, 0, 0);
                        var r = Chart.DrawRectangle(zone.Id, zone.StartIndex, zone.TopPrice, index, zone.BottomPrice, c);
                        if (r != null) { r.IsFilled = true; r.Thickness = 1; }
                    }
                    else if (!zone.IsOB && ShowFvgZones)
                    {
                        Color c = zone.IsBullish ? Color.FromArgb(25, 0, 255, 127) : Color.FromArgb(25, 255, 127, 0);
                        var r = Chart.DrawRectangle(zone.Id, zone.StartIndex, zone.TopPrice, index, zone.BottomPrice, c);
                        if (r != null) { r.IsFilled = true; r.Thickness = 1; }
                    }
                }
            }

            // 8. Volume Profile blocks
            if (index > 0 && index % VpLookback == 0)
                CalculateVolumeProfileBlock(index - VpLookback, index - 1);

            if (index == Bars.Count - 2)
                CalculateVolumeProfileBlock(index - (index % VpLookback), index);
        }

        // ── Order Execution ───────────────────────────────────────────────────

        private void TryOpenBuy(int index, double poc, double vah, double val)
        {
            // Single entry: skip jika buy sudah ada
            if (HasActivePosition(TradeType.Buy))
            {
                Print("Buy skipped – posisi buy sudah aktif.");
                return;
            }

            // Tutup sell jika ada (reversal)
            ClosePositions(TradeType.Sell);

            double entryPrice = Bars.ClosePrices[index];

            // SL
            double slPrice = !double.IsNaN(_lastSwingLow)
                ? _lastSwingLow - SlBufferPips * Symbol.PipSize
                : entryPrice - FallbackSlPips * Symbol.PipSize;

            double slPips = (entryPrice - slPrice) / Symbol.PipSize;
            if (slPips <= 0) { Print("Buy skipped – invalid SL."); return; }

            // TP1 = POC (adjusted by ratio), TP2 = VAH
            double tp1Price = entryPrice + (poc - entryPrice) * Tp1Ratio;
            double tp2Price = vah;

            double tp1Pips = (tp1Price - entryPrice) / Symbol.PipSize;
            double tp2Pips = (tp2Price - entryPrice) / Symbol.PipSize;

            // Validasi TP harus di atas entry
            if (tp1Pips <= 0) { Print("Buy skipped – TP1 tidak valid untuk buy."); return; }

            double totalLots = CalculateVolume(slPips);
            double halfLots  = RoundVolume(totalLots / 2.0);

            double unitsHalf = Symbol.QuantityToVolumeInUnits(halfLots);

            // Posisi 1: TP1 = POC
            var r1 = ExecuteMarketOrder(TradeType.Buy, SymbolName, unitsHalf,
                TradeLabel + "_TP1", slPips, tp1Pips);

            if (r1.IsSuccessful)
                Print("BUY TP1 opened | {0} lots | SL={1:F5} | TP1(POC)={2:F5}", halfLots, slPrice, tp1Price);
            else
                Print("BUY TP1 FAILED: {0}", r1.Error);

            // Posisi 2: TP2 = VAH (hanya jika valid)
            if (tp2Pips > 0)
            {
                var r2 = ExecuteMarketOrder(TradeType.Buy, SymbolName, unitsHalf,
                    TradeLabel + "_TP2", slPips, tp2Pips);

                if (r2.IsSuccessful)
                    Print("BUY TP2 opened | {0} lots | SL={1:F5} | TP2(VAH)={2:F5}", halfLots, slPrice, tp2Price);
                else
                    Print("BUY TP2 FAILED: {0}", r2.Error);
            }
        }

        private void TryOpenSell(int index, double poc, double vah, double val)
        {
            // Single entry: skip jika sell sudah ada
            if (HasActivePosition(TradeType.Sell))
            {
                Print("Sell skipped – posisi sell sudah aktif.");
                return;
            }

            // Tutup buy jika ada (reversal)
            ClosePositions(TradeType.Buy);

            double entryPrice = Bars.ClosePrices[index];

            // SL
            double slPrice = !double.IsNaN(_lastSwingHigh)
                ? _lastSwingHigh + SlBufferPips * Symbol.PipSize
                : entryPrice + FallbackSlPips * Symbol.PipSize;

            double slPips = (slPrice - entryPrice) / Symbol.PipSize;
            if (slPips <= 0) { Print("Sell skipped – invalid SL."); return; }

            // TP1 = POC (adjusted by ratio), TP2 = VAL
            double tp1Price = entryPrice - (entryPrice - poc) * Tp1Ratio;
            double tp2Price = val;

            double tp1Pips = (entryPrice - tp1Price) / Symbol.PipSize;
            double tp2Pips = (entryPrice - tp2Price) / Symbol.PipSize;

            // Validasi TP harus di bawah entry
            if (tp1Pips <= 0) { Print("Sell skipped – TP1 tidak valid untuk sell."); return; }

            double totalLots = CalculateVolume(slPips);
            double halfLots  = RoundVolume(totalLots / 2.0);

            double unitsHalf = Symbol.QuantityToVolumeInUnits(halfLots);

            // Posisi 1: TP1 = POC
            var r1 = ExecuteMarketOrder(TradeType.Sell, SymbolName, unitsHalf,
                TradeLabel + "_TP1", slPips, tp1Pips);

            if (r1.IsSuccessful)
                Print("SELL TP1 opened | {0} lots | SL={1:F5} | TP1(POC)={2:F5}", halfLots, slPrice, tp1Price);
            else
                Print("SELL TP1 FAILED: {0}", r1.Error);

            // Posisi 2: TP2 = VAL (hanya jika valid)
            if (tp2Pips > 0)
            {
                var r2 = ExecuteMarketOrder(TradeType.Sell, SymbolName, unitsHalf,
                    TradeLabel + "_TP2", slPips, tp2Pips);

                if (r2.IsSuccessful)
                    Print("SELL TP2 opened | {0} lots | SL={1:F5} | TP2(VAL)={2:F5}", halfLots, slPrice, tp2Price);
                else
                    Print("SELL TP2 FAILED: {0}", r2.Error);
            }
        }

        // ── Visual: TP/SL lines ───────────────────────────────────────────────

        private void DrawTpSlLines(int index, double slPx, double tp1Px, double tp2Px, bool isBuy)
        {
            DateTime t = Bars.OpenTimes[index];
            DateTime t2 = t.AddMinutes(1); // dummy end; cTrader trend lines need 2 points

            string prefix = "Sig_" + index;

            // SL line (merah)
            Chart.DrawHorizontalLine(prefix + "_SL", slPx, Color.FromArgb(180, 255, 50, 50));

            // TP1 = POC (kuning)
            if (tp1Px > 0)
                Chart.DrawHorizontalLine(prefix + "_TP1", tp1Px, Color.Gold);

            // TP2 = VAH (buy) atau VAL (sell) – hijau/merah muda
            if (tp2Px > 0)
                Chart.DrawHorizontalLine(prefix + "_TP2", tp2Px,
                    isBuy ? Color.LimeGreen : Color.Tomato);
        }

        // ── Position Helpers ──────────────────────────────────────────────────

        private bool HasActivePosition(TradeType type)
        {
            foreach (var pos in Positions)
                if (pos.SymbolName == SymbolName &&
                    (pos.Label == TradeLabel + "_TP1" || pos.Label == TradeLabel + "_TP2") &&
                    pos.TradeType == type)
                    return true;
            return false;
        }

        private void ClosePositions(TradeType type)
        {
            foreach (var pos in Positions)
            {
                if (pos.SymbolName == SymbolName &&
                    (pos.Label == TradeLabel + "_TP1" || pos.Label == TradeLabel + "_TP2") &&
                    pos.TradeType == type)
                {
                    var result = ClosePosition(pos);
                    if (!result.IsSuccessful)
                        Print("Close {0} FAILED: {1}", type, result.Error);
                }
            }
        }

        // ── Volume / Risk ─────────────────────────────────────────────────────

        private double CalculateVolume(double slPips)
        {
            double riskAmount = Account.Equity * (RiskPercent / 100.0);
            double lots = riskAmount / (slPips * Symbol.PipValue);
            lots = Math.Max(MinVolumeLots, Math.Min(MaxVolumeLots, lots));
            return RoundVolume(lots);
        }

        private double RoundVolume(double lots)
        {
            double step = Symbol.VolumeInUnitsStep / Symbol.QuantityToVolumeInUnits(1.0);
            lots = Math.Round(lots / step) * step;
            return Math.Max(MinVolumeLots, lots);
        }

        // ── SMC Condition ─────────────────────────────────────────────────────

        private bool EvaluateSmcCondition(int index, bool isBullish)
        {
            if (!UseSmcFilter) return true;

            double low  = Bars.LowPrices[index];
            double high = Bars.HighPrices[index];

            foreach (var zone in _activeZones)
            {
                if (zone.IsBullish != isBullish || zone.IsMitigated) continue;

                bool modeMatch = FilterModeSMC == SmcFilterMode2.Both
                    || (FilterModeSMC == SmcFilterMode2.OrderBlock   &&  zone.IsOB)
                    || (FilterModeSMC == SmcFilterMode2.FairValueGap && !zone.IsOB);

                if (!modeMatch) continue;

                bool touches = isBullish
                    ? (low <= zone.TopPrice && high >= zone.BottomPrice)
                    : (high >= zone.BottomPrice && low <= zone.TopPrice);

                if (touches) return true;
            }
            return false;
        }

        // ── Volume Profile ────────────────────────────────────────────────────

        private bool TryGetVpLevels(int index, out double vah, out double val, out double poc, out double minPrice, out double maxPrice)
        {
            vah = 0; val = 0; poc = 0; minPrice = 0; maxPrice = 0;

            if (FilterMode == VpFilterMode2.Rolling)
            {
                int start = index - VpLookback + 1;
                if (start >= 0) { GetVolumeProfileLevels(start, index, out vah, out val, out poc, out minPrice, out maxPrice); return true; }
            }
            else if (FilterMode == VpFilterMode2.PreviousBlock)
            {
                int blockIdx = index / VpLookback;
                if (blockIdx > 0)
                {
                    int ps = (blockIdx - 1) * VpLookback;
                    GetVolumeProfileLevels(ps, ps + VpLookback - 1, out vah, out val, out poc, out minPrice, out maxPrice);
                    return true;
                }
            }
            else if (FilterMode == VpFilterMode2.DevelopingBlock)
            {
                int bs = index - (index % VpLookback);
                if (index - bs >= 5) { GetVolumeProfileLevels(bs, index, out vah, out val, out poc, out minPrice, out maxPrice); return true; }
            }
            return false;
        }

        private bool TryGetVpLevels(int index, out double vah, out double val, out double poc)
        {
            double dummyMin, dummyMax;
            return TryGetVpLevels(index, out vah, out val, out poc, out dummyMin, out dummyMax);
        }

        private void GetVolumeProfileLevels(int s, int e, out double vah, out double val, out double poc)
        {
            double dummyMin, dummyMax;
            GetVolumeProfileLevels(s, e, out vah, out val, out poc, out dummyMin, out dummyMax);
        }

        private void GetVolumeProfileLevels(int s, int e, out double vah, out double val, out double poc, out double minPrice, out double maxPrice)
        {
            vah = 0; val = 0; poc = 0; minPrice = 0; maxPrice = 0;
            if (e - s < 5) return;

            double minP = double.MaxValue, maxP = double.MinValue;
            for (int i = s; i <= e; i++)
            {
                if (Bars.HighPrices[i] > maxP) maxP = Bars.HighPrices[i];
                if (Bars.LowPrices[i]  < minP) minP = Bars.LowPrices[i];
            }

            minPrice = minP;
            maxPrice = maxP;

            double range = maxP - minP;
            if (range <= 0) return;

            int bins = VpBins;
            double bSz = range / bins;
            double[] bVol = new double[bins];
            double[] bPx  = new double[bins];
            for (int i = 0; i < bins; i++) bPx[i] = minP + i * bSz;

            for (int i = s; i <= e; i++)
            {
                double v = Bars.TickVolumes[i];
                if (v <= 0) continue;
                int lo = Math.Max(0, Math.Min(bins-1, (int)Math.Floor((Bars.LowPrices[i]  - minP) / bSz)));
                int hi = Math.Max(0, Math.Min(bins-1, (int)Math.Floor((Bars.HighPrices[i] - minP) / bSz)));
                double share = v / (hi - lo + 1);
                for (int b = lo; b <= hi; b++) bVol[b] += share;
            }

            int pocBin = 0; double maxV = 0, totV = 0;
            for (int i = 0; i < bins; i++) { totV += bVol[i]; if (bVol[i] > maxV) { maxV = bVol[i]; pocBin = i; } }
            poc = bPx[pocBin] + bSz / 2.0;

            double target = totV * (ValueAreaPercentage / 100.0);
            double cur = bVol[pocBin];
            int li = pocBin, hi2 = pocBin;
            while (cur < target)
            {
                bool cd = li > 0, cu = hi2 < bins - 1;
                if (!cd && !cu) break;
                if (cd && cu) { if (bVol[li-1] >= bVol[hi2+1]) { li--;  cur += bVol[li]; } else { hi2++; cur += bVol[hi2]; } }
                else if (cd) { li--;  cur += bVol[li]; }
                else         { hi2++; cur += bVol[hi2]; }
            }
            val = bPx[li];
            vah = bPx[hi2] + bSz;
        }

        private void CalculateVolumeProfileBlock(int s, int e)
        {
            if (e - s < 5) return;
            double vah, val, poc;
            GetVolumeProfileLevels(s, e, out vah, out val, out poc);

            double minP = double.MaxValue, maxP = double.MinValue;
            for (int i = s; i <= e; i++)
            {
                if (Bars.HighPrices[i] > maxP) maxP = Bars.HighPrices[i];
                if (Bars.LowPrices[i]  < minP) minP = Bars.LowPrices[i];
            }

            DateTime st = Bars.OpenTimes[s];
            DateTime et = Bars.OpenTimes[e];

            Chart.DrawTrendLine("VP_POC_"+s, st, poc, et, poc, Color.Gold, 2, LineStyle.Solid);
            Chart.DrawText("VP_POC_Label_"+s, "  POC", st, poc, Color.Gold);

            Chart.DrawTrendLine("VP_VAH_"+s, st, vah, et, vah, Color.Tomato, 1, LineStyle.Lines);
            Chart.DrawText("VP_VAH_Label_"+s, "  VAH", st, vah, Color.Tomato);

            Chart.DrawTrendLine("VP_VAL_"+s, st, val, et, val, Color.LimeGreen, 1, LineStyle.Lines);
            Chart.DrawText("VP_VAL_Label_"+s, "  VAL", st, val, Color.LimeGreen);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private double GetBarDelta(int i)
        {
            double hi = Bars.HighPrices[i], lo = Bars.LowPrices[i];
            if (hi == lo) return 0;
            return Bars.TickVolumes[i] * (Bars.ClosePrices[i] - Bars.OpenPrices[i]) / (hi - lo);
        }

        private double GetCumulativeDelta(int endIndex)
        {
            int bs = endIndex - (endIndex % VpLookback);
            double cum = 0;
            for (int i = bs; i <= endIndex; i++) cum += GetBarDelta(i);
            return cum;
        }

        private bool IsSwingHigh(int index, int period)
        {
            if (index < period * 2) return false;
            int t = index - period; double hi = Bars.HighPrices[t];
            for (int i = t - period; i < t; i++) if (Bars.HighPrices[i] > hi) return false;
            for (int i = t + 1; i <= index; i++) if (Bars.HighPrices[i] > hi) return false;
            return true;
        }

        private bool IsSwingLow(int index, int period)
        {
            if (index < period * 2) return false;
            int t = index - period; double lo = Bars.LowPrices[t];
            for (int i = t - period; i < t; i++) if (Bars.LowPrices[i] < lo) return false;
            for (int i = t + 1; i <= index; i++) if (Bars.LowPrices[i] < lo) return false;
            return true;
        }

        private int FindBullishObCandle(int idx)
        {
            for (int i = idx; i >= Math.Max(0, idx - 20); i--)
                if (Bars.ClosePrices[i] < Bars.OpenPrices[i]) return i;
            return idx;
        }

        private int FindBearishObCandle(int idx)
        {
            for (int i = idx; i >= Math.Max(0, idx - 20); i--)
                if (Bars.ClosePrices[i] > Bars.OpenPrices[i]) return i;
            return idx;
        }
    }
}
