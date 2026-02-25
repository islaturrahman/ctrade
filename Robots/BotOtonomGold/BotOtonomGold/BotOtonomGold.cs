using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None, AddIndicators = true)]
    public class BotOtonomGold : Robot
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

        // ═══════════════════════════════════════
        //  VIRGIN CLUSTER PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("Cluster Lookback (candles)", Group = "Virgin Cluster", DefaultValue = 20, MinValue = 5, MaxValue = 50)]
        public int ClusterLookback { get; set; }

        [Parameter("Cluster Price Tolerance (pips)", Group = "Virgin Cluster", DefaultValue = 3.0, MinValue = 1.0, MaxValue = 10.0)]
        public double ClusterTolerancePips { get; set; }

        [Parameter("Cluster Dominance % Threshold", Group = "Virgin Cluster", DefaultValue = 65.0, MinValue = 51.0, MaxValue = 90.0)]
        public double ClusterDominanceThreshold { get; set; }

        [Parameter("Min Bubbles in Cluster", Group = "Virgin Cluster", DefaultValue = 5, MinValue = 2, MaxValue = 30)]
        public int MinBubblesInCluster { get; set; }

        [Parameter("Min Bubbles for Virgin Signal", Group = "Virgin Cluster", DefaultValue = 3, MinValue = 2, MaxValue = 20)]
        public int MinBubblesForVirgin { get; set; }

        [Parameter("Min Bubbles for CheckEntry", Group = "Virgin Cluster", DefaultValue = 5, MinValue = 2, MaxValue = 30)]
        public int MinBubblesForCheckEntry { get; set; }

        [Parameter("Max Virgin Zone Age (bars)", Group = "Virgin Cluster", DefaultValue = 100, MinValue = 10, MaxValue = 500)]
        public int MaxVirginZoneAge { get; set; }

        // ═══════════════════════════════════════
        //  HTF TREND FILTER
        // ═══════════════════════════════════════

        [Parameter("Enable HTF Trend Filter", Group = "HTF Trend", DefaultValue = true)]
        public bool EnableHTFFilter { get; set; }

        [Parameter("HTF Timeframe", Group = "HTF Trend", DefaultValue = "Minute15")]
        public TimeFrame HTFTimeframe { get; set; }

        [Parameter("HTF Swing Lookback (bars)", Group = "HTF Trend", DefaultValue = 10, MinValue = 3, MaxValue = 30)]
        public int HTFSwingLookback { get; set; }

        // ═══════════════════════════════════════
        //  SMA TREND FILTER
        // ═══════════════════════════════════════

        [Parameter("Enable SMA Filter", Group = "SMA Filter", DefaultValue = true)]
        public bool EnableSMAFilter { get; set; }

        [Parameter("SMA Period", Group = "SMA Filter", DefaultValue = 50, MinValue = 5, MaxValue = 200)]
        public int SmaPeriod { get; set; }

        // ═══════════════════════════════════════
        //  SMC (SMART MONEY CONCEPTS)
        // ═══════════════════════════════════════

        [Parameter("Enable SMC Filter", Group = "SMC", DefaultValue = true)]
        public bool EnableSMC { get; set; }

        [Parameter("Swing Lookback", Group = "SMC", DefaultValue = 5, MinValue = 2, MaxValue = 20)]
        public int SwingLookback { get; set; }

        [Parameter("Order Block Max Age (bars)", Group = "SMC", DefaultValue = 200, MinValue = 10, MaxValue = 1000)]
        public int OBMaxAge { get; set; }

        [Parameter("FVG Min Size (pips)", Group = "SMC", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 50)]
        public double FVGMinPips { get; set; }

        [Parameter("OB Min Impulse (pips)", Group = "SMC", DefaultValue = 200, MinValue = 2, MaxValue = 1000)]
        public double OBMinImpulsePips { get; set; }

        [Parameter("Max Active FVGs", Group = "SMC", DefaultValue = 5, MinValue = 1, MaxValue = 20)]
        public int MaxActiveFVGs { get; set; }

        [Parameter("Min Confluence Score", Group = "SMC", DefaultValue = 4, MinValue = 1, MaxValue = 7)]
        public int MinConfluenceScore { get; set; }

        [Parameter("Show SMC Visuals", Group = "SMC", DefaultValue = true)]
        public bool ShowSMCVisuals { get; set; }

        // ═══════════════════════════════════════
        //  RISK MANAGEMENT
        // ═══════════════════════════════════════

        [Parameter("Fixed Lot Size", Group = "Risk", DefaultValue = 0.01, MinValue = 0.01, MaxValue = 100)]
        public double FixedLots { get; set; }

        [Parameter("Max Concurrent Positions", Group = "Risk", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int MaxPositions { get; set; }

        [Parameter("Max Trades Per Day", Group = "Risk", DefaultValue = 10, MinValue = 1, MaxValue = 50)]
        public int MaxTradesPerDay { get; set; }

        [Parameter("Max Daily Loss %", Group = "Risk", DefaultValue = 3.0, MinValue = 0.5, MaxValue = 20)]
        public double MaxDailyLossPercent { get; set; }

        [Parameter("Max Spread (pips)", Group = "Risk", DefaultValue = 3.0, MinValue = 0.1, MaxValue = 50)]
        public double MaxSpreadPips { get; set; }

        [Parameter("Cooldown Bars", Group = "Risk", DefaultValue = 2, MinValue = 0, MaxValue = 20)]
        public int CooldownBars { get; set; }

        // ═══════════════════════════════════════
        //  DYNAMIC RISK (SMART MONEY)
        // ═══════════════════════════════════════

        [Parameter("Risk-Reward Ratio", Group = "Dynamic Risk", DefaultValue = 2.0, MinValue = 0.5, MaxValue = 10.0)]
        public double RiskRewardRatio { get; set; }

        [Parameter("SL Buffer (pips)", Group = "Dynamic Risk", DefaultValue = 2.0, MinValue = 0.0, MaxValue = 20.0)]
        public double SlBufferPips { get; set; }

        [Parameter("Fallback SL (pips)", Group = "Dynamic Risk", DefaultValue = 50.0)]
        public double FallbackSlPips { get; set; }

        [Parameter("Use OB Trailing Stop", Group = "Dynamic Risk", DefaultValue = true)]
        public bool UseOBTrailingStop { get; set; }

        // ═══════════════════════════════════════
        //  TIME SESSIONS
        // ═══════════════════════════════════════

        [Parameter("Trade Sydney Session (18:00 - 02:00 EST)", Group = "Time Session", DefaultValue = false)]
        public bool TradeSydney { get; set; }

        [Parameter("Trade Tokyo Session (19:00 - 04:00 EST)", Group = "Time Session", DefaultValue = false)]
        public bool TradeTokyo { get; set; }

        [Parameter("Trade London Session (03:00 - 12:00 EST)", Group = "Time Session", DefaultValue = true)]
        public bool TradeLondon { get; set; }

        [Parameter("Trade NY Session (08:00 - 18:00 EST)", Group = "Time Session", DefaultValue = true)]
        public bool TradeNY { get; set; }

        // ═══════════════════════════════════════
        //  VISUAL
        // ═══════════════════════════════════════

        [Parameter("Show Bubbles", Group = "Visual", DefaultValue = true)]
        public bool ShowBubbles { get; set; }

        [Parameter("Bubble Opacity (%)", Group = "Visual", DefaultValue = 127, MinValue = 10, MaxValue = 255)]
        public int BubbleOpacity { get; set; }

        [Parameter("Show Cluster Zones", Group = "Visual", DefaultValue = true)]
        public bool ShowClusterZones { get; set; }

        [Parameter("Show P/D Zones", Group = "Visual SMC LuxAlgo", DefaultValue = true)]
        public bool ShowPDZones { get; set; }

        [Parameter("Show EQH / EQL", Group = "Visual SMC LuxAlgo", DefaultValue = true)]
        public bool ShowEqhEql { get; set; }

        [Parameter("EQH/EQL Tolerance Pips", Group = "Visual SMC LuxAlgo", DefaultValue = 15.0)]
        public double EqhEqlTolerancePips { get; set; }

        // ═══════════════════════════════════════
        //  PRIVATE FIELDS
        // ═══════════════════════════════════════

        private const string BotLabel = "BotOtonomGold";

        // Order Flow Engine
        private Dictionary<int, CandleFootprint> candleFootprints;
        private Ticks ticks;
        private int lastKnownBarIndex = 0;

        // Cluster Zones
        private List<ClusterZone> clusterZones = new List<ClusterZone>();
        private HashSet<string> virginClusters = new HashSet<string>();
        private HashSet<string> testedClusters = new HashSet<string>();

        // ── PENDING BUBBLE SETUPS ──
        private class PendingBubbleSetup
        {
            public TradeType Direction { get; set; }
            public double SetupPrice { get; set; }
            public int SetupBarIndex { get; set; }
        }
        private List<PendingBubbleSetup> pendingBubbleSetups = new List<PendingBubbleSetup>();

        // Indicators
        private SimpleMovingAverage sma;

        // HTF
        private Bars htfBars;
        private TrendDirection htfTrend = TrendDirection.Neutral;

        // SMC Engine
        private List<SwingPoint> swingPoints = new List<SwingPoint>();
        private List<OrderBlock> orderBlocks = new List<OrderBlock>();
        private List<FairValueGap> fvgList = new List<FairValueGap>();
        private SmcTrend smcTrend = SmcTrend.Undefined;
        private double lastSwingHigh = 0;
        private int lastSwingHighIndex = 0;
        private double lastSwingLow = double.MaxValue;
        private int lastSwingLowIndex = 0;
        private int smcStructureCount = 0;
        private double lastBosLevel = 0;  // BOS dedup
        private int lastSmcSignalBar = -999; // SMC signal cooldown

        // Risk State
        private double dailyStartBalance;
        private int dailyTradeCount;
        private DateTime lastTradeDay;
        private int lastTradeBarIndex = -999;

        // Stats
        private int totalSignals = 0;
        private int totalTrades = 0;
        private int tradesWon = 0;
        private int tradesLost = 0;

        // ═══════════════════════════════════════
        //  LIFECYCLE
        // ═══════════════════════════════════════

        protected override void OnStart()
        {
            candleFootprints = new Dictionary<int, CandleFootprint>();
            ticks = MarketData.GetTicks();
            sma = Indicators.SimpleMovingAverage(Bars.ClosePrices, SmaPeriod);

            dailyStartBalance = Account.Balance;
            dailyTradeCount = 0;
            lastTradeDay = Server.Time.Date;

            // HTF init
            if (EnableHTFFilter)
            {
                try
                {
                    htfBars = MarketData.GetBars(HTFTimeframe);
                    Print($"HTF Filter: {HTFTimeframe} loaded ({htfBars.Count} bars)");
                }
                catch (Exception ex)
                {
                    Print($"⚠️ HTF failed: {ex.Message} — disabled");
                    EnableHTFFilter = false;
                }
            }

            Print("═══════════════════════════════════════════════════");
            Print("  BOT OTONOM GOLD — Order Flow + SMC Concept");
            Print("═══════════════════════════════════════════════════");
            Print($"Symbol: {SymbolName} | TF: {TimeFrame}");
            Print($"SMA: {SmaPeriod} | HTF: {EnableHTFFilter} ({HTFTimeframe})");
            Print($"SMC: {EnableSMC} | SwingLB={SwingLookback} | OBAge={OBMaxAge}");
            Print($"Virgin: MinBubbles={MinBubblesForVirgin} | MaxAge={MaxVirginZoneAge}");
            Print("═══════════════════════════════════════════════════");

            if (ticks != null)
            {
                ticks.Tick += OnNewTick;
                ProcessHistoricalTicks();
            }
            else
            {
                Print("⚠️ Ticks not available");
            }

            Positions.Closed += OnPositionClosed;
            
            // Tambahkan Tombol UI Manual
            CreateDashboardControls();
        }

        protected override void OnTick()
        {
            ResetDailyCounters();
        }

        private void CreateDashboardControls()
        {
            var btnClearOB = new Button
            {
                Text = "🗑️ Clear All OBs",
                BackgroundColor = Color.Firebrick,
                ForegroundColor = Color.White,
                Margin = new Thickness(0, 0, 10, 50),
                Height = 30,
                Width = 120,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom
            };

            btnClearOB.Click += args => DeleteAllOrderBlocks();

            Chart.AddControl(btnClearOB);
        }

        private void DeleteAllOrderBlocks()
        {
            int count = orderBlocks.Count;
            foreach (var ob in orderBlocks)
            {
                try
                {
                    Chart.RemoveObject($"OB_{ob.BarIndex}_{(ob.IsBullish ? "B" : "S")}");
                    Chart.RemoveObject($"OBL_{ob.BarIndex}");
                }
                catch { }
            }
            orderBlocks.Clear();
            Print($"🗑️ DASHBOARD COMMAND: {count} Order Blocks forcefully cleared!");
        }

        private void LogDashboard()
        {
            int currentBar = Bars.Count - 1;
            double price = Bars.ClosePrices.LastValue;
            DateTime barTime = Bars.OpenTimes.LastValue;

            Print("─────────────────────────────────────────────");
            Print($"  BAR #{currentBar} | {barTime:HH:mm:ss} | Price: {price:F2}");
            Print("─────────────────────────────────────────────");

            // ── SMA ──
            if (EnableSMAFilter)
            {
                double smaValue = sma.Result.LastValue;
                double dist = (price - smaValue) / Symbol.PipSize;
                string pos = price > smaValue ? "ABOVE ▲" : "BELOW ▼";
                Print($"  📈 SMA({SmaPeriod}): {smaValue:F2} | Price {pos} ({dist:+0.0;-0.0} pips)");
            }else{
                Print("  📈 SMA: Disabled");
            }

            // ── HTF TREND ──
            if (EnableHTFFilter)
            {
                string htfIcon = htfTrend == TrendDirection.Up ? "📈 UP" :
                                 htfTrend == TrendDirection.Down ? "📉 DOWN" : "➡️ NEUTRAL";
                Print($"  🕐 HTF({HTFTimeframe}): {htfIcon}");
            }
            else
            {
                Print("  🕐 HTF: Disabled");
            }

            // ── SMC ──
            if (EnableSMC)
            {
                string smcIcon = smcTrend == SmcTrend.Bullish ? "🟢 BULLISH" :
                                 smcTrend == SmcTrend.Bearish ? "🔴 BEARISH" : "⚪ UNDEFINED";
                Print($"  🏦 SMC Trend: {smcIcon}");

                // Swing Points
                SwingPoint lastSH = null, lastSL = null;
                for (int i = swingPoints.Count - 1; i >= 0; i--)
                {
                    if (lastSH == null && swingPoints[i].Type == SwingType.High) lastSH = swingPoints[i];
                    if (lastSL == null && swingPoints[i].Type == SwingType.Low) lastSL = swingPoints[i];
                    if (lastSH != null && lastSL != null) break;
                }
                string shStr = lastSH != null ? $"{lastSH.Price:F2} (bar {lastSH.BarIndex})" : "—";
                string slStr = lastSL != null ? $"{lastSL.Price:F2} (bar {lastSL.BarIndex})" : "—";
                Print($"  🔺 Swing H: {shStr} | 🔻 Swing L: {slStr}");

                // Order Blocks
                int activeOBBull = 0, activeOBBear = 0;
                OrderBlock nearestBullOB = null, nearestBearOB = null;
                foreach (var ob in orderBlocks)
                {
                    if (ob.IsMitigated || currentBar - ob.BarIndex > OBMaxAge) continue;
                    if (ob.IsBullish)
                    {
                        activeOBBull++;
                        if (nearestBullOB == null || Math.Abs(price - ob.PriceHigh) < Math.Abs(price - nearestBullOB.PriceHigh))
                            nearestBullOB = ob;
                    }
                    else
                    {
                        activeOBBear++;
                        if (nearestBearOB == null || Math.Abs(price - ob.PriceLow) < Math.Abs(price - nearestBearOB.PriceLow))
                            nearestBearOB = ob;
                    }
                }
                Print($"  📦 OB Active: {activeOBBull} Bull + {activeOBBear} Bear = {activeOBBull + activeOBBear}");
                if (nearestBullOB != null)
                    Print($"     ↳ Nearest Bull OB: {nearestBullOB.PriceLow:F2}-{nearestBullOB.PriceHigh:F2} ({((price - nearestBullOB.PriceHigh) / Symbol.PipSize):+0.0;-0.0} pips)");
                if (nearestBearOB != null)
                    Print($"     ↳ Nearest Bear OB: {nearestBearOB.PriceLow:F2}-{nearestBearOB.PriceHigh:F2} ({((nearestBearOB.PriceLow - price) / Symbol.PipSize):+0.0;-0.0} pips)");

                // FVGs
                int activeFVGBull = 0, activeFVGBear = 0;
                foreach (var fvg in fvgList)
                {
                    if (fvg.IsFilled || currentBar - fvg.BarIndex > OBMaxAge / 2) continue;
                    if (fvg.IsBullish) activeFVGBull++;
                    else activeFVGBear++;
                }
                Print($"  ⚡ FVG Active: {activeFVGBull} Bull + {activeFVGBear} Bear = {activeFVGBull + activeFVGBear}");

                // Price position relative to SMC zones
                bool inBullOB = IsInOrderBlock(price, TradeType.Buy);
                bool inBearOB = IsInOrderBlock(price, TradeType.Sell);
                bool inBullFVG = IsInFairValueGap(price, TradeType.Buy);
                bool inBearFVG = IsInFairValueGap(price, TradeType.Sell);
                if (inBullOB || inBearOB || inBullFVG || inBearFVG)
                {
                    string zones = "";
                    if (inBullOB) zones += "Bull-OB ";
                    if (inBearOB) zones += "Bear-OB ";
                    if (inBullFVG) zones += "Bull-FVG ";
                    if (inBearFVG) zones += "Bear-FVG ";
                    Print($"  🎯 PRICE IN ZONE: {zones.Trim()}");
                }
            }
            else
            {
                Print("  🏦 SMC: Disabled");
            }

            // ── ORDER FLOW ──
            int virginCount = virginClusters.Count;
            int totalZones = clusterZones.Count;
            int buyZones = 0, sellZones = 0;
            ClusterZone nearestVirgin = null;
            double nearestDist = double.MaxValue;

            foreach (var zone in clusterZones)
            {
                if (!virginClusters.Contains(zone.ZoneId)) continue;
                int totalBubbles = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                if (totalBubbles < MinBubblesForVirgin) continue;
                if (zone.Dominance == ClusterDominance.BuyDominated) buyZones++;
                else if (zone.Dominance == ClusterDominance.SellDominated) sellZones++;

                double dist = Math.Abs(price - zone.CenterPrice);
                if (dist < nearestDist) { nearestDist = dist; nearestVirgin = zone; }
            }
            Print($"  🫧 OrderFlow: {virginCount} virgin zones ({buyZones} Buy / {sellZones} Sell) | Total: {totalZones}");
            if (nearestVirgin != null)
            {
                double distPips = (price - nearestVirgin.CenterPrice) / Symbol.PipSize;
                int bub = nearestVirgin.TotalBuyBubbles + nearestVirgin.TotalSellBubbles;
                Print($"     ↳ Nearest Virgin: {nearestVirgin.CenterPrice:F2} ({distPips:+0.0;-0.0} pips) | {nearestVirgin.Dominance} | {bub} bubbles");
            }

            // ── RISK STATUS ──
            var openPos = Positions.FindAll(BotLabel, SymbolName);
            double dailyPnL = Account.Equity - dailyStartBalance;
            Print($"  💰 Positions: {openPos.Length}/{MaxPositions} | Day Trades: {dailyTradeCount}/{MaxTradesPerDay} | Day P&L: {dailyPnL:+0.00;-0.00}");
            Print("─────────────────────────────────────────────");
        }

        protected override void OnStop()
        {
            Print("═══════════════════════════════════════════════════");
            Print("  SESSION SUMMARY");
            Print("═══════════════════════════════════════════════════");
            Print($"Signals: {totalSignals} | Trades: {totalTrades}");
            Print($"Won: {tradesWon} | Lost: {tradesLost}");
            double wr = totalTrades > 0 ? (double)tradesWon / totalTrades * 100 : 0;
            Print($"Win Rate: {wr:F1}% | Zones: {clusterZones.Count}");
            Print("═══════════════════════════════════════════════════");
        }

        // ═══════════════════════════════════════
        //  TICK PROCESSING (ORDER FLOW ENGINE)
        // ═══════════════════════════════════════

        private void OnNewTick(TicksTickEventArgs obj)
        {
            if (ticks.Count > 0)
            {
                ProcessSingleTick(ticks.Last());

                int currentBar = Bars.Count - 1;
                if (ShowBubbles && candleFootprints.ContainsKey(currentBar))
                    DrawCurrentCandleBubbles(candleFootprints[currentBar]);
            }
        }

        private void ProcessHistoricalTicks()
        {
            int tickCount = ticks.Count;
            if (tickCount == 0) { Print("⚠️ No tick data"); return; }

            int start = Math.Max(0, tickCount - 20000);
            Print($"Processing {tickCount - start} historical ticks...");

            for (int i = start; i < tickCount; i++)
                ProcessSingleTick(ticks[i]);

            // Finalize all historical candles
            foreach (var kvp in candleFootprints.Where(x => !x.Value.IsFinalized).OrderBy(x => x.Key))
                kvp.Value.IsFinalized = true;

            BuildClusterZonesFromHistory();
            Print($"✓ Complete! Zones: {clusterZones.Count}");
        }

        private void ProcessSingleTick(Tick tick)
        {
            int barIndex = FindBarIndex(tick.Time);
            if (barIndex < 0) return;

            if (!candleFootprints.ContainsKey(barIndex))
            {
                candleFootprints[barIndex] = new CandleFootprint
                {
                    BarIndex = barIndex,
                    BarTime = Bars.OpenTimes[barIndex]
                };
            }

            var fp = candleFootprints[barIndex];
            if (fp.IsFinalized) return;

            // Determine buy/sell via tick direction
            bool isBuy = false, isSell = false;

            if (fp.LastAsk > 0 && tick.Ask > fp.LastAsk)
                isBuy = true;
            else if (fp.LastBid > 0 && tick.Bid < fp.LastBid)
                isSell = true;
            else if (fp.LastAsk > 0)
            {
                double midLast = (fp.LastBid + fp.LastAsk) / 2.0;
                double midNow = (tick.Bid + tick.Ask) / 2.0;
                if (midNow > midLast) isBuy = true;
                else if (midNow < midLast) isSell = true;
            }

            double price = isBuy ? tick.Ask : tick.Bid;
            double rounded = RoundToPip(price);

            if (!fp.PriceLevels.ContainsKey(rounded))
                fp.PriceLevels[rounded] = new PriceLevel { Price = rounded };

            var level = fp.PriceLevels[rounded];
            if (isBuy) { level.BuyCount++; fp.TotalBuyCount++; }
            else if (isSell) { level.SellCount++; fp.TotalSellCount++; }

            level.TotalCount++;
            fp.TotalTicks++;
            fp.LastBid = tick.Bid;
            fp.LastAsk = tick.Ask;
        }

        // ═══════════════════════════════════════
        //  CLUSTER ZONE ENGINE
        // ═══════════════════════════════════════

        private void BuildClusterZonesFromHistory()
        {
            clusterZones.Clear();
            virginClusters.Clear();
            testedClusters.Clear();

            double tolerancePrice = ClusterTolerancePips * Symbol.PipSize;

            foreach (var candleKvp in candleFootprints.OrderBy(x => x.Key))
            {
                var fp = candleKvp.Value;
                foreach (var levelKvp in fp.PriceLevels)
                {
                    double levelPrice = levelKvp.Key;
                    var level = levelKvp.Value;
                    int delta = level.BuyCount - level.SellCount;
                    int absDelta = Math.Abs(delta);

                    if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                        continue;

                    // Try to add to existing zone
                    bool added = false;
                    foreach (var zone in clusterZones)
                    {
                        if (Math.Abs(levelPrice - zone.CenterPrice) <= tolerancePrice)
                        {
                            if (delta > 0) { zone.TotalBuyBubbles++; zone.TotalBuyVolume += level.BuyCount; }
                            else { zone.TotalSellBubbles++; zone.TotalSellVolume += level.SellCount; }
                            zone.LastBarIndex = fp.BarIndex;
                            zone.CenterPrice = (zone.CenterPrice + levelPrice) / 2.0;
                            added = true;
                            break;
                        }
                    }

                    if (!added)
                    {
                        var z = new ClusterZone
                        {
                            ZoneId = $"CZ_{fp.BarIndex}_{levelPrice:F5}",
                            CenterPrice = levelPrice,
                            FirstBarIndex = fp.BarIndex,
                            LastBarIndex = fp.BarIndex,
                            PriceMin = levelPrice - tolerancePrice,
                            PriceMax = levelPrice + tolerancePrice,
                            IsVirgin = true
                        };
                        if (delta > 0) { z.TotalBuyBubbles = 1; z.TotalBuyVolume = level.BuyCount; }
                        else { z.TotalSellBubbles = 1; z.TotalSellVolume = level.SellCount; }

                        clusterZones.Add(z);
                        virginClusters.Add(z.ZoneId);
                    }
                }
            }

            // Calculate dominance
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

            if (ShowClusterZones)
                DrawAllClusterZones();
        }

        private void FinalizeCandle(CandleFootprint fp)
        {
            fp.IsFinalized = true;
            if (fp.TotalTicks < 10) return;

            // Draw bubbles for finalized candle
            if (ShowBubbles)
            {
                foreach (var lvl in fp.PriceLevels)
                {
                    int delta = lvl.Value.BuyCount - lvl.Value.SellCount;
                    if (Math.Abs(delta) >= MinDeltaPerLevel && lvl.Value.TotalCount >= MinVolumePerLevel)
                        DrawFootprintBubble(fp.BarIndex, lvl.Value, delta, delta > 0);
                }
            }

            // Update cluster zones live
            UpdateClusterZonesWithCandle(fp);
        }

        private void UpdateClusterZonesWithCandle(CandleFootprint fp)
        {
            double tolerancePrice = ClusterTolerancePips * Symbol.PipSize;

            foreach (var levelKvp in fp.PriceLevels)
            {
                double levelPrice = levelKvp.Key;
                var level = levelKvp.Value;
                int delta = level.BuyCount - level.SellCount;
                if (Math.Abs(delta) < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                    continue;

                bool added = false;
                foreach (var zone in clusterZones)
                {
                    if (Math.Abs(levelPrice - zone.CenterPrice) <= tolerancePrice)
                    {
                        if (delta > 0) { zone.TotalBuyBubbles++; zone.TotalBuyVolume += level.BuyCount; }
                        else { zone.TotalSellBubbles++; zone.TotalSellVolume += level.SellCount; }
                        zone.LastBarIndex = fp.BarIndex;

                        // Recalculate dominance
                        int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                        double buyPct = total > 0 ? (double)zone.TotalBuyBubbles / total * 100.0 : 0;
                        zone.BuyPercent = buyPct;
                        if (buyPct >= ClusterDominanceThreshold) zone.Dominance = ClusterDominance.BuyDominated;
                        else if ((100.0 - buyPct) >= ClusterDominanceThreshold) zone.Dominance = ClusterDominance.SellDominated;
                        else zone.Dominance = ClusterDominance.Consolidated;

                        if (ShowClusterZones) DrawSingleClusterZone(zone);
                        added = true;
                        break;
                    }
                }

                if (!added)
                {
                    var z = new ClusterZone
                    {
                        ZoneId = $"CZ_{fp.BarIndex}_{levelPrice:F5}",
                        CenterPrice = levelPrice,
                        FirstBarIndex = fp.BarIndex,
                        LastBarIndex = fp.BarIndex,
                        PriceMin = levelPrice - tolerancePrice,
                        PriceMax = levelPrice + tolerancePrice,
                        IsVirgin = true,
                        Dominance = delta > 0 ? ClusterDominance.BuyDominated : ClusterDominance.SellDominated
                    };
                    if (delta > 0) { z.TotalBuyBubbles = 1; z.TotalBuyVolume = level.BuyCount; }
                    else { z.TotalSellBubbles = 1; z.TotalSellVolume = level.SellCount; }

                    clusterZones.Add(z);
                    virginClusters.Add(z.ZoneId);
                    if (ShowClusterZones) DrawSingleClusterZone(z);
                }
            }
        }

        private bool IsValidSessionToTrade()
        {
            // Konversi Waktu Broker ke EST (GMT-5) sesuai pedoman blok grid
            DateTime estTime = Server.TimeInUtc.AddHours(-5);
            int hour = estTime.Hour;
            
            // Mengacu pada blok Grid gambar:
            bool isLondon = (hour >= 3 && hour < 12);
            bool isNY     = (hour >= 8 && hour < 18);
            bool isSydney = (hour >= 18 || hour < 2);
            bool isTokyo  = (hour >= 19 || hour < 4);

            if (isSydney && TradeSydney) return true;
            if (isTokyo && TradeTokyo) return true;
            if (isLondon && TradeLondon) return true;
            if (isNY && TradeNY) return true;

            return false;
        }

        protected override void OnBar()
        {
            UpdateHTFTrend();

            // SMC Engine update
            if (EnableSMC)
            {
                UpdateSMCEngine();
                UpdateOBTrailingStop();
                
                // ── LUXALGO VISUAL OVERLAYS ──
                if (ShowPDZones) DrawVisualPDZones();
                if (ShowEqhEql) CheckVisualEqhEql();
            }

            // Finalize previous candle
            int prevBar = Bars.Count - 2;
            if (prevBar >= 0 && candleFootprints.ContainsKey(prevBar) && !candleFootprints[prevBar].IsFinalized)
                FinalizeCandle(candleFootprints[prevBar]);

            // ── DASHBOARD LOG ──
            LogDashboard();

            // ── SPECIAL ENTRY 1: Virgin Zone inside SMC OB ──
            CheckEntry();

            // ── SPECIAL ENTRY 2: Single Bubble inside SMC OB ──
            CheckBubbleInSmcSignal();

            // ── SIGNAL: Order Flow trigger + SMC confluence ──
            CheckVirginClusterSignal();

            // Memory cleanup
            PruneOldFootprints();
        }

        // ═══════════════════════════════════════
        //  SPECIAL ENTRY LOGIC
        // ═══════════════════════════════════════

        private void CheckEntry()
        {
            if (!EnableSMC) return;
            if (!IsValidSessionToTrade()) return;

            double currentPrice = Bars.ClosePrices.LastValue;

            // Iterate over all active virgin zones
            foreach (var zone in clusterZones.ToList())
            {
                if (!virginClusters.Contains(zone.ZoneId)) continue;
                if (zone.Dominance == ClusterDominance.Consolidated) continue;

                int totalBubbles = zone.Dominance == ClusterDominance.BuyDominated ? zone.TotalBuyBubbles : zone.TotalSellBubbles;
                
                // Syarat Pertama: Delta volume kuat (Minimum Bubbles khusus untuk Entry ini)
                if (totalBubbles < MinBubblesForCheckEntry) continue;

                // Syarat Kedua: Harga menyentuh Virgin Zone ini
                if (currentPrice >= zone.PriceMin && currentPrice <= zone.PriceMax)
                {
                    TradeType direction = zone.Dominance == ClusterDominance.BuyDominated ? TradeType.Buy : TradeType.Sell;

                    // Syarat Ketiga (Golden Rule): Zona tersebut secara harfiah berada DI DALAM Order Block
                    if (IsInOrderBlock(zone.CenterPrice, direction))
                    {
                        string dir = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                        Print($"💎 CHECK ENTRY TRIGGERED: {dir} | Virgin Zone inside {dir} OB | Strong Delta: {totalBubbles} bubbles!");

                        // Hapus zone agar tidak memicu sinyal loop
                        virginClusters.Remove(zone.ZoneId);
                        testedClusters.Add(zone.ZoneId);
                        zone.IsVirgin = false;

                        ExecuteTrade(direction);
                        return; // 1 spesial entry per eksekusi
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  BUBBLE SMC ENTRY LOGIC (DELAYED & CONFIRMED)
        // ═══════════════════════════════════════

        private void CheckBubbleInSmcSignal()
        {
            if (!EnableSMC) return;
            if (!IsValidSessionToTrade()) return;

            int prevBar = Bars.Count - 2;
            if (prevBar < 0 || !candleFootprints.ContainsKey(prevBar)) return;

            var fp = candleFootprints[prevBar];
            double currentPrice = Bars.ClosePrices.LastValue;

            // Filter Global Perang Volume: Jika harga penutupan sedang berada di area konflik (Bullish & Bearish OB tumpang tindih)
            if (IsInOrderBlock(currentPrice, TradeType.Buy) && IsInOrderBlock(currentPrice, TradeType.Sell))
            {
                if (pendingBubbleSetups.Count > 0)
                {
                    Print($"  🚫 BUBBLE SIGNAL: Strict Conflict Area! (Overlapping OBs) — Canceling all pending bubble setups.");
                    pendingBubbleSetups.Clear();
                }
                return;
            }

            // 1. EVALUASI SETUP BUBBLE YANG TERTUNDA
            for (int i = pendingBubbleSetups.Count - 1; i >= 0; i--)
            {
                var setup = pendingBubbleSetups[i];
                TradeType oppDir = setup.Direction == TradeType.Buy ? TradeType.Sell : TradeType.Buy;

                // Batalkan jika OB asli sudah hilang atau harga tidak lagi di dalam OB tersebut
                if (!IsInOrderBlock(setup.SetupPrice, setup.Direction))
                {
                    pendingBubbleSetups.RemoveAt(i);
                    continue;
                }

                // Batalkan jika muncul OB berlawanan yang menimpa area setup
                if (IsInOrderBlock(setup.SetupPrice, oppDir))
                {
                    pendingBubbleSetups.RemoveAt(i);
                    continue;
                }

                if (prevBar > setup.SetupBarIndex)
                {
                    // ── FILTER TREN SMC (SMART MONEY CONCEPTS) ──
                    bool trendConflict = false;
                    if (smcTrend != SmcTrend.Undefined)
                    {
                        if (setup.Direction == TradeType.Buy && smcTrend == SmcTrend.Bearish) trendConflict = true;
                        if (setup.Direction == TradeType.Sell && smcTrend == SmcTrend.Bullish) trendConflict = true;
                    }

                    if (trendConflict)
                    {
                        Print($"  🚫 BUBBLE CANCELLED: Setup {setup.Direction} conflicts with global SMC Trend ({smcTrend})");
                        pendingBubbleSetups.RemoveAt(i);
                        continue;
                    }

                    // Cari Konfirmasi Bubble di Candle Terbaru
                    bool hasConfirmBubble = false;
                    foreach (var lvl in fp.PriceLevels.Values)
                    {
                        int delta = lvl.BuyCount - lvl.SellCount;
                        if (Math.Abs(delta) >= MinDeltaPerLevel && lvl.TotalCount >= MinVolumePerLevel)
                        {
                            TradeType bDir = delta > 0 ? TradeType.Buy : TradeType.Sell;
                            if (bDir == setup.Direction && IsInOrderBlock(lvl.Price, setup.Direction))
                            {
                                hasConfirmBubble = true;
                                break;
                            }
                        }
                    }

                    if (hasConfirmBubble)
                    {
                        // ── FILTER ORDERFLOW DELTA & REJECTION (PINBAR / ENGULFING) ──
                        double open = Bars.OpenPrices[prevBar];
                        double close = Bars.ClosePrices[prevBar];
                        double high = Bars.HighPrices[prevBar];
                        double low = Bars.LowPrices[prevBar];
                        
                        double bodyTop = Math.Max(open, close);
                        double bodyBottom = Math.Min(open, close);
                        double bodySize = bodyTop - bodyBottom;
                        double upperWick = high - bodyTop;
                        double lowerWick = bodyBottom - low;

                        bool isRejection = false;
                        double minRejectionWick = 3.0 * Symbol.PipSize; // Sumbu penolakan minimal 3 pips
                        
                        int totalCandleDelta = fp.TotalBuyCount - fp.TotalSellCount;
                        bool isDeltaAligned = false;
                        
                        if (setup.Direction == TradeType.Buy)
                        {
                            bool isBullishClose = close > open; // Body hijau
                            bool isPinbar = lowerWick >= (bodySize * 1.5) && lowerWick >= minRejectionWick; // Sumbu bawah memanjang
                            
                            // Tolak keras jika ekor lawannya (atas) terlalu panjang (Doji Gila / Shooting Star)
                            bool invalidUpperWick = upperWick > bodySize && upperWick > (lowerWick * 0.5);
                            
                            isRejection = (isBullishClose || isPinbar) && !invalidUpperWick;
                            isDeltaAligned = totalCandleDelta >= -(fp.TotalBuyCount * 0.1); // Toleransi delta negatif super tipis jika sedang Pinbar pantulan
                        }
                        else
                        {
                            bool isBearishClose = close < open; // Body merah
                            bool isPinbar = upperWick >= (bodySize * 1.5) && upperWick >= minRejectionWick; // Sumbu atas memanjang
                            
                            // Tolak keras jika ekor lawannya (bawah) terlalu panjang (Doji Gila / Hammer)
                            bool invalidLowerWick = lowerWick > bodySize && lowerWick > (upperWick * 0.5);
                            
                            isRejection = (isBearishClose || isPinbar) && !invalidLowerWick;
                            isDeltaAligned = totalCandleDelta <= (fp.TotalSellCount * 0.1); // Toleransi delta positif tipis
                        }

                        if (isRejection && isDeltaAligned)
                        {
                            string dirName = setup.Direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                            Print($"💥 CONFIRMED BUBBLE SIGNAL: {dirName} | Wick & 2nd Bubble confirmed inside OB!");
                            
                            pendingBubbleSetups.Clear(); // Bersihkan setup lain setelah entry
                            ExecuteTrade(setup.Direction);
                            return; // Eksekusi
                        }
                    }
                }
            }

            // 2. DETEKSI BUBBLE INISIAL BARU
            foreach (var levelKvp in fp.PriceLevels)
            {
                double price = levelKvp.Key;
                var level = levelKvp.Value;
                int delta = level.BuyCount - level.SellCount;

                if (Math.Abs(delta) >= MinDeltaPerLevel && level.TotalCount >= MinVolumePerLevel)
                {
                    TradeType direction = delta > 0 ? TradeType.Buy : TradeType.Sell;
                    TradeType oppDir = direction == TradeType.Buy ? TradeType.Sell : TradeType.Buy;

                    bool inOB = IsInOrderBlock(price, direction);
                    bool inOppOB = IsInOrderBlock(price, oppDir);

                    if (inOB && !inOppOB)
                    {
                        // Mendaftarkan setup jika belum ada
                        bool exists = false;
                        foreach (var setup in pendingBubbleSetups)
                        {
                            if (setup.Direction == direction && Math.Abs(setup.SetupPrice - price) < 5 * Symbol.PipSize)
                                exists = true;
                        }

                        if (!exists)
                        {
                            pendingBubbleSetups.Add(new PendingBubbleSetup
                            {
                                Direction = direction,
                                SetupPrice = price,
                                SetupBarIndex = prevBar
                            });
                            string dirName = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                            Print($"⏳ PENDING BUBBLE: {dirName} at {price:F2}. Waiting for wick & next bubble confirmation...");
                        }
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  SIGNAL ENGINE
        //  Trigger: Order Flow (Virgin Cluster touch)
        //  Direction: SMC Trend (BOS/CHoCH)
        //  Confluence: OB, FVG, SMA, HTF
        // ═══════════════════════════════════════

        private void CheckVirginClusterSignal()
        {
            double currentPrice = Bars.ClosePrices.LastValue;
            int currentBar = Bars.Count - 1;

            foreach (var zone in clusterZones)
            {
                if (!virginClusters.Contains(zone.ZoneId))
                    continue;

                int totalBubbles = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                if (totalBubbles < MinBubblesForVirgin)
                    continue;

                int age = currentBar - zone.LastBarIndex;
                if (age > MaxVirginZoneAge)
                {
                    virginClusters.Remove(zone.ZoneId);
                    continue;
                }

                if (zone.Dominance == ClusterDominance.Consolidated)
                    continue;

                // ══════════════════════════════════════
                //  PRICE TOUCH → ENTRY TRIGGERED
                // ══════════════════════════════════════
                if (currentPrice >= zone.PriceMin && currentPrice <= zone.PriceMax)
                {
                    virginClusters.Remove(zone.ZoneId);
                    testedClusters.Add(zone.ZoneId);
                    zone.IsVirgin = false;

                    // ── DIRECTION ──
                    TradeType ofDirection = zone.Dominance == ClusterDominance.BuyDominated
                        ? TradeType.Buy : TradeType.Sell;
                    TradeType direction = ofDirection;
                    int confluenceScore = 1; // OF trigger = 1 point

                    Print($"🔮 OF TRIGGER: Virgin Cluster at {zone.CenterPrice:F2} | {zone.Dominance} | {totalBubbles} bubbles | Age: {age}");
                    totalSignals++;

                    // ── SMC TREND (directional filter) ──
                    if (EnableSMC && smcTrend != SmcTrend.Undefined)
                    {
                        TradeType smcDir = smcTrend == SmcTrend.Bullish ? TradeType.Buy : TradeType.Sell;

                        if (ofDirection == smcDir)
                        {
                            // OF agrees with SMC → strong signal, direction confirmed
                            confluenceScore += 2;
                            Print($"  ✅ SMC Trend: {smcTrend} agrees with OF ({ofDirection}) +2");
                        }
                        else
                        {
                            // OF disagrees with SMC → SKIP (conflicting signals)
                            Print($"  🚫 SMC Conflict: OF={ofDirection} vs SMC={smcTrend} — skipped");
                            return;
                        }
                    }

                    // ── SMA FILTER ──
                    if (EnableSMAFilter)
                    {
                        double smaValue = sma.Result.LastValue;
                        bool smaOk = (direction == TradeType.Buy && currentPrice > smaValue) ||
                                     (direction == TradeType.Sell && currentPrice < smaValue);
                        if (!smaOk)
                        {
                            Print($"  🚫 SMA: {direction} but price {(direction == TradeType.Buy ? "below" : "above")} SMA {smaValue:F2} — skipped");
                            return;
                        }
                        confluenceScore++;
                    }

                    // ── HTF FILTER ──
                    if (EnableHTFFilter && htfTrend != TrendDirection.Neutral)
                    {
                        bool htfOk = (direction == TradeType.Buy && htfTrend == TrendDirection.Up) ||
                                     (direction == TradeType.Sell && htfTrend == TrendDirection.Down);
                        if (!htfOk)
                        {
                            Print($"  🚫 HTF: {direction} vs HTF={htfTrend} — skipped");
                            return;
                        }
                        confluenceScore++;
                    }

                    // ── SMC ZONES (bonus confluence, NOT triggers) ──
                    bool inOB = false, inFVG = false;
                    if (EnableSMC)
                    {
                        inOB = IsInOrderBlock(currentPrice, direction);
                        inFVG = IsInFairValueGap(currentPrice, direction);
                        if (inOB) { confluenceScore++; Print($"  ✅ In Order Block +1"); }
                        if (inFVG) { confluenceScore++; Print($"  ✅ In FVG +1"); }
                    }

                    // ── MINIMUM SCORE CHECK ──
                    string dir = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                    string smcStr = EnableSMC ? $"SMC={smcTrend}" : "SMC=off";
                    string obStr = inOB ? "OB✓" : "OB✗";
                    string fvgStr = inFVG ? "FVG✓" : "FVG✗";

                    if (confluenceScore < MinConfluenceScore)
                    {
                        Print($"📊 WEAK: {dir} | Score: {confluenceScore}/{MinConfluenceScore} min | {smcStr} {obStr} {fvgStr} — skipped");
                        return;
                    }

                    Print($"📊 SIGNAL: {dir} | Score: {confluenceScore}/7 | {smcStr} {obStr} {fvgStr}");

                    ExecuteTrade(direction);
                    return; // One signal per bar
                }
            }
        }

        // ═══════════════════════════════════════
        //  TRADE EXECUTION
        // ═══════════════════════════════════════

        private void ExecuteTrade(TradeType direction)
        {
            // 1. Daily loss limit
            double dailyLoss = dailyStartBalance - Account.Equity;
            if (dailyLoss >= dailyStartBalance * (MaxDailyLossPercent / 100.0))
            {
                Print("🚫 Daily loss limit — skipped");
                return;
            }

            // 2. Max trades/day
            if (dailyTradeCount >= MaxTradesPerDay)
            {
                Print("🚫 Max trades/day — skipped");
                return;
            }

            // 3. Max positions
            var openPos = Positions.FindAll(BotLabel, SymbolName);
            if (openPos.Length >= MaxPositions)
            {
                Print("🚫 Max positions — skipped");
                return;
            }

            // 4. Spread
            double spread = Symbol.Spread / Symbol.PipSize;
            if (spread > MaxSpreadPips)
            {
                Print($"🚫 Spread {spread:F1} > {MaxSpreadPips} — skipped");
                return;
            }

            // 5. Cooldown
            int currentBar = Bars.Count - 1;
            if (currentBar - lastTradeBarIndex < CooldownBars)
            {
                Print("🚫 Cooldown — skipped");
                return;
            }

            // 6. No duplicate direction
            foreach (var pos in openPos)
            {
                if (pos.TradeType == direction)
                {
                    Print($"🚫 Already have {direction} — skipped");
                    return;
                }
            }

            // ── DYNAMIC SL/TP (SMC) ──
            double currentPrice = direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            double estimatedSlPips = FallbackSlPips;

            if (EnableSMC)
            {
                OrderBlock nearestBullOB = null;
                OrderBlock nearestBearOB = null;
                int currentBarIdx = Bars.Count - 1;

                foreach (var ob in orderBlocks)
                {
                    if (ob.IsMitigated || currentBarIdx - ob.BarIndex > OBMaxAge) continue;
                    if (ob.IsBullish)
                    {
                        if (nearestBullOB == null || Math.Abs(currentPrice - ob.PriceHigh) < Math.Abs(currentPrice - nearestBullOB.PriceHigh))
                            nearestBullOB = ob;
                    }
                    else
                    {
                        if (nearestBearOB == null || Math.Abs(currentPrice - ob.PriceLow) < Math.Abs(currentPrice - nearestBearOB.PriceLow))
                            nearestBearOB = ob;
                    }
                }

                if (direction == TradeType.Buy && nearestBullOB != null)
                {
                    double slPrice = nearestBullOB.PriceLow - (SlBufferPips * Symbol.PipSize);
                    estimatedSlPips = (currentPrice - slPrice) / Symbol.PipSize;
                }
                else if (direction == TradeType.Sell && nearestBearOB != null)
                {
                    double slPrice = nearestBearOB.PriceHigh + (SlBufferPips * Symbol.PipSize);
                    estimatedSlPips = (slPrice - currentPrice) / Symbol.PipSize;
                }
            }

            // Ensure SL is strictly positive and wider than spread
            double spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            double minSl = Math.Round(spreadPips + 2.0, 1);
            if (estimatedSlPips < minSl) estimatedSlPips = minSl;

            double estimatedTpPips = estimatedSlPips * RiskRewardRatio;

            // ── Volume ──
            double volume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(FixedLots));
            if (volume < Symbol.VolumeInUnitsMin) volume = Symbol.VolumeInUnitsMin;

            // ── Execute ──
            var result = ExecuteMarketOrder(direction, SymbolName, volume, BotLabel, 
                stopLossPips: estimatedSlPips, takeProfitPips: estimatedTpPips);

            if (result.IsSuccessful)
            {
                totalTrades++;
                dailyTradeCount++;
                lastTradeBarIndex = currentBar;

                string dir = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                Print($"✅ {dir} | Vol:{volume}");
            }
            else
            {
                Print($"❌ Order failed: {result.Error}");
            }
        }

        // ═══════════════════════════════════════
        //  TRAILING STOP (SMC STRUCTURAL)
        // ═══════════════════════════════════════

        private void UpdateOBTrailingStop()
        {
            if (!UseOBTrailingStop || !EnableSMC) return;

            var openPositions = Positions.FindAll(BotLabel, SymbolName);
            if (openPositions.Length == 0) return;

            double currentPrice = Bars.ClosePrices.LastValue;
            int currentBarIdx = Bars.Count - 1;

            OrderBlock nearestBullBelow = null;
            OrderBlock nearestBearAbove = null;

            // Cari OB pelindung terdekat
            foreach (var ob in orderBlocks)
            {
                if (ob.IsMitigated || currentBarIdx - ob.BarIndex > OBMaxAge) continue;

                if (ob.IsBullish && ob.PriceLow < currentPrice)
                {
                    // Temukan alas OB Bullish TERTINGGI yang masih BERSADA DI BAWAH harga saat ini
                    if (nearestBullBelow == null || ob.PriceLow > nearestBullBelow.PriceLow)
                        nearestBullBelow = ob;
                }
                else if (!ob.IsBullish && ob.PriceHigh > currentPrice)
                {
                    // Temukan atap OB Bearish TERENDAH yang masih BERSADA DI ATAS harga saat ini
                    if (nearestBearAbove == null || ob.PriceHigh < nearestBearAbove.PriceHigh)
                        nearestBearAbove = ob;
                }
            }

            // Terapkan Trailing Stop secara bertahap kepada posisi yang terbuka
            foreach (var position in openPositions)
            {
                if (position.TradeType == TradeType.Buy && nearestBullBelow != null)
                {
                    double newSL = Math.Round(nearestBullBelow.PriceLow - (SlBufferPips * Symbol.PipSize), Symbol.Digits);

                    // Pastikan SL yang baru (newSL) BERADA DI ATAS SL LAMA, dan berjarak wajar (misal tidak kurang dari 4 pips ke bawah harga agar tidak tersambar spread liar)
                    if ((position.StopLoss == null || newSL > position.StopLoss.Value) && newSL < currentPrice - (4 * Symbol.PipSize))
                    {
                        Print($"🛡️ SMC TRAIL: Geser SL BUY berlindung di dasar OB -> {newSL}");
                        ModifyPositionAsync(position, newSL, position.TakeProfit);
                    }
                }
                else if (position.TradeType == TradeType.Sell && nearestBearAbove != null)
                {
                    double newSL = Math.Round(nearestBearAbove.PriceHigh + (SlBufferPips * Symbol.PipSize), Symbol.Digits);

                    // Pastikan SL yang baru BERADA DI BAWAH SL LAMA
                    if ((position.StopLoss == null || newSL < position.StopLoss.Value) && newSL > currentPrice + (4 * Symbol.PipSize))
                    {
                        Print($"🛡️ SMC TRAIL: Geser SL SELL berlindung di atap OB -> {newSL}");
                        ModifyPositionAsync(position, newSL, position.TakeProfit);
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  RISK MANAGEMENT (SIMPLE)
        // ═══════════════════════════════════════

        private void ResetDailyCounters()
        {
            if (Server.Time.Date != lastTradeDay)
            {
                dailyStartBalance = Account.Balance;
                dailyTradeCount = 0;
                lastTradeDay = Server.Time.Date;
            }
        }



        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos.Label != BotLabel || pos.SymbolName != SymbolName) return;

            if (pos.NetProfit >= 0) { tradesWon++; Print($"✅ Won: +${pos.NetProfit:F2}"); }
            else { tradesLost++; Print($"❌ Lost: -${Math.Abs(pos.NetProfit):F2}"); }
        }

        // ═══════════════════════════════════════
        //  HTF TREND ENGINE
        // ═══════════════════════════════════════

        private void UpdateHTFTrend()
        {
            if (!EnableHTFFilter || htfBars == null || htfBars.Count < HTFSwingLookback + 2)
                return;

            int last = htfBars.Count - 1;
            int half = HTFSwingLookback / 2;

            double prevHigh = 0, prevLow = double.MaxValue;
            double currHigh = 0, currLow = double.MaxValue;

            for (int i = last - HTFSwingLookback; i <= last - half; i++)
            {
                if (i < 0 || i >= htfBars.Count) continue;
                if (htfBars.HighPrices[i] > prevHigh) prevHigh = htfBars.HighPrices[i];
                if (htfBars.LowPrices[i] < prevLow) prevLow = htfBars.LowPrices[i];
            }

            for (int i = last - half + 1; i <= last; i++)
            {
                if (i < 0 || i >= htfBars.Count) continue;
                if (htfBars.HighPrices[i] > currHigh) currHigh = htfBars.HighPrices[i];
                if (htfBars.LowPrices[i] < currLow) currLow = htfBars.LowPrices[i];
            }

            TrendDirection newTrend;
            bool isHH = currHigh > prevHigh, isHL = currLow > prevLow;
            bool isLH = currHigh < prevHigh, isLL = currLow < prevLow;

            if (isHH && isHL) newTrend = TrendDirection.Up;
            else if (isLH && isLL) newTrend = TrendDirection.Down;
            else newTrend = TrendDirection.Neutral;

            if (newTrend != htfTrend)
            {
                htfTrend = newTrend;
                string icon = htfTrend == TrendDirection.Up ? "📈" :
                              htfTrend == TrendDirection.Down ? "📉" : "➡️";
                Print($"{icon} HTF Trend: {htfTrend}");
            }
        }

        // ═══════════════════════════════════════
        //  LUXALGO SMC VISUAL ENGINE
        // ═══════════════════════════════════════

        private void DrawVisualPDZones()
        {
            if (lastSwingHigh == 0 || lastSwingLow == double.MaxValue) return;
            
            double equilibrium = (lastSwingHigh + lastSwingLow) / 2.0;
            
            // Anchor kotak tepat di tempat Swing Point lahir
            int startBar = Math.Min(lastSwingHighIndex, lastSwingLowIndex);
            if (startBar <= 0) startBar = Math.Max(0, Bars.Count - 60);

            int endBar = Bars.Count + 5; // Sedikit menjorok ke depan

            // Hapus blok kotak yang lama agar tidak mengotori layar
            Chart.RemoveObject("PDPremiumBox");
            Chart.RemoveObject("PDDiscountBox");
            Chart.RemoveObject("PDTopLbl");
            Chart.RemoveObject("PDBotLbl");

            // 1. Garis Batas Atas (Premium)
            Chart.DrawTrendLine("PDTopLine", startBar, lastSwingHigh, endBar, lastSwingHigh, Color.Red, 1, LineStyle.Solid);
            Chart.DrawText("PDTopTxt", "Premium", endBar + 1, lastSwingHigh, Color.Red);

            // 2. Garis Tengah (Equilibrium)
            Chart.DrawTrendLine("PDEqLine", startBar, equilibrium, endBar, equilibrium, Color.DarkOrange, 1, LineStyle.LinesDots);
            Chart.DrawText("PDEqTxt", "Equilibrium", endBar + 1, equilibrium, Color.DarkOrange);

            // 3. Garis Batas Bawah (Discount)
            Chart.DrawTrendLine("PDBotLine", startBar, lastSwingLow, endBar, lastSwingLow, Color.DeepSkyBlue, 1, LineStyle.Solid);
            Chart.DrawText("PDBotTxt", "Discount", endBar + 1, lastSwingLow, Color.DeepSkyBlue);
        }

        private void CheckVisualEqhEql()
        {
            if (swingPoints.Count < 3) return;
            
            // Collect recent Highs and Lows
            var highs = swingPoints.Where(s => s.Type == SwingType.High).Reverse().Take(3).ToList();
            var lows = swingPoints.Where(s => s.Type == SwingType.Low).Reverse().Take(3).ToList();

            // Check EQH (Equal Highs)
            if (highs.Count >= 2)
            {
                double diff = Math.Abs(highs[0].Price - highs[1].Price) / Symbol.PipSize;
                if (diff <= EqhEqlTolerancePips)
                {
                    string eqhName = $"EQH_{highs[1].BarIndex}";
                    Chart.DrawTrendLine(eqhName, highs[1].BarIndex, highs[1].Price, highs[0].BarIndex, highs[1].Price, Color.Red, 1, LineStyle.LinesDots);
                    Chart.DrawText(eqhName + "_txt", "EQH", highs[0].BarIndex + 1, highs[1].Price, Color.Red);
                }
            }

            // Check EQL (Equal Lows)
            if (lows.Count >= 2)
            {
                double diff = Math.Abs(lows[0].Price - lows[1].Price) / Symbol.PipSize;
                if (diff <= EqhEqlTolerancePips)
                {
                    string eqlName = $"EQL_{lows[1].BarIndex}";
                    Chart.DrawTrendLine(eqlName, lows[1].BarIndex, lows[1].Price, lows[0].BarIndex, lows[1].Price, Color.DeepSkyBlue, 1, LineStyle.LinesDots);
                    Chart.DrawText(eqlName + "_txt", "EQL", lows[0].BarIndex + 1, lows[1].Price, Color.DeepSkyBlue);
                }
            }
        }

        // ═══════════════════════════════════════
        //  SMC ENGINE
        // ═══════════════════════════════════════

        private void UpdateSMCEngine()
        {
            if (Bars.Count < SwingLookback * 2 + 2) return;

            DetectSwingPoints();
            UpdateMarketStructure();
            DetectOrderBlocks();
            DetectFVGs();
            CheckOBMitigation();
            CheckFVGFill();
            PruneSMCObjects();

            if (ShowSMCVisuals)
                DrawSMCVisuals();
        }

        private void DetectSwingPoints()
        {
            int checkBar = Bars.Count - 1 - SwingLookback;
            if (checkBar < SwingLookback) return;

            // Check for swing high
            double high = Bars.HighPrices[checkBar];
            bool isSwingHigh = true;
            for (int i = 1; i <= SwingLookback; i++)
            {
                if (Bars.HighPrices[checkBar - i] >= high || Bars.HighPrices[checkBar + i] >= high)
                { isSwingHigh = false; break; }
            }

            if (isSwingHigh)
            {
                // Avoid duplicates
                bool exists = false;
                for (int s = swingPoints.Count - 1; s >= Math.Max(0, swingPoints.Count - 5); s--)
                {
                    if (swingPoints[s].BarIndex == checkBar) { exists = true; break; }
                }
                if (!exists)
                {
                    swingPoints.Add(new SwingPoint
                    {
                        Type = SwingType.High,
                        Price = high,
                        BarIndex = checkBar
                    });
                }
            }

            // Check for swing low
            double low = Bars.LowPrices[checkBar];
            bool isSwingLow = true;
            for (int i = 1; i <= SwingLookback; i++)
            {
                if (Bars.LowPrices[checkBar - i] <= low || Bars.LowPrices[checkBar + i] <= low)
                { isSwingLow = false; break; }
            }

            if (isSwingLow)
            {
                bool exists = false;
                for (int s = swingPoints.Count - 1; s >= Math.Max(0, swingPoints.Count - 5); s--)
                {
                    if (swingPoints[s].BarIndex == checkBar) { exists = true; break; }
                }
                if (!exists)
                {
                    swingPoints.Add(new SwingPoint
                    {
                        Type = SwingType.Low,
                        Price = low,
                        BarIndex = checkBar
                    });
                }
            }
        }

        private void UpdateMarketStructure()
        {
            if (swingPoints.Count < 4) return;

            // Get latest swing high and swing low
            SwingPoint latestSH = null, prevSH = null;
            SwingPoint latestSL = null, prevSL = null;

            for (int i = swingPoints.Count - 1; i >= 0; i--)
            {
                if (swingPoints[i].Type == SwingType.High)
                {
                    if (latestSH == null) latestSH = swingPoints[i];
                    else if (prevSH == null) { prevSH = swingPoints[i]; }
                }
                else
                {
                    if (latestSL == null) latestSL = swingPoints[i];
                    else if (prevSL == null) { prevSL = swingPoints[i]; }
                }
                
                // Kita hanya butuh 1 pass untuk minimal latestSH & latestSL, prev bisa null
                if (latestSH != null && latestSL != null && prevSH != null && prevSL != null) break;
            }

            // Ganti syarat dari 4 poin wajib (prevSH/SL wajib) menjadi cuma 2 poin (latestSH/SL)
            if (latestSH == null || latestSL == null)
                return;

            double currentClose = Bars.ClosePrices.LastValue;
            SmcTrend oldTrend = smcTrend;

            // ── INISIALISASI TREN (Anti-Undefined) ──
            if (smcTrend == SmcTrend.Undefined)
            {
                // Jika titik terdekat lebih tinggi dari sebelumnya, kita assumsi Bullish
                if (prevSH != null && latestSH.Price > prevSH.Price) smcTrend = SmcTrend.Bullish;
                else if (prevSL != null && latestSL.Price < prevSL.Price) smcTrend = SmcTrend.Bearish;
                else if (currentClose > latestSH.Price) smcTrend = SmcTrend.Bullish;
                else if (currentClose < latestSL.Price) smcTrend = SmcTrend.Bearish;
                else smcTrend = SmcTrend.Bullish; // Default fallback jika semua sideways patah tewas

                if (smcTrend != SmcTrend.Undefined)
                    Print($"📊 SMC Initial Trend detected: {smcTrend}");
            }

            // ── BOS & CHoCH DETECTION ──
            int currentBar = Bars.Count - 1;
            if (smcTrend == SmcTrend.Bullish)
            {
                // Bullish BOS: Harga menembus puncak tertinggi yang paling baru (latestSH)
                if (currentClose > latestSH.Price && Math.Abs(currentClose - latestSH.Price) > Symbol.PipSize)
                {
                    lastSwingHigh = latestSH.Price;
                    lastSwingHighIndex = latestSH.BarIndex;
                    lastSwingLow = latestSL.Price;
                    lastSwingLowIndex = latestSL.BarIndex;
                    // BOS dedup: catat supaya tidak menge-print ulang di level yang sama
                    if (Math.Abs(latestSH.Price - lastBosLevel) > Symbol.PipSize)
                    {
                        lastBosLevel = latestSH.Price;
                        int x1 = latestSH.BarIndex;
                        double y = latestSH.Price;
                        Chart.DrawTrendLine($"BOS_{x1}", x1, y, currentBar, y, Color.SeaGreen, 1, LineStyle.LinesDots);
                        Chart.DrawText($"BOS_txt_{x1}", "BOS", currentBar, y, Color.SeaGreen);
                        Print($"📊 SMC BOS ↑ Bullish continuation above {latestSH.Price:F2}");
                    }
                }
                // Bearish CHoCH: Tren berbalik menjadi Bearish karena harga menjebol lembah terbaru (latestSL)
                else if (currentClose < latestSL.Price && Math.Abs(latestSL.Price - currentClose) > Symbol.PipSize)
                {
                    smcTrend = SmcTrend.Bearish;
                    lastSwingHigh = latestSH.Price;
                    lastSwingHighIndex = latestSH.BarIndex;
                    lastSwingLow = latestSL.Price;
                    lastSwingLowIndex = latestSL.BarIndex;
                    smcStructureCount++;
                    
                    int x1 = latestSL.BarIndex;
                    double y = latestSL.Price;
                    Chart.DrawTrendLine($"CHoCH_{x1}", x1, y, currentBar, y, Color.Crimson, 1, LineStyle.LinesDots);
                    Chart.DrawText($"CHoCH_txt_{x1}", "CHoCH", currentBar, y, Color.Crimson);
                    
                    Print($"🔄 SMC CHoCH → Bearish! Broke below latest support at {latestSL.Price:F2}");
                }
            }
            else if (smcTrend == SmcTrend.Bearish)
            {
                // Bearish BOS: Harga menembus lembah terdalam yang paling baru (latestSL)
                if (currentClose < latestSL.Price && Math.Abs(latestSL.Price - currentClose) > Symbol.PipSize)
                {
                    lastSwingHigh = latestSH.Price;
                    lastSwingHighIndex = latestSH.BarIndex;
                    lastSwingLow = latestSL.Price;
                    lastSwingLowIndex = latestSL.BarIndex;
                    // BOS dedup: catat supaya tidak menge-print ulang di level yang sama
                    if (Math.Abs(latestSL.Price - lastBosLevel) > Symbol.PipSize)
                    {
                        lastBosLevel = latestSL.Price;
                        int x1 = latestSL.BarIndex;
                        double y = latestSL.Price;
                        Chart.DrawTrendLine($"BOS_{x1}", x1, y, currentBar, y, Color.Crimson, 1, LineStyle.LinesDots);
                        Chart.DrawText($"BOS_txt_{x1}", "BOS", currentBar, y, Color.Crimson);
                        Print($"📊 SMC BOS ↓ Bearish continuation below {latestSL.Price:F2}");
                    }
                }
                // Bullish CHoCH: Tren berbalik menjadi Bullish karena harga menembus puncak terbaru (latestSH)
                else if (currentClose > latestSH.Price && Math.Abs(currentClose - latestSH.Price) > Symbol.PipSize)
                {
                    smcTrend = SmcTrend.Bullish;
                    lastSwingHigh = latestSH.Price;
                    lastSwingHighIndex = latestSH.BarIndex;
                    lastSwingLow = latestSL.Price;
                    lastSwingLowIndex = latestSL.BarIndex;
                    smcStructureCount++;
                    
                    int x1 = latestSH.BarIndex;
                    double y = latestSH.Price;
                    Chart.DrawTrendLine($"CHoCH_{x1}", x1, y, currentBar, y, Color.SeaGreen, 1, LineStyle.LinesDots);
                    Chart.DrawText($"CHoCH_txt_{x1}", "CHoCH", currentBar, y, Color.SeaGreen);
                        
                    Print($"🔄 SMC CHoCH → Bullish! Broke above latest resistance at {latestSH.Price:F2}");
                }
            }
        }

        private void DetectOrderBlocks()
        {
            // Order Block = last opposing candle before an impulse move
            // We check the candle before a BOS-level move
            int currentBar = Bars.Count - 1;
            if (currentBar < 3) return;

            int checkBar = currentBar - 1;

            // Bullish OB: bearish candle followed by strong bullish impulse
            double prevClose = Bars.ClosePrices[checkBar - 1];
            double prevOpen = Bars.OpenPrices[checkBar - 1];
            double currClose = Bars.ClosePrices[checkBar];
            double currOpen = Bars.OpenPrices[checkBar];

            bool prevBearish = prevClose < prevOpen;
            bool currBullish = currClose > currOpen;
            double impulseSize = Math.Abs(currClose - currOpen) / Symbol.PipSize;

            // Bullish Order Block: bearish candle → strong bullish candle
            if (prevBearish && currBullish && impulseSize > OBMinImpulsePips)
            {
                // Check if OB already exists at this bar
                bool exists = false;
                foreach (var ob in orderBlocks)
                    if (ob.BarIndex == checkBar - 1 && ob.IsBullish) { exists = true; break; }

                if (!exists)
                {
                    orderBlocks.Add(new OrderBlock
                    {
                        BarIndex = checkBar - 1,
                        PriceHigh = Math.Max(prevOpen, prevClose),
                        PriceLow = Bars.LowPrices[checkBar - 1],
                        IsBullish = true,
                        IsMitigated = false,
                        CreatedAt = currentBar
                    });
                    Print($"🟩 Bullish OB detected at bar {checkBar - 1}: {Bars.LowPrices[checkBar - 1]:F2}-{Math.Max(prevOpen, prevClose):F2}");
                }
            }

            // Bearish Order Block: bullish candle → strong bearish candle
            bool prevBullish = prevClose > prevOpen;
            bool currBearish = currClose < currOpen;
            double impulseDown = Math.Abs(currOpen - currClose) / Symbol.PipSize;

            if (prevBullish && currBearish && impulseDown > OBMinImpulsePips)
            {
                bool exists = false;
                foreach (var ob in orderBlocks)
                    if (ob.BarIndex == checkBar - 1 && !ob.IsBullish) { exists = true; break; }

                if (!exists)
                {
                    orderBlocks.Add(new OrderBlock
                    {
                        BarIndex = checkBar - 1,
                        PriceHigh = Bars.HighPrices[checkBar - 1],
                        PriceLow = Math.Min(prevOpen, prevClose),
                        IsBullish = false,
                        IsMitigated = false,
                        CreatedAt = currentBar
                    });
                    Print($"🟥 Bearish OB detected at bar {checkBar - 1}: {Math.Min(prevOpen, prevClose):F2}-{Bars.HighPrices[checkBar - 1]:F2}");
                }
            }
        }

        private void DetectFVGs()
        {
            int currentBar = Bars.Count - 1;
            if (currentBar < 3) return;

            // Cap active FVGs to prevent accumulation
            int activeFVGs = 0;
            foreach (var f in fvgList)
                if (!f.IsFilled && currentBar - f.BarIndex <= OBMaxAge / 2) activeFVGs++;
            if (activeFVGs >= MaxActiveFVGs) return;

            // Check 3-candle pattern ending at checkBar
            int i = currentBar - 1; // middle candle
            double minGapSize = FVGMinPips * Symbol.PipSize;

            // Bullish FVG: candle[i-1].High < candle[i+1].Low (gap up)
            double highBefore = Bars.HighPrices[i - 1];
            double lowAfter = Bars.LowPrices[i + 1];
            if (lowAfter > highBefore && (lowAfter - highBefore) >= minGapSize)
            {
                bool exists = false;
                foreach (var fvg in fvgList)
                    if (fvg.BarIndex == i && fvg.IsBullish) { exists = true; break; }

                if (!exists)
                {
                    fvgList.Add(new FairValueGap
                    {
                        BarIndex = i,
                        PriceHigh = lowAfter,
                        PriceLow = highBefore,
                        IsBullish = true,
                        IsFilled = false,
                        CreatedAt = currentBar
                    });
                }
            }

            // Bearish FVG: candle[i-1].Low > candle[i+1].High (gap down)
            double lowBefore = Bars.LowPrices[i - 1];
            double highAfter = Bars.HighPrices[i + 1];
            if (lowBefore > highAfter && (lowBefore - highAfter) >= minGapSize)
            {
                bool exists = false;
                foreach (var fvg in fvgList)
                    if (fvg.BarIndex == i && !fvg.IsBullish) { exists = true; break; }

                if (!exists)
                {
                    fvgList.Add(new FairValueGap
                    {
                        BarIndex = i,
                        PriceHigh = lowBefore,
                        PriceLow = highAfter,
                        IsBullish = false,
                        IsFilled = false,
                        CreatedAt = currentBar
                    });
                }
            }
        }

        private void CheckOBMitigation()
        {
            double currentClose = Bars.ClosePrices.LastValue;
            foreach (var ob in orderBlocks)
            {
                if (ob.IsMitigated) continue;

                // Bullish OB mitigated when price closes below its low
                if (ob.IsBullish && currentClose < ob.PriceLow)
                {
                    ob.IsMitigated = true;
                    Print($"💥 Bullish OB mitigated at {ob.PriceLow:F2}");
                }
                // Bearish OB mitigated when price closes above its high
                else if (!ob.IsBullish && currentClose > ob.PriceHigh)
                {
                    ob.IsMitigated = true;
                    Print($"💥 Bearish OB mitigated at {ob.PriceHigh:F2}");
                }
            }
        }

        private void CheckFVGFill()
        {
            double currentHigh = Bars.HighPrices.LastValue;
            double currentLow = Bars.LowPrices.LastValue;

            foreach (var fvg in fvgList)
            {
                if (fvg.IsFilled) continue;

                // Bullish FVG filled when price drops into the gap
                if (fvg.IsBullish && currentLow <= fvg.PriceLow)
                    fvg.IsFilled = true;
                // Bearish FVG filled when price rises into the gap
                else if (!fvg.IsBullish && currentHigh >= fvg.PriceHigh)
                    fvg.IsFilled = true;
            }
        }

        private bool IsInOrderBlock(double price, TradeType direction)
        {
            int currentBar = Bars.Count - 1;
            double tolerance = 2.0 * Symbol.PipSize; // small proximity buffer

            foreach (var ob in orderBlocks)
            {
                if (ob.IsMitigated) continue;
                if (currentBar - ob.BarIndex > OBMaxAge) continue;

                // Buy → must be in/near a Bullish OB (demand zone)
                if (direction == TradeType.Buy && ob.IsBullish)
                {
                    if (price >= ob.PriceLow - tolerance && price <= ob.PriceHigh + tolerance)
                        return true;
                }
                // Sell → must be in/near a Bearish OB (supply zone)
                else if (direction == TradeType.Sell && !ob.IsBullish)
                {
                    if (price >= ob.PriceLow - tolerance && price <= ob.PriceHigh + tolerance)
                        return true;
                }
            }
            return false;
        }

        private bool IsInFairValueGap(double price, TradeType direction)
        {
            int currentBar = Bars.Count - 1;
            double tolerance = 1.0 * Symbol.PipSize;

            foreach (var fvg in fvgList)
            {
                if (fvg.IsFilled) continue;
                if (currentBar - fvg.BarIndex > OBMaxAge / 2) continue; // FVG expires faster

                if (direction == TradeType.Buy && fvg.IsBullish)
                {
                    if (price >= fvg.PriceLow - tolerance && price <= fvg.PriceHigh + tolerance)
                        return true;
                }
                else if (direction == TradeType.Sell && !fvg.IsBullish)
                {
                    if (price >= fvg.PriceLow - tolerance && price <= fvg.PriceHigh + tolerance)
                        return true;
                }
            }
            return false;
        }

        private void PruneSMCObjects()
        {
            int currentBar = Bars.Count - 1;

            // Keep only last 100 swing points
            if (swingPoints.Count > 100)
                swingPoints.RemoveRange(0, swingPoints.Count - 100);

            // Remove old mitigated OBs
            orderBlocks.RemoveAll(ob => ob.IsMitigated && currentBar - ob.BarIndex > OBMaxAge * 2);

            // Remove old filled FVGs
            fvgList.RemoveAll(fvg => fvg.IsFilled && currentBar - fvg.BarIndex > OBMaxAge / 2);
        }

        // ═══════════════════════════════════════
        //  SMC VISUALS
        // ═══════════════════════════════════════

        private void DrawSMCVisuals()
        {
            int currentBar = Bars.Count - 1;

            // Identify the single latest Bullish and Bearish OB for visual display
            var latestBullishOB = orderBlocks.LastOrDefault(ob => ob.IsBullish && !ob.IsMitigated && currentBar - ob.BarIndex <= OBMaxAge);
            var latestBearishOB = orderBlocks.LastOrDefault(ob => !ob.IsBullish && !ob.IsMitigated && currentBar - ob.BarIndex <= OBMaxAge);

            // Draw Order Blocks (Only Latest)
            foreach (var ob in orderBlocks)
            {
                string obName = $"OB_{ob.BarIndex}_{(ob.IsBullish ? "B" : "S")}";
                string lblName = $"OBL_{ob.BarIndex}";
                
                // Selalu hapus visual ob lama untuk menjaga kebersihan chart
                try
                {
                    Chart.RemoveObject(obName);
                    Chart.RemoveObject(lblName);
                } catch { }

                // Hanya gambar visual jika ob ini adalah yang TERBARU dan belum dimitigasi
                if (ob != latestBullishOB && ob != latestBearishOB) continue;
                if (ob.IsMitigated || currentBar - ob.BarIndex > OBMaxAge) continue;

                Color obColor = ob.IsBullish
                    ? Color.FromArgb(50, 0, 200, 100)
                    : Color.FromArgb(50, 200, 50, 50);

                try
                {
                    var rect = Chart.DrawRectangle(obName, ob.BarIndex, ob.PriceHigh,
                        Math.Min(ob.BarIndex + OBMaxAge, currentBar + 10), ob.PriceLow, obColor);
                    if (rect != null) { rect.IsFilled = true; rect.Thickness = 1; }

                    // Label
                    string lblText = ob.IsBullish ? "OB 🟩" : "OB 🟥";
                    var lbl = Chart.DrawText(lblName, lblText, ob.BarIndex, ob.IsBullish ? ob.PriceLow : ob.PriceHigh,
                        ob.IsBullish ? Color.FromArgb(200, 0, 200, 100) : Color.FromArgb(200, 200, 50, 50));
                    if (lbl != null) { lbl.FontSize = 7; lbl.IsBold = true; }
                }
                catch { }
            }

            // Identify the single latest Bullish and Bearish FVG for visual display
            var latestBullishFVG = fvgList.LastOrDefault(fvg => fvg.IsBullish && !fvg.IsFilled && currentBar - fvg.BarIndex <= OBMaxAge / 2);
            var latestBearishFVG = fvgList.LastOrDefault(fvg => !fvg.IsBullish && !fvg.IsFilled && currentBar - fvg.BarIndex <= OBMaxAge / 2);

            // Draw FVGs (Only Latest)
            foreach (var fvg in fvgList)
            {
                string fvgName = $"FVG_{fvg.BarIndex}_{(fvg.IsBullish ? "B" : "S")}";
                
                // Selalu hapus visual lama
                try { Chart.RemoveObject(fvgName); } catch { }

                // Hanya gambar jika ini yang TERBARU dan belum tertutup penuh
                if (fvg != latestBullishFVG && fvg != latestBearishFVG) continue;
                if (fvg.IsFilled || currentBar - fvg.BarIndex > OBMaxAge / 2) continue;

                Color fvgColor = fvg.IsBullish
                    ? Color.FromArgb(30, 0, 200, 255)
                    : Color.FromArgb(30, 255, 0, 200);

                try
                {
                    var rect = Chart.DrawRectangle(fvgName, fvg.BarIndex - 1, fvg.PriceHigh,
                        Math.Min(fvg.BarIndex + 20, currentBar + 5), fvg.PriceLow, fvgColor);
                    if (rect != null) { rect.IsFilled = true; rect.Thickness = 1; }
                }
                catch { }
            }

            // Draw swing points (last 20)
            int drawCount = Math.Min(20, swingPoints.Count);
            for (int s = swingPoints.Count - drawCount; s < swingPoints.Count; s++)
            {
                var sp = swingPoints[s];
                string spName = $"SP_{sp.BarIndex}_{sp.Type}";
                try
                {
                    Chart.RemoveObject(spName); 
                    string marker = sp.Type == SwingType.High ? "▼" : "▲";
                    Color spColor = sp.Type == SwingType.High ? Color.FromArgb(180, 255, 100, 100) : Color.FromArgb(180, 100, 255, 100);
                    var txt = Chart.DrawText(spName, marker, sp.BarIndex, sp.Price, spColor);
                    if (txt != null) { txt.FontSize = 8; txt.IsBold = true; }
                }
                catch { }
            }

            // SMC Trend label
            try
            {
                Chart.RemoveObject("SMC_TREND");
                string trendTxt = $"SMC: {smcTrend}";
                Color trendColor = smcTrend == SmcTrend.Bullish ? Color.LimeGreen :
                                   smcTrend == SmcTrend.Bearish ? Color.Tomato : Color.Gray;
                Chart.DrawStaticText("SMC_TREND", trendTxt, VerticalAlignment.Top, HorizontalAlignment.Right, trendColor);
            }
            catch { }
        }

        // ═══════════════════════════════════════
        //  DRAWING (SIMPLIFIED)
        // ═══════════════════════════════════════

        private void DrawCurrentCandleBubbles(CandleFootprint fp)
        {
            foreach (var lvl in fp.PriceLevels)
            {
                int delta = lvl.Value.BuyCount - lvl.Value.SellCount;
                int absDelta = Math.Abs(delta);
                if (absDelta >= MinDeltaPerLevel && lvl.Value.TotalCount >= MinVolumePerLevel)
                    DrawFootprintBubble(fp.BarIndex, lvl.Value, delta, delta > 0);
            }
        }

        private void DrawFootprintBubble(int barIndex, PriceLevel level, int delta, bool isBuy)
        {
            int absDelta = Math.Abs(delta);
            Color baseColor = isBuy ? Color.Green : Color.Red;
            Color color = Color.FromArgb(BubbleOpacity, baseColor.R, baseColor.G, baseColor.B);

            double high = Bars.HighPrices[barIndex];
            double low = Bars.LowPrices[barIndex];
            double range = high - low;
            if (range < Symbol.PipSize) return;

            double sizePct = Math.Min(1.5, 0.05 + (absDelta / 20.0) * 1.0);
            double radius = range * sizePct * 0.8;
            double minR = Symbol.PipSize >= 0.1 ? range * 0.005 : Symbol.PipSize * 2;
            if (radius < minR) radius = minR;

            DateTime barTime = Bars.OpenTimes[barIndex];
            TimeSpan barDur = barIndex > 0 ? Bars.OpenTimes[barIndex] - Bars.OpenTimes[barIndex - 1] : TimeSpan.FromMinutes(1);
            TimeSpan width = TimeSpan.FromTicks((long)(barDur.Ticks * 0.5));

            string name = $"FB_{barIndex}_{level.Price:F5}";

            try
            {
                var e = Chart.DrawEllipse(name, barTime.Subtract(width), level.Price + radius,
                    barTime.Add(width), level.Price - radius, color);
                if (e != null) { e.IsFilled = true; e.Thickness = 1; }
            }
            catch { }
        }

        private void DrawSingleClusterZone(ClusterZone zone)
        {
            if (!ShowClusterZones) return;
            int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
            if (total < MinBubblesInCluster) return;

            Color zoneColor = zone.Dominance == ClusterDominance.BuyDominated
                ? Color.FromArgb(40, 0, 255, 0)
                : zone.Dominance == ClusterDominance.SellDominated
                    ? Color.FromArgb(40, 255, 0, 0)
                    : Color.FromArgb(20, 255, 255, 0);

            string name = $"CZ_{zone.ZoneId}";
            try { Chart.RemoveObject(name); } catch { }

            try
            {
                var rect = Chart.DrawRectangle(name, zone.FirstBarIndex, zone.PriceMax,
                    Math.Min(zone.LastBarIndex + 5, Bars.Count - 1), zone.PriceMin, zoneColor);
                if (rect != null) { rect.IsFilled = true; rect.Thickness = 1; }

                // Virgin marker
                if (virginClusters.Contains(zone.ZoneId))
                {
                    string vName = $"V_{zone.ZoneId}";
                    try { Chart.RemoveObject(vName); } catch { }
                    var lbl = Chart.DrawText(vName, "V", zone.LastBarIndex + 1, zone.CenterPrice, Color.White);
                    if (lbl != null) { lbl.FontSize = 8; lbl.IsBold = true; }
                }
            }
            catch { }
        }

        private void DrawAllClusterZones()
        {
            foreach (var zone in clusterZones)
                DrawSingleClusterZone(zone);
        }

        // ═══════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════

        private int FindBarIndex(DateTime time)
        {
            int barCount = Bars.Count;
            if (barCount == 0) return 0;

            // Fast path: cached bar
            if (lastKnownBarIndex >= 0 && lastKnownBarIndex < barCount)
            {
                DateTime start = Bars.OpenTimes[lastKnownBarIndex];
                DateTime end = lastKnownBarIndex < barCount - 1 ? Bars.OpenTimes[lastKnownBarIndex + 1] : Server.Time;
                if (time >= start && time < end) return lastKnownBarIndex;

                int next = lastKnownBarIndex + 1;
                if (next < barCount)
                {
                    DateTime nextEnd = next + 1 < barCount ? Bars.OpenTimes[next + 1] : Server.Time;
                    if (time >= Bars.OpenTimes[next] && time < nextEnd)
                    {
                        lastKnownBarIndex = next;
                        return next;
                    }
                }
            }

            // Binary search fallback
            int lo = 0, hi = barCount - 1;
            while (lo < hi)
            {
                int mid = (lo + hi + 1) / 2;
                if (Bars.OpenTimes[mid] <= time) lo = mid;
                else hi = mid - 1;
            }
            lastKnownBarIndex = lo;
            return lo;
        }

        private double RoundToPip(double price)
        {
            double pip = Symbol.PipSize;
            if (pip >= 0.1)
            {
                double group;
                if (price >= 10000) group = 50;
                else if (price >= 1000) group = 10;
                else if (price >= 100) group = 5;
                else group = 1;
                return Math.Round(price / group) * group;
            }
            return Math.Round(price / pip) * pip;
        }

        private void PruneOldFootprints()
        {
            int threshold = Bars.Count - 1 - ClusterLookback * 2;
            if (threshold <= 0) return;

            var toRemove = new List<int>();
            foreach (var key in candleFootprints.Keys)
                if (key < threshold) toRemove.Add(key);

            foreach (var k in toRemove)
                candleFootprints.Remove(k);

            // Also prune SMC visuals for removed objects
            if (EnableSMC)
            {
                int currentBar = Bars.Count - 1;
                // Clean up old OB visuals
                for (int i = orderBlocks.Count - 1; i >= 0; i--)
                {
                    if (orderBlocks[i].IsMitigated || currentBar - orderBlocks[i].BarIndex > OBMaxAge * 2)
                    {
                        try
                        {
                            Chart.RemoveObject($"OB_{orderBlocks[i].BarIndex}_{(orderBlocks[i].IsBullish ? "B" : "S")}");
                            Chart.RemoveObject($"OBL_{orderBlocks[i].BarIndex}");
                        }
                        catch { }
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  ENUMS & DATA CLASSES
        // ═══════════════════════════════════════

        public enum TrendDirection { Up, Down, Neutral }
        public enum ClusterDominance { BuyDominated, SellDominated, Consolidated }
        public enum SmcTrend { Bullish, Bearish, Undefined }
        public enum SwingType { High, Low }

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

        private class SwingPoint
        {
            public SwingType Type { get; set; }
            public double Price { get; set; }
            public int BarIndex { get; set; }
        }

        private class OrderBlock
        {
            public int BarIndex { get; set; }
            public double PriceHigh { get; set; }
            public double PriceLow { get; set; }
            public bool IsBullish { get; set; }
            public bool IsMitigated { get; set; }
            public int CreatedAt { get; set; }
        }

        private class FairValueGap
        {
            public int BarIndex { get; set; }
            public double PriceHigh { get; set; }
            public double PriceLow { get; set; }
            public bool IsBullish { get; set; }
            public bool IsFilled { get; set; }
            public int CreatedAt { get; set; }
        }
    }
}