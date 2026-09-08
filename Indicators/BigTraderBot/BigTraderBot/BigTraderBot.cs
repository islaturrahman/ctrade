using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None, AddIndicators = true)]
    public class OrderFlowBot : Robot
    {
        // ═══════════════════════════════════════
        //  ORDER FLOW ANALYSIS PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("── ORDER FLOW ──", DefaultValue = "────────────────")]
        public string Separator1 { get; set; }

        [Parameter("Min Delta Per Level", Group = "Order Flow", DefaultValue = 3, MinValue = 1)]
        public int MinDeltaPerLevel { get; set; }

        [Parameter("Min Volume Per Level", Group = "Order Flow", DefaultValue = 1, MinValue = 1)]
        public int MinVolumePerLevel { get; set; }

        [Parameter("Pip Step (Thickness)", Group = "Order Flow", DefaultValue = 1.0, MinValue = 0.1, Step = 0.1)]
        public double PipStep { get; set; }

        [Parameter("Enable Vacuum/Spoofing Detection", Group = "Order Flow", DefaultValue = true)]
        public bool EnableVacuumDetection { get; set; }

        [Parameter("Vacuum Min Pips Jump", Group = "Order Flow", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 20.0)]
        public double VacuumMinPips { get; set; }

        [Parameter("Min Bubbles for Signal", Group = "Order Flow", DefaultValue = 2, MinValue = 1)]
        public int MinBubblesForSignal { get; set; }

        [Parameter("Volume Spike Multiplier", Group = "Order Flow", DefaultValue = 1.3, MinValue = 1.1, MaxValue = 3.0)]
        public double VolumeSpikeMultiplier { get; set; }

        // ═══════════════════════════════════════
        //  CLUSTER ZONE PARAMETERS (NEW)
        // ═══════════════════════════════════════

        [Parameter("── CLUSTER ZONE ──", DefaultValue = "────────────────")]
        public string Separator2 { get; set; }

        [Parameter("Cluster Lookback (candles)", Group = "Cluster Zone", DefaultValue = 20, MinValue = 5, MaxValue = 50)]
        public int ClusterLookback { get; set; }

        [Parameter("Cluster Price Tolerance (pips)", Group = "Cluster Zone", DefaultValue = 3.0, MinValue = 1.0, MaxValue = 10.0)]
        public double ClusterTolerancePips { get; set; }

        [Parameter("Cluster Dominance % Threshold", Group = "Cluster Zone", DefaultValue = 65.0, MinValue = 51.0, MaxValue = 90.0)]
        public double ClusterDominanceThreshold { get; set; }

        [Parameter("Min Bubbles in Cluster", Group = "Cluster Zone", DefaultValue = 5, MinValue = 2, MaxValue = 30)]
        public int MinBubblesInCluster { get; set; }

        [Parameter("Enable Virgin Cluster Detection", Group = "Cluster Zone", DefaultValue = true)]
        public bool EnableVirginCluster { get; set; }

        [Parameter("Enable Failed Auction Detection", Group = "Cluster Zone", DefaultValue = true)]
        public bool EnableFailedAuction { get; set; }

        [Parameter("Enable Cluster-to-Cluster TP", Group = "Cluster Zone", DefaultValue = true)]
        public bool EnableClusterTP { get; set; }

        // ═══════════════════════════════════════
        //  INTRA-CANDLE TIMER (NEW)
        // ═══════════════════════════════════════

        [Parameter("── INTRA-CANDLE TIMER ──", DefaultValue = "────────────────")]
        public string Separator3 { get; set; }

        [Parameter("Enable Intra-Candle Timer", Group = "Intra-Candle Timer", DefaultValue = true)]
        public bool EnableIntraCandleTimer { get; set; }

        [Parameter("Entry Window Start (%)", Group = "Intra-Candle Timer", DefaultValue = 40, MinValue = 10, MaxValue = 90)]
        public int EntryWindowStartPct { get; set; }

        [Parameter("Entry Window End (%)", Group = "Intra-Candle Timer", DefaultValue = 80, MinValue = 20, MaxValue = 99)]
        public int EntryWindowEndPct { get; set; }

        // ═══════════════════════════════════════
        //  SIGNAL FILTERS
        // ═══════════════════════════════════════

        [Parameter("Signal: Volume Spike", Group = "Signals", DefaultValue = true)]
        public bool EnableVolumeSpikeSignal { get; set; }

        [Parameter("Signal: Absorption (Reversal)", Group = "Signals", DefaultValue = true)]
        public bool EnableAbsorptionSignal { get; set; }

        [Parameter("Signal: Climax (Exhaustion)", Group = "Signals", DefaultValue = true)]
        public bool EnableClimaxSignal { get; set; }

        [Parameter("Signal: Simple Imbalance", Group = "Signals", DefaultValue = true)]
        public bool EnableSimpleImbalance { get; set; }

        [Parameter("Signal: Cluster Confirmation", Group = "Signals", DefaultValue = true)]
        public bool EnableClusterConfirmation { get; set; }

        // ═══════════════════════════════════════
        //  RISK MANAGEMENT
        // ═══════════════════════════════════════

        [Parameter("Position Size Mode", Group = "Risk Management", DefaultValue = PositionSizeMode.FixedLots)]
        public PositionSizeMode SizeMode { get; set; }

        [Parameter("Fixed Lot Size", Group = "Risk Management", DefaultValue = 0.01, MinValue = 0.01, MaxValue = 100)]
        public double FixedLots { get; set; }

        [Parameter("Risk % Per Trade", Group = "Risk Management", DefaultValue = 1.0, MinValue = 0.1, MaxValue = 10)]
        public double RiskPercent { get; set; }

        [Parameter("Max Daily Loss %", Group = "Risk Management", DefaultValue = 3.0, MinValue = 0.5, MaxValue = 20)]
        public double MaxDailyLossPercent { get; set; }

        [Parameter("Max Concurrent Positions", Group = "Risk Management", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int MaxPositions { get; set; }

        [Parameter("Max Trades Per Day", Group = "Risk Management", DefaultValue = 10, MinValue = 1, MaxValue = 100)]
        public int MaxTradesPerDay { get; set; }

        // ═══════════════════════════════════════
        //  STOP LOSS & TAKE PROFIT
        // ═══════════════════════════════════════

        [Parameter("SL/TP Mode", Group = "SL/TP", DefaultValue = SlTpMode.ATRBased)]
        public SlTpMode StopMode { get; set; }

        [Parameter("ATR Period", Group = "SL/TP", DefaultValue = 14, MinValue = 5, MaxValue = 50)]
        public int AtrPeriod { get; set; }

        [Parameter("SL ATR Multiplier", Group = "SL/TP", DefaultValue = 1.5, MinValue = 0.5, MaxValue = 5.0)]
        public double SlAtrMultiplier { get; set; }

        [Parameter("TP ATR Multiplier", Group = "SL/TP", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double TpAtrMultiplier { get; set; }

        [Parameter("Fixed SL (pips)", Group = "SL/TP", DefaultValue = 15, MinValue = 3, MaxValue = 200)]
        public double FixedSlPips { get; set; }

        [Parameter("Fixed TP (pips)", Group = "SL/TP", DefaultValue = 25, MinValue = 3, MaxValue = 500)]
        public double FixedTpPips { get; set; }

        // ═══════════════════════════════════════
        //  TRAILING STOP
        // ═══════════════════════════════════════

        [Parameter("Enable Trailing Stop", Group = "Trailing", DefaultValue = true)]
        public bool EnableTrailingStop { get; set; }

        [Parameter("Trailing Trigger (pips)", Group = "Trailing", DefaultValue = 10, MinValue = 1, MaxValue = 200)]
        public double TrailingTriggerPips { get; set; }

        [Parameter("Trailing Distance (pips)", Group = "Trailing", DefaultValue = 5, MinValue = 1, MaxValue = 100)]
        public double TrailingDistancePips { get; set; }

        // ═══════════════════════════════════════
        //  FILTERS
        // ═══════════════════════════════════════

        [Parameter("Max Spread (pips)", Group = "Filters", DefaultValue = 3.0, MinValue = 0.1, MaxValue = 50)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Enable Session Filter", Group = "Filters", DefaultValue = false)]
        public bool EnableSessionFilter { get; set; }

        [Parameter("Session Start Hour (UTC)", Group = "Filters", DefaultValue = 7, MinValue = 0, MaxValue = 23)]
        public int SessionStartHour { get; set; }

        [Parameter("Session End Hour (UTC)", Group = "Filters", DefaultValue = 20, MinValue = 0, MaxValue = 23)]
        public int SessionEndHour { get; set; }

        [Parameter("Cooldown Bars After Trade", Group = "Filters", DefaultValue = 2, MinValue = 0, MaxValue = 20)]
        public int CooldownBars { get; set; }

        // ═══════════════════════════════════════
        //  VISUAL SETTINGS
        // ═══════════════════════════════════════

        [Parameter("Show Bubbles on Chart", Group = "Visual", DefaultValue = true)]
        public bool ShowBubbles { get; set; }

        [Parameter("Bubble Opacity (%)", Group = "Visual", DefaultValue = 127, MinValue = 10, MaxValue = 255)]
        public int BubbleOpacity { get; set; }

        [Parameter("Buy Bubble Color", Group = "Visual", DefaultValue = "Green")]
        public string BuyBubbleColor { get; set; }

        [Parameter("Sell Bubble Color", Group = "Visual", DefaultValue = "Red")]
        public string SellBubbleColor { get; set; }

        [Parameter("Vacuum Bubble Color", Group = "Visual", DefaultValue = "Yellow")]
        public string VacuumBubbleColor { get; set; }

        [Parameter("Bubble Size Multiplier", Group = "Visual", DefaultValue = 0.8, MinValue = 0.01, MaxValue = 2.0)]
        public double BubbleSizeMultiplier { get; set; }

        [Parameter("Show Delta Labels", Group = "Visual", DefaultValue = true)]
        public bool ShowDeltaLabels { get; set; }

        [Parameter("Signal Font Size", Group = "Visual", DefaultValue = 10, MinValue = 8, MaxValue = 14)]
        public int SignalFontSize { get; set; }

        [Parameter("Show Cluster Zones", Group = "Visual", DefaultValue = true)]
        public bool ShowClusterZones { get; set; }

        // ═══════════════════════════════════════
        //  PRIVATE FIELDS
        // ═══════════════════════════════════════

        private const string BotLabel = "OrderFlowBot";

        private Dictionary<int, CandleFootprint> candleFootprints;
        private Ticks ticks;
        private int bubblesDrawn = 0;
        private double maxDeltaPerLevel = 0;
        private int maxVolumePerLevel = 0;
        private HashSet<int> drawnCandles = new HashSet<int>();
        private Dictionary<int, int> lastDrawnTickCountPerCandle = new Dictionary<int, int>();
        private HashSet<string> signalsAnalyzed = new HashSet<string>();

        // Risk Management State
        private AverageTrueRange atr;
        private double dailyStartBalance;
        private int dailyTradeCount;
        private DateTime lastTradeDay;
        private int lastTradeBarIndex = -999;

        // Signal Tracking
        private string pendingSignalType = "";
        private TradeType pendingSignalDirection;
        private bool hasPendingSignal = false;
        private int pendingSignalBarIndex = -1;
        private double pendingClusterTPPips = 0;

        // Cluster Zone Tracking
        private List<ClusterZone> clusterZones = new List<ClusterZone>();
        private HashSet<string> virginClusters = new HashSet<string>();  // Key = zone ID
        private HashSet<string> testedClusters = new HashSet<string>();  // Zones already touched by price

        // Stats
        private int totalSignalsGenerated = 0;
        private int totalTradesOpened = 0;
        private int totalTradesWon = 0;
        private int totalTradesLost = 0;

        protected override void OnStart()
        {
            candleFootprints = new Dictionary<int, CandleFootprint>();
            clusterZones = new List<ClusterZone>();
            ticks = MarketData.GetTicks();
            atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Exponential);

            dailyStartBalance = Account.Balance;
            dailyTradeCount = 0;
            lastTradeDay = Server.Time.Date;

            AdjustParametersForTimeframe();

            Print("═══════════════════════════════════════════════════");
            Print("  ORDER FLOW BOT — Cluster Zone Edition");
            Print("═══════════════════════════════════════════════════");
            Print($"Symbol: {SymbolName} | TF: {TimeFrame}");
            Print($"Cluster Lookback: {ClusterLookback} | Tolerance: {ClusterTolerancePips} pips | Dominance: {ClusterDominanceThreshold}%");
            Print($"Virgin Cluster: {EnableVirginCluster} | Failed Auction: {EnableFailedAuction} | Cluster TP: {EnableClusterTP}");
            Print($"Intra-Candle Timer: {EnableIntraCandleTimer} | Window: {EntryWindowStartPct}%-{EntryWindowEndPct}%");
            Print("═══════════════════════════════════════════════════");

            ticks.Tick += OnNewTick;
            ProcessHistoricalTicks();

            Positions.Closed += OnPositionClosed;
        }

        protected override void OnTick()
        {
            ResetDailyCountersIfNeeded();

            if (EnableTrailingStop)
                ManageTrailingStops();
        }

        protected override void OnBar()
        {
            CheckCompletedCandles();

            if (hasPendingSignal)
                ExecutePendingSignal();
        }

        protected override void OnStop()
        {
            Print("═══════════════════════════════════════════════════");
            Print("  ORDER FLOW BOT — SESSION SUMMARY");
            Print("═══════════════════════════════════════════════════");
            Print($"Total Signals: {totalSignalsGenerated}");
            Print($"Total Trades: {totalTradesOpened}");
            Print($"Won: {totalTradesWon} | Lost: {totalTradesLost}");
            double winRate = totalTradesOpened > 0 ? (double)totalTradesWon / totalTradesOpened * 100.0 : 0;
            Print($"Win Rate: {winRate:F1}%");
            Print($"Cluster Zones Detected: {clusterZones.Count}");
            Print("═══════════════════════════════════════════════════");
        }

        // ═══════════════════════════════════════
        //  TICK PROCESSING
        // ═══════════════════════════════════════

        private void OnNewTick(TicksTickEventArgs obj)
        {
            if (ticks.Count > 0)
            {
                var latestTick = ticks.Last();
                ProcessSingleTick(latestTick);

                int currentBarIndex = Bars.Count - 1;
                if (candleFootprints.ContainsKey(currentBarIndex))
                {
                    var currentFootprint = candleFootprints[currentBarIndex];

                    if (ShowBubbles)
                        DrawCurrentCandleBubbles(currentFootprint);

                    // Check intra-candle timer for live signals
                    if (EnableIntraCandleTimer && hasPendingSignal)
                        CheckIntraCandleWindow(currentBarIndex);
                }
            }
        }

        private void ProcessHistoricalTicks()
        {
            int tickCount = ticks.Count;
            Print($"Processing {tickCount} historical ticks...");

            if (tickCount == 0)
            {
                Print("⚠️ No tick data available");
                return;
            }

            int start = Math.Max(0, tickCount - 20000);

            for (int i = start; i < tickCount; i++)
            {
                ProcessSingleTick(ticks[i]);

                if ((i - start) % 5000 == 0 && i > start)
                    Print($"Progress: {i - start} / {tickCount - start}");
            }

            FinalizeAllCandles();
            BuildClusterZonesFromHistory();

            Print($"✓ Processing complete! Bubbles drawn: {bubblesDrawn}");
            Print($"✓ Cluster zones built: {clusterZones.Count}");
        }

        private void ProcessSingleTick(Tick tick)
        {
            int barIndex = FindBarIndex(tick.Time);
            if (barIndex < 0)
                return;

            if (!candleFootprints.ContainsKey(barIndex))
            {
                candleFootprints[barIndex] = new CandleFootprint
                {
                    BarIndex = barIndex,
                    BarTime = Bars.OpenTimes[barIndex]
                };
            }

            CandleFootprint footprint = candleFootprints[barIndex];

            if (footprint.IsFinalized)
                return;

            bool isBuyTick = false;
            bool isSellTick = false;

            double midLast = footprint.LastAsk > 0 ? (footprint.LastBid + footprint.LastAsk) / 2.0 : 0;
            double midNow = (tick.Bid + tick.Ask) / 2.0;

            if (footprint.LastAsk > 0 && tick.Ask > footprint.LastAsk)
                isBuyTick = true;
            else if (footprint.LastBid > 0 && tick.Bid < footprint.LastBid)
                isSellTick = true;
            else if (midLast > 0)
            {
                if (midNow > midLast) isBuyTick = true;
                else if (midNow < midLast) isSellTick = true;
            }

            double price = isBuyTick ? tick.Ask : tick.Bid;
            double barHigh = Bars.HighPrices[barIndex];
            double barLow = Bars.LowPrices[barIndex];

            // Strict clamp check: exclude ticks outside current bar boundaries (+/- 0.5 pip tolerance for spread)
            double tolerance = Symbol.PipSize * 0.5;
            if (price > barHigh + tolerance || price < barLow - tolerance)
                return;

            double roundedPrice = RoundToPip(price);

            if (!footprint.PriceLevels.ContainsKey(roundedPrice))
                footprint.PriceLevels[roundedPrice] = new PriceLevel { Price = roundedPrice };

            PriceLevel level = footprint.PriceLevels[roundedPrice];

            if (isBuyTick)
            {
                level.BuyCount++;
                footprint.TotalBuyCount++;
            }
            else if (isSellTick)
            {
                level.SellCount++;
                footprint.TotalSellCount++;
            }

            // Liquidity Vacuum / Spoofing Check: Sudden price displacement on small tick volume
            if (midLast > 0)
            {
                double priceJumpPips = Math.Abs(midNow - midLast) / Symbol.PipSize;
                if (EnableVacuumDetection && priceJumpPips >= VacuumMinPips)
                {
                    level.IsVacuumSpoof = true;
                    level.PriceImpact = priceJumpPips / Math.Max(1, level.TotalCount);
                }
            }

            level.TotalCount++;
            footprint.TotalTicks++;
            footprint.LastBid = tick.Bid;
            footprint.LastAsk = tick.Ask;
            footprint.LastTime = tick.Time;
        }

        // ═══════════════════════════════════════
        //  CLUSTER ZONE ENGINE (NEW)
        // ═══════════════════════════════════════

        /// <summary>
        /// Scans last N candles and groups bubbles by price proximity into ClusterZones.
        /// Each zone tracks buy/sell dominance for direction confirmation.
        /// </summary>
        private ClusterZoneResult AnalyzeClusterAtPrice(double currentPrice, int currentBarIndex)
        {
            double tolerancePrice = ClusterTolerancePips * Symbol.PipSize;
            double priceMin = currentPrice - tolerancePrice;
            double priceMax = currentPrice + tolerancePrice;

            int totalBuyBubbles = 0;
            int totalSellBubbles = 0;
            int totalBuyVolume = 0;
            int totalSellVolume = 0;
            int candlesWithBubbles = 0;
            double maxBuyVolAtLevel = 0;
            double maxSellVolAtLevel = 0;

            int lookbackStart = Math.Max(0, currentBarIndex - ClusterLookback);

            for (int i = lookbackStart; i < currentBarIndex; i++)
            {
                if (!candleFootprints.ContainsKey(i))
                    continue;

                var fp = candleFootprints[i];
                bool candleHadBubble = false;

                foreach (var kvp in fp.PriceLevels)
                {
                    double levelPrice = kvp.Key;
                    if (levelPrice < priceMin || levelPrice > priceMax)
                        continue;

                    PriceLevel level = kvp.Value;
                    int delta = level.BuyCount - level.SellCount;
                    int absDelta = Math.Abs(delta);

                    if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                        continue;

                    if (delta > 0)
                    {
                        totalBuyBubbles++;
                        totalBuyVolume += level.BuyCount;
                        if (level.BuyCount > maxBuyVolAtLevel) maxBuyVolAtLevel = level.BuyCount;
                    }
                    else
                    {
                        totalSellBubbles++;
                        totalSellVolume += level.SellCount;
                        if (level.SellCount > maxSellVolAtLevel) maxSellVolAtLevel = level.SellCount;
                    }

                    candleHadBubble = true;
                }

                if (candleHadBubble)
                    candlesWithBubbles++;
            }

            int totalBubbles = totalBuyBubbles + totalSellBubbles;

            if (totalBubbles < MinBubblesInCluster)
                return new ClusterZoneResult { IsValid = false };

            double buyPct = totalBubbles > 0 ? (double)totalBuyBubbles / totalBubbles * 100.0 : 0;
            double sellPct = 100.0 - buyPct;

            ClusterDominance dominance;
            TradeType direction;

            if (buyPct >= ClusterDominanceThreshold)
            {
                dominance = ClusterDominance.BuyDominated;
                direction = TradeType.Buy;
            }
            else if (sellPct >= ClusterDominanceThreshold)
            {
                dominance = ClusterDominance.SellDominated;
                direction = TradeType.Sell;
            }
            else
            {
                dominance = ClusterDominance.Consolidated; // "Perang volume" — skip
                direction = TradeType.Buy; // irrelevant
            }

            return new ClusterZoneResult
            {
                IsValid = true,
                Dominance = dominance,
                Direction = direction,
                TotalBuyBubbles = totalBuyBubbles,
                TotalSellBubbles = totalSellBubbles,
                TotalBuyVolume = totalBuyVolume,
                TotalSellVolume = totalSellVolume,
                BuyPercent = buyPct,
                SellPercent = sellPct,
                CandlesWithBubbles = candlesWithBubbles,
                MaxBuyVolume = maxBuyVolAtLevel,
                MaxSellVolume = maxSellVolAtLevel
            };
        }

        /// <summary>
        /// Builds a list of significant cluster zones from historical data.
        /// Used for Cluster-to-Cluster TP and Virgin Cluster detection.
        /// </summary>
        private void BuildClusterZonesFromHistory()
        {
            clusterZones.Clear();
            virginClusters.Clear();
            testedClusters.Clear();

            double tolerancePrice = ClusterTolerancePips * Symbol.PipSize;

            var sortedCandles = candleFootprints
                .OrderBy(kvp => kvp.Key)
                .ToList();

            foreach (var candleKvp in sortedCandles)
            {
                var fp = candleKvp.Value;
                if (!fp.IsFinalized) continue;

                foreach (var levelKvp in fp.PriceLevels)
                {
                    double levelPrice = levelKvp.Key;
                    PriceLevel level = levelKvp.Value;

                    int delta = level.BuyCount - level.SellCount;
                    int absDelta = Math.Abs(delta);

                    if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                        continue;

                    // Check if this level already belongs to an existing zone
                    bool addedToExisting = false;
                    foreach (var zone in clusterZones)
                    {
                        if (Math.Abs(levelPrice - zone.CenterPrice) <= tolerancePrice)
                        {
                            // Add bubble to zone
                            if (delta > 0)
                            {
                                zone.TotalBuyBubbles++;
                                zone.TotalBuyVolume += level.BuyCount;
                            }
                            else
                            {
                                zone.TotalSellBubbles++;
                                zone.TotalSellVolume += level.SellCount;
                            }
                            zone.LastBarIndex = fp.BarIndex;
                            zone.CenterPrice = (zone.CenterPrice + levelPrice) / 2.0; // rolling average
                            addedToExisting = true;
                            break;
                        }
                    }

                    if (!addedToExisting)
                    {
                        // Create new zone
                        var newZone = new ClusterZone
                        {
                            ZoneId = $"CZ_{fp.BarIndex}_{levelPrice:F5}",
                            CenterPrice = levelPrice,
                            FirstBarIndex = fp.BarIndex,
                            LastBarIndex = fp.BarIndex,
                            PriceMin = levelPrice - tolerancePrice,
                            PriceMax = levelPrice + tolerancePrice
                        };

                        if (delta > 0)
                        {
                            newZone.TotalBuyBubbles = 1;
                            newZone.TotalBuyVolume = level.BuyCount;
                        }
                        else
                        {
                            newZone.TotalSellBubbles = 1;
                            newZone.TotalSellVolume = level.SellCount;
                        }

                        clusterZones.Add(newZone);

                        // All new zones start as virgin
                        if (EnableVirginCluster)
                            virginClusters.Add(newZone.ZoneId);
                    }
                }
            }

            // Finalize dominance for each zone
            foreach (var zone in clusterZones)
            {
                int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                if (total == 0) continue;

                double buyPct = (double)zone.TotalBuyBubbles / total * 100.0;
                zone.BuyPercent = buyPct;

                if (buyPct >= ClusterDominanceThreshold)
                    zone.Dominance = ClusterDominance.BuyDominated;
                else if ((100.0 - buyPct) >= ClusterDominanceThreshold)
                    zone.Dominance = ClusterDominance.SellDominated;
                else
                    zone.Dominance = ClusterDominance.Consolidated;
            }

            // Draw cluster zones on chart
            if (ShowClusterZones)
                DrawAllClusterZones();

            Print($"✓ Built {clusterZones.Count} cluster zones from history");
        }

        /// <summary>
        /// Checks if current price is touching a virgin cluster zone.
        /// Returns zone if found and marks it as tested.
        /// </summary>
        private ClusterZone FindVirginClusterAtPrice(double currentPrice)
        {
            foreach (var zone in clusterZones)
            {
                if (!virginClusters.Contains(zone.ZoneId))
                    continue;

                if (currentPrice >= zone.PriceMin && currentPrice <= zone.PriceMax)
                {
                    virginClusters.Remove(zone.ZoneId);
                    testedClusters.Add(zone.ZoneId);
                    zone.IsVirgin = false;
                    Print($"🔮 Virgin Cluster touched! Zone: {zone.CenterPrice:F5} | Dominance: {zone.Dominance}");
                    return zone;
                }
            }
            return null;
        }

        /// <summary>
        /// Finds the next cluster zone above or below current price.
        /// Used for Cluster-to-Cluster TP targeting.
        /// </summary>
        private double FindNextClusterTP(double currentPrice, TradeType direction)
        {
            if (!EnableClusterTP) return 0;

            ClusterZone nextZone = null;
            double bestDistance = double.MaxValue;

            foreach (var zone in clusterZones)
            {
                int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                if (total < MinBubblesInCluster) continue;

                double distance = zone.CenterPrice - currentPrice;

                if (direction == TradeType.Buy && distance > ClusterTolerancePips * Symbol.PipSize)
                {
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        nextZone = zone;
                    }
                }
                else if (direction == TradeType.Sell && distance < -ClusterTolerancePips * Symbol.PipSize)
                {
                    double absDist = Math.Abs(distance);
                    if (absDist < bestDistance)
                    {
                        bestDistance = absDist;
                        nextZone = zone;
                    }
                }
            }

            if (nextZone != null)
            {
                double tpPips = bestDistance / Symbol.PipSize;
                Print($"🎯 Cluster-to-Cluster TP: {nextZone.CenterPrice:F5} = {tpPips:F1} pips");
                return tpPips;
            }

            return 0;
        }

        /// <summary>
        /// Detects Failed Auction: high volume cluster but price goes nowhere.
        /// Indicates exhaustion of dominant side.
        /// </summary>
        private bool IsFailedAuction(CandleFootprint footprint, ClusterZoneResult clusterResult)
        {
            if (!EnableFailedAuction) return false;
            if (!clusterResult.IsValid) return false;
            if (clusterResult.Dominance == ClusterDominance.Consolidated) return false;

            // Failed auction: high bubble count but candle body is small
            double open = Bars.OpenPrices[footprint.BarIndex];
            double close = Bars.ClosePrices[footprint.BarIndex];
            double high = Bars.HighPrices[footprint.BarIndex];
            double low = Bars.LowPrices[footprint.BarIndex];

            double bodySize = Math.Abs(close - open);
            double candleRange = high - low;

            if (candleRange < Symbol.PipSize) return false;

            double bodyRatio = bodySize / candleRange;

            // Small body (< 30% of range) with high volume = no follow through = Failed Auction
            bool isFailedAuction = bodyRatio < 0.30 && clusterResult.TotalBuyBubbles + clusterResult.TotalSellBubbles >= MinBubblesInCluster;

            if (isFailedAuction)
                Print($"⚠️ Failed Auction detected! Body ratio: {bodyRatio:F2} | Cluster bubbles: {clusterResult.TotalBuyBubbles + clusterResult.TotalSellBubbles}");

            return isFailedAuction;
        }

        /// <summary>
        /// Check if current time is within the valid intra-candle entry window.
        /// For TF5M: valid window = menit 2-4 (40%-80% of candle duration)
        /// </summary>
        private bool IsWithinIntraCandleWindow(int barIndex)
        {
            if (!EnableIntraCandleTimer) return true;

            DateTime barOpen = Bars.OpenTimes[barIndex];
            DateTime now = Server.Time;

            TimeSpan barDuration = GetBarDuration();
            TimeSpan elapsed = now - barOpen;

            double elapsedPct = elapsed.TotalSeconds / barDuration.TotalSeconds * 100.0;

            bool inWindow = elapsedPct >= EntryWindowStartPct && elapsedPct <= EntryWindowEndPct;

            if (!inWindow)
                Print($"⏱️ Outside intra-candle window ({elapsedPct:F0}% | Valid: {EntryWindowStartPct}%-{EntryWindowEndPct}%)");

            return inWindow;
        }

        private void CheckIntraCandleWindow(int barIndex)
        {
            // This is called on each tick when there's a pending signal
            // The actual execution window check happens in ExecutePendingSignal
        }

        private TimeSpan GetBarDuration()
        {
            if (Bars.Count >= 2)
                return Bars.OpenTimes[Bars.Count - 1] - Bars.OpenTimes[Bars.Count - 2];

            // Fallback based on timeframe name
            string tf = TimeFrame.ToString();
            if (tf.Contains("5")) return TimeSpan.FromMinutes(5);
            if (tf.Contains("15")) return TimeSpan.FromMinutes(15);
            if (tf.Contains("60") || tf.Contains("Hour")) return TimeSpan.FromHours(1);
            return TimeSpan.FromMinutes(1);
        }

        // ═══════════════════════════════════════
        //  SIGNAL ANALYSIS (UPGRADED)
        // ═══════════════════════════════════════

        private void CheckCompletedCandles()
        {
            int currentBarIndex = Bars.Count - 1;

            var completedCandles = candleFootprints
                .Where(kvp => kvp.Key < currentBarIndex && !kvp.Value.IsFinalized)
                .ToList();

            foreach (var kvp in completedCandles)
                FinalizeCandle(kvp.Value);
        }

        private void FinalizeAllCandles()
        {
            var allCandles = candleFootprints
                .Where(kvp => !kvp.Value.IsFinalized)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            Print($"Finalizing {allCandles.Count} candles...");

            foreach (var kvp in allCandles)
            {
                var footprint = kvp.Value;
                footprint.IsFinalized = true;

                int buyBubblesCount = 0;
                int sellBubblesCount = 0;

                foreach (var levelKvp in footprint.PriceLevels)
                {
                    PriceLevel level = levelKvp.Value;
                    int delta = level.BuyCount - level.SellCount;
                    int absDelta = Math.Abs(delta);

                    if (absDelta >= MinDeltaPerLevel && level.TotalCount >= MinVolumePerLevel)
                    {
                        if (delta > 0) buyBubblesCount++;
                        else sellBubblesCount++;
                    }
                }

                footprint.BuyBubblesCount = buyBubblesCount;
                footprint.SellBubblesCount = sellBubblesCount;

                string barTimeKey = footprint.BarTime.ToString("yyyyMMddHHmmss");
                if (!signalsAnalyzed.Contains(barTimeKey))
                {
                    AnalyzeEntrySignals(footprint, isHistorical: true);
                    signalsAnalyzed.Add(barTimeKey);
                }
            }

            Print("✓ Candles finalized (historical — no trades)");
        }

        private void FinalizeCandle(CandleFootprint footprint)
        {
            footprint.IsFinalized = true;

            if (footprint.TotalTicks < 10)
                return;

            int buyBubblesCount = 0;
            int sellBubblesCount = 0;

            foreach (var levelKvp in footprint.PriceLevels)
            {
                PriceLevel level = levelKvp.Value;
                int delta = level.BuyCount - level.SellCount;
                int absDelta = Math.Abs(delta);
                bool isBuyPressure = delta > 0;

                if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                    continue;

                if (ShowBubbles)
                    DrawFootprintBubble(footprint.BarIndex, level, delta, isBuyPressure);

                if (isBuyPressure) buyBubblesCount++;
                else sellBubblesCount++;

                bubblesDrawn++;
                if (absDelta > maxDeltaPerLevel) maxDeltaPerLevel = absDelta;
                if (level.TotalCount > maxVolumePerLevel) maxVolumePerLevel = level.TotalCount;
            }

            footprint.BuyBubblesCount = buyBubblesCount;
            footprint.SellBubblesCount = sellBubblesCount;

            if (ShowBubbles)
                DrawBubbleCounter(footprint.BarIndex, buyBubblesCount, sellBubblesCount);

            // Update cluster zones with new candle data
            UpdateClusterZonesWithNewCandle(footprint);

            string barTimeKey = footprint.BarTime.ToString("yyyyMMddHHmmss");
            if (!signalsAnalyzed.Contains(barTimeKey))
            {
                AnalyzeEntrySignals(footprint, isHistorical: false);
                signalsAnalyzed.Add(barTimeKey);
            }
        }

        private void UpdateClusterZonesWithNewCandle(CandleFootprint footprint)
        {
            double tolerancePrice = ClusterTolerancePips * Symbol.PipSize;

            foreach (var levelKvp in footprint.PriceLevels)
            {
                double levelPrice = levelKvp.Key;
                PriceLevel level = levelKvp.Value;

                int delta = level.BuyCount - level.SellCount;
                int absDelta = Math.Abs(delta);

                if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                    continue;

                bool addedToExisting = false;
                foreach (var zone in clusterZones)
                {
                    if (Math.Abs(levelPrice - zone.CenterPrice) <= tolerancePrice)
                    {
                        if (delta > 0) { zone.TotalBuyBubbles++; zone.TotalBuyVolume += level.BuyCount; }
                        else { zone.TotalSellBubbles++; zone.TotalSellVolume += level.SellCount; }
                        zone.LastBarIndex = footprint.BarIndex;

                        // Recalculate dominance
                        int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                        double buyPct = total > 0 ? (double)zone.TotalBuyBubbles / total * 100.0 : 0;
                        zone.BuyPercent = buyPct;

                        if (buyPct >= ClusterDominanceThreshold) zone.Dominance = ClusterDominance.BuyDominated;
                        else if ((100.0 - buyPct) >= ClusterDominanceThreshold) zone.Dominance = ClusterDominance.SellDominated;
                        else zone.Dominance = ClusterDominance.Consolidated;

                        addedToExisting = true;
                        break;
                    }
                }

                if (!addedToExisting)
                {
                    var newZone = new ClusterZone
                    {
                        ZoneId = $"CZ_{footprint.BarIndex}_{levelPrice:F5}",
                        CenterPrice = levelPrice,
                        FirstBarIndex = footprint.BarIndex,
                        LastBarIndex = footprint.BarIndex,
                        PriceMin = levelPrice - tolerancePrice,
                        PriceMax = levelPrice + tolerancePrice,
                        IsVirgin = true
                    };

                    if (delta > 0) { newZone.TotalBuyBubbles = 1; newZone.TotalBuyVolume = level.BuyCount; }
                    else { newZone.TotalSellBubbles = 1; newZone.TotalSellVolume = level.SellCount; }

                    newZone.Dominance = delta > 0 ? ClusterDominance.BuyDominated : ClusterDominance.SellDominated;

                    clusterZones.Add(newZone);
                    if (EnableVirginCluster) virginClusters.Add(newZone.ZoneId);
                }
            }
        }

        /// <summary>
        /// UPGRADED: AnalyzeEntrySignals now incorporates:
        /// 1. Cluster Zone confirmation (horizontal bubble alignment)
        /// 2. Virgin Cluster boost
        /// 3. Failed Auction detection
        /// 4. Fixed Climax direction (netDelta > 0 or < 0 only, not >= 0)
        /// 5. Fixed Absorption direction (reversal logic)
        /// 6. Cluster-to-Cluster TP calculation
        /// </summary>
        private void AnalyzeEntrySignals(CandleFootprint footprint, bool isHistorical)
        {
            int totalBubbles = footprint.BuyBubblesCount + footprint.SellBubblesCount;
            int netDelta = footprint.BuyBubblesCount - footprint.SellBubblesCount;
            int absDelta = Math.Abs(netDelta);

            if (totalBubbles < MinBubblesForSignal)
                return;

            double avgBubbles = GetAverageBubbleCount(footprint.BarIndex, 10);
            if (avgBubbles < 2) avgBubbles = 2;

            double spikeThreshold = avgBubbles * VolumeSpikeMultiplier;

            // ── Analyze cluster zone at current price level ──
            double currentPrice = Bars.ClosePrices[footprint.BarIndex];
            ClusterZoneResult clusterResult = new ClusterZoneResult { IsValid = false };

            if (EnableClusterConfirmation)
                clusterResult = AnalyzeClusterAtPrice(currentPrice, footprint.BarIndex);

            // Check for virgin cluster
            ClusterZone virginZone = null;
            if (EnableVirginCluster && !isHistorical)
                virginZone = FindVirginClusterAtPrice(currentPrice);

            // Check for failed auction
            bool isFailedAuction = IsFailedAuction(footprint, clusterResult);

            string signal = "";
            string signalColorName = "";
            TradeType direction = TradeType.Buy;
            string signalType = "";
            bool clusterConfirmed = false;

            // ─────────────────────────────────────────
            // 1. VOLUME SPIKE + CLUSTER CONFIRMATION
            // ─────────────────────────────────────────
            if (EnableVolumeSpikeSignal && totalBubbles >= spikeThreshold)
            {
                if (netDelta >= 2)
                {
                    direction = TradeType.Buy;
                    signalType = "SPIKE_BUY";

                    // Confirm with cluster zone
                    if (clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.BuyDominated)
                    {
                        signal = "▲▲";
                        signalColorName = "Cyan";
                        clusterConfirmed = true;
                        Print($"✅ SPIKE_BUY confirmed by cluster! Buy%={clusterResult.BuyPercent:F0}%");
                    }
                    else if (!clusterResult.IsValid || clusterResult.Dominance != ClusterDominance.SellDominated)
                    {
                        signal = "▲▲";
                        signalColorName = "Cyan";
                    }
                    // If cluster says SELL dominated — skip spike buy signal
                }
                else if (netDelta <= -2)
                {
                    direction = TradeType.Sell;
                    signalType = "SPIKE_SELL";

                    if (clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.SellDominated)
                    {
                        signal = "▼▼";
                        signalColorName = "Magenta";
                        clusterConfirmed = true;
                        Print($"✅ SPIKE_SELL confirmed by cluster! Sell%={clusterResult.SellPercent:F0}%");
                    }
                    else if (!clusterResult.IsValid || clusterResult.Dominance != ClusterDominance.BuyDominated)
                    {
                        signal = "▼▼";
                        signalColorName = "Magenta";
                    }
                }
            }

            // ─────────────────────────────────────────
            // 2. ABSORPTION — FIXED: true reversal logic
            // ─────────────────────────────────────────
            if (string.IsNullOrEmpty(signal) && EnableAbsorptionSignal && totalBubbles >= 4 && absDelta <= 1)
            {
                double open = Bars.OpenPrices[footprint.BarIndex];
                double close = Bars.ClosePrices[footprint.BarIndex];

                // FIXED: Absorption means price was DOMINATED by one side but got absorbed
                // Reversal = OPPOSITE of candle direction
                if (close > open)
                {
                    // Bullish candle but buy volume absorbed by sellers = potential reversal DOWN
                    signal = "◆";
                    signalColorName = "Yellow";
                    direction = TradeType.Sell;  // FIXED: reversal from bullish
                    signalType = "ABSORB_SELL";
                }
                else
                {
                    // Bearish candle but sell volume absorbed by buyers = potential reversal UP
                    signal = "◆";
                    signalColorName = "Yellow";
                    direction = TradeType.Buy;   // FIXED: reversal from bearish
                    signalType = "ABSORB_BUY";
                }

                // Cluster confirmation for absorption
                if (clusterResult.IsValid && clusterResult.Dominance != ClusterDominance.Consolidated)
                {
                    if (clusterResult.Direction == direction)
                        clusterConfirmed = true;
                    else
                    {
                        // Cluster opposes absorption direction — cancel signal
                        signal = "";
                        Print($"⚫ Absorption cancelled — cluster opposes direction");
                    }
                }
            }

            // ─────────────────────────────────────────
            // 3. CLIMAX — FIXED: strict netDelta check
            // ─────────────────────────────────────────
            if (string.IsNullOrEmpty(signal) && EnableClimaxSignal && totalBubbles >= 6)
            {
                int prevBubbles = GetPreviousBubbleCount(footprint.BarIndex);
                if (prevBubbles >= 5)
                {
                    // FIXED: netDelta > 0 (strictly positive) = buy climax = sell reversal
                    if (netDelta > 0)
                    {
                        signal = "TOP";
                        signalColorName = "Lime";
                        direction = TradeType.Sell;
                        signalType = "CLIMAX_TOP";

                        // Climax at virgin cluster = very strong signal
                        if (virginZone != null && virginZone.Dominance == ClusterDominance.SellDominated)
                        {
                            signalColorName = "Orange";
                            signalType = "CLIMAX_TOP_VIRGIN";
                            Print($"🔮 CLIMAX TOP at Virgin Sell Cluster! Extra strong signal.");
                        }
                    }
                    // FIXED: netDelta < 0 (strictly negative) = sell climax = buy reversal
                    else if (netDelta < 0)
                    {
                        signal = "BOT";
                        signalColorName = "Orange";
                        direction = TradeType.Buy;
                        signalType = "CLIMAX_BOT";

                        if (virginZone != null && virginZone.Dominance == ClusterDominance.BuyDominated)
                        {
                            signalColorName = "Lime";
                            signalType = "CLIMAX_BOT_VIRGIN";
                            Print($"🔮 CLIMAX BOT at Virgin Buy Cluster! Extra strong signal.");
                        }
                    }
                    // netDelta == 0 → truly ambiguous → SKIP (fixed bug)
                }
            }

            // ─────────────────────────────────────────
            // 4. FAILED AUCTION — NEW signal type
            // ─────────────────────────────────────────
            if (string.IsNullOrEmpty(signal) && isFailedAuction && clusterResult.IsValid)
            {
                // Failed auction: trade opposite to cluster dominance
                if (clusterResult.Dominance == ClusterDominance.BuyDominated)
                {
                    signal = "FA↓";
                    signalColorName = "Orange";
                    direction = TradeType.Sell;
                    signalType = "FAILED_AUCTION_SELL";
                    Print($"🔴 Failed Auction: Buy cluster failed → SELL");
                }
                else if (clusterResult.Dominance == ClusterDominance.SellDominated)
                {
                    signal = "FA↑";
                    signalColorName = "Lime";
                    direction = TradeType.Buy;
                    signalType = "FAILED_AUCTION_BUY";
                    Print($"🟢 Failed Auction: Sell cluster failed → BUY");
                }
            }

            // ─────────────────────────────────────────
            // 5. VIRGIN CLUSTER TOUCH — NEW signal type
            // ─────────────────────────────────────────
            if (string.IsNullOrEmpty(signal) && virginZone != null && virginZone.Dominance != ClusterDominance.Consolidated)
            {
                direction = virginZone.Dominance == ClusterDominance.BuyDominated ? TradeType.Buy : TradeType.Sell;
                signal = virginZone.Dominance == ClusterDominance.BuyDominated ? "V↑" : "V↓";
                signalColorName = "White";
                signalType = direction == TradeType.Buy ? "VIRGIN_BUY" : "VIRGIN_SELL";
                clusterConfirmed = true;
                Print($"🔮 Virgin Cluster signal! {virginZone.Dominance} | Buy%={virginZone.BuyPercent:F0}%");
            }

            // ─────────────────────────────────────────
            // 6. SIMPLE IMBALANCE + CLUSTER FILTER
            // ─────────────────────────────────────────
            if (string.IsNullOrEmpty(signal) && EnableSimpleImbalance && totalBubbles >= 3)
            {
                if (netDelta >= 2)
                {
                    direction = TradeType.Buy;
                    signalType = "IMBAL_BUY";

                    // Require cluster confirmation for simple imbalance
                    if (!clusterResult.IsValid || clusterResult.Dominance == ClusterDominance.BuyDominated)
                    {
                        signal = "[B]";
                        signalColorName = "LightGreen";
                        if (clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.BuyDominated)
                            clusterConfirmed = true;
                    }
                    // Skip if cluster says SELL
                }
                else if (netDelta <= -2)
                {
                    direction = TradeType.Sell;
                    signalType = "IMBAL_SELL";

                    if (!clusterResult.IsValid || clusterResult.Dominance == ClusterDominance.SellDominated)
                    {
                        signal = "[S]";
                        signalColorName = "LightCoral";
                        if (clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.SellDominated)
                            clusterConfirmed = true;
                    }
                }
            }

            // ─────────────────────────────────────────
            // 7. LIQUIDITY VACUUM / SPOOFING IMPULSE
            // ─────────────────────────────────────────
            if (string.IsNullOrEmpty(signal) && EnableVacuumDetection)
            {
                int vacuumCount = footprint.PriceLevels.Values.Count(l => l.IsVacuumSpoof);
                if (vacuumCount >= 1)
                {
                    double open = Bars.OpenPrices[footprint.BarIndex];
                    double close = Bars.ClosePrices[footprint.BarIndex];

                    if (close > open)
                    {
                        signal = "VAC↑";
                        signalColorName = "Yellow";
                        direction = TradeType.Buy;
                        signalType = "VACUUM_BUY";
                        clusterConfirmed = true;
                        Print($"⚡ LIQUIDITY VACUUM / SPOOFING BUY: Price swept {vacuumCount} thin levels!");
                    }
                    else if (close < open)
                    {
                        signal = "VAC↓";
                        signalColorName = "Yellow";
                        direction = TradeType.Sell;
                        signalType = "VACUUM_SELL";
                        clusterConfirmed = true;
                        Print($"⚡ LIQUIDITY VACUUM / SPOOFING SELL: Price swept {vacuumCount} thin levels!");
                    }
                }
            }

            // ─────────────────────────────────────────
            // SKIP: Consolidated zone (perang volume)
            // ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(signal) && clusterResult.IsValid && clusterResult.Dominance == ClusterDominance.Consolidated)
            {
                // Only skip if this is a weaker signal type (imbalance, absorption)
                if (signalType.StartsWith("IMBAL") || signalType.StartsWith("ABSORB"))
                {
                    Print($"⚫ Signal {signalType} skipped — cluster is CONSOLIDATED (perang volume)");
                    signal = "";
                }
            }

            // ─────────────────────────────────────────
            // FINAL: Register signal if valid
            // ─────────────────────────────────────────
            if (!string.IsNullOrEmpty(signal))
            {
                footprint.SignalText = signal;
                footprint.SignalColor = signalColorName;
                footprint.HasSignal = true;
                footprint.SignalTotalBubbles = totalBubbles;
                footprint.SignalNetDelta = netDelta;
                footprint.ClusterConfirmed = clusterConfirmed;
                totalSignalsGenerated++;

                if (ShowBubbles)
                    DrawEntrySignalFromFootprint(footprint);

                if (!isHistorical)
                {
                    string clusterTag = clusterConfirmed ? " [CLUSTER✓]" : "";
                    string virginTag = virginZone != null ? " [VIRGIN🔮]" : "";
                    string auctionTag = isFailedAuction ? " [FAILED_AUCTION]" : "";
                    Print($"🔔 SIGNAL: {signalType}{clusterTag}{virginTag}{auctionTag} | Bubbles={totalBubbles} NetΔ={netDelta} | Bar {footprint.BarTime:HH:mm:ss}");

                    if (clusterResult.IsValid)
                        Print($"   Cluster: Buy%={clusterResult.BuyPercent:F0}% | Sell%={clusterResult.SellPercent:F0}% | Dominance={clusterResult.Dominance}");

                    pendingSignalType = signalType;
                    pendingSignalDirection = direction;
                    hasPendingSignal = true;
                    pendingSignalBarIndex = footprint.BarIndex;

                    // Pre-calculate cluster TP
                    pendingClusterTPPips = FindNextClusterTP(currentPrice, direction);
                }
            }
        }

        // ═══════════════════════════════════════
        //  TRADE EXECUTION WITH RISK MANAGEMENT
        // ═══════════════════════════════════════

        private void ExecutePendingSignal()
        {
            hasPendingSignal = false;

            // 1. Daily loss limit
            if (IsDailyLossLimitHit())
            {
                Print("🚫 Daily loss limit reached — trade skipped");
                return;
            }

            // 2. Max trades per day
            if (dailyTradeCount >= MaxTradesPerDay)
            {
                Print($"🚫 Max trades/day ({MaxTradesPerDay}) reached — trade skipped");
                return;
            }

            // 3. Max concurrent positions
            var openPositions = Positions.FindAll(BotLabel, SymbolName);
            if (openPositions.Length >= MaxPositions)
            {
                Print($"🚫 Max positions ({MaxPositions}) reached — trade skipped");
                return;
            }

            // 4. Spread filter
            double spreadPips = Symbol.Spread / Symbol.PipSize;
            if (spreadPips > MaxSpreadPips)
            {
                Print($"🚫 Spread too wide ({spreadPips:F1} > {MaxSpreadPips:F1} pips) — trade skipped");
                return;
            }

            // 5. Session filter
            if (EnableSessionFilter)
            {
                int hour = Server.Time.Hour;
                if (SessionStartHour < SessionEndHour)
                {
                    if (hour < SessionStartHour || hour >= SessionEndHour)
                    {
                        Print($"🚫 Outside trading session — trade skipped");
                        return;
                    }
                }
                else
                {
                    if (hour < SessionStartHour && hour >= SessionEndHour)
                    {
                        Print($"🚫 Outside trading session — trade skipped");
                        return;
                    }
                }
            }

            // 6. Cooldown
            int currentBar = Bars.Count - 1;
            if (currentBar - lastTradeBarIndex < CooldownBars)
            {
                Print($"🚫 Cooldown active ({CooldownBars} bars) — trade skipped");
                return;
            }

            // 7. Intra-candle timer check
            if (!IsWithinIntraCandleWindow(currentBar))
            {
                Print($"🚫 Intra-candle timer: outside entry window — trade skipped");
                return;
            }

            // 8. Avoid duplicate direction
            foreach (var pos in openPositions)
            {
                if (pos.TradeType == pendingSignalDirection)
                {
                    Print($"🚫 Already have {pendingSignalDirection} position open — trade skipped");
                    return;
                }
            }

            // ── Calculate SL/TP ──
            double slPips, tpPips;

            if (StopMode == SlTpMode.ATRBased)
            {
                double atrValue = atr.Result.LastValue;
                double atrPips = atrValue / Symbol.PipSize;
                slPips = Math.Round(atrPips * SlAtrMultiplier, 1);
                tpPips = Math.Round(atrPips * TpAtrMultiplier, 1);
                slPips = Math.Max(slPips, 3);
                tpPips = Math.Max(tpPips, 3);
                Print($"📐 ATR = {atrValue:F5} ({atrPips:F1} pips) → SL={slPips:F1} TP={tpPips:F1}");
            }
            else
            {
                slPips = FixedSlPips;
                tpPips = FixedTpPips;
            }

            // Override TP with Cluster-to-Cluster if available and larger
            if (EnableClusterTP && pendingClusterTPPips > 0)
            {
                double clusterTPAdjusted = pendingClusterTPPips * 0.9; // 10% buffer before cluster
                if (clusterTPAdjusted > tpPips)
                {
                    Print($"🎯 Cluster-to-Cluster TP override: {tpPips:F1} → {clusterTPAdjusted:F1} pips");
                    tpPips = clusterTPAdjusted;
                }
            }

            // ── Calculate Position Size ──
            double volume;

            if (SizeMode == PositionSizeMode.RiskPercent)
            {
                double riskAmount = Account.Balance * (RiskPercent / 100.0);
                double pipValue = Symbol.PipValue;

                if (pipValue <= 0)
                {
                    Print("⚠️ Cannot calculate pip value — using fixed lots");
                    volume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(FixedLots));
                }
                else
                {
                    double calculatedVolume = riskAmount / (slPips * pipValue);
                    volume = Symbol.NormalizeVolumeInUnits(calculatedVolume, RoundingMode.Down);
                }

                Print($"💰 Risk: {RiskPercent}% of ${Account.Balance:F2} → Volume: {volume}");
            }
            else
            {
                volume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(FixedLots));
            }

            if (volume < Symbol.VolumeInUnitsMin)
                volume = Symbol.VolumeInUnitsMin;

            if (volume > Symbol.VolumeInUnitsMax)
                volume = Symbol.VolumeInUnitsMax;

            // ── Execute Trade ──
            string comment = $"{pendingSignalType}|{DateTime.UtcNow:HHmmss}";

            var result = ExecuteMarketOrder(
                pendingSignalDirection,
                SymbolName,
                volume,
                BotLabel,
                slPips,
                tpPips,
                comment
            );

            if (result.IsSuccessful)
            {
                totalTradesOpened++;
                dailyTradeCount++;
                lastTradeBarIndex = currentBar;

                string dir = pendingSignalDirection == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                Print($"✅ {dir} executed | {pendingSignalType} | Vol: {volume} | SL: {slPips} | TP: {tpPips}");

                DrawTradeMarker(currentBar, pendingSignalDirection);
            }
            else
            {
                Print($"❌ Order failed: {result.Error}");
            }

            pendingClusterTPPips = 0;
        }

        // ═══════════════════════════════════════
        //  RISK MANAGEMENT
        // ═══════════════════════════════════════

        private bool IsDailyLossLimitHit()
        {
            double currentEquity = Account.Equity;
            double dailyLoss = dailyStartBalance - currentEquity;
            double maxLoss = dailyStartBalance * (MaxDailyLossPercent / 100.0);
            return dailyLoss >= maxLoss;
        }

        private void ResetDailyCountersIfNeeded()
        {
            if (Server.Time.Date != lastTradeDay)
            {
                Print($"📅 New trading day — resetting counters (prev trades: {dailyTradeCount})");
                dailyStartBalance = Account.Balance;
                dailyTradeCount = 0;
                lastTradeDay = Server.Time.Date;
            }
        }

        private void ManageTrailingStops()
        {
            var positions = Positions.FindAll(BotLabel, SymbolName);

            foreach (var position in positions)
            {
                if (position.Pips < TrailingTriggerPips)
                    continue;

                double newSlPrice;

                if (position.TradeType == TradeType.Buy)
                {
                    newSlPrice = Symbol.Bid - TrailingDistancePips * Symbol.PipSize;
                    if (position.StopLoss == null || newSlPrice > position.StopLoss.Value)
                        ModifyPosition(position, newSlPrice, position.TakeProfit);
                }
                else
                {
                    newSlPrice = Symbol.Ask + TrailingDistancePips * Symbol.PipSize;
                    if (position.StopLoss == null || newSlPrice < position.StopLoss.Value)
                        ModifyPosition(position, newSlPrice, position.TakeProfit);
                }
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos.Label != BotLabel || pos.SymbolName != SymbolName) return;

            if (pos.NetProfit >= 0)
            {
                totalTradesWon++;
                Print($"✅ Trade WON: +${pos.NetProfit:F2} ({pos.Pips:F1} pips) | {pos.Comment}");
            }
            else
            {
                totalTradesLost++;
                Print($"❌ Trade LOST: -${Math.Abs(pos.NetProfit):F2} ({pos.Pips:F1} pips) | {pos.Comment}");
            }
        }

        // ═══════════════════════════════════════
        //  DRAWING
        // ═══════════════════════════════════════

        private void DrawAllClusterZones()
        {
            int drawnZones = 0;
            foreach (var zone in clusterZones)
            {
                int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                if (total < MinBubblesInCluster) continue;

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
                try
                {
                    var rect = Chart.DrawRectangle(
                        rectName,
                        zone.FirstBarIndex,
                        zone.PriceMax,
                        Math.Min(zone.LastBarIndex + 5, Bars.Count - 1),
                        zone.PriceMin,
                        zoneColor
                    );
                    if (rect != null)
                    {
                        rect.IsFilled = true;
                        rect.Thickness = 1;
                    }
                    drawnZones++;
                }
                catch { }
            }

            Print($"✓ Drew {drawnZones} cluster zones on chart");
        }

        private void DrawTradeMarker(int barIndex, TradeType tradeType)
        {
            string name = $"Trade_{barIndex}_{DateTime.UtcNow.Ticks}";
            double price;
            Color color;
            string text;

            if (tradeType == TradeType.Buy)
            {
                price = Bars.LowPrices[barIndex] - Symbol.PipSize * 5;
                color = Color.LimeGreen;
                text = "▲ BUY";
            }
            else
            {
                price = Bars.HighPrices[barIndex] + Symbol.PipSize * 5;
                color = Color.OrangeRed;
                text = "▼ SELL";
            }

            var label = Chart.DrawText(name, text, barIndex, price, color);
            if (label != null)
            {
                label.FontSize = 10;
                label.IsBold = true;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = tradeType == TradeType.Buy ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            }
        }

        private void DrawCurrentCandleBubbles(CandleFootprint footprint)
        {
            string prefix = $"Footprint_{footprint.BarIndex}_";
            string counterName = $"BubbleCount_{footprint.BarIndex}";

            var objectsToRemove = Chart.Objects
                .Where(obj => obj.Name.StartsWith(prefix) || obj.Name == counterName)
                .ToList();

            foreach (var obj in objectsToRemove)
                Chart.RemoveObject(obj.Name);

            int buyBubblesCount = 0;
            int sellBubblesCount = 0;

            foreach (var levelKvp in footprint.PriceLevels)
            {
                PriceLevel level = levelKvp.Value;
                int delta = level.BuyCount - level.SellCount;
                int absDelta = Math.Abs(delta);
                bool isBuyPressure = delta > 0;

                if (absDelta >= MinDeltaPerLevel && level.TotalCount >= MinVolumePerLevel)
                {
                    DrawFootprintBubble(footprint.BarIndex, level, delta, isBuyPressure);
                    if (isBuyPressure) buyBubblesCount++;
                    else sellBubblesCount++;
                }
            }

            footprint.BuyBubblesCount = buyBubblesCount;
            footprint.SellBubblesCount = sellBubblesCount;

            DrawBubbleCounter(footprint.BarIndex, buyBubblesCount, sellBubblesCount);
        }

        private void DrawFootprintBubble(int barIndex, PriceLevel level, int delta, bool isBuy)
        {
            // Absolute Delta: |V_buy - V_sell|
            int absDelta = Math.Abs(level.BuyCount - level.SellCount);

            Color baseColor;
            if (level.IsVacuumSpoof)
                baseColor = GetColorFromString(VacuumBubbleColor);
            else
                baseColor = isBuy ? GetColorFromString(BuyBubbleColor) : GetColorFromString(SellBubbleColor);

            Color color = Color.FromArgb(BubbleOpacity, baseColor.R, baseColor.G, baseColor.B);

            double candleHigh = Bars.HighPrices[barIndex];
            double candleLow = Bars.LowPrices[barIndex];

            double priceStep = Symbol.PipSize * (PipStep > 0 ? PipStep : 1.0);
            double bubbleCenterY = level.Price + (priceStep / 2.0);

            // Strict Boundary Check: Skip if bubble level is completely outside candle High/Low range
            if (level.Price > candleHigh + (priceStep * 0.5) || level.Price < candleLow - (priceStep * 0.5))
                return;

            // Clamp center Y strictly inside candle range
            bubbleCenterY = Math.Min(candleHigh, Math.Max(candleLow, bubbleCenterY));

            // Smooth Non-Linear Tanh Scaling based on Absolute Delta
            // Baseline reference is 2x MinDeltaPerLevel
            double baselineDelta = Math.Max(1.0, MinDeltaPerLevel * 2.0);
            double deltaRatio = absDelta / baselineDelta;
            
            // Tanh scaling smoothly maps small to high delta without exploding in size
            double sizeScale = 0.50 + 0.80 * Math.Tanh(deltaRatio * 0.5);
            sizeScale *= BubbleSizeMultiplier;

            // Vertical Radius: Scaled relative to PipStep height so bubble stays on its price level
            double verticalRadius = (priceStep / 2.0) * Math.Min(1.25, Math.Max(0.55, sizeScale));
            double topY = Math.Min(candleHigh, bubbleCenterY + verticalRadius);
            double bottomY = Math.Max(candleLow, bubbleCenterY - verticalRadius);

            DateTime barTime = Bars.OpenTimes[barIndex];
            TimeSpan barDuration = barIndex > 0
                ? Bars.OpenTimes[barIndex] - Bars.OpenTimes[barIndex - 1]
                : TimeSpan.FromMinutes(1);

            // Horizontal Width: Scaled safely within candle duration
            double widthFraction = Math.Min(0.40, 0.12 + (sizeScale * 0.15));
            TimeSpan bubbleWidthTime = TimeSpan.FromTicks((long)(barDuration.Ticks * widthFraction));

            string name = $"Footprint_{barIndex}_{level.Price.ToString("F5").Replace(".", "_")}";

            try
            {
                var ellipse = Chart.DrawEllipse(name + "_bubble",
                    barTime.Subtract(bubbleWidthTime), topY,
                    barTime.Add(bubbleWidthTime), bottomY,
                    color);

                if (ellipse != null)
                {
                    ellipse.IsFilled = true;
                    ellipse.Thickness = 1;
                }
            }
            catch { }

            if (ShowDeltaLabels && absDelta >= 3)
            {
                string labelTextStr = level.IsVacuumSpoof ? "VAC" : $"{delta:+#;-#;0}";
                var text = Chart.DrawText(name + "_label", labelTextStr, barIndex, bubbleCenterY, Color.White);
                if (text != null)
                {
                    text.FontSize = Math.Max(6, Math.Min(9, SignalFontSize));
                    text.HorizontalAlignment = HorizontalAlignment.Center;
                    text.VerticalAlignment = VerticalAlignment.Center;
                    text.IsBold = absDelta >= 10 || level.IsVacuumSpoof;
                }
            }
        }

        private void DrawBubbleCounter(int barIndex, int buyBubbles, int sellBubbles)
        {
            if (buyBubbles == 0 && sellBubbles == 0) return;

            double candleLow = Bars.LowPrices[barIndex];
            double candleRange = Bars.HighPrices[barIndex] - Bars.LowPrices[barIndex];
            double labelOffset = candleRange * 0.3;

            if (Symbol.PipSize >= 0.1) labelOffset = Math.Max(labelOffset, candleRange * 0.2);
            else labelOffset = Math.Max(labelOffset, Symbol.PipSize * 5);

            double labelPrice = candleLow - labelOffset;
            string labelText = $"{buyBubbles} | {sellBubbles}";

            string name = $"BubbleCount_{barIndex}";
            var label = Chart.DrawText(name, labelText, barIndex, labelPrice, Color.White);

            if (label != null)
            {
                label.FontSize = 8;
                label.HorizontalAlignment = HorizontalAlignment.Center;
                label.VerticalAlignment = VerticalAlignment.Top;

                if (buyBubbles > sellBubbles) label.Color = Color.LimeGreen;
                else if (sellBubbles > buyBubbles) label.Color = Color.OrangeRed;
            }
        }

        private void DrawEntrySignalFromFootprint(CandleFootprint footprint)
        {
            if (!footprint.HasSignal || string.IsNullOrEmpty(footprint.SignalText)) return;

            int barIndex = -1;
            for (int i = 0; i < Bars.Count; i++)
            {
                if (Bars.OpenTimes[i] == footprint.BarTime)
                {
                    barIndex = i;
                    break;
                }
            }

            if (barIndex < 0 || barIndex >= Bars.Count) return;
            if (barIndex >= Bars.HighPrices.Count || barIndex >= Bars.LowPrices.Count) return;

            double high = Bars.HighPrices[barIndex];
            double low = Bars.LowPrices[barIndex];
            double candleRange = high - low;

            double minOffset = Symbol.PipSize >= 0.1 ? Symbol.PipSize * 2 : Symbol.PipSize * 3;
            double offset = Math.Max(candleRange * 0.15, minOffset);

            // Cluster-confirmed signals get extra star marker
            string signalText = footprint.ClusterConfirmed
                ? footprint.SignalText + "★"
                : footprint.SignalText;

            string baseName = $"OFSig_{footprint.BarTime:yyyyMMddHHmmss}";
            Color signalColor = GetColorFromString(footprint.SignalColor);

            try
            {
                var toRemove = Chart.Objects.Where(obj => obj.Name == baseName).ToList();
                foreach (var obj in toRemove) Chart.RemoveObject(obj.Name);

                double signalY = high + offset;
                var label = Chart.DrawText(baseName, signalText, barIndex, signalY, signalColor);

                if (label != null)
                {
                    label.FontSize = footprint.ClusterConfirmed ? SignalFontSize + 4 : SignalFontSize + 2;
                    label.IsBold = true;
                    label.HorizontalAlignment = HorizontalAlignment.Center;
                    label.VerticalAlignment = VerticalAlignment.Bottom;
                }
            }
            catch (Exception ex)
            {
                Print($"❌ Error drawing signal at bar {barIndex}: {ex.Message}");
            }
        }

        // ═══════════════════════════════════════
        //  HELPER METHODS
        // ═══════════════════════════════════════

        private void AdjustParametersForTimeframe()
        {
            string tf = TimeFrame.ToString();

            if (tf == "Minute" || tf == "Minute2" || tf == "Minute3")
            {
                Print("⚡ Detected VERY SHORT timeframe — applying aggressive settings");
                if (MinBubblesForSignal > 3) MinBubblesForSignal = 2;
                if (VolumeSpikeMultiplier > 1.5) VolumeSpikeMultiplier = 1.3;
            }
            else if (tf == "Minute5" || tf == "Minute10" || tf == "Minute15")
            {
                Print("⚡ Detected SHORT timeframe — applying relaxed settings");
                if (MinBubblesForSignal > 4) MinBubblesForSignal = 3;
                if (VolumeSpikeMultiplier > 1.6) VolumeSpikeMultiplier = 1.4;
            }
        }

        private double GetAverageBubbleCount(int currentIndex, int lookback)
        {
            int sum = 0;
            int count = 0;

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
            int prevIndex = currentIndex - 1;
            if (prevIndex >= 0 && candleFootprints.ContainsKey(prevIndex))
            {
                var fp = candleFootprints[prevIndex];
                return fp.BuyBubblesCount + fp.SellBubblesCount;
            }
            return 0;
        }

        private double RoundToPip(double price)
        {
            double step = Symbol.PipSize * (PipStep > 0 ? PipStep : 1.0);
            return Math.Floor(price / step) * step;
        }

        private int FindBarIndex(DateTime time)
        {
            if (Bars == null || Bars.Count == 0) return -1;
            if (time < Bars.OpenTimes[0]) return -1; // Discard ticks before available chart history

            int left = 0;
            int right = Bars.Count - 1;
            int bestMatch = -1;

            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (Bars.OpenTimes[mid] <= time)
                {
                    bestMatch = mid;
                    left = mid + 1;
                }
                else
                {
                    right = mid - 1;
                }
            }

            // Ensure the tick time falls strictly before the next bar open time
            if (bestMatch >= 0 && bestMatch < Bars.Count - 1)
            {
                if (time >= Bars.OpenTimes[bestMatch + 1])
                    return -1;
            }

            return bestMatch;
        }

        private Color GetColorFromString(string colorName)
        {
            if (string.IsNullOrEmpty(colorName)) return Color.White;

            switch (colorName.ToLower())
            {
                case "red": return Color.Red;
                case "green": return Color.Green;
                case "lime": return Color.Lime;
                case "blue": return Color.Blue;
                case "yellow": return Color.Yellow;
                case "orange": return Color.Orange;
                case "purple": return Color.Purple;
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

        public enum PositionSizeMode { FixedLots, RiskPercent }
        public enum SlTpMode { ATRBased, FixedPips }
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
            public DateTime LastTime { get; set; }
            public bool IsFinalized { get; set; }
            public int BuyBubblesCount { get; set; }
            public int SellBubblesCount { get; set; }
            public string SignalText { get; set; }
            public string SignalColor { get; set; }
            public bool HasSignal { get; set; }
            public int SignalTotalBubbles { get; set; }
            public int SignalNetDelta { get; set; }
            public bool ClusterConfirmed { get; set; }  // NEW: cluster confirmation flag
        }

        private class PriceLevel
        {
            public double Price { get; set; }
            public int BuyCount { get; set; }
            public int SellCount { get; set; }
            public int TotalCount { get; set; }
            public double PriceImpact { get; set; }
            public bool IsVacuumSpoof { get; set; }
        }

        // NEW: Cluster Zone data structure
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

        // NEW: Result object from cluster analysis
        private class ClusterZoneResult
        {
            public bool IsValid { get; set; }
            public ClusterDominance Dominance { get; set; }
            public TradeType Direction { get; set; }
            public int TotalBuyBubbles { get; set; }
            public int TotalSellBubbles { get; set; }
            public int TotalBuyVolume { get; set; }
            public int TotalSellVolume { get; set; }
            public double BuyPercent { get; set; }
            public double SellPercent { get; set; }
            public int CandlesWithBubbles { get; set; }
            public double MaxBuyVolume { get; set; }
            public double MaxSellVolume { get; set; }
        }
    }
}