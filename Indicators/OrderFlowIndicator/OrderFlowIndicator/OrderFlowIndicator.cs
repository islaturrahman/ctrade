using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(AccessRights = AccessRights.None, IsOverlay = true)]
    public class OrderFlowIndicator : Indicator
    {
        // ═══════════════════════════════════════
        //  ORDER FLOW PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("Min Delta Per Level", Group = "Order Flow", DefaultValue = 3, MinValue = 1)]
        public int MinDeltaPerLevel { get; set; }

        [Parameter("Min Volume Per Level", Group = "Order Flow", DefaultValue = 1, MinValue = 1)]
        public int MinVolumePerLevel { get; set; }

        [Parameter("Min Bubbles for Signal", Group = "Order Flow", DefaultValue = 2, MinValue = 1)]
        public int MinBubblesForSignal { get; set; }

        [Parameter("Volume Spike Multiplier", Group = "Order Flow", DefaultValue = 1.3, MinValue = 1.1, MaxValue = 3.0)]
        public double VolumeSpikeMultiplier { get; set; }

        // ═══════════════════════════════════════
        //  CLUSTER ZONE PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("Cluster Lookback (candles)", Group = "Cluster Zone", DefaultValue = 20, MinValue = 5, MaxValue = 100)]
        public int ClusterLookback { get; set; }

        [Parameter("Cluster Price Tolerance (pips)", Group = "Cluster Zone", DefaultValue = 3.0, MinValue = 1.0, MaxValue = 10.0)]
        public double ClusterTolerancePips { get; set; }

        [Parameter("Cluster Dominance % Threshold", Group = "Cluster Zone", DefaultValue = 65.0, MinValue = 51.0, MaxValue = 90.0)]
        public double ClusterDominanceThreshold { get; set; }

        [Parameter("Min Bubbles in Cluster", Group = "Cluster Zone", DefaultValue = 5, MinValue = 2, MaxValue = 30)]
        public int MinBubblesInCluster { get; set; }

        [Parameter("Show Cluster Zones", Group = "Cluster Zone", DefaultValue = true)]
        public bool ShowClusterZones { get; set; }

        [Parameter("Show Virgin Cluster Label", Group = "Cluster Zone", DefaultValue = true)]
        public bool ShowVirginLabel { get; set; }

        [Parameter("Extend Zone Right (bars)", Group = "Cluster Zone", DefaultValue = 10, MinValue = 0, MaxValue = 100)]
        public int ExtendZoneBars { get; set; }

        // ═══════════════════════════════════════
        //  SIGNAL PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("Signal: Volume Spike", Group = "Signals", DefaultValue = true)]
        public bool EnableVolumeSpikeSignal { get; set; }

        [Parameter("Signal: Absorption", Group = "Signals", DefaultValue = true)]
        public bool EnableAbsorptionSignal { get; set; }

        [Parameter("Signal: Climax", Group = "Signals", DefaultValue = true)]
        public bool EnableClimaxSignal { get; set; }

        [Parameter("Signal: Simple Imbalance", Group = "Signals", DefaultValue = true)]
        public bool EnableSimpleImbalance { get; set; }

        [Parameter("Signal: Failed Auction", Group = "Signals", DefaultValue = true)]
        public bool EnableFailedAuction { get; set; }

        [Parameter("Signal: Virgin Cluster", Group = "Signals", DefaultValue = true)]
        public bool EnableVirginCluster { get; set; }

        // ═══════════════════════════════════════
        //  VISUAL PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("Show Bubbles", Group = "Visual", DefaultValue = true)]
        public bool ShowBubbles { get; set; }

        [Parameter("Bubble Opacity", Group = "Visual", DefaultValue = 127, MinValue = 10, MaxValue = 255)]
        public int BubbleOpacity { get; set; }

        [Parameter("Buy Bubble Color", Group = "Visual", DefaultValue = "Green")]
        public string BuyBubbleColor { get; set; }

        [Parameter("Sell Bubble Color", Group = "Visual", DefaultValue = "Red")]
        public string SellBubbleColor { get; set; }

        [Parameter("Bubble Size Multiplier", Group = "Visual", DefaultValue = 0.8, MinValue = 0.01, MaxValue = 2.0)]
        public double BubbleSizeMultiplier { get; set; }

        [Parameter("Show Delta Labels", Group = "Visual", DefaultValue = true)]
        public bool ShowDeltaLabels { get; set; }

        [Parameter("Show Bubble Counter", Group = "Visual", DefaultValue = true)]
        public bool ShowBubbleCounter { get; set; }

        [Parameter("Show Signals", Group = "Visual", DefaultValue = true)]
        public bool ShowSignals { get; set; }

        [Parameter("Signal Font Size", Group = "Visual", DefaultValue = 10, MinValue = 8, MaxValue = 16)]
        public int SignalFontSize { get; set; }

        [Parameter("Show Info Panel", Group = "Visual", DefaultValue = true)]
        public bool ShowInfoPanel { get; set; }

        // ═══════════════════════════════════════
        //  PRIVATE FIELDS
        // ═══════════════════════════════════════

        private Dictionary<int, CandleFootprint> candleFootprints;
        private Ticks ticks;
        private List<ClusterZone> clusterZones = new List<ClusterZone>();
        private HashSet<string> virginClusters = new HashSet<string>();
        private HashSet<string> testedClusters = new HashSet<string>();
        private HashSet<string> signalsDrawn = new HashSet<string>();
        private int bubblesDrawn = 0;
        private int signalsDetected = 0;

        protected override void Initialize()
        {
            candleFootprints = new Dictionary<int, CandleFootprint>();
            clusterZones = new List<ClusterZone>();

            Print("═══════════════════════════════════════════════════");
            Print("  ORDER FLOW INDICATOR — Cluster Zone Edition");
            Print($"  Symbol: {SymbolName} | TF: {TimeFrame}");
            Print("═══════════════════════════════════════════════════");

            ticks = MarketData.GetTicks();
            ticks.Tick += OnNewTick;

            ProcessHistoricalTicks();
        }

        public override void Calculate(int index)
        {
            // Triggered on each new bar — finalize completed candles
            if (index < Bars.Count - 1)
            {
                int barIndex = index;
                if (candleFootprints.ContainsKey(barIndex) && !candleFootprints[barIndex].IsFinalized)
                {
                    FinalizeCandle(candleFootprints[barIndex]);
                    UpdateClusterZonesWithNewCandle(candleFootprints[barIndex]);
                }
            }

            // Update info panel
            if (ShowInfoPanel)
                DrawInfoPanel();
        }

        // ═══════════════════════════════════════
        //  TICK PROCESSING
        // ═══════════════════════════════════════

        private void OnNewTick(TicksTickEventArgs obj)
        {
            if (ticks.Count == 0) return;

            var latestTick = ticks.Last();
            ProcessSingleTick(latestTick);

            int currentBarIndex = Bars.Count - 1;
            if (candleFootprints.ContainsKey(currentBarIndex) && ShowBubbles)
                DrawCurrentCandleBubbles(candleFootprints[currentBarIndex]);
        }

        private void ProcessHistoricalTicks()
        {
            int tickCount = ticks.Count;
            if (tickCount == 0)
            {
                Print("⚠️ No tick data available");
                return;
            }

            Print($"Processing {tickCount} historical ticks...");
            int start = Math.Max(0, tickCount - 20000);

            for (int i = start; i < tickCount; i++)
                ProcessSingleTick(ticks[i]);

            FinalizeAllCandles();
            BuildClusterZonesFromHistory();

            Print($"✓ Done! Bubbles: {bubblesDrawn} | Signals: {signalsDetected} | Clusters: {clusterZones.Count}");
        }

        private void ProcessSingleTick(Tick tick)
        {
            int barIndex = FindBarIndex(tick.Time);
            if (barIndex < 0) return;

            if (!candleFootprints.ContainsKey(barIndex))
                candleFootprints[barIndex] = new CandleFootprint { BarIndex = barIndex, BarTime = Bars.OpenTimes[barIndex] };

            CandleFootprint footprint = candleFootprints[barIndex];
            if (footprint.IsFinalized) return;

            bool isBuyTick = false;
            bool isSellTick = false;

            if (footprint.LastAsk > 0 && tick.Ask > footprint.LastAsk)
                isBuyTick = true;
            else if (footprint.LastBid > 0 && tick.Bid < footprint.LastBid)
                isSellTick = true;
            else if (footprint.LastAsk > 0)
            {
                double midLast = (footprint.LastBid + footprint.LastAsk) / 2.0;
                double midNow = (tick.Bid + tick.Ask) / 2.0;
                if (midNow > midLast) isBuyTick = true;
                else if (midNow < midLast) isSellTick = true;
            }

            double price = isBuyTick ? tick.Ask : tick.Bid;
            double roundedPrice = RoundToPip(price);

            if (!footprint.PriceLevels.ContainsKey(roundedPrice))
                footprint.PriceLevels[roundedPrice] = new PriceLevel { Price = roundedPrice };

            PriceLevel level = footprint.PriceLevels[roundedPrice];

            if (isBuyTick) { level.BuyCount++; footprint.TotalBuyCount++; }
            else if (isSellTick) { level.SellCount++; footprint.TotalSellCount++; }

            level.TotalCount++;
            footprint.TotalTicks++;
            footprint.LastBid = tick.Bid;
            footprint.LastAsk = tick.Ask;
        }

        // ═══════════════════════════════════════
        //  FINALIZE CANDLES
        // ═══════════════════════════════════════

        private void FinalizeAllCandles()
        {
            var allCandles = candleFootprints
                .Where(kvp => !kvp.Value.IsFinalized)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            foreach (var kvp in allCandles)
            {
                var fp = kvp.Value;
                fp.IsFinalized = true;
                CountBubbles(fp);

                if (ShowBubbles) DrawAllBubblesForCandle(fp);

                string key = fp.BarTime.ToString("yyyyMMddHHmmss");
                if (!signalsDrawn.Contains(key))
                {
                    AnalyzeAndDrawSignal(fp);
                    signalsDrawn.Add(key);
                }
            }
        }

        private void FinalizeCandle(CandleFootprint footprint)
        {
            if (footprint.TotalTicks < 5) return;

            footprint.IsFinalized = true;
            CountBubbles(footprint);

            if (ShowBubbles) DrawAllBubblesForCandle(footprint);

            string key = footprint.BarTime.ToString("yyyyMMddHHmmss");
            if (!signalsDrawn.Contains(key))
            {
                AnalyzeAndDrawSignal(footprint);
                signalsDrawn.Add(key);
            }
        }

        private void CountBubbles(CandleFootprint fp)
        {
            int buy = 0, sell = 0;
            foreach (var kvp in fp.PriceLevels)
            {
                int delta = kvp.Value.BuyCount - kvp.Value.SellCount;
                int absDelta = Math.Abs(delta);
                if (absDelta >= MinDeltaPerLevel && kvp.Value.TotalCount >= MinVolumePerLevel)
                {
                    if (delta > 0) buy++;
                    else sell++;
                }
            }
            fp.BuyBubblesCount = buy;
            fp.SellBubblesCount = sell;
        }

        // ═══════════════════════════════════════
        //  CLUSTER ZONE ENGINE
        // ═══════════════════════════════════════

        private void BuildClusterZonesFromHistory()
        {
            clusterZones.Clear();
            virginClusters.Clear();
            testedClusters.Clear();

            double tol = ClusterTolerancePips * Symbol.PipSize;

            foreach (var candleKvp in candleFootprints.OrderBy(k => k.Key))
            {
                var fp = candleKvp.Value;
                if (!fp.IsFinalized) continue;

                foreach (var levelKvp in fp.PriceLevels)
                {
                    AddLevelToCluster(levelKvp.Key, levelKvp.Value, fp.BarIndex, tol, drawOnCreate: false);
                }
            }

            FinalizeClusterDominance();

            if (ShowClusterZones)
                DrawAllClusterZones();

            Print($"✓ Built {clusterZones.Count} cluster zones");
        }

        private void UpdateClusterZonesWithNewCandle(CandleFootprint footprint)
        {
            double tol = ClusterTolerancePips * Symbol.PipSize;

            foreach (var levelKvp in footprint.PriceLevels)
            {
                int delta = levelKvp.Value.BuyCount - levelKvp.Value.SellCount;
                int absDelta = Math.Abs(delta);
                if (absDelta < MinDeltaPerLevel || levelKvp.Value.TotalCount < MinVolumePerLevel) continue;

                AddLevelToCluster(levelKvp.Key, levelKvp.Value, footprint.BarIndex, tol, drawOnCreate: true);
            }
        }

        private void AddLevelToCluster(double levelPrice, PriceLevel level, int barIndex, double tol, bool drawOnCreate)
        {
            int delta = level.BuyCount - level.SellCount;
            int absDelta = Math.Abs(delta);
            if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel) return;

            foreach (var zone in clusterZones)
            {
                if (Math.Abs(levelPrice - zone.CenterPrice) <= tol)
                {
                    if (delta > 0) { zone.TotalBuyBubbles++; zone.TotalBuyVolume += level.BuyCount; }
                    else { zone.TotalSellBubbles++; zone.TotalSellVolume += level.SellCount; }

                    zone.LastBarIndex = barIndex;
                    zone.CenterPrice = (zone.CenterPrice + levelPrice) / 2.0;
                    RecalcDominance(zone);

                    if (drawOnCreate) DrawSingleClusterZone(zone);
                    return;
                }
            }

            // New zone
            var newZone = new ClusterZone
            {
                ZoneId = $"CZ_{barIndex}_{levelPrice:F5}",
                CenterPrice = levelPrice,
                PriceMin = levelPrice - tol,
                PriceMax = levelPrice + tol,
                FirstBarIndex = barIndex,
                LastBarIndex = barIndex,
                IsVirgin = true
            };

            if (delta > 0) { newZone.TotalBuyBubbles = 1; newZone.TotalBuyVolume = level.BuyCount; }
            else { newZone.TotalSellBubbles = 1; newZone.TotalSellVolume = level.SellCount; }

            newZone.Dominance = delta > 0 ? ClusterDominance.BuyDominated : ClusterDominance.SellDominated;
            newZone.BuyPercent = delta > 0 ? 100.0 : 0.0;

            clusterZones.Add(newZone);
            if (EnableVirginCluster) virginClusters.Add(newZone.ZoneId);

            if (drawOnCreate) DrawSingleClusterZone(newZone);
        }

        private void RecalcDominance(ClusterZone zone)
        {
            int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
            if (total == 0) return;

            double buyPct = (double)zone.TotalBuyBubbles / total * 100.0;
            zone.BuyPercent = buyPct;

            if (buyPct >= ClusterDominanceThreshold) zone.Dominance = ClusterDominance.BuyDominated;
            else if ((100.0 - buyPct) >= ClusterDominanceThreshold) zone.Dominance = ClusterDominance.SellDominated;
            else zone.Dominance = ClusterDominance.Consolidated;
        }

        private void FinalizeClusterDominance()
        {
            foreach (var zone in clusterZones)
                RecalcDominance(zone);
        }

        private ClusterZoneResult AnalyzeClusterAtPrice(double price, int currentBarIndex)
        {
            double tol = ClusterTolerancePips * Symbol.PipSize;
            double priceMin = price - tol;
            double priceMax = price + tol;

            int buyBubbles = 0, sellBubbles = 0, buyVol = 0, sellVol = 0;
            int lookbackStart = Math.Max(0, currentBarIndex - ClusterLookback);

            for (int i = lookbackStart; i < currentBarIndex; i++)
            {
                if (!candleFootprints.ContainsKey(i)) continue;

                foreach (var kvp in candleFootprints[i].PriceLevels)
                {
                    if (kvp.Key < priceMin || kvp.Key > priceMax) continue;

                    int delta = kvp.Value.BuyCount - kvp.Value.SellCount;
                    int absDelta = Math.Abs(delta);
                    if (absDelta < MinDeltaPerLevel || kvp.Value.TotalCount < MinVolumePerLevel) continue;

                    if (delta > 0) { buyBubbles++; buyVol += kvp.Value.BuyCount; }
                    else { sellBubbles++; sellVol += kvp.Value.SellCount; }
                }
            }

            int total = buyBubbles + sellBubbles;
            if (total < MinBubblesInCluster) return new ClusterZoneResult { IsValid = false };

            double buyPct = (double)buyBubbles / total * 100.0;
            ClusterDominance dominance;

            if (buyPct >= ClusterDominanceThreshold) dominance = ClusterDominance.BuyDominated;
            else if ((100.0 - buyPct) >= ClusterDominanceThreshold) dominance = ClusterDominance.SellDominated;
            else dominance = ClusterDominance.Consolidated;

            return new ClusterZoneResult
            {
                IsValid = true,
                Dominance = dominance,
                TotalBuyBubbles = buyBubbles,
                TotalSellBubbles = sellBubbles,
                BuyPercent = buyPct,
                SellPercent = 100.0 - buyPct
            };
        }

        private ClusterZone FindVirginClusterAtPrice(double price)
        {
            foreach (var zone in clusterZones)
            {
                if (!virginClusters.Contains(zone.ZoneId)) continue;
                if (price >= zone.PriceMin && price <= zone.PriceMax)
                {
                    virginClusters.Remove(zone.ZoneId);
                    testedClusters.Add(zone.ZoneId);
                    zone.IsVirgin = false;
                    return zone;
                }
            }
            return null;
        }

        // ═══════════════════════════════════════
        //  SIGNAL ANALYSIS
        // ═══════════════════════════════════════

        private void AnalyzeAndDrawSignal(CandleFootprint footprint)
        {
            int totalBubbles = footprint.BuyBubblesCount + footprint.SellBubblesCount;
            int netDelta = footprint.BuyBubblesCount - footprint.SellBubblesCount;
            int absDelta = Math.Abs(netDelta);

            if (totalBubbles < MinBubblesForSignal) return;

            double avgBubbles = GetAverageBubbleCount(footprint.BarIndex, 10);
            if (avgBubbles < 2) avgBubbles = 2;
            double spikeThreshold = avgBubbles * VolumeSpikeMultiplier;

            double currentPrice = Bars.ClosePrices[footprint.BarIndex];
            var clusterResult = AnalyzeClusterAtPrice(currentPrice, footprint.BarIndex);
            var virginZone = EnableVirginCluster ? FindVirginClusterAtPrice(currentPrice) : null;

            // Failed Auction check
            bool isFailedAuction = false;
            if (EnableFailedAuction && clusterResult.IsValid && clusterResult.Dominance != ClusterDominance.Consolidated)
            {
                double open = Bars.OpenPrices[footprint.BarIndex];
                double close = Bars.ClosePrices[footprint.BarIndex];
                double range = Bars.HighPrices[footprint.BarIndex] - Bars.LowPrices[footprint.BarIndex];
                if (range > Symbol.PipSize)
                    isFailedAuction = (Math.Abs(close - open) / range) < 0.30;
            }

            string signal = "";
            string colorName = "";
            bool clusterConfirmed = false;
            string signalType = "";

            // 1. VOLUME SPIKE
            if (EnableVolumeSpikeSignal && totalBubbles >= spikeThreshold)
            {
                if (netDelta >= 2)
                {
                    bool opposed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.SellDominated;
                    if (!opposed)
                    {
                        signal = "▲▲";
                        colorName = "Cyan";
                        signalType = "SPIKE BUY";
                        clusterConfirmed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.BuyDominated;
                    }
                }
                else if (netDelta <= -2)
                {
                    bool opposed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.BuyDominated;
                    if (!opposed)
                    {
                        signal = "▼▼";
                        colorName = "Magenta";
                        signalType = "SPIKE SELL";
                        clusterConfirmed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.SellDominated;
                    }
                }
            }

            // 2. ABSORPTION (reversal)
            if (string.IsNullOrEmpty(signal) && EnableAbsorptionSignal && totalBubbles >= 4 && absDelta <= 1)
            {
                double open = Bars.OpenPrices[footprint.BarIndex];
                double close = Bars.ClosePrices[footprint.BarIndex];
                bool bullishCandle = close > open;

                string absDir = bullishCandle ? "SELL" : "BUY";
                bool clusterOk = !clusterResult.IsValid ||
                    (bullishCandle && clusterResult.Dominance != ClusterDominance.BuyDominated) ||
                    (!bullishCandle && clusterResult.Dominance != ClusterDominance.SellDominated);

                if (clusterOk)
                {
                    signal = "◆";
                    colorName = "Yellow";
                    signalType = $"ABSORB {absDir}";
                    clusterConfirmed = clusterResult.IsValid;
                }
            }

            // 3. CLIMAX (strict netDelta)
            if (string.IsNullOrEmpty(signal) && EnableClimaxSignal && totalBubbles >= 6)
            {
                int prevBubbles = GetPreviousBubbleCount(footprint.BarIndex);
                if (prevBubbles >= 5)
                {
                    if (netDelta > 0)
                    {
                        signal = "TOP";
                        colorName = virginZone != null ? "Orange" : "Lime";
                        signalType = "CLIMAX TOP";
                    }
                    else if (netDelta < 0)
                    {
                        signal = "BOT";
                        colorName = virginZone != null ? "Lime" : "Orange";
                        signalType = "CLIMAX BOT";
                    }
                    // netDelta == 0 → skip
                }
            }

            // 4. FAILED AUCTION
            if (string.IsNullOrEmpty(signal) && isFailedAuction)
            {
                if (clusterResult.Dominance == ClusterDominance.BuyDominated)
                {
                    signal = "FA↓";
                    colorName = "Orange";
                    signalType = "FAILED AUCTION SELL";
                }
                else if (clusterResult.Dominance == ClusterDominance.SellDominated)
                {
                    signal = "FA↑";
                    colorName = "Lime";
                    signalType = "FAILED AUCTION BUY";
                }
            }

            // 5. VIRGIN CLUSTER
            if (string.IsNullOrEmpty(signal) && virginZone != null && virginZone.Dominance != ClusterDominance.Consolidated)
            {
                signal = virginZone.Dominance == ClusterDominance.BuyDominated ? "V↑" : "V↓";
                colorName = "White";
                signalType = $"VIRGIN {virginZone.Dominance}";
                clusterConfirmed = true;
            }

            // 6. SIMPLE IMBALANCE
            if (string.IsNullOrEmpty(signal) && EnableSimpleImbalance && totalBubbles >= 3)
            {
                if (netDelta >= 2)
                {
                    bool opposed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.SellDominated;
                    bool consolidated = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.Consolidated;
                    if (!opposed && !consolidated)
                    {
                        signal = "[B]";
                        colorName = "LightGreen";
                        signalType = "IMBAL BUY";
                        clusterConfirmed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.BuyDominated;
                    }
                }
                else if (netDelta <= -2)
                {
                    bool opposed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.BuyDominated;
                    bool consolidated = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.Consolidated;
                    if (!opposed && !consolidated)
                    {
                        signal = "[S]";
                        colorName = "LightCoral";
                        signalType = "IMBAL SELL";
                        clusterConfirmed = clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.SellDominated;
                    }
                }
            }

            // ── Draw signal if detected ──
            if (!string.IsNullOrEmpty(signal) && ShowSignals)
            {
                footprint.HasSignal = true;
                footprint.SignalText = clusterConfirmed ? signal + "★" : signal;
                footprint.SignalColor = colorName;
                footprint.SignalType = signalType;
                footprint.ClusterConfirmed = clusterConfirmed;
                footprint.ClusterBuyPct = clusterResult.IsValid ? clusterResult.BuyPercent : 0;
                footprint.ClusterSellPct = clusterResult.IsValid ? clusterResult.SellPercent : 0;

                DrawSignalLabel(footprint);
                signalsDetected++;

                Print($"🔔 {signalType}{(clusterConfirmed ? " [CLUSTER✓]" : "")} | Bar {footprint.BarTime:HH:mm} | Bubbles={totalBubbles} NetΔ={netDelta}");
                if (clusterResult.IsValid)
                    Print($"   Cluster → B:{clusterResult.BuyPercent:F0}% S:{clusterResult.SellPercent:F0}% | {clusterResult.Dominance}");
            }
        }

        // ═══════════════════════════════════════
        //  DRAWING
        // ═══════════════════════════════════════

        private void DrawAllBubblesForCandle(CandleFootprint fp)
        {
            foreach (var levelKvp in fp.PriceLevels)
            {
                PriceLevel level = levelKvp.Value;
                int delta = level.BuyCount - level.SellCount;
                int absDelta = Math.Abs(delta);
                if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel) continue;

                DrawFootprintBubble(fp.BarIndex, level, delta, delta > 0);
                bubblesDrawn++;
            }

            if (ShowBubbleCounter)
                DrawBubbleCounter(fp.BarIndex, fp.BuyBubblesCount, fp.SellBubblesCount);
        }

        private void DrawCurrentCandleBubbles(CandleFootprint footprint)
        {
            string prefix = $"FP_{footprint.BarIndex}_";
            string counterName = $"BC_{footprint.BarIndex}";

            var toRemove = Chart.Objects
                .Where(obj => obj.Name.StartsWith(prefix) || obj.Name == counterName)
                .ToList();
            foreach (var obj in toRemove) Chart.RemoveObject(obj.Name);

            int buy = 0, sell = 0;
            foreach (var levelKvp in footprint.PriceLevels)
            {
                PriceLevel level = levelKvp.Value;
                int delta = level.BuyCount - level.SellCount;
                int absDelta = Math.Abs(delta);
                if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel) continue;

                DrawFootprintBubble(footprint.BarIndex, level, delta, delta > 0);
                if (delta > 0) buy++; else sell++;
            }

            footprint.BuyBubblesCount = buy;
            footprint.SellBubblesCount = sell;

            if (ShowBubbleCounter)
                DrawBubbleCounter(footprint.BarIndex, buy, sell);
        }

        private void DrawFootprintBubble(int barIndex, PriceLevel level, int delta, bool isBuy)
        {
            int absDelta = Math.Abs(delta);
            Color baseColor = isBuy ? GetColorFromString(BuyBubbleColor) : GetColorFromString(SellBubbleColor);
            Color color = Color.FromArgb(BubbleOpacity, baseColor.R, baseColor.G, baseColor.B);

            double high = Bars.HighPrices[barIndex];
            double low = Bars.LowPrices[barIndex];
            double range = high - low;

            double sizePct;
            if (absDelta <= 5) sizePct = 0.05 + (absDelta / 5.0) * 0.15;
            else if (absDelta <= 10) sizePct = 0.20 + ((absDelta - 5) / 5.0) * 0.30;
            else if (absDelta <= 15) sizePct = 0.50 + ((absDelta - 10) / 5.0) * 0.30;
            else if (absDelta <= 20) sizePct = 0.80 + ((absDelta - 15) / 5.0) * 0.40;
            else sizePct = 1.20 + (Math.Min(10, absDelta - 20) / 10.0) * 0.60;

            sizePct = Math.Min(1.8, sizePct * BubbleSizeMultiplier);
            if (level.TotalCount > 30) sizePct *= (1.0 + Math.Min(0.10, (level.TotalCount - 30) / 400.0));

            double radius = Math.Max(range * sizePct, Symbol.PipSize >= 0.1 ? range * 0.005 : Symbol.PipSize * 2);

            DateTime barTime = Bars.OpenTimes[barIndex];
            TimeSpan barDur = barIndex > 0
                ? Bars.OpenTimes[barIndex] - Bars.OpenTimes[barIndex - 1]
                : TimeSpan.FromMinutes(1);

            double wf;
            if (sizePct <= 0.20) wf = 0.30;
            else if (sizePct <= 0.40) wf = 0.50;
            else if (sizePct <= 0.60) wf = 0.75;
            else if (sizePct <= 0.80) wf = 1.00;
            else if (sizePct <= 1.00) wf = 1.25;
            else wf = 1.50;

            TimeSpan bw = TimeSpan.FromTicks((long)(barDur.Ticks * wf));
            string name = $"FP_{barIndex}_{level.Price:F5}".Replace(".", "_");

            try
            {
                var ellipse = Chart.DrawEllipse(name + "_b",
                    barTime.Subtract(bw), level.Price + radius,
                    barTime.Add(bw), level.Price - radius, color);
                if (ellipse != null) { ellipse.IsFilled = true; ellipse.Thickness = 1; }
            }
            catch { }

            if (ShowDeltaLabels && absDelta >= 3)
            {
                var txt = Chart.DrawText(name + "_l", $"{delta:+#;-#;0}", barIndex, level.Price, Color.White);
                if (txt != null)
                {
                    txt.FontSize = absDelta >= 20 ? 9 : absDelta >= 15 ? 8 : absDelta >= 10 ? 7 : absDelta >= 6 ? 6 : 5;
                    txt.HorizontalAlignment = HorizontalAlignment.Center;
                    txt.VerticalAlignment = VerticalAlignment.Center;
                    txt.IsBold = absDelta >= 15;
                }
            }
        }

        private void DrawBubbleCounter(int barIndex, int buy, int sell)
        {
            if (buy == 0 && sell == 0) return;

            double low = Bars.LowPrices[barIndex];
            double range = Bars.HighPrices[barIndex] - low;
            double offset = Math.Max(range * 0.3, Symbol.PipSize >= 0.1 ? range * 0.2 : Symbol.PipSize * 5);

            var lbl = Chart.DrawText($"BC_{barIndex}", $"{buy} | {sell}", barIndex, low - offset, Color.White);
            if (lbl != null)
            {
                lbl.FontSize = 8;
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.VerticalAlignment = VerticalAlignment.Top;
                lbl.Color = buy > sell ? Color.LimeGreen : sell > buy ? Color.OrangeRed : Color.White;
            }
        }

        private void DrawSignalLabel(CandleFootprint footprint)
        {
            int barIndex = -1;
            for (int i = 0; i < Bars.Count; i++)
            {
                if (Bars.OpenTimes[i] == footprint.BarTime) { barIndex = i; break; }
            }
            if (barIndex < 0) return;

            double high = Bars.HighPrices[barIndex];
            double range = high - Bars.LowPrices[barIndex];
            double offset = Math.Max(range * 0.15, Symbol.PipSize >= 0.1 ? Symbol.PipSize * 2 : Symbol.PipSize * 3);

            string name = $"OFSig_{footprint.BarTime:yyyyMMddHHmmss}";
            try { Chart.RemoveObject(name); } catch { }

            Color col = GetColorFromString(footprint.SignalColor);
            var lbl = Chart.DrawText(name, footprint.SignalText, barIndex, high + offset, col);
            if (lbl != null)
            {
                lbl.FontSize = footprint.ClusterConfirmed ? SignalFontSize + 4 : SignalFontSize + 2;
                lbl.IsBold = true;
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                lbl.VerticalAlignment = VerticalAlignment.Bottom;
            }

            // Tooltip-style info below candle
            if (footprint.ClusterConfirmed)
            {
                string infoName = $"OFInfo_{footprint.BarTime:yyyyMMddHHmmss}";
                string infoText = $"B:{footprint.ClusterBuyPct:F0}% S:{footprint.ClusterSellPct:F0}%";
                double low = Bars.LowPrices[barIndex];
                double infoOffset = Math.Max(range * 0.4, Symbol.PipSize * 8);

                try { Chart.RemoveObject(infoName); } catch { }
                var info = Chart.DrawText(infoName, infoText, barIndex, low - infoOffset, Color.FromArgb(180, 255, 255, 255));
                if (info != null)
                {
                    info.FontSize = 7;
                    info.HorizontalAlignment = HorizontalAlignment.Center;
                    info.VerticalAlignment = VerticalAlignment.Top;
                }
            }
        }

        private void DrawSingleClusterZone(ClusterZone zone)
        {
            if (!ShowClusterZones) return;

            int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
            if (total < MinBubblesInCluster) return;

            Color zoneColor;
            switch (zone.Dominance)
            {
                case ClusterDominance.BuyDominated:
                    zoneColor = Color.FromArgb(40, 0, 255, 0);
                    break;
                case ClusterDominance.SellDominated:
                    zoneColor = Color.FromArgb(40, 255, 0, 0);
                    break;
                default:
                    zoneColor = Color.FromArgb(20, 255, 255, 0);
                    break;
            }

            string rectName = $"ClusterZone_{zone.ZoneId}";
            try { Chart.RemoveObject(rectName); } catch { }

            int rightBar = Math.Min(zone.LastBarIndex + ExtendZoneBars, Bars.Count - 1);

            try
            {
                var rect = Chart.DrawRectangle(rectName,
                    zone.FirstBarIndex, zone.PriceMax,
                    rightBar, zone.PriceMin,
                    zoneColor);
                if (rect != null) { rect.IsFilled = true; rect.Thickness = 1; }
            }
            catch { }

            // Virgin label
            if (ShowVirginLabel && zone.IsVirgin && virginClusters.Contains(zone.ZoneId))
            {
                string lblName = $"VirginLbl_{zone.ZoneId}";
                try { Chart.RemoveObject(lblName); } catch { }

                Color lblColor = zone.Dominance == ClusterDominance.BuyDominated ? Color.LimeGreen : Color.OrangeRed;
                string lblText = zone.Dominance == ClusterDominance.BuyDominated
                    ? $"🔮 Virgin Buy {zone.BuyPercent:F0}%"
                    : $"🔮 Virgin Sell {100 - zone.BuyPercent:F0}%";

                var lbl = Chart.DrawText(lblName, lblText, zone.FirstBarIndex, zone.PriceMax, lblColor);
                if (lbl != null)
                {
                    lbl.FontSize = 8;
                    lbl.VerticalAlignment = VerticalAlignment.Bottom;
                }
            }
        }

        private void DrawAllClusterZones()
        {
            int drawn = 0;
            foreach (var zone in clusterZones)
            {
                DrawSingleClusterZone(zone);
                drawn++;
            }
            Print($"✓ Drew {drawn} cluster zone rectangles");
        }

        private void DrawInfoPanel()
        {
            string panelName = "OFInfoPanel";
            try { Chart.RemoveObject(panelName); } catch { }

            int currentBar = Bars.Count - 1;
            CandleFootprint currentFp = candleFootprints.ContainsKey(currentBar)
                ? candleFootprints[currentBar] : null;

            int liveBuy = currentFp?.BuyBubblesCount ?? 0;
            int liveSell = currentFp?.SellBubblesCount ?? 0;

            double currentPrice = Symbol.Bid;
            var clusterResult = AnalyzeClusterAtPrice(currentPrice, currentBar);

            string dominanceText = !clusterResult.IsValid
                ? "No Cluster"
                : clusterResult.Dominance == ClusterDominance.BuyDominated
                    ? $"BUY {clusterResult.BuyPercent:F0}%"
                    : clusterResult.Dominance == ClusterDominance.SellDominated
                        ? $"SELL {clusterResult.SellPercent:F0}%"
                        : $"CONSOLIDATED";

            string panelText =
                $"ORDER FLOW\n" +
                $"Bubbles: ▲{liveBuy} ▼{liveSell}\n" +
                $"Cluster: {dominanceText}\n" +
                $"Zones: {clusterZones.Count} | Virgin: {virginClusters.Count}\n" +
                $"Signals: {signalsDetected}";

            double panelPrice = Bars.HighPrices[currentBar] + Symbol.PipSize * 20;
            var panel = Chart.DrawText(panelName, panelText, currentBar - 5, panelPrice, Color.White);
            if (panel != null)
            {
                panel.FontSize = 9;
                panel.HorizontalAlignment = HorizontalAlignment.Right;
            }
        }

        // ═══════════════════════════════════════
        //  HELPER METHODS
        // ═══════════════════════════════════════

        private double GetAverageBubbleCount(int currentIndex, int lookback)
        {
            int sum = 0, count = 0;
            for (int i = currentIndex - lookback; i < currentIndex; i++)
            {
                if (i >= 0 && candleFootprints.ContainsKey(i))
                {
                    var fp = candleFootprints[i];
                    sum += fp.BuyBubblesCount + fp.SellBubblesCount;
                    count++;
                }
            }
            return count > 0 ? (double)sum / count : 0;
        }

        private int GetPreviousBubbleCount(int currentIndex)
        {
            int prev = currentIndex - 1;
            if (prev >= 0 && candleFootprints.ContainsKey(prev))
                return candleFootprints[prev].BuyBubblesCount + candleFootprints[prev].SellBubblesCount;
            return 0;
        }

        private double RoundToPip(double price)
        {
            if (Symbol.PipSize >= 0.1)
            {
                double g = price >= 10000 ? 50 : price >= 1000 ? 10 : price >= 100 ? 5 : 1;
                return Math.Round(price / g) * g;
            }
            return Math.Round(price / Symbol.PipSize) * Symbol.PipSize;
        }

        private int FindBarIndex(DateTime time)
        {
            for (int i = Bars.Count - 1; i >= 0; i--)
            {
                DateTime start = Bars.OpenTimes[i];
                DateTime end = i < Bars.Count - 1 ? Bars.OpenTimes[i + 1] : Server.Time;
                if (time >= start && time < end) return i;
            }
            return Bars.Count - 1;
        }

        private Color GetColorFromString(string name)
        {
            switch ((name ?? "").ToLower())
            {
                case "red": return Color.Red;
                case "green": return Color.Green;
                case "lime": return Color.Lime;
                case "blue": return Color.Blue;
                case "yellow": return Color.Yellow;
                case "orange": return Color.Orange;
                case "cyan": return Color.Cyan;
                case "white": return Color.White;
                case "magenta": return Color.Magenta;
                case "lightgreen": return Color.LightGreen;
                case "lightcoral": return Color.LightCoral;
                default: return Color.White;
            }
        }

        // ═══════════════════════════════════════
        //  ENUMS
        // ═══════════════════════════════════════

        public enum ClusterDominance { BuyDominated, SellDominated, Consolidated }

        // ═══════════════════════════════════════
        //  DATA CLASSES
        // ═══════════════════════════════════════

        private class CandleFootprint
        {
            public int BarIndex { get; set; }
            public DateTime BarTime { get; set; }
            public Dictionary<double, PriceLevel> PriceLevels { get; set; } = new Dictionary<double, PriceLevel>();
            public int TotalTicks { get; set; }
            public int TotalBuyCount { get; set; }
            public int TotalSellCount { get; set; }
            public double LastBid { get; set; }
            public double LastAsk { get; set; }
            public bool IsFinalized { get; set; }
            public int BuyBubblesCount { get; set; }
            public int SellBubblesCount { get; set; }
            public bool HasSignal { get; set; }
            public string SignalText { get; set; }
            public string SignalColor { get; set; }
            public string SignalType { get; set; }
            public bool ClusterConfirmed { get; set; }
            public double ClusterBuyPct { get; set; }
            public double ClusterSellPct { get; set; }
        }

        private class PriceLevel
        {
            public double Price { get; set; }
            public int BuyCount { get; set; }
            public int SellCount { get; set; }
            public int TotalCount { get; set; }
        }

        private class ClusterZone
        {
            public string ZoneId { get; set; }
            public double CenterPrice { get; set; }
            public double PriceMin { get; set; }
            public double PriceMax { get; set; }
            public int FirstBarIndex { get; set; }
            public int LastBarIndex { get; set; }
            public int TotalBuyBubbles { get; set; }
            public int TotalSellBubbles { get; set; }
            public int TotalBuyVolume { get; set; }
            public int TotalSellVolume { get; set; }
            public double BuyPercent { get; set; }
            public ClusterDominance Dominance { get; set; }
            public bool IsVirgin { get; set; } = true;
        }

        private class ClusterZoneResult
        {
            public bool IsValid { get; set; }
            public ClusterDominance Dominance { get; set; }
            public int TotalBuyBubbles { get; set; }
            public int TotalSellBubbles { get; set; }
            public double BuyPercent { get; set; }
            public double SellPercent { get; set; }
        }
    }
}