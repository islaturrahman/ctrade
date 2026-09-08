using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Collections;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo
{
    public enum VpFilterMode
    {
        Rolling,
        PreviousBlock,
        DevelopingBlock
    }

    public enum SmcFilterMode
    {
        Both,
        OrderBlock,
        FairValueGap
    }

    [Levels(30.0, 70.0)]
    [Indicator(IsOverlay = false, AccessRights = AccessRights.None)]
    public class Rsi_VolumeProfile : Indicator
    {
        [Parameter("Source", Group = "RSI")]
        public DataSeries Source { get; set; }

        [Parameter("Periods", DefaultValue = 14, Group = "RSI")]
        public int Periods { get; set; }

        [Parameter("Filter Signals with VP", DefaultValue = true, Group = "Volume Profile")]
        public bool FilterSignalsWithVp { get; set; }

        [Parameter("VP Filter Mode", DefaultValue = VpFilterMode.PreviousBlock, Group = "Volume Profile")]
        public VpFilterMode FilterMode { get; set; }

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

        [Parameter("Use SMC Filter", DefaultValue = false, Group = "Smart Money Concepts (SMC)")]
        public bool UseSmcFilter { get; set; }

        [Parameter("SMC Swing Period", DefaultValue = 5, MinValue = 2, MaxValue = 20, Group = "Smart Money Concepts (SMC)")]
        public int SmcSwingPeriod { get; set; }

        [Parameter("SMC Filter Mode", DefaultValue = SmcFilterMode.Both, Group = "Smart Money Concepts (SMC)")]
        public SmcFilterMode FilterModeSMC { get; set; }

        [Parameter("Show OB Zones", DefaultValue = true, Group = "Smart Money Concepts (SMC)")]
        public bool ShowObZones { get; set; }

        [Parameter("Show FVG Zones", DefaultValue = true, Group = "Smart Money Concepts (SMC)")]
        public bool ShowFvgZones { get; set; }

        [Output("RSI", LineColor = "RoyalBlue", Thickness = 2)]
        public IndicatorDataSeries Result { get; set; }

        private RelativeStrengthIndex _rsi;
        private SimpleMovingAverage _volumeSma;

        // SMC State Fields
        private double _lastSwingHigh;
        private double _lastSwingLow;
        private bool _isBullishStructure;

        public class Zone
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

        protected override void Initialize()
        {
            _rsi = Indicators.RelativeStrengthIndex(Source, Periods);
            _volumeSma = Indicators.SimpleMovingAverage(Bars.TickVolumes, VolumeSmaPeriods);

            _lastSwingHigh = double.NaN;
            _lastSwingLow = double.NaN;
            _isBullishStructure = true; // default structure
            _activeZones = new List<Zone>();
        }

        public override void Calculate(int index)
        {
            Result[index] = _rsi.Result[index];

            if (index < 1)
                return;

            // 1. SMC Market Structure Tracking (Swing Points)
            if (IsSwingHigh(index, SmcSwingPeriod))
            {
                _lastSwingHigh = Bars.HighPrices[index - SmcSwingPeriod];
            }
            if (IsSwingLow(index, SmcSwingPeriod))
            {
                _lastSwingLow = Bars.LowPrices[index - SmcSwingPeriod];
            }

            double currentClose = Bars.ClosePrices[index];

            // BOS / CHoCH state transitions & OB zone creation
            if (!double.IsNaN(_lastSwingHigh) && currentClose > _lastSwingHigh)
            {
                bool isChoch = !_isBullishStructure;
                _isBullishStructure = true;

                string breakLabel = isChoch ? "CHoCH" : "BOS";
                Chart.DrawText("SMC_Break_" + index, "  " + breakLabel, index, Bars.HighPrices[index] + 5 * Symbol.PipSize, Color.LimeGreen);

                // Detect and add Bullish OB
                int obIdx = FindBullishObCandle(index, _lastSwingHigh);
                var ob = new Zone
                {
                    Id = "SMC_OB_Bull_" + index,
                    TopPrice = Bars.HighPrices[obIdx],
                    BottomPrice = Bars.LowPrices[obIdx],
                    StartIndex = obIdx,
                    IsOB = true,
                    IsBullish = true,
                    IsMitigated = false
                };
                _activeZones.Add(ob);

                _lastSwingHigh = double.NaN;
            }
            else if (!double.IsNaN(_lastSwingLow) && currentClose < _lastSwingLow)
            {
                bool isChoch = _isBullishStructure;
                _isBullishStructure = false;

                string breakLabel = isChoch ? "CHoCH" : "BOS";
                Chart.DrawText("SMC_Break_" + index, "  " + breakLabel, index, Bars.LowPrices[index] - 5 * Symbol.PipSize, Color.Tomato);

                // Detect and add Bearish OB
                int obIdx = FindBearishObCandle(index, _lastSwingLow);
                var ob = new Zone
                {
                    Id = "SMC_OB_Bear_" + index,
                    TopPrice = Bars.HighPrices[obIdx],
                    BottomPrice = Bars.LowPrices[obIdx],
                    StartIndex = obIdx,
                    IsOB = true,
                    IsBullish = false,
                    IsMitigated = false
                };
                _activeZones.Add(ob);

                _lastSwingLow = double.NaN;
            }

            // Detect Fair Value Gaps (FVG)
            if (index >= 2)
            {
                if (Bars.LowPrices[index - 2] > Bars.HighPrices[index])
                {
                    var fvg = new Zone
                    {
                        Id = "SMC_FVG_Bull_" + index,
                        TopPrice = Bars.LowPrices[index - 2],
                        BottomPrice = Bars.HighPrices[index],
                        StartIndex = index - 2,
                        IsOB = false,
                        IsBullish = true,
                        IsMitigated = false
                    };
                    _activeZones.Add(fvg);
                }
                else if (Bars.HighPrices[index - 2] < Bars.LowPrices[index])
                {
                    var fvg = new Zone
                    {
                        Id = "SMC_FVG_Bear_" + index,
                        TopPrice = Bars.LowPrices[index],
                        BottomPrice = Bars.HighPrices[index - 2],
                        StartIndex = index - 2,
                        IsOB = false,
                        IsBullish = false,
                        IsMitigated = false
                    };
                    _activeZones.Add(fvg);
                }
            }

            // 2. Signal evaluation (Checks if signals occur)
            string signalName = "RsiSignal_" + index;
            string chartSignalName = "ChartRsiSignal_" + index;
            string textSignalName = "TextRsiSignal_" + index;

            bool potentialBuy = _rsi.Result[index] > 30 && _rsi.Result[index - 1] <= 30;
            bool potentialSell = _rsi.Result[index] < 70 && _rsi.Result[index - 1] >= 70;

            if (potentialBuy || potentialSell)
            {
                bool triggerBuy = potentialBuy;
                bool triggerSell = potentialSell;

                if (FilterSignalsWithVp)
                {
                    double vah = 0, val = 0, poc = 0;
                    bool hasVp = false;

                    if (FilterMode == VpFilterMode.Rolling)
                    {
                        int rollingStart = index - VpLookback + 1;
                        if (rollingStart >= 0)
                        {
                            GetVolumeProfileLevels(rollingStart, index, out vah, out val, out poc);
                            hasVp = true;
                        }
                    }
                    else if (FilterMode == VpFilterMode.PreviousBlock)
                    {
                        int currentBlockIndex = index / VpLookback;
                        if (currentBlockIndex > 0)
                        {
                            int prevBlockStart = (currentBlockIndex - 1) * VpLookback;
                            int prevBlockEnd = prevBlockStart + VpLookback - 1;
                            GetVolumeProfileLevels(prevBlockStart, prevBlockEnd, out vah, out val, out poc);
                            hasVp = true;
                        }
                    }
                    else if (FilterMode == VpFilterMode.DevelopingBlock)
                    {
                        int currentBlockStart = index - (index % VpLookback);
                        if (index - currentBlockStart >= 5)
                        {
                            GetVolumeProfileLevels(currentBlockStart, index, out vah, out val, out poc);
                            hasVp = true;
                        }
                    }

                    if (hasVp)
                    {
                        double currentOpen = Bars.OpenPrices[index];
                        double volume = Bars.TickVolumes[index];
                        double barDelta = GetBarDelta(index);
                        double gamma = barDelta - GetBarDelta(index - 1);

                        bool volumeCondition = !UseVolumeFilter || (volume >= MinVolumeMultiplier * _volumeSma.Result[index]);

                        if (potentialBuy)
                        {
                            bool priceBelowVal = val > 0 && currentClose < val;
                            bool isBullishCandle = currentClose > currentOpen;
                            bool pocMagnetCondition = !UsePocMagnetFilter || (poc > 0 && currentClose < poc);

                            bool deltaCondition = !ConfirmWithDelta || (barDelta > 0);
                            bool gammaCondition = !ConfirmWithGamma || (gamma > 0);

                            // SMC OB/FVG Touch Filter
                            bool smcCondition = true;
                            if (UseSmcFilter)
                            {
                                smcCondition = false;
                                double low = Bars.LowPrices[index];
                                double high = Bars.HighPrices[index];
                                foreach (var zone in _activeZones)
                                {
                                    if (zone.IsBullish && !zone.IsMitigated)
                                    {
                                        if (FilterModeSMC == SmcFilterMode.Both ||
                                            (FilterModeSMC == SmcFilterMode.OrderBlock && zone.IsOB) ||
                                            (FilterModeSMC == SmcFilterMode.FairValueGap && !zone.IsOB))
                                        {
                                            if (low <= zone.TopPrice && high >= zone.BottomPrice)
                                            {
                                                smcCondition = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            triggerBuy = priceBelowVal && isBullishCandle && pocMagnetCondition && volumeCondition && deltaCondition && gammaCondition && smcCondition;
                        }
                        else if (potentialSell)
                        {
                            bool priceAboveVah = vah > 0 && currentClose > vah;
                            bool isBearishCandle = currentClose < currentOpen;
                            bool pocMagnetCondition = !UsePocMagnetFilter || (poc > 0 && currentClose > poc);

                            bool deltaCondition = !ConfirmWithDelta || (barDelta < 0);
                            bool gammaCondition = !ConfirmWithGamma || (gamma < 0);

                            // SMC OB/FVG Touch Filter
                            bool smcCondition = true;
                            if (UseSmcFilter)
                            {
                                smcCondition = false;
                                double low = Bars.LowPrices[index];
                                double high = Bars.HighPrices[index];
                                foreach (var zone in _activeZones)
                                {
                                    if (!zone.IsBullish && !zone.IsMitigated)
                                    {
                                        if (FilterModeSMC == SmcFilterMode.Both ||
                                            (FilterModeSMC == SmcFilterMode.OrderBlock && zone.IsOB) ||
                                            (FilterModeSMC == SmcFilterMode.FairValueGap && !zone.IsOB))
                                        {
                                            if (high >= zone.BottomPrice && low <= zone.TopPrice)
                                            {
                                                smcCondition = true;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }

                            triggerSell = priceAboveVah && isBearishCandle && pocMagnetCondition && volumeCondition && deltaCondition && gammaCondition && smcCondition;
                        }
                    }
                    else
                    {
                        triggerBuy = false;
                        triggerSell = false;
                    }
                }

                if (triggerBuy)
                {
                    double volume = Bars.TickVolumes[index];
                    double cumDelta = GetCumulativeDelta(index);
                    double gamma = GetBarDelta(index) - GetBarDelta(index - 1);
                    string labelText = string.Format("IDX: {0}\nVol: {1:N0}\nCD: {2:N0}\nG: {3:N0}", index, volume, cumDelta, gamma);

                    IndicatorArea.DrawIcon(signalName, ChartIconType.Circle, index, _rsi.Result[index], Color.Green);
                    Chart.DrawIcon(chartSignalName, ChartIconType.Circle, index, Bars.LowPrices[index] - 5 * Symbol.PipSize, Color.Green);
                    
                    if (ShowSignalLabels)
                    {
                        Chart.DrawText(textSignalName, labelText, index, Bars.LowPrices[index] - 15 * Symbol.PipSize, Color.LimeGreen);
                    }
                    else
                    {
                        Chart.RemoveObject(textSignalName);
                    }
                    
                    Print("Buy Signal - IDX: {0}, Vol: {1}, CD: {2:F1}, Gamma: {3:F1}", index, volume, cumDelta, gamma);
                }
                else if (triggerSell)
                {
                    double volume = Bars.TickVolumes[index];
                    double cumDelta = GetCumulativeDelta(index);
                    double gamma = GetBarDelta(index) - GetBarDelta(index - 1);
                    string labelText = string.Format("IDX: {0}\nVol: {1:N0}\nCD: {2:N0}\nG: {3:N0}", index, volume, cumDelta, gamma);

                    IndicatorArea.DrawIcon(signalName, ChartIconType.Circle, index, _rsi.Result[index], Color.Red);
                    Chart.DrawIcon(chartSignalName, ChartIconType.Circle, index, Bars.HighPrices[index] + 5 * Symbol.PipSize, Color.Red);
                    
                    if (ShowSignalLabels)
                    {
                        Chart.DrawText(textSignalName, labelText, index, Bars.HighPrices[index] + 15 * Symbol.PipSize, Color.Tomato);
                    }
                    else
                    {
                        Chart.RemoveObject(textSignalName);
                    }
                    
                    Print("Sell Signal - IDX: {0}, Vol: {1}, CD: {2:F1}, Gamma: {3:F1}", index, volume, cumDelta, gamma);
                }
                else
                {
                    IndicatorArea.RemoveObject(signalName);
                    Chart.RemoveObject(chartSignalName);
                    Chart.RemoveObject(textSignalName);
                }
            }
            else
            {
                IndicatorArea.RemoveObject(signalName);
                Chart.RemoveObject(chartSignalName);
                Chart.RemoveObject(textSignalName);
            }

            // 3. Update mitigation status of active zones AFTER evaluating signals
            for (int i = _activeZones.Count - 1; i >= 0; i--)
            {
                var zone = _activeZones[i];
                if (zone.IsBullish)
                {
                    if (zone.IsOB)
                    {
                        if (currentClose < zone.BottomPrice)
                        {
                            zone.IsMitigated = true;
                            Chart.RemoveObject(zone.Id);
                        }
                    }
                    else // FVG
                    {
                        if (Bars.LowPrices[index] <= zone.BottomPrice)
                        {
                            zone.IsMitigated = true;
                            Chart.RemoveObject(zone.Id);
                        }
                    }
                }
                else // Bearish
                {
                    if (zone.IsOB)
                    {
                        if (currentClose > zone.TopPrice)
                        {
                            zone.IsMitigated = true;
                            Chart.RemoveObject(zone.Id);
                        }
                    }
                    else // FVG
                    {
                        if (Bars.HighPrices[index] >= zone.TopPrice)
                        {
                            zone.IsMitigated = true;
                            Chart.RemoveObject(zone.Id);
                        }
                    }
                }
            }

            // Clean up mitigated zones from our tracking list
            _activeZones.RemoveAll(z => z.IsMitigated);

            // 4. Draw active swing levels and zones on the right
            if (index == Bars.Count - 1)
            {
                if (!double.IsNaN(_lastSwingHigh))
                {
                    Chart.DrawTrendLine("SMC_SwingHigh", index - 30, _lastSwingHigh, index, _lastSwingHigh, Color.Orange, 1, LineStyle.Dots);
                    Chart.DrawText("SMC_SwingHigh_Label", "  SH", index, _lastSwingHigh, Color.Orange);
                }
                else
                {
                    Chart.RemoveObject("SMC_SwingHigh");
                    Chart.RemoveObject("SMC_SwingHigh_Label");
                }

                if (!double.IsNaN(_lastSwingLow))
                {
                    Chart.DrawTrendLine("SMC_SwingLow", index - 30, _lastSwingLow, index, _lastSwingLow, Color.RoyalBlue, 1, LineStyle.Dots);
                    Chart.DrawText("SMC_SwingLow_Label", "  SL", index, _lastSwingLow, Color.RoyalBlue);
                }
                else
                {
                    Chart.RemoveObject("SMC_SwingLow");
                    Chart.RemoveObject("SMC_SwingLow_Label");
                }

                // Shaded zones (OB & FVG)
                foreach (var zone in _activeZones)
                {
                    if (zone.IsOB)
                    {
                        if (ShowObZones)
                        {
                            Color obColor = zone.IsBullish 
                                ? Color.FromArgb(40, 0, 255, 0)
                                : Color.FromArgb(40, 255, 0, 0);
                            var rect = Chart.DrawRectangle(zone.Id, zone.StartIndex, zone.TopPrice, index, zone.BottomPrice, obColor);
                            if (rect != null) { rect.IsFilled = true; rect.Thickness = 1; }
                        }
                    }
                    else
                    {
                        if (ShowFvgZones)
                        {
                            Color fvgColor = zone.IsBullish
                                ? Color.FromArgb(25, 0, 255, 127)
                                : Color.FromArgb(25, 255, 127, 0);
                            var rect = Chart.DrawRectangle(zone.Id, zone.StartIndex, zone.TopPrice, index, zone.BottomPrice, fvgColor);
                            if (rect != null) { rect.IsFilled = true; rect.Thickness = 1; }
                        }
                    }
                }
            }

            // Calculate Volume Profile block-by-block
            // 1. Calculate static historical blocks as they complete
            if (index > 0 && index % VpLookback == 0)
            {
                CalculateVolumeProfileBlock(index - VpLookback, index - 1);
            }

            // 2. Calculate the current active block on the latest tick
            if (index == Bars.Count - 1)
            {
                int currentBlockStart = index - (index % VpLookback);
                CalculateVolumeProfileBlock(currentBlockStart, index);
            }
        }

        private void GetVolumeProfileLevels(int startIndex, int endIndex, out double vah, out double val, out double poc)
        {
            vah = 0;
            val = 0;
            poc = 0;

            if (endIndex - startIndex < 5)
                return;

            // 1. Find Min and Max Price over block
            double minPrice = double.MaxValue;
            double maxPrice = double.MinValue;

            for (int i = startIndex; i <= endIndex; i++)
            {
                if (Bars.HighPrices[i] > maxPrice) maxPrice = Bars.HighPrices[i];
                if (Bars.LowPrices[i] < minPrice) minPrice = Bars.LowPrices[i];
            }

            double priceRange = maxPrice - minPrice;
            if (priceRange <= 0)
                return;

            // 2. Setup Bins
            int bins = VpBins;
            double binSize = priceRange / bins;
            double[] binVolumes = new double[bins];
            double[] binPrices = new double[bins];

            for (int i = 0; i < bins; i++)
            {
                binPrices[i] = minPrice + i * binSize;
            }

            // 3. Populate Volume into Bins
            for (int i = startIndex; i <= endIndex; i++)
            {
                double high = Bars.HighPrices[i];
                double low = Bars.LowPrices[i];
                double volume = Bars.TickVolumes[i];

                if (volume <= 0)
                    continue;

                int lowBin = (int)Math.Floor((low - minPrice) / binSize);
                int highBin = (int)Math.Floor((high - minPrice) / binSize);

                if (lowBin < 0) lowBin = 0;
                if (lowBin >= bins) lowBin = bins - 1;
                if (highBin < 0) highBin = 0;
                if (highBin >= bins) highBin = bins - 1;

                int binsSpanned = highBin - lowBin + 1;
                double volumeShare = volume / binsSpanned;

                for (int b = lowBin; b <= highBin; b++)
                {
                    binVolumes[b] += volumeShare;
                }
            }

            // 4. Find POC
            int pocBin = 0;
            double maxVolume = 0;
            double totalVolume = 0;

            for (int i = 0; i < bins; i++)
            {
                totalVolume += binVolumes[i];
                if (binVolumes[i] > maxVolume)
                {
                    maxVolume = binVolumes[i];
                    pocBin = i;
                }
            }

            poc = binPrices[pocBin] + (binSize / 2.0);

            // 5. Calculate Value Area (VAH / VAL)
            double targetVolume = totalVolume * (ValueAreaPercentage / 100.0);
            double currentVolume = binVolumes[pocBin];
            int lowIdx = pocBin;
            int highIdx = pocBin;

            while (currentVolume < targetVolume)
            {
                bool canExpandDown = lowIdx > 0;
                bool canExpandUp = highIdx < bins - 1;

                if (!canExpandDown && !canExpandUp)
                    break;

                if (canExpandDown && canExpandUp)
                {
                    if (binVolumes[lowIdx - 1] >= binVolumes[highIdx + 1])
                    {
                        lowIdx--;
                        currentVolume += binVolumes[lowIdx];
                    }
                    else
                    {
                        highIdx++;
                        currentVolume += binVolumes[highIdx];
                    }
                }
                else if (canExpandDown)
                {
                    lowIdx--;
                    currentVolume += binVolumes[lowIdx];
                }
                else
                {
                    highIdx++;
                    currentVolume += binVolumes[highIdx];
                }
            }

            val = binPrices[lowIdx];
            vah = binPrices[highIdx] + binSize;
        }

        private void CalculateVolumeProfileBlock(int startIndex, int endIndex)
        {
            if (endIndex - startIndex < 5)
                return;

            // 1. Find Min and Max Price over block
            double minPrice = double.MaxValue;
            double maxPrice = double.MinValue;

            for (int i = startIndex; i <= endIndex; i++)
            {
                if (Bars.HighPrices[i] > maxPrice) maxPrice = Bars.HighPrices[i];
                if (Bars.LowPrices[i] < minPrice) minPrice = Bars.LowPrices[i];
            }

            double priceRange = maxPrice - minPrice;
            if (priceRange <= 0)
                return;

            // 2. Setup Bins
            int bins = VpBins;
            double binSize = priceRange / bins;
            double[] binVolumes = new double[bins];
            double[] binPrices = new double[bins];

            for (int i = 0; i < bins; i++)
            {
                binPrices[i] = minPrice + i * binSize;
            }

            // 3. Populate Volume into Bins
            for (int i = startIndex; i <= endIndex; i++)
            {
                double high = Bars.HighPrices[i];
                double low = Bars.LowPrices[i];
                double volume = Bars.TickVolumes[i];

                if (volume <= 0)
                    continue;

                int lowBin = (int)Math.Floor((low - minPrice) / binSize);
                int highBin = (int)Math.Floor((high - minPrice) / binSize);

                if (lowBin < 0) lowBin = 0;
                if (lowBin >= bins) lowBin = bins - 1;
                if (highBin < 0) highBin = 0;
                if (highBin >= bins) highBin = bins - 1;

                int binsSpanned = highBin - lowBin + 1;
                double volumeShare = volume / binsSpanned;

                for (int b = lowBin; b <= highBin; b++)
                {
                    binVolumes[b] += volumeShare;
                }
            }

            // 4. Find POC
            int pocBin = 0;
            double maxVolume = 0;
            double totalVolume = 0;

            for (int i = 0; i < bins; i++)
            {
                totalVolume += binVolumes[i];
                if (binVolumes[i] > maxVolume)
                {
                    maxVolume = binVolumes[i];
                    pocBin = i;
                }
            }

            double pocPrice = binPrices[pocBin] + (binSize / 2.0);

            // 5. Calculate Value Area (VAH / VAL)
            double targetVolume = totalVolume * (ValueAreaPercentage / 100.0);
            double currentVolume = binVolumes[pocBin];
            int lowIdx = pocBin;
            int highIdx = pocBin;

            while (currentVolume < targetVolume)
            {
                bool canExpandDown = lowIdx > 0;
                bool canExpandUp = highIdx < bins - 1;

                if (!canExpandDown && !canExpandUp)
                    break;

                if (canExpandDown && canExpandUp)
                {
                    if (binVolumes[lowIdx - 1] >= binVolumes[highIdx + 1])
                    {
                        lowIdx--;
                        currentVolume += binVolumes[lowIdx];
                    }
                    else
                    {
                        highIdx++;
                        currentVolume += binVolumes[highIdx];
                    }
                }
                else if (canExpandDown)
                {
                    lowIdx--;
                    currentVolume += binVolumes[lowIdx];
                }
                else
                {
                    highIdx++;
                    currentVolume += binVolumes[highIdx];
                }
            }

            double valPrice = binPrices[lowIdx];
            double vahPrice = binPrices[highIdx] + binSize;

            DateTime startTime = Bars.OpenTimes[startIndex];
            DateTime endTime = Bars.OpenTimes[endIndex];

            // Unique names for each block's lines
            string pocName = "VP_POC_" + startIndex;
            string vahName = "VP_VAH_" + startIndex;
            string valName = "VP_VAL_" + startIndex;

            string pocLabel = "VP_POC_Label_" + startIndex;
            string vahLabel = "VP_VAH_Label_" + startIndex;
            string valLabel = "VP_VAL_Label_" + startIndex;

            // 6. Draw Trend Lines and Text Labels on Main Chart
            // POC - Gold line
            Chart.DrawTrendLine(pocName, startTime, pocPrice, endTime, pocPrice, Color.Gold, 2, LineStyle.Solid);
            Chart.DrawText(pocLabel, "  POC", startTime, pocPrice, Color.Gold);

            // VAH - Tomato (Reddish) dashed line
            Chart.DrawTrendLine(vahName, startTime, vahPrice, endTime, vahPrice, Color.Tomato, 1, LineStyle.Lines);
            Chart.DrawText(vahLabel, "  VAH", startTime, vahPrice, Color.Tomato);

            // VAL - LimeGreen dashed line
            Chart.DrawTrendLine(valName, startTime, valPrice, endTime, valPrice, Color.LimeGreen, 1, LineStyle.Lines);
            Chart.DrawText(valLabel, "  VAL", startTime, valPrice, Color.LimeGreen);
        }

        private double GetBarDelta(int i)
        {
            double high = Bars.HighPrices[i];
            double low = Bars.LowPrices[i];
            double open = Bars.OpenPrices[i];
            double close = Bars.ClosePrices[i];
            double volume = Bars.TickVolumes[i];

            if (high == low)
                return 0;

            // Estimate delta based on candle body size vs range
            return volume * (close - open) / (high - low);
        }

        private double GetCumulativeDelta(int endIndex)
        {
            int currentBlockStart = endIndex - (endIndex % VpLookback);
            double cumDelta = 0;
            for (int i = currentBlockStart; i <= endIndex; i++)
            {
                cumDelta += GetBarDelta(i);
            }
            return cumDelta;
        }

        private bool IsSwingHigh(int index, int period)
        {
            if (index < period * 2) return false;
            int target = index - period;
            double targetHigh = Bars.HighPrices[target];
            
            for (int i = target - period; i < target; i++)
            {
                if (Bars.HighPrices[i] > targetHigh) return false;
            }
            for (int i = target + 1; i <= index; i++)
            {
                if (Bars.HighPrices[i] > targetHigh) return false;
            }
            return true;
        }

        private bool IsSwingLow(int index, int period)
        {
            if (index < period * 2) return false;
            int target = index - period;
            double targetLow = Bars.LowPrices[target];
            
            for (int i = target - period; i < target; i++)
            {
                if (Bars.LowPrices[i] < targetLow) return false;
            }
            for (int i = target + 1; i <= index; i++)
            {
                if (Bars.LowPrices[i] < targetLow) return false;
            }
            return true;
        }

        private int FindBullishObCandle(int currentIndex, double swingHighLevel)
        {
            int obIndex = currentIndex;
            for (int i = currentIndex; i >= currentIndex - 20 && i >= 0; i--)
            {
                if (Bars.ClosePrices[i] < Bars.OpenPrices[i])
                {
                    obIndex = i;
                    break;
                }
            }
            return obIndex;
        }

        private int FindBearishObCandle(int currentIndex, double swingLowLevel)
        {
            int obIndex = currentIndex;
            for (int i = currentIndex; i >= currentIndex - 20 && i >= 0; i--)
            {
                if (Bars.ClosePrices[i] > Bars.OpenPrices[i])
                {
                    obIndex = i;
                    break;
                }
            }
            return obIndex;
        }
    }
}