using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;
using cAlgo.API.Internals;

namespace cAlgo.Robots
{
    [Robot(TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
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

        // BUG FIX #1: Parameter ini sekarang benar-benar digunakan
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

        [Parameter("OB Min Impulse (pips)", Group = "SMC", DefaultValue = 10.0, MinValue = 2, MaxValue = 1000)]
        public double OBMinImpulsePips { get; set; }

        [Parameter("Max Active FVGs", Group = "SMC", DefaultValue = 5, MinValue = 1, MaxValue = 20)]
        public int MaxActiveFVGs { get; set; }

        [Parameter("Min Confluence Score", Group = "SMC", DefaultValue = 2, MinValue = 1, MaxValue = 7)]
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

        [Parameter("Max Trades Per Day", Group = "Risk", DefaultValue = 10, MinValue = 1, MaxValue = 500)]
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

        [Parameter("Use Markov-ATR Trailing Stop", Group = "Dynamic Risk", DefaultValue = true)]
        public bool UseMarkovATRTrailingStop { get; set; }

        [Parameter("ATR Period", Group = "Dynamic Risk", DefaultValue = 14, MinValue = 1, MaxValue = 100)]
        public int AtrPeriod { get; set; }

        [Parameter("Base ATR Multiplier for SL", Group = "Dynamic Risk", DefaultValue = 1.5, MinValue = 0.5, MaxValue = 5.0)]
        public double BaseAtrMultiplier { get; set; }

        [Parameter("Reverse On SMC CHoCH/BOS", Group = "Dynamic Risk", DefaultValue = false)]
        public bool ReverseOnSmcChOch { get; set; }

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
        //  REGIME CHANGE SIGNAL
        // ═══════════════════════════════════════

        [Parameter("Enable Regime Change Entry", Group = "Regime Change", DefaultValue = true)]
        public bool EnableRegimeChangeEntry { get; set; }

        [Parameter("Regime: Require Markov Confirm", Group = "Regime Change", DefaultValue = true)]
        public bool RegimeRequireMarkovConfirm { get; set; }

        [Parameter("Regime: Min ATR Impulse (mult)", Group = "Regime Change", DefaultValue = 0.5, MinValue = 0.1, MaxValue = 3.0)]
        public double RegimeMinAtrImpulse { get; set; }

        [Parameter("Regime: Cooldown Bars After Change", Group = "Regime Change", DefaultValue = 1, MinValue = 0, MaxValue = 10)]
        public int RegimeCooldownBars { get; set; }

        // ═══════════════════════════════════════
        //  MARKOV CONFIG
        // ═══════════════════════════════════════

        [Parameter("Emergency Exit Prob (%)", Group = "Markov Engine", DefaultValue = 60.0, MinValue = 10.0, MaxValue = 99.0)]
        public double MarkovExitProbability { get; set; }

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
        //  VOLUME PROFILE
        // ═══════════════════════════════════════

        [Parameter("Show Volume Profile", Group = "Volume Profile", DefaultValue = true)]
        public bool ShowVolumeProfile { get; set; }

        [Parameter("VP Lookback (bars)", Group = "Volume Profile", DefaultValue = 100, MinValue = 5, MaxValue = 500)]
        public int VpLookback { get; set; }

        [Parameter("VP Width (bars)", Group = "Volume Profile", DefaultValue = 15, MinValue = 2, MaxValue = 100)]
        public int VpWidthBars { get; set; }

        [Parameter("VP Number of Bins", Group = "Volume Profile", DefaultValue = 30, MinValue = 10, MaxValue = 100)]
        public int VpBins { get; set; }

        [Parameter("VP Bar Height Multiplier", Group = "Volume Profile", DefaultValue = 0.9, MinValue = 0.1, MaxValue = 2.0)]
        public double VpHeightMultiplier { get; set; }

        [Parameter("VP Buy Color", Group = "Volume Profile", DefaultValue = "DarkGreen")]
        public Color VpBuyColor { get; set; }

        [Parameter("VP Sell Color", Group = "Volume Profile", DefaultValue = "DarkRed")]
        public Color VpSellColor { get; set; }

        [Parameter("VP Opacity (0-255)", Group = "Volume Profile", DefaultValue = 100, MinValue = 0, MaxValue = 255)]
        public int VpOpacity { get; set; }

        // ═══════════════════════════════════════
        //  VOLUME PROFILE STRATEGY
        // ═══════════════════════════════════════

        [Parameter("Use VP Value Filters", Group = "Volume Profile Strategy", DefaultValue = true)]
        public bool UseVpFilters { get; set; }

        [Parameter("VP Confluence Bonus", Group = "Volume Profile Strategy", DefaultValue = true)]
        public bool VpConfluenceBonus { get; set; }

        [Parameter("Use POC as TakeProfit", Group = "Volume Profile Strategy", DefaultValue = false)]
        public bool UsePocAsTp { get; set; }

        [Parameter("Show Value Area Lines", Group = "Volume Profile Strategy", DefaultValue = true)]
        public bool ShowValueAreaLines { get; set; }

        // ═══════════════════════════════════════
        //  PRIVATE FIELDS
        // ═══════════════════════════════════════

        private const string BotLabel = "BotOtonomGold";

        // Order Flow Engine
        private Dictionary<int, CandleFootprint> candleFootprints;
        private Ticks ticks;
        private int lastKnownBarIndex = 0;

        // Volume Profile
        private List<string> drawnVpObjects = new List<string>();
        private DateTime lastVpRedrawTime = DateTime.MinValue;

        // Volume Profile Strategy
        private double currentPoc = 0;
        private double currentVah = 0;
        private double currentVal = 0;

        // Cluster Zones
        private List<ClusterZone> clusterZones = new List<ClusterZone>();
        private HashSet<string> virginClusters = new HashSet<string>();
        private HashSet<string> testedClusters = new HashSet<string>();

        // Pending Bubble Setups
        private class PendingBubbleSetup
        {
            public TradeType Direction { get; set; }
            public double SetupPrice { get; set; }
            public int SetupBarIndex { get; set; }
        }
        private List<PendingBubbleSetup> pendingBubbleSetups = new List<PendingBubbleSetup>();

        // Indicators
        private SimpleMovingAverage sma;
        private AverageTrueRange atr;

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
        private double lastBosLevel = 0;
        private int lastSmcSignalBar = -999;

        // Trading State
        private int dailyTradeCount = 0;
        private double dailyStartBalance;
        private DateTime lastTradeDay;
        private int lastTradeBarIndex = -1;

        // BUG FIX #2: Flag untuk mencegah double entry dalam satu bar
        private bool _entryExecutedThisBar = false;

        // Markov Chain
        private MarketState currentMarketState = MarketState.Flat;
        private MarketState prevMarketState    = MarketState.Flat;   // tracking untuk regime change
        private List<MarketState> stateHistory = new List<MarketState>();
        private double[,] transitionMatrix = new double[3, 3];
        private const int MarkovLookback = 100;

        // Regime Change Tracking
        private SmcTrend  prevSmcTrend       = SmcTrend.Undefined;
        private bool      _regimeJustChanged = false;  // flag: regime berubah di bar ini
        private TradeType _regimeDirection   = TradeType.Buy;
        private int       _lastRegimeChangeBar = -999;

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
            // Clear any leftover Volume Profile objects from previous runs
            try
            {
                var allObjects = Chart.FindAllObjects<ChartObject>();
                foreach (var obj in allObjects)
                {
                    if (obj.Name.StartsWith("VP_"))
                    {
                        Chart.RemoveObject(obj.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                Print($"⚠️ Failed to clear old VP objects: {ex.Message}");
            }

            candleFootprints = new Dictionary<int, CandleFootprint>();
            ticks = MarketData.GetTicks();

            // BUG FIX #1: Gunakan SmaPeriod dari parameter, BUKAN hardcoded 100
            sma = Indicators.SimpleMovingAverage(Bars.ClosePrices, SmaPeriod);
            atr = Indicators.AverageTrueRange(AtrPeriod, MovingAverageType.Simple);

            // BUG FIX #3: Inisialisasi dailyStartBalance di OnStart agar daily loss check benar
            dailyStartBalance = Account.Balance;
            dailyTradeCount = 0;
            lastTradeDay = Server.Time.Date;

            Print($"🤖 {BotLabel} Started | SMA={SmaPeriod} | ATR={AtrPeriod} | MarkovTrail={UseMarkovATRTrailingStop}");

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
            Print($"SMA({SmaPeriod}) | HTF: {EnableHTFFilter} ({HTFTimeframe})");
            Print($"SMC: {EnableSMC} | SwingLB={SwingLookback} | OBAge={OBMaxAge}");
            Print($"Virgin: MinBubbles={MinBubblesForVirgin} | MaxAge={MaxVirginZoneAge}");
            Print("═══════════════════════════════════════════════════");

            if (ticks != null && ticks.Count > 0)
            {
                ticks.Tick += OnNewTick;
                ProcessHistoricalTicks();
                Print($"✓ Tick data loaded: {ticks.Count} ticks, {clusterZones.Count} zones built");
            }
            else
            {
                // FIX #10: Backtest fallback — bangun footprint dari OHLC jika tick tidak tersedia
                Print("⚠️ Ticks not available — using OHLC fallback for backtest");
                BuildOhlcFallbackFootprints();
            }

            Positions.Closed += OnPositionClosed;

            if (ShowVolumeProfile)
            {
                Chart.ZoomChanged += OnChartZoomChanged;
                Chart.ScrollChanged += OnChartScrollChanged;
                UpdateVolumeProfileVisuals();
            }
        }

        protected override void OnTick()
        {
            ResetDailyCounters();
        }

        protected override void OnBar()
        {
            if (Bars.Count < 2) return;

            // BUG FIX #2: Reset flag entry di awal setiap bar baru
            _entryExecutedThisBar = false;
            _regimeJustChanged    = false;  // Reset regime change flag

            // FIX #10: Jika menggunakan OHLC fallback (backtest tanpa tick), update footprint setiap bar
            if (ticks == null || ticks.Count == 0)
                AddOhlcFootprintForPrevBar();

            // Update Markov state
            UpdateMarketState();

            // Update HTF
            UpdateHTFTrend();

            // SMC Engine
            if (EnableSMC)
            {
                UpdateSMCEngine();
                if (ShowPDZones) DrawVisualPDZones();
                if (ShowEqhEql) CheckVisualEqhEql();
            }

            // Trailing Stop Markov-ATR
            UpdateMarkovTrailingStop();

            // Finalize candle sebelumnya (hanya candle yang sudah closed)
            int prevBar = Bars.Count - 2;
            if (prevBar >= 0 && candleFootprints.ContainsKey(prevBar) && !candleFootprints[prevBar].IsFinalized)
                FinalizeCandle(candleFootprints[prevBar]);

            // Dashboard log
            LogDashboard();

            // ── SESSION CHECK: cek waktu trading sebelum semua entry logic ──
            if (!IsValidSessionToTrade())
            {
                PruneOldFootprints();
                return;
            }

            // ── DIAGNOSTIC: log state setiap 50 bar untuk debug backtest ──
            if (Bars.Count % 50 == 0)
            {
                Print($"[DIAG Bar#{Bars.Count}] SMC={smcTrend} HTF={htfTrend} Zones={clusterZones.Count} " +
                      $"Virgin={virginClusters.Count} OBs={orderBlocks.Count(o => !o.IsMitigated)} " +
                      $"FVGs={fvgList.Count(f => !f.IsFilled)}");
            }

            // ── ENTRY LOGIC (berurutan, berhenti setelah pertama kali execute) ──
            // Urutan: RegimeChange → CheckEntry → CheckBubbleInSmcSignal → CheckVirginClusterSignal
            // Regime Change mendapat prioritas tertinggi karena merupakan event high-conviction

            if (!_entryExecutedThisBar)
                CheckRegimeChangeSignal();

            if (!_entryExecutedThisBar)
                CheckEntry();

            if (!_entryExecutedThisBar)
                CheckBubbleInSmcSignal();

            if (!_entryExecutedThisBar)
                CheckVirginClusterSignal();

            // ── REVERSAL PROTECTION (SETELAH entry, bukan sebelum) ──
            // BUG FIX #4: CheckSmcReversal dipindah ke AKHIR agar tidak
            // langsung menutup posisi yang baru saja dibuka di bar yang sama
            CheckSmcReversal();


            PruneOldFootprints();

            if (ShowVolumeProfile)
            {
                UpdateVolumeProfileVisuals();
            }
        }

        protected override void OnStop()
        {
            if (drawnVpObjects != null)
            {
                foreach (var objName in drawnVpObjects)
                {
                    Chart.RemoveObject(objName);
                }
                drawnVpObjects.Clear();
            }

            Print("═══════════════════════════════════════════════════");
            Print("  SESSION SUMMARY");
            Print("═══════════════════════════════════════════════════");
            Print($"Signals: {totalSignals} | Trades: {totalTrades}");
            Print($"Won: {tradesWon} | Lost: {tradesLost}");
            double wr = totalTrades > 0 ? (double)tradesWon / totalTrades * 100 : 0;
            Print($"Win Rate: {wr:F1}% | Zones: {clusterZones.Count}");
            Print("═══════════════════════════════════════════════════");
        }

        private void OnChartZoomChanged(ChartZoomEventArgs args)
        {
            UpdateVolumeProfileVisuals();
        }

        private void OnChartScrollChanged(ChartScrollEventArgs args)
        {
            UpdateVolumeProfileVisuals();
        }

        // ═══════════════════════════════════════
        //  TICK PROCESSING (ORDER FLOW ENGINE)
        // BUG FIX #5: Klasifikasi tick Buy/Sell yang benar
        //  - Buy = tick yang memukul ASK (aggressor buyer)
        //  - Sell = tick yang memukul BID (aggressor seller)
        //  Metode: bandingkan mid-price sekarang vs mid-price sebelumnya
        //  sebagai proxy agressor side (karena cAlgo tidak expose Last Price)
        // ═══════════════════════════════════════

        private void OnNewTick(TicksTickEventArgs obj)
        {
            if (ticks.Count > 0)
            {
                ProcessSingleTick(ticks.Last());

                int currentBar = Bars.Count - 1;
                if (ShowBubbles && candleFootprints.ContainsKey(currentBar))
                    DrawCurrentCandleBubbles(candleFootprints[currentBar]);

                if (ShowVolumeProfile && (Server.Time - lastVpRedrawTime).TotalMilliseconds >= 250)
                {
                    UpdateVolumeProfileVisuals();
                }
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

            foreach (var kvp in candleFootprints.Where(x => !x.Value.IsFinalized).OrderBy(x => x.Key))
                kvp.Value.IsFinalized = true;

            // BUG FIX #6: Cluster build dari history menggunakan lookup O(1)
            BuildClusterZonesFromHistory();
            Print($"✓ Complete! Zones: {clusterZones.Count}");
        }

        // ═══════════════════════════════════════
        //  FIX #10: OHLC FALLBACK — Backtest tanpa tick data
        //  Mensintesis footprint dari OHLC sehingga cluster zones tetap terbentuk.
        //  Buy volume = proporsi bullish candle, Sell volume = proporsi bearish candle.
        // ═══════════════════════════════════════

        private void BuildOhlcFallbackFootprints()
        {
            int totalBars = Bars.Count;
            int start = Math.Max(0, totalBars - 500); // Ambil 500 bar terakhir sebagai history
            Print($"[OHLC Fallback] Building footprints from {totalBars - start} bars...");

            for (int i = start; i < totalBars - 1; i++) // Tidak proses bar yang sedang running
                AddOhlcFootprintForBar(i);

            BuildClusterZonesFromHistory();
            Print($"[OHLC Fallback] Complete. Zones built: {clusterZones.Count}");
        }

        private void AddOhlcFootprintForPrevBar()
        {
            int prevBar = Bars.Count - 2;
            if (prevBar < 0) return;
            AddOhlcFootprintForBar(prevBar);
        }

        private void AddOhlcFootprintForBar(int barIndex)
        {
            if (candleFootprints.ContainsKey(barIndex)) return;

            double open  = Bars.OpenPrices[barIndex];
            double close = Bars.ClosePrices[barIndex];
            double high  = Bars.HighPrices[barIndex];
            double low   = Bars.LowPrices[barIndex];

            bool isBullish = close >= open;
            double range = high - low;
            if (range < Symbol.PipSize) return; // Candle terlalu kecil, skip

            var fp = new CandleFootprint
            {
                BarIndex  = barIndex,
                BarTime   = Bars.OpenTimes[barIndex],
                IsFinalized = true
            };

            // Sintesis: pisahkan candle menjadi beberapa level harga
            int numLevels = Math.Max(3, (int)(range / Symbol.PipSize / 2));
            numLevels = Math.Min(numLevels, 10);
            double levelStep = range / numLevels;

            for (int l = 0; l < numLevels; l++)
            {
                double levelPrice = RoundToPipGold(low + (l + 0.5) * levelStep);
                bool isPriceBullish = levelPrice >= Math.Min(open, close) && levelPrice <= Math.Max(open, close);
                
                // Volume sintetis: body levels mendapat lebih banyak volume daripada wick
                int vol = isPriceBullish ? (int)(MinVolumePerLevel * 3) : MinVolumePerLevel;

                if (!fp.PriceLevels.ContainsKey(levelPrice))
                    fp.PriceLevels[levelPrice] = new PriceLevel { Price = levelPrice };

                var lvl = fp.PriceLevels[levelPrice];
                if (isBullish)
                {
                    lvl.BuyCount  += vol;
                    fp.TotalBuyCount  += vol;
                }
                else
                {
                    lvl.SellCount += vol;
                    fp.TotalSellCount += vol;
                }
                lvl.TotalCount += vol;
                fp.TotalTicks  += vol;
            }

            candleFootprints[barIndex] = fp;

            // Update cluster zones secara live
            UpdateClusterZonesWithCandle(fp);
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

            // BUG FIX #5: Gunakan mid-price comparison sebagai aggressor proxy
            // Lebih akurat dari sekedar membandingkan Ask vs Ask sebelumnya
            bool isBuy = false, isSell = false;

            if (fp.LastBid > 0 && fp.LastAsk > 0)
            {
                double midPrev = (fp.LastBid + fp.LastAsk) / 2.0;
                double midNow  = (tick.Bid + tick.Ask) / 2.0;

                // Jika mid naik → buyer aggressor (hit ASK)
                if (midNow > midPrev + Symbol.TickSize * 0.5)
                    isBuy = true;
                // Jika mid turun → seller aggressor (hit BID)
                else if (midNow < midPrev - Symbol.TickSize * 0.5)
                    isSell = true;
                // Jika tidak bergerak, cek apakah tick ini dekat ASK atau BID
                else
                {
                    double spread = tick.Ask - tick.Bid;
                    if (spread > 0)
                    {
                        double ratio = (midNow - tick.Bid) / spread;
                        if (ratio > 0.6) isBuy = true;
                        else if (ratio < 0.4) isSell = true;
                    }
                }
            }
            else
            {
                // First tick in candle: tidak bisa determine arah, skip clasifikasi
                fp.LastBid = tick.Bid;
                fp.LastAsk = tick.Ask;
                return;
            }

            // BUG FIX #7: RoundToPip untuk Gold menggunakan step $0.10 bukan $50
            double price = (tick.Bid + tick.Ask) / 2.0;
            double rounded = RoundToPipGold(price);

            if (!fp.PriceLevels.ContainsKey(rounded))
                fp.PriceLevels[rounded] = new PriceLevel { Price = rounded };

            var level = fp.PriceLevels[rounded];
            if (isBuy)  { level.BuyCount++;  fp.TotalBuyCount++;  }
            else if (isSell) { level.SellCount++; fp.TotalSellCount++; }

            level.TotalCount++;
            fp.TotalTicks++;
            fp.LastBid = tick.Bid;
            fp.LastAsk = tick.Ask;
        }

        // BUG FIX #7: Fungsi rounding khusus Gold (XAUUSD)
        // Gold bergerak dalam tick $0.01, pip = $0.10
        // Cluster dengan toleransi 3 pip = $0.30 sudah cukup granular
        private double RoundToPipGold(double price)
        {
            double pipSize = Symbol.PipSize; // Untuk XAUUSD biasanya 0.1
            // Bulatkan ke pip terdekat
            return Math.Round(price / pipSize) * pipSize;
        }

        // ═══════════════════════════════════════
        //  CLUSTER ZONE ENGINE
        //  BUG FIX #6: Menggunakan Dictionary<double, ClusterZone> sebagai
        //  lookup O(1) untuk menghindari nested loop O(n²) yang menyebabkan freeze
        // ═══════════════════════════════════════

        private void BuildClusterZonesFromHistory()
        {
            clusterZones.Clear();
            virginClusters.Clear();
            testedClusters.Clear();

            double tolerancePrice = ClusterTolerancePips * Symbol.PipSize;

            // BUG FIX #6: Gunakan Dictionary untuk lookup cluster yang ada
            // Key = harga center yang sudah dibulatkan, Value = index di clusterZones
            var zoneLookup = new Dictionary<double, int>();

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

                    // Cari zona terdekat di sekitar harga ini dengan toleransi
                    double lookupKey = Math.Round(levelPrice / tolerancePrice) * tolerancePrice;
                    
                    // Coba beberapa kandidat key di sekitar harga
                    bool added = false;
                    for (int offset = -1; offset <= 1 && !added; offset++)
                    {
                        double candidateKey = lookupKey + offset * tolerancePrice;
                        if (zoneLookup.TryGetValue(Math.Round(candidateKey, 5), out int zoneIdx))
                        {
                            var zone = clusterZones[zoneIdx];
                            if (Math.Abs(levelPrice - zone.CenterPrice) <= tolerancePrice)
                            {
                                if (delta > 0) { zone.TotalBuyBubbles++; zone.TotalBuyVolume += level.BuyCount; }
                                else           { zone.TotalSellBubbles++; zone.TotalSellVolume += level.SellCount; }
                                zone.LastBarIndex = fp.BarIndex;
                                // Update center price (rolling average)
                                zone.CenterPrice = (zone.CenterPrice + levelPrice) / 2.0;
                                added = true;
                            }
                        }
                    }

                    if (!added)
                    {
                        var z = new ClusterZone
                        {
                            ZoneId = $"CZ_{fp.BarIndex}_{levelPrice:F2}",
                            CenterPrice = levelPrice,
                            FirstBarIndex = fp.BarIndex,
                            LastBarIndex = fp.BarIndex,
                            PriceMin = levelPrice - tolerancePrice,
                            PriceMax = levelPrice + tolerancePrice,
                            IsVirgin = true
                        };
                        if (delta > 0) { z.TotalBuyBubbles = 1; z.TotalBuyVolume = level.BuyCount; }
                        else           { z.TotalSellBubbles = 1; z.TotalSellVolume = level.SellCount; }

                        int newIdx = clusterZones.Count;
                        clusterZones.Add(z);
                        virginClusters.Add(z.ZoneId);
                        zoneLookup[Math.Round(lookupKey, 5)] = newIdx;
                    }
                }
            }

            // Hitung dominance
            foreach (var zone in clusterZones)
            {
                RecalculateDominance(zone);
            }

            if (ShowClusterZones)
                DrawAllClusterZones();
        }

        private void RecalculateDominance(ClusterZone zone)
        {
            int total = zone.TotalBuyBubbles + zone.TotalSellBubbles;
            if (total == 0) return;
            double buyPct = (double)zone.TotalBuyBubbles / total * 100.0;
            zone.BuyPercent = buyPct;

            if (buyPct >= ClusterDominanceThreshold)
                zone.Dominance = ClusterDominance.BuyDominated;
            else if ((100.0 - buyPct) >= ClusterDominanceThreshold)
                zone.Dominance = ClusterDominance.SellDominated;
            else
                zone.Dominance = ClusterDominance.Consolidated;
        }

        private void FinalizeCandle(CandleFootprint fp)
        {
            fp.IsFinalized = true;
            if (fp.TotalTicks < 5) return;

            if (ShowBubbles)
            {
                foreach (var lvl in fp.PriceLevels)
                {
                    int delta = lvl.Value.BuyCount - lvl.Value.SellCount;
                    if (Math.Abs(delta) >= MinDeltaPerLevel && lvl.Value.TotalCount >= MinVolumePerLevel)
                        DrawFootprintBubble(fp.BarIndex, lvl.Value, delta, delta > 0);
                }
            }

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

                // Cari zona terdekat secara linear (live update - jumlah zone tidak terlalu besar)
                ClusterZone nearestZone = null;
                double nearestDist = double.MaxValue;

                foreach (var zone in clusterZones)
                {
                    double dist = Math.Abs(levelPrice - zone.CenterPrice);
                    if (dist <= tolerancePrice && dist < nearestDist)
                    {
                        nearestDist = dist;
                        nearestZone = zone;
                    }
                }

                if (nearestZone != null)
                {
                    if (delta > 0) { nearestZone.TotalBuyBubbles++; nearestZone.TotalBuyVolume += level.BuyCount; }
                    else           { nearestZone.TotalSellBubbles++; nearestZone.TotalSellVolume += level.SellCount; }
                    nearestZone.LastBarIndex = fp.BarIndex;
                    RecalculateDominance(nearestZone);
                    if (ShowClusterZones) DrawSingleClusterZone(nearestZone);
                }
                else
                {
                    var z = new ClusterZone
                    {
                        ZoneId = $"CZ_{fp.BarIndex}_{levelPrice:F2}",
                        CenterPrice = levelPrice,
                        FirstBarIndex = fp.BarIndex,
                        LastBarIndex = fp.BarIndex,
                        PriceMin = levelPrice - tolerancePrice,
                        PriceMax = levelPrice + tolerancePrice,
                        IsVirgin = true,
                        Dominance = delta > 0 ? ClusterDominance.BuyDominated : ClusterDominance.SellDominated
                    };
                    if (delta > 0) { z.TotalBuyBubbles = 1; z.TotalBuyVolume = level.BuyCount; }
                    else           { z.TotalSellBubbles = 1; z.TotalSellVolume = level.SellCount; }

                    clusterZones.Add(z);
                    virginClusters.Add(z.ZoneId);
                    if (ShowClusterZones) DrawSingleClusterZone(z);
                }
            }
        }

        // ═══════════════════════════════════════
        //  SESSION FILTER
        // ═══════════════════════════════════════

        private bool IsValidSessionToTrade()
        {
            DateTime estTime = Server.TimeInUtc.AddHours(-5);
            int hour = estTime.Hour;

            bool isLondon = (hour >= 3 && hour < 12);
            bool isNY     = (hour >= 8 && hour < 18);
            bool isSydney = (hour >= 18 || hour < 2);
            bool isTokyo  = (hour >= 19 || hour < 4);

            if (isSydney && TradeSydney) return true;
            if (isTokyo  && TradeTokyo)  return true;
            if (isLondon && TradeLondon) return true;
            if (isNY     && TradeNY)     return true;

            return false;
        }

        // ═══════════════════════════════════════
        //  EARLY EXIT LOGIC (SMC REVERSAL PROTECTION)
        //  BUG FIX #4: Sekarang dipanggil SETELAH semua entry logic
        //  sehingga tidak bisa menutup posisi yang baru dibuka di bar yang sama
        // ═══════════════════════════════════════

        private void CheckSmcReversal()
        {
            var openPositions = Positions.FindAll(BotLabel, SymbolName);
            if (openPositions.Length == 0) return;

            foreach (var pos in openPositions)
            {
                // BUG FIX #4: Jangan tutup posisi yang baru dibuka di bar ini
                int currentBar = Bars.Count - 1;
                // Estimasi: posisi yang entry bar-nya = bar sekarang → skip reversal check
                // (cAlgo tidak expose entry bar secara langsung, kita pakai lastTradeBarIndex)
                if (currentBar == lastTradeBarIndex) continue;

                bool isReversalDetected = false;

                if (pos.TradeType == TradeType.Buy && smcTrend == SmcTrend.Bearish)
                    isReversalDetected = true;
                else if (pos.TradeType == TradeType.Sell && smcTrend == SmcTrend.Bullish)
                    isReversalDetected = true;

                if (isReversalDetected)
                {
                    TradeType originalDirection = pos.TradeType;

                    if (ReverseOnSmcChOch)
                    {
                        Print($"🚨 SMC CHoCH: {originalDirection} → reversing position");
                        ClosePositionAsync(pos);
                        ExecuteTrade(originalDirection == TradeType.Buy ? TradeType.Sell : TradeType.Buy);
                    }
                    else
                    {
                        Print($"🚨 SMC CHoCH: Closing {originalDirection} early — structure shifted");
                        ClosePositionAsync(pos);
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  SPECIAL ENTRY 1: Virgin Zone inside SMC OB
        // ═══════════════════════════════════════

        private void CheckEntry()
        {
            if (!EnableSMC) return;
            if (_entryExecutedThisBar) return;

            double currentPrice = Bars.ClosePrices.LastValue;

            foreach (var zone in clusterZones.ToList())
            {
                if (!virginClusters.Contains(zone.ZoneId)) continue;
                if (zone.Dominance == ClusterDominance.Consolidated) continue;

                int totalBubbles = zone.Dominance == ClusterDominance.BuyDominated
                    ? zone.TotalBuyBubbles : zone.TotalSellBubbles;

                if (totalBubbles < MinBubblesForCheckEntry) continue;

                if (currentPrice >= zone.PriceMin && currentPrice <= zone.PriceMax)
                {
                    TradeType direction = zone.Dominance == ClusterDominance.BuyDominated
                        ? TradeType.Buy : TradeType.Sell;

                    if (IsInOrderBlock(zone.CenterPrice, direction))
                    {
                        if (smcTrend != SmcTrend.Undefined && smcTrend != SmcTrend.Ranging)
                        {
                            if (direction == TradeType.Buy  && smcTrend == SmcTrend.Bearish) continue;
                            if (direction == TradeType.Sell && smcTrend == SmcTrend.Bullish) continue;
                        }

                        if (UseVpFilters && currentPoc > 0)
                        {
                            if (direction == TradeType.Buy && currentPrice > currentPoc)
                            {
                                Print($"  🚫 VP Filter: CheckEntry Buy blocked above POC ({currentPoc:F2})");
                                continue;
                            }
                            if (direction == TradeType.Sell && currentPrice < currentPoc)
                            {
                                Print($"  🚫 VP Filter: CheckEntry Sell blocked below POC ({currentPoc:F2})");
                                continue;
                            }
                        }

                        string dir = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                        Print($"💎 CHECK ENTRY: {dir} | Virgin Zone in OB | Delta: {totalBubbles} bubbles");

                        virginClusters.Remove(zone.ZoneId);
                        testedClusters.Add(zone.ZoneId);
                        zone.IsVirgin = false;

                        ExecuteTrade(direction);
                        return;
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  SPECIAL ENTRY 2: Bubble Signal (Delayed & Confirmed)
        //  BUG FIX #8: FVG check sekarang hanya menggunakan candle yang sudah CLOSED
        // ═══════════════════════════════════════

        private void CheckBubbleInSmcSignal()
        {
            if (!EnableSMC) return;
            if (_entryExecutedThisBar) return;

            // BUG FIX #8: prevBar = candle yang sudah closed (bukan current)
            int prevBar = Bars.Count - 2;
            if (prevBar < 0 || !candleFootprints.ContainsKey(prevBar)) return;

            var fp = candleFootprints[prevBar];

            // 1. Evaluasi setup yang tertunda
            for (int i = pendingBubbleSetups.Count - 1; i >= 0; i--)
            {
                var setup = pendingBubbleSetups[i];

                if (prevBar <= setup.SetupBarIndex) continue;

                // SMC Trend filter
                bool trendConflict = false;
                if (smcTrend == SmcTrend.Undefined)
                {
                    trendConflict = true;
                }
                else
                {
                    if (setup.Direction == TradeType.Buy  && smcTrend == SmcTrend.Bearish) trendConflict = true;
                    if (setup.Direction == TradeType.Sell && smcTrend == SmcTrend.Bullish) trendConflict = true;
                }

                if (trendConflict)
                {
                    Print($"  🚫 BUBBLE CANCELLED: {setup.Direction} conflicts with SMC ({smcTrend})");
                    pendingBubbleSetups.RemoveAt(i);
                    continue;
                }

                // Cari konfirmasi bubble di candle prevBar
                bool hasConfirmBubble = false;
                foreach (var lvl in fp.PriceLevels.Values)
                {
                    int delta = lvl.BuyCount - lvl.SellCount;
                    if (Math.Abs(delta) >= MinDeltaPerLevel && lvl.TotalCount >= MinVolumePerLevel)
                    {
                        TradeType bDir = delta > 0 ? TradeType.Buy : TradeType.Sell;
                        if (bDir == setup.Direction)
                        {
                            hasConfirmBubble = true;
                            break;
                        }
                    }
                }

                if (hasConfirmBubble)
                {
                    double open  = Bars.OpenPrices[prevBar];
                    double close = Bars.ClosePrices[prevBar];
                    double high  = Bars.HighPrices[prevBar];
                    double low   = Bars.LowPrices[prevBar];

                    double bodyTop    = Math.Max(open, close);
                    double bodyBottom = Math.Min(open, close);
                    double bodySize   = bodyTop - bodyBottom;
                    double upperWick  = high - bodyTop;
                    double lowerWick  = bodyBottom - low;
                    double minRejectionWick = 3.0 * Symbol.PipSize;

                    bool isRejection  = false;
                    bool isDeltaAligned = false;
                    int totalCandleDelta = fp.TotalBuyCount - fp.TotalSellCount;

                    if (setup.Direction == TradeType.Buy)
                    {
                        bool isBullishClose  = close > open;
                        bool isPinbar        = lowerWick >= (bodySize * 1.5) && lowerWick >= minRejectionWick;
                        bool invalidUpperWick = upperWick > bodySize && upperWick > lowerWick * 0.5;
                        isRejection   = (isBullishClose || isPinbar) && !invalidUpperWick;
                        isDeltaAligned = totalCandleDelta >= -(fp.TotalBuyCount * 0.1);
                    }
                    else
                    {
                        bool isBearishClose  = close < open;
                        bool isPinbar        = upperWick >= (bodySize * 1.5) && upperWick >= minRejectionWick;
                        bool invalidLowerWick = lowerWick > bodySize && lowerWick > upperWick * 0.5;
                        isRejection   = (isBearishClose || isPinbar) && !invalidLowerWick;
                        isDeltaAligned = totalCandleDelta <= (fp.TotalSellCount * 0.1);
                    }

                    if (isRejection && isDeltaAligned)
                    {
                        double currentPrice = Bars.ClosePrices.LastValue;
                        if (UseVpFilters && currentPoc > 0)
                        {
                            if (setup.Direction == TradeType.Buy && currentPrice > currentPoc)
                            {
                                Print($"  🚫 VP Filter: Confirmed Buy blocked above POC ({currentPoc:F2})");
                                pendingBubbleSetups.RemoveAt(i);
                                continue;
                            }
                            if (setup.Direction == TradeType.Sell && currentPrice < currentPoc)
                            {
                                Print($"  🚫 VP Filter: Confirmed Sell blocked below POC ({currentPoc:F2})");
                                pendingBubbleSetups.RemoveAt(i);
                                continue;
                            }
                        }

                        string dirName = setup.Direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                        Print($"💥 CONFIRMED BUBBLE: {dirName} | Wick + 2nd Bubble confirmed");
                        pendingBubbleSetups.Clear();
                        ExecuteTrade(setup.Direction);
                        return;
                    }
                }
            }

            // 2. Deteksi bubble inisial baru
            foreach (var levelKvp in fp.PriceLevels)
            {
                if (_entryExecutedThisBar) break;

                double price = levelKvp.Key;
                var level    = levelKvp.Value;
                int delta    = level.BuyCount - level.SellCount;

                if (Math.Abs(delta) < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                    continue;

                TradeType direction = delta > 0 ? TradeType.Buy : TradeType.Sell;

                // Harus berada dalam SMC Zone yang valid
                bool inSMCZone    = false;
                bool inOppositeZone = false;

                foreach (var ob in orderBlocks)
                {
                    if (ob.IsMitigated) continue;
                    if (price >= ob.PriceLow && price <= ob.PriceHigh)
                    {
                        if (ob.IsBullish  && direction == TradeType.Buy)  inSMCZone = true;
                        if (!ob.IsBullish && direction == TradeType.Sell) inSMCZone = true;
                        if (ob.IsBullish  && direction == TradeType.Sell) inOppositeZone = true;
                        if (!ob.IsBullish && direction == TradeType.Buy)  inOppositeZone = true;
                    }
                }

                if (!inSMCZone || inOppositeZone) continue;
                if (smcTrend == SmcTrend.Undefined) continue;

                if (smcTrend != SmcTrend.Ranging)
                {
                    if (direction == TradeType.Buy  && smcTrend == SmcTrend.Bearish) continue;
                    if (direction == TradeType.Sell && smcTrend == SmcTrend.Bullish) continue;
                }

                bool exists = pendingBubbleSetups.Any(s =>
                    s.Direction == direction && Math.Abs(s.SetupPrice - price) < 5 * Symbol.PipSize);

                if (!exists)
                {
                    pendingBubbleSetups.Add(new PendingBubbleSetup
                    {
                        Direction     = direction,
                        SetupPrice    = price,
                        SetupBarIndex = prevBar
                    });
                    string dirName = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                    Print($"⏳ PENDING BUBBLE: {dirName} at {price:F2} — waiting for wick + confirmation");
                }
            }
        }

        // ═══════════════════════════════════════
        //  SIGNAL ENGINE: Virgin Cluster + SMC Confluence
        // ═══════════════════════════════════════

        private void CheckVirginClusterSignal()
        {
            if (_entryExecutedThisBar) return;

            double currentPrice = Bars.ClosePrices.LastValue;
            int currentBar = Bars.Count - 1;

            foreach (var zone in clusterZones)
            {
                if (!virginClusters.Contains(zone.ZoneId)) continue;

                int totalBubbles = zone.TotalBuyBubbles + zone.TotalSellBubbles;
                if (totalBubbles < MinBubblesForVirgin) continue;

                int age = currentBar - zone.LastBarIndex;
                if (age > MaxVirginZoneAge)
                {
                    virginClusters.Remove(zone.ZoneId);
                    continue;
                }

                if (zone.Dominance == ClusterDominance.Consolidated) continue;

                if (currentPrice >= zone.PriceMin && currentPrice <= zone.PriceMax)
                {
                    TradeType direction = zone.Dominance == ClusterDominance.BuyDominated
                        ? TradeType.Buy : TradeType.Sell;

                    if (UseVpFilters && currentPoc > 0)
                    {
                        if (direction == TradeType.Buy && currentPrice > currentPoc)
                        {
                            Print($"  🚫 VP Filter: Virgin Buy blocked above POC ({currentPoc:F2})");
                            continue;
                        }
                        if (direction == TradeType.Sell && currentPrice < currentPoc)
                        {
                            Print($"  🚫 VP Filter: Virgin Sell blocked below POC ({currentPoc:F2})");
                            continue;
                        }
                    }

                    virginClusters.Remove(zone.ZoneId);
                    testedClusters.Add(zone.ZoneId);
                    zone.IsVirgin = false;

                    int confluenceScore = 1; // OF trigger = 1

                    Print($"🔮 OF TRIGGER: Virgin at {zone.CenterPrice:F2} | {zone.Dominance} | {totalBubbles} bubbles | Age:{age}");
                    totalSignals++;

                    // SMC Trend filter
                    if (EnableSMC && smcTrend != SmcTrend.Undefined)
                    {
                        TradeType smcDir = smcTrend == SmcTrend.Bullish ? TradeType.Buy : TradeType.Sell;
                        if (direction == smcDir)
                        {
                            confluenceScore += 2;
                            Print($"  ✅ SMC Trend: {smcTrend} +2");
                        }
                        else
                        {
                            Print($"  🚫 SMC Conflict: OF={direction} vs SMC={smcTrend} — skipped");
                            return;
                        }
                    }

                    // SMA filter
                    if (EnableSMAFilter)
                    {
                        double smaValue = sma.Result.LastValue;
                        bool smaOk = (direction == TradeType.Buy  && currentPrice > smaValue) ||
                                     (direction == TradeType.Sell && currentPrice < smaValue);
                        if (!smaOk)
                        {
                            Print($"  🚫 SMA({SmaPeriod})={smaValue:F2}: price wrong side — skipped");
                            return;
                        }
                        confluenceScore++;
                    }

                    // HTF filter
                    if (EnableHTFFilter && htfTrend != TrendDirection.Neutral)
                    {
                        bool htfOk = (direction == TradeType.Buy  && htfTrend == TrendDirection.Up) ||
                                     (direction == TradeType.Sell && htfTrend == TrendDirection.Down);
                        if (!htfOk)
                        {
                            Print($"  🚫 HTF={htfTrend} conflicts with {direction} — skipped");
                            return;
                        }
                        confluenceScore++;
                    }

                    // SMC Zone bonuses
                    bool inOB = false, inFVG = false;
                    if (EnableSMC)
                    {
                        inOB  = IsInOrderBlock(currentPrice, direction);
                        inFVG = IsInFairValueGap(currentPrice, direction);
                        if (inOB)  { confluenceScore++; Print($"  ✅ In Order Block +1"); }
                        if (inFVG) { confluenceScore++; Print($"  ✅ In FVG +1"); }
                    }

                    if (VpConfluenceBonus && currentPoc > 0)
                    {
                        double priceRange = Chart.TopY - Chart.BottomY;
                        double vpStep = priceRange > 0 ? (priceRange / VpBins) : (5.0 * Symbol.PipSize);

                        if (Math.Abs(currentPrice - currentPoc) <= vpStep)
                        {
                            confluenceScore++;
                            Print($"  ✅ VP Confluence: Near POC ({currentPoc:F2}) +1");
                        }
                        else if (direction == TradeType.Buy && Math.Abs(currentPrice - currentVal) <= vpStep)
                        {
                            confluenceScore++;
                            Print($"  ✅ VP Confluence: Near VAL ({currentVal:F2}) +1");
                        }
                        else if (direction == TradeType.Sell && Math.Abs(currentPrice - currentVah) <= vpStep)
                        {
                            confluenceScore++;
                            Print($"  ✅ VP Confluence: Near VAH ({currentVah:F2}) +1");
                        }
                    }

                    string dir    = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                    string smcStr = EnableSMC ? $"SMC={smcTrend}" : "SMC=off";

                    if (confluenceScore < MinConfluenceScore)
                    {
                        Print($"📊 WEAK: {dir} | Score:{confluenceScore}/{MinConfluenceScore} | {smcStr} — skipped");
                        return;
                    }

                    Print($"📊 SIGNAL: {dir} | Score:{confluenceScore}/7 | {smcStr} OB={inOB} FVG={inFVG}");
                    ExecuteTrade(direction);
                    return;
                }
            }
        }

        // ═══════════════════════════════════════
        //  SPECIAL ENTRY 4: Regime Change Signal
        //  Trigger: CHoCH (SMC structure flip) ATAU Markov state Flat→Bullish/Bearish
        //  Logika:
        //    1. Cek apakah ada regime change di bar ini (_regimeJustChanged)
        //    2. Validasi arah dengan probabilitas Markov (opsional)
        //    3. Pastikan impulse candle cukup kuat (min ATR multiplier)
        //    4. Tidak ada posisi berlawanan arah yang sudah open
        // ═══════════════════════════════════════

        private void CheckRegimeChangeSignal()
        {
            if (!EnableRegimeChangeEntry) return;
            if (_entryExecutedThisBar) return;
            if (!_regimeJustChanged) return;

            int currentBar = Bars.Count - 1;

            // Cooldown: jangan entry terlalu cepat setelah regime change terakhir
            if (currentBar - _lastRegimeChangeBar > RegimeCooldownBars)
            {
                // Regime change sudah terlalu lama lewat, batal
                _regimeJustChanged = false;
                return;
            }

            TradeType direction = _regimeDirection;
            double currentPrice = Bars.ClosePrices.LastValue;
            double atrVal       = atr.Result.LastValue;

            // 1. Validasi Markov probability
            if (RegimeRequireMarkovConfirm && stateHistory.Count >= 3)
            {
                int stateIdx = (int)currentMarketState;
                double total = 0;
                for (int i = 0; i < 3; i++) total += transitionMatrix[stateIdx, i];

                if (total > 0)
                {
                    int targetStateIdx = direction == TradeType.Buy
                        ? (int)MarketState.Bullish
                        : (int)MarketState.Bearish;

                    double continueProb = transitionMatrix[stateIdx, targetStateIdx] / total;
                    if (continueProb < 0.35) // Markov tidak mendukung arah ini
                    {
                        Print($"  🚫 REGIME: Markov prob {continueProb*100:F0}% too low for {direction}");
                        _regimeJustChanged = false;
                        return;
                    }

                    Print($"  ✅ REGIME: Markov prob {continueProb*100:F0}% supports {direction}");
                }
            }

            // 2. Validasi kekuatan impulse candle (body >= RegimeMinAtrImpulse * ATR)
            if (atrVal > 0)
            {
                double open     = Bars.OpenPrices[currentBar];
                double close    = Bars.ClosePrices[currentBar];
                double bodySize = Math.Abs(close - open);
                double minBody  = atrVal * RegimeMinAtrImpulse;

                if (bodySize < minBody)
                {
                    Print($"  🚫 REGIME: Impulse {bodySize / Symbol.PipSize:F1}p < min {minBody / Symbol.PipSize:F1}p — skipped");
                    _regimeJustChanged = false;
                    return;
                }
            }

            // 3. Tidak boleh ada posisi yang berlawanan arah
            var openPos = Positions.FindAll(BotLabel, SymbolName);
            TradeType opposite = direction == TradeType.Buy ? TradeType.Sell : TradeType.Buy;
            if (openPos.Any(p => p.TradeType == opposite))
            {
                Print($"  🚫 REGIME: Opposite position already open — skipped");
                _regimeJustChanged = false;
                return;
            }

            // 4. SMA filter (opsional)
            if (EnableSMAFilter)
            {
                double smaValue = sma.Result.LastValue;
                bool smaOk = (direction == TradeType.Buy  && currentPrice > smaValue) ||
                             (direction == TradeType.Sell && currentPrice < smaValue);
                if (!smaOk)
                {
                    Print($"  🚫 REGIME: SMA({SmaPeriod}) wrong side — skipped");
                    _regimeJustChanged = false;
                    return;
                }
            }

            string dirStr = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
            string source = smcTrend != prevSmcTrend ? "CHoCH" : "MarkovFlip";
            Print($"🌊 REGIME CHANGE ENTRY: {dirStr} | Source={source} | SMC={smcTrend} | Markov={currentMarketState}");

            totalSignals++;
            _regimeJustChanged = false;
            ExecuteTrade(direction);
        }

        // ═══════════════════════════════════════
        //  TRADE EXECUTION
        // ═══════════════════════════════════════

        private void ExecuteTrade(TradeType direction)

        {
            if (_entryExecutedThisBar)
            {
                Print("🚫 Entry already executed this bar — skipped");
                return;
            }

            // 1. Daily loss limit
            // BUG FIX #3: dailyStartBalance sudah diinit di OnStart, bukan 0
            double dailyLoss = dailyStartBalance - Account.Equity;
            if (dailyLoss >= dailyStartBalance * (MaxDailyLossPercent / 100.0))
            {
                Print("🚫 Daily loss limit reached — skipped");
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

            // 4. Spread check
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
                Print("🚫 Cooldown active — skipped");
                return;
            }

            // 6. No duplicate direction
            foreach (var pos in openPos)
            {
                if (pos.TradeType == direction)
                {
                    Print($"🚫 Already have {direction} position — skipped");
                    return;
                }
            }

            // ── Dynamic SL/TP berdasarkan SMC Order Block ──
            double currentPrice = direction == TradeType.Buy ? Symbol.Ask : Symbol.Bid;
            double estimatedSlPips = FallbackSlPips;

            if (EnableSMC)
            {
                OrderBlock nearestOB = null;
                double nearestDist = double.MaxValue;

                foreach (var ob in orderBlocks)
                {
                    if (ob.IsMitigated || currentBar - ob.BarIndex > OBMaxAge) continue;
                    bool matching = direction == TradeType.Buy ? ob.IsBullish : !ob.IsBullish;
                    if (!matching) continue;

                    double refPrice = direction == TradeType.Buy ? ob.PriceHigh : ob.PriceLow;
                    double dist = Math.Abs(currentPrice - refPrice);
                    if (dist < nearestDist) { nearestDist = dist; nearestOB = ob; }
                }

                if (nearestOB != null)
                {
                    if (direction == TradeType.Buy)
                    {
                        double slPrice = nearestOB.PriceLow - (SlBufferPips * Symbol.PipSize);
                        estimatedSlPips = (currentPrice - slPrice) / Symbol.PipSize;
                    }
                    else
                    {
                        double slPrice = nearestOB.PriceHigh + (SlBufferPips * Symbol.PipSize);
                        estimatedSlPips = (slPrice - currentPrice) / Symbol.PipSize;
                    }
                }
            }

            // SL minimum: spread + 2 pip buffer
            double spreadPips = (Symbol.Ask - Symbol.Bid) / Symbol.PipSize;
            double minSl = Math.Round(spreadPips + 2.0, 1);
            if (estimatedSlPips < minSl) estimatedSlPips = minSl;
            if (estimatedSlPips > FallbackSlPips * 2) estimatedSlPips = FallbackSlPips;

            double estimatedTpPips = estimatedSlPips * RiskRewardRatio;

            if (UsePocAsTp && currentPoc > 0)
            {
                double distToPoc = 0;
                if (direction == TradeType.Buy && currentPoc > currentPrice)
                {
                    distToPoc = (currentPoc - currentPrice) / Symbol.PipSize;
                }
                else if (direction == TradeType.Sell && currentPoc < currentPrice)
                {
                    distToPoc = (currentPrice - currentPoc) / Symbol.PipSize;
                }

                if (distToPoc > 3.0)
                {
                    estimatedTpPips = Math.Round(distToPoc, 1);
                    Print($"🎯 VP Strategy: Targeting POC at {currentPoc:F2} | TP={estimatedTpPips:F1} pips");
                }
            }

            double volume = Symbol.NormalizeVolumeInUnits(Symbol.QuantityToVolumeInUnits(FixedLots));
            if (volume < Symbol.VolumeInUnitsMin) volume = Symbol.VolumeInUnitsMin;

            var result = ExecuteMarketOrder(direction, SymbolName, volume, BotLabel,
                stopLossPips: estimatedSlPips,
                takeProfitPips: estimatedTpPips);

            if (result.IsSuccessful)
            {
                totalTrades++;
                dailyTradeCount++;
                lastTradeBarIndex = currentBar;
                _entryExecutedThisBar = true; // BUG FIX #2

                string dir = direction == TradeType.Buy ? "🟢 BUY" : "🔴 SELL";
                Print($"✅ {dir} | Vol:{volume} | SL:{estimatedSlPips:F1}p | TP:{estimatedTpPips:F1}p");
            }
            else
            {
                Print($"❌ Order failed: {result.Error}");
            }
        }

        // ═══════════════════════════════════════
        //  TRAILING STOP: MARKOV-ATR HYBRID
        //  BUG FIX #9: Logika multiplier diperbaiki
        //  - Saat bullish trend kuat: KENCANGKAN SL (lindungi profit)
        //  - Saat mau reversal: LONGGARKAN dulu sebentar (beri ruang napas)
        //  Ini adalah filosofi yang lebih benar untuk trailing stop
        // ═══════════════════════════════════════

        private void UpdateMarkovTrailingStop()
        {
            if (!UseMarkovATRTrailingStop) return;

            var openPositions = Positions.FindAll(BotLabel, SymbolName);
            if (openPositions.Length == 0) return;

            double currentPrice = Bars.ClosePrices.LastValue;
            double currentAtr   = atr.Result.LastValue;
            if (currentAtr <= 0) return;

            int currentStateIdx = (int)currentMarketState;
            double totalTransitions = 0;
            for (int i = 0; i < 3; i++) totalTransitions += transitionMatrix[currentStateIdx, i];

            double probContinueBull = totalTransitions > 0
                ? transitionMatrix[currentStateIdx, (int)MarketState.Bullish] / totalTransitions : 0.33;
            double probContinueBear = totalTransitions > 0
                ? transitionMatrix[currentStateIdx, (int)MarketState.Bearish] / totalTransitions : 0.33;

            foreach (var position in openPositions)
            {
                double dynamicMultiplier = BaseAtrMultiplier;

                if (position.TradeType == TradeType.Buy)
                {
                    // BUG FIX #9: Saat bullish momentum kuat → kencangkan SL untuk lock profit
                    if (probContinueBull > 0.6)
                        dynamicMultiplier = BaseAtrMultiplier * 0.8; // SL lebih dekat = lebih agresif proteksi
                    // Saat reversal bearish kemungkinan tinggi → longgarkan sedikit agar tidak ter-stop premature
                    else if (probContinueBear > 0.5)
                        dynamicMultiplier = BaseAtrMultiplier * 1.3;

                    double newSL = Math.Round(currentPrice - (dynamicMultiplier * currentAtr), Symbol.Digits);

                    // Trail hanya maju (naik), tidak boleh mundur (turun)
                    bool slShouldMove = position.StopLoss == null || newSL > position.StopLoss.Value;
                    // Jaga minimum jarak dari harga agar tidak langsung kena SL
                    bool sufficientGap = newSL < currentPrice - (3 * Symbol.PipSize);

                    if (slShouldMove && sufficientGap)
                    {
                        Print($"🛡️ TRAIL BUY: {currentMarketState} (BullProb={probContinueBull*100:F0}%) ATRx{dynamicMultiplier:F1} → SL={newSL:F2}");
                        ModifyPositionAsync(position, newSL, position.TakeProfit);
                    }
                }
                else if (position.TradeType == TradeType.Sell)
                {
                    // BUG FIX #9: Saat bearish momentum kuat → kencangkan SL
                    if (probContinueBear > 0.6)
                        dynamicMultiplier = BaseAtrMultiplier * 0.8;
                    else if (probContinueBull > 0.5)
                        dynamicMultiplier = BaseAtrMultiplier * 1.3;

                    double newSL = Math.Round(currentPrice + (dynamicMultiplier * currentAtr), Symbol.Digits);

                    // Trail hanya maju (turun), tidak boleh mundur (naik)
                    bool slShouldMove = position.StopLoss == null || newSL < position.StopLoss.Value;
                    bool sufficientGap = newSL > currentPrice + (3 * Symbol.PipSize);

                    if (slShouldMove && sufficientGap)
                    {
                        Print($"🛡️ TRAIL SELL: {currentMarketState} (BearProb={probContinueBear*100:F0}%) ATRx{dynamicMultiplier:F1} → SL={newSL:F2}");
                        ModifyPositionAsync(position, newSL, position.TakeProfit);
                    }
                }
            }
        }

        // ═══════════════════════════════════════
        //  RISK MANAGEMENT
        //  BUG FIX #3: dailyStartBalance reset dengan benar
        // ═══════════════════════════════════════

        private void ResetDailyCounters()
        {
            if (Server.Time.Date != lastTradeDay)
            {
                // Reset di awal hari baru, bukan di OnStart
                dailyStartBalance = Account.Balance;
                dailyTradeCount   = 0;
                lastTradeDay      = Server.Time.Date;
                Print($"📅 New day: {lastTradeDay:yyyy-MM-dd} | Balance: {dailyStartBalance:F2}");
            }
        }

        private void OnPositionClosed(PositionClosedEventArgs args)
        {
            var pos = args.Position;
            if (pos.Label != BotLabel || pos.SymbolName != SymbolName) return;

            if (pos.NetProfit >= 0) { tradesWon++;  Print($"✅ Won: +${pos.NetProfit:F2}"); }
            else                    { tradesLost++; Print($"❌ Lost: -${Math.Abs(pos.NetProfit):F2}"); }
        }

        // ═══════════════════════════════════════
        //  MARKOV CHAIN STATE TRACKING
        //  Menggunakan rolling window yang benar
        // ═══════════════════════════════════════

        private void UpdateMarketState()
        {
            if (Bars.Count < 2) return;

            int currentBar = Bars.Count - 1;
            double open    = Bars.OpenPrices[currentBar];
            double close   = Bars.ClosePrices[currentBar];
            double atrVal  = atr.Result.LastValue;
            if (atrVal <= 0) return;

            double bodyThreshold = atrVal * 0.3;

            MarketState newState;
            if      (close > open + bodyThreshold) newState = MarketState.Bullish;
            else if (close < open - bodyThreshold) newState = MarketState.Bearish;
            else                                    newState = MarketState.Flat;

            if (stateHistory.Count == 0)
            {
                currentMarketState = newState;
                stateHistory.Add(newState);
                return;
            }

            MarketState prevState = stateHistory.Last();

            // Rolling window: hapus transisi terlama jika sudah penuh
            if (stateHistory.Count >= MarkovLookback)
            {
                MarketState oldPrev = stateHistory[0];
                MarketState oldNext = stateHistory[1];
                if (transitionMatrix[(int)oldPrev, (int)oldNext] > 0)
                    transitionMatrix[(int)oldPrev, (int)oldNext]--;
                stateHistory.RemoveAt(0);
            }

            transitionMatrix[(int)prevState, (int)newState]++;
            stateHistory.Add(newState);

            // Deteksi Markov regime change: Flat → Bullish atau Flat → Bearish
            if (prevMarketState == MarketState.Flat && newState != MarketState.Flat)
            {
                // Hanya set flag jika belum ada CHoCH flag di bar ini
                if (!_regimeJustChanged)
                {
                    _regimeJustChanged  = true;
                    _regimeDirection    = newState == MarketState.Bullish ? TradeType.Buy : TradeType.Sell;
                    _lastRegimeChangeBar = Bars.Count - 1;
                    Print($"⚡ MARKOV REGIME: {prevMarketState} → {newState} | Dir={_regimeDirection}");
                }
            }

            prevMarketState    = currentMarketState;
            currentMarketState = newState;
        }

        // ═══════════════════════════════════════
        //  HTF TREND ENGINE
        // ═══════════════════════════════════════

        private void UpdateHTFTrend()
        {
            if (!EnableHTFFilter || htfBars == null || htfBars.Count < HTFSwingLookback + 2) return;

            int last = htfBars.Count - 1;
            int half = HTFSwingLookback / 2;

            double prevHigh = 0, prevLow = double.MaxValue;
            double currHigh = 0, currLow = double.MaxValue;

            for (int i = last - HTFSwingLookback; i <= last - half; i++)
            {
                if (i < 0 || i >= htfBars.Count) continue;
                if (htfBars.HighPrices[i] > prevHigh) prevHigh = htfBars.HighPrices[i];
                if (htfBars.LowPrices[i]  < prevLow)  prevLow  = htfBars.LowPrices[i];
            }

            for (int i = last - half + 1; i <= last; i++)
            {
                if (i < 0 || i >= htfBars.Count) continue;
                if (htfBars.HighPrices[i] > currHigh) currHigh = htfBars.HighPrices[i];
                if (htfBars.LowPrices[i]  < currLow)  currLow  = htfBars.LowPrices[i];
            }

            bool isHH = currHigh > prevHigh, isHL = currLow > prevLow;
            bool isLH = currHigh < prevHigh, isLL = currLow < prevLow;

            TrendDirection newTrend;
            if      (isHH && isHL) newTrend = TrendDirection.Up;
            else if (isLH && isLL) newTrend = TrendDirection.Down;
            else                    newTrend = TrendDirection.Neutral;

            if (newTrend != htfTrend)
            {
                htfTrend = newTrend;
                string icon = htfTrend == TrendDirection.Up ? "📈" :
                              htfTrend == TrendDirection.Down ? "📉" : "➡️";
                Print($"{icon} HTF Trend changed: {htfTrend}");
            }
        }

        // ═══════════════════════════════════════
        //  SMC ENGINE
        //  BUG FIX #8: DetectFVGs hanya menggunakan candle yang sudah closed
        // ═══════════════════════════════════════

        private void UpdateSMCEngine()
        {
            if (Bars.Count < SwingLookback * 2 + 2) return;

            DetectSwingPoints();
            UpdateMarketStructure();
            DetectOrderBlocks();
            DetectFVGs();        // BUG FIX #8 applied here
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

            // Swing High
            double high = Bars.HighPrices[checkBar];
            bool isSwingHigh = true;
            for (int i = 1; i <= SwingLookback; i++)
            {
                if (Bars.HighPrices[checkBar - i] >= high || Bars.HighPrices[checkBar + i] >= high)
                { isSwingHigh = false; break; }
            }

            if (isSwingHigh)
            {
                bool exists = swingPoints.TakeLast(5).Any(s => s.BarIndex == checkBar);
                if (!exists)
                {
                    swingPoints.Add(new SwingPoint { Type = SwingType.High, Price = high, BarIndex = checkBar });
                    lastSwingHigh      = high;
                    lastSwingHighIndex = checkBar;
                }
            }

            // Swing Low
            double low = Bars.LowPrices[checkBar];
            bool isSwingLow = true;
            for (int i = 1; i <= SwingLookback; i++)
            {
                if (Bars.LowPrices[checkBar - i] <= low || Bars.LowPrices[checkBar + i] <= low)
                { isSwingLow = false; break; }
            }

            if (isSwingLow)
            {
                bool exists = swingPoints.TakeLast(5).Any(s => s.BarIndex == checkBar);
                if (!exists)
                {
                    swingPoints.Add(new SwingPoint { Type = SwingType.Low, Price = low, BarIndex = checkBar });
                    lastSwingLow      = low;
                    lastSwingLowIndex = checkBar;
                }
            }
        }

        private void UpdateMarketStructure()
        {
            if (swingPoints.Count < 2) return;

            SwingPoint latestSH = null, prevSH = null;
            SwingPoint latestSL = null, prevSL = null;

            for (int i = swingPoints.Count - 1; i >= 0; i--)
            {
                if (swingPoints[i].Type == SwingType.High)
                {
                    if (latestSH == null)      latestSH = swingPoints[i];
                    else if (prevSH == null)   prevSH   = swingPoints[i];
                }
                else
                {
                    if (latestSL == null)      latestSL = swingPoints[i];
                    else if (prevSL == null)   prevSL   = swingPoints[i];
                }
                if (latestSH != null && latestSL != null && prevSH != null && prevSL != null) break;
            }

            if (latestSH == null || latestSL == null) return;

            double currentClose = Bars.ClosePrices.LastValue;

            // Initial trend detection
            if (smcTrend == SmcTrend.Undefined)
            {
                if      (prevSH != null && latestSH.Price > prevSH.Price) smcTrend = SmcTrend.Bullish;
                else if (prevSL != null && latestSL.Price < prevSL.Price) smcTrend = SmcTrend.Bearish;
                else if (currentClose > latestSH.Price)                   smcTrend = SmcTrend.Bullish;
                else if (currentClose < latestSL.Price)                   smcTrend = SmcTrend.Bearish;
                else                                                       smcTrend = SmcTrend.Bullish;

                Print($"📊 SMC Initial: {smcTrend}");
            }

            double minMove = Symbol.PipSize * 2; // BUG: menghindari micro-flip dari noise

            if (smcTrend == SmcTrend.Bullish)
            {
                // BOS Bullish: tembus atas
                if (currentClose > latestSH.Price + minMove)
                {
                    if (Math.Abs(latestSH.Price - lastBosLevel) > Symbol.PipSize)
                    {
                        lastBosLevel = latestSH.Price;
                        Print($"📊 BOS ↑ Bullish above {latestSH.Price:F2}");
                    }
                }
                // CHoCH → Bearish
                else if (currentClose < latestSL.Price - minMove)
                {
                    SmcTrend oldTrend = smcTrend;
                    smcTrend = SmcTrend.Bearish;
                    Print($"🔄 CHoCH → Bearish! Broke support {latestSL.Price:F2}");

                    // Set regime change flag untuk entry signal
                    if (oldTrend != SmcTrend.Undefined && oldTrend != smcTrend)
                    {
                        _regimeJustChanged  = true;
                        _regimeDirection    = TradeType.Sell;
                        _lastRegimeChangeBar = Bars.Count - 1;
                    }
                }
            }
            else if (smcTrend == SmcTrend.Bearish)
            {
                // BOS Bearish: tembus bawah
                if (currentClose < latestSL.Price - minMove)
                {
                    if (Math.Abs(latestSL.Price - lastBosLevel) > Symbol.PipSize)
                    {
                        lastBosLevel = latestSL.Price;
                        Print($"📊 BOS ↓ Bearish below {latestSL.Price:F2}");
                    }
                }
                // CHoCH → Bullish
                else if (currentClose > latestSH.Price + minMove)
                {
                    SmcTrend oldTrend = smcTrend;
                    smcTrend = SmcTrend.Bullish;
                    Print($"🔄 CHoCH → Bullish! Broke resistance {latestSH.Price:F2}");

                    // Set regime change flag untuk entry signal
                    if (oldTrend != SmcTrend.Undefined && oldTrend != smcTrend)
                    {
                        _regimeJustChanged  = true;
                        _regimeDirection    = TradeType.Buy;
                        _lastRegimeChangeBar = Bars.Count - 1;
                    }
                }
            }

            // Update prevSmcTrend setelah semua perubahan diproses
            prevSmcTrend = smcTrend;
        }

        private void DetectOrderBlocks()
        {
            int currentBar = Bars.Count - 1;
            if (currentBar < 3) return;

            int checkBar = currentBar - 1; // candle yang baru saja closed

            double prevClose = Bars.ClosePrices[checkBar - 1];
            double prevOpen  = Bars.OpenPrices[checkBar - 1];
            double currClose = Bars.ClosePrices[checkBar];
            double currOpen  = Bars.OpenPrices[checkBar];

            bool prevBearish  = prevClose < prevOpen;
            bool currBullish  = currClose > currOpen;
            double impulsePips = Math.Abs(currClose - currOpen) / Symbol.PipSize;

            // Bullish OB: bearish candle → strong bullish impulse
            if (prevBearish && currBullish && impulsePips >= OBMinImpulsePips)
            {
                bool exists = orderBlocks.Any(ob => ob.BarIndex == checkBar - 1 && ob.IsBullish);
                if (!exists)
                {
                    orderBlocks.Add(new OrderBlock
                    {
                        BarIndex   = checkBar - 1,
                        PriceHigh  = Math.Max(prevOpen, prevClose),
                        PriceLow   = Bars.LowPrices[checkBar - 1],
                        IsBullish  = true,
                        IsMitigated = false,
                        CreatedAt  = currentBar
                    });
                    Print($"🟩 Bull OB: {Bars.LowPrices[checkBar-1]:F2}-{Math.Max(prevOpen,prevClose):F2}");
                }
            }

            bool prevBullish   = prevClose > prevOpen;
            bool currBearish   = currClose < currOpen;
            double impulseDown = Math.Abs(currOpen - currClose) / Symbol.PipSize;

            // Bearish OB: bullish candle → strong bearish impulse
            if (prevBullish && currBearish && impulseDown >= OBMinImpulsePips)
            {
                bool exists = orderBlocks.Any(ob => ob.BarIndex == checkBar - 1 && !ob.IsBullish);
                if (!exists)
                {
                    orderBlocks.Add(new OrderBlock
                    {
                        BarIndex   = checkBar - 1,
                        PriceHigh  = Bars.HighPrices[checkBar - 1],
                        PriceLow   = Math.Min(prevOpen, prevClose),
                        IsBullish  = false,
                        IsMitigated = false,
                        CreatedAt  = currentBar
                    });
                    Print($"🟥 Bear OB: {Math.Min(prevOpen,prevClose):F2}-{Bars.HighPrices[checkBar-1]:F2}");
                }
            }
        }

        private void DetectFVGs()
        {
            int currentBar = Bars.Count - 1;
            // BUG FIX #8: Gunakan i = currentBar - 2 (middle candle dari 3 candle yang SEMUA sudah closed)
            // Sebelumnya i = currentBar - 1, yang mana candle i+1 = currentBar masih berjalan
            int i = currentBar - 2;
            if (i < 1) return;

            // Cap active FVGs
            int activeFVGs = fvgList.Count(f => !f.IsFilled && currentBar - f.BarIndex <= OBMaxAge / 2);
            if (activeFVGs >= MaxActiveFVGs) return;

            double minGapSize = FVGMinPips * Symbol.PipSize;

            // Bullish FVG: gap antara high candle[i-1] dan low candle[i+1]
            double highBefore = Bars.HighPrices[i - 1];
            double lowAfter   = Bars.LowPrices[i + 1]; // i+1 sekarang adalah closed candle
            if (lowAfter > highBefore && (lowAfter - highBefore) >= minGapSize)
            {
                bool exists = fvgList.Any(f => f.BarIndex == i && f.IsBullish);
                if (!exists)
                {
                    fvgList.Add(new FairValueGap
                    {
                        BarIndex  = i,
                        PriceHigh = lowAfter,
                        PriceLow  = highBefore,
                        IsBullish = true,
                        IsFilled  = false,
                        CreatedAt = currentBar
                    });
                }
            }

            // Bearish FVG: gap antara low candle[i-1] dan high candle[i+1]
            double lowBefore  = Bars.LowPrices[i - 1];
            double highAfter  = Bars.HighPrices[i + 1];
            if (lowBefore > highAfter && (lowBefore - highAfter) >= minGapSize)
            {
                bool exists = fvgList.Any(f => f.BarIndex == i && !f.IsBullish);
                if (!exists)
                {
                    fvgList.Add(new FairValueGap
                    {
                        BarIndex  = i,
                        PriceHigh = lowBefore,
                        PriceLow  = highAfter,
                        IsBullish = false,
                        IsFilled  = false,
                        CreatedAt = currentBar
                    });
                }
            }
        }

        private void CheckOBMitigation()
        {
            double close = Bars.ClosePrices.LastValue;
            foreach (var ob in orderBlocks)
            {
                if (ob.IsMitigated) continue;
                if (ob.IsBullish  && close < ob.PriceLow)  { ob.IsMitigated = true; Print($"💥 Bull OB mitigated @ {ob.PriceLow:F2}"); }
                if (!ob.IsBullish && close > ob.PriceHigh) { ob.IsMitigated = true; Print($"💥 Bear OB mitigated @ {ob.PriceHigh:F2}"); }
            }
        }

        private void CheckFVGFill()
        {
            double high = Bars.HighPrices.LastValue;
            double low  = Bars.LowPrices.LastValue;
            foreach (var fvg in fvgList)
            {
                if (fvg.IsFilled) continue;
                if (fvg.IsBullish  && low  <= fvg.PriceLow)  fvg.IsFilled = true;
                if (!fvg.IsBullish && high >= fvg.PriceHigh) fvg.IsFilled = true;
            }
        }

        private bool IsInOrderBlock(double price, TradeType direction)
        {
            int currentBar = Bars.Count - 1;
            double tolerance = 2.0 * Symbol.PipSize;

            foreach (var ob in orderBlocks)
            {
                if (ob.IsMitigated || currentBar - ob.BarIndex > OBMaxAge) continue;
                if (direction == TradeType.Buy && ob.IsBullish)
                {
                    if (price >= ob.PriceLow - tolerance && price <= ob.PriceHigh + tolerance) return true;
                }
                else if (direction == TradeType.Sell && !ob.IsBullish)
                {
                    if (price >= ob.PriceLow - tolerance && price <= ob.PriceHigh + tolerance) return true;
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
                if (fvg.IsFilled || currentBar - fvg.BarIndex > OBMaxAge / 2) continue;
                if (direction == TradeType.Buy && fvg.IsBullish)
                {
                    if (price >= fvg.PriceLow - tolerance && price <= fvg.PriceHigh + tolerance) return true;
                }
                else if (direction == TradeType.Sell && !fvg.IsBullish)
                {
                    if (price >= fvg.PriceLow - tolerance && price <= fvg.PriceHigh + tolerance) return true;
                }
            }
            return false;
        }

        private void PruneSMCObjects()
        {
            int currentBar = Bars.Count - 1;

            if (swingPoints.Count > 100)
                swingPoints.RemoveRange(0, swingPoints.Count - 100);

            orderBlocks.RemoveAll(ob =>  ob.IsMitigated && currentBar - ob.BarIndex > OBMaxAge * 2);
            fvgList.RemoveAll(fvg => fvg.IsFilled  && currentBar - fvg.BarIndex > OBMaxAge / 2);
        }

        // ═══════════════════════════════════════
        //  LUXALGO VISUAL OVERLAYS
        // ═══════════════════════════════════════

        private void DrawVisualPDZones()
        {
            if (lastSwingHigh == 0 || lastSwingLow == double.MaxValue) return;

            double equilibrium = (lastSwingHigh + lastSwingLow) / 2.0;
            int startBar = Math.Min(lastSwingHighIndex, lastSwingLowIndex);
            if (startBar <= 0) startBar = Math.Max(0, Bars.Count - 60);
            int endBar = Bars.Count + 5;

            Chart.RemoveObject("PDTopLine"); Chart.RemoveObject("PDTopTxt");
            Chart.RemoveObject("PDEqLine");  Chart.RemoveObject("PDEqTxt");
            Chart.RemoveObject("PDBotLine"); Chart.RemoveObject("PDBotTxt");

            Chart.DrawTrendLine("PDTopLine", startBar, lastSwingHigh, endBar, lastSwingHigh, Color.Red, 1, LineStyle.Solid);
            Chart.DrawText("PDTopTxt", "Premium", endBar + 1, lastSwingHigh, Color.Red);
            Chart.DrawTrendLine("PDEqLine", startBar, equilibrium, endBar, equilibrium, Color.DarkOrange, 1, LineStyle.LinesDots);
            Chart.DrawText("PDEqTxt", "Equilibrium", endBar + 1, equilibrium, Color.DarkOrange);
            Chart.DrawTrendLine("PDBotLine", startBar, lastSwingLow, endBar, lastSwingLow, Color.DeepSkyBlue, 1, LineStyle.Solid);
            Chart.DrawText("PDBotTxt", "Discount", endBar + 1, lastSwingLow, Color.DeepSkyBlue);
        }

        private void CheckVisualEqhEql()
        {
            if (swingPoints.Count < 3) return;

            var highs = swingPoints.Where(s => s.Type == SwingType.High).Reverse().Take(3).ToList();
            var lows  = swingPoints.Where(s => s.Type == SwingType.Low).Reverse().Take(3).ToList();

            if (highs.Count >= 2)
            {
                double diff = Math.Abs(highs[0].Price - highs[1].Price) / Symbol.PipSize;
                if (diff <= EqhEqlTolerancePips)
                {
                    string name = $"EQH_{highs[1].BarIndex}";
                    Chart.DrawTrendLine(name, highs[1].BarIndex, highs[1].Price, highs[0].BarIndex, highs[1].Price, Color.Red, 1, LineStyle.LinesDots);
                    Chart.DrawText(name + "_txt", "EQH", highs[0].BarIndex + 1, highs[1].Price, Color.Red);
                }
            }

            if (lows.Count >= 2)
            {
                double diff = Math.Abs(lows[0].Price - lows[1].Price) / Symbol.PipSize;
                if (diff <= EqhEqlTolerancePips)
                {
                    string name = $"EQL_{lows[1].BarIndex}";
                    Chart.DrawTrendLine(name, lows[1].BarIndex, lows[1].Price, lows[0].BarIndex, lows[1].Price, Color.DeepSkyBlue, 1, LineStyle.LinesDots);
                    Chart.DrawText(name + "_txt", "EQL", lows[0].BarIndex + 1, lows[1].Price, Color.DeepSkyBlue);
                }
            }
        }

        // ═══════════════════════════════════════
        //  SMC VISUALS
        // ═══════════════════════════════════════

        private void DrawSMCVisuals()
        {
            int currentBar = Bars.Count - 1;

            var latestBullOB = orderBlocks.LastOrDefault(ob =>  ob.IsBullish && !ob.IsMitigated && currentBar - ob.BarIndex <= OBMaxAge);
            var latestBearOB = orderBlocks.LastOrDefault(ob => !ob.IsBullish && !ob.IsMitigated && currentBar - ob.BarIndex <= OBMaxAge);

            foreach (var ob in orderBlocks)
            {
                string obName  = $"OB_{ob.BarIndex}_{(ob.IsBullish ? "B" : "S")}";
                string lblName = $"OBL_{ob.BarIndex}";
                try { Chart.RemoveObject(obName); Chart.RemoveObject(lblName); } catch { }

                if (ob != latestBullOB && ob != latestBearOB) continue;
                if (ob.IsMitigated || currentBar - ob.BarIndex > OBMaxAge) continue;

                Color obColor = ob.IsBullish
                    ? Color.FromArgb(50, 0, 200, 100)
                    : Color.FromArgb(50, 200, 50, 50);

                try
                {
                    var rect = Chart.DrawRectangle(obName, ob.BarIndex, ob.PriceHigh,
                        Math.Min(ob.BarIndex + OBMaxAge, currentBar + 10), ob.PriceLow, obColor);
                    if (rect != null) { rect.IsFilled = true; rect.Thickness = 1; }

                    string lblText = ob.IsBullish ? "OB 🟩" : "OB 🟥";
                    var lbl = Chart.DrawText(lblName, lblText, ob.BarIndex,
                        ob.IsBullish ? ob.PriceLow : ob.PriceHigh,
                        ob.IsBullish ? Color.FromArgb(200, 0, 200, 100) : Color.FromArgb(200, 200, 50, 50));
                    if (lbl != null) { lbl.FontSize = 7; lbl.IsBold = true; }
                }
                catch { }
            }

            var latestBullFVG = fvgList.LastOrDefault(f =>  f.IsBullish && !f.IsFilled && currentBar - f.BarIndex <= OBMaxAge / 2);
            var latestBearFVG = fvgList.LastOrDefault(f => !f.IsBullish && !f.IsFilled && currentBar - f.BarIndex <= OBMaxAge / 2);

            foreach (var fvg in fvgList)
            {
                string fvgName = $"FVG_{fvg.BarIndex}_{(fvg.IsBullish ? "B" : "S")}";
                try { Chart.RemoveObject(fvgName); } catch { }

                if (fvg != latestBullFVG && fvg != latestBearFVG) continue;
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

            // Swing points (last 20)
            int drawFrom = Math.Max(0, swingPoints.Count - 20);
            for (int s = drawFrom; s < swingPoints.Count; s++)
            {
                var sp     = swingPoints[s];
                string name = $"SP_{sp.BarIndex}_{sp.Type}";
                try
                {
                    Chart.RemoveObject(name);
                    string marker  = sp.Type == SwingType.High ? "▼" : "▲";
                    Color spColor  = sp.Type == SwingType.High
                        ? Color.FromArgb(180, 255, 100, 100)
                        : Color.FromArgb(180, 100, 255, 100);
                    var txt = Chart.DrawText(name, marker, sp.BarIndex, sp.Price, spColor);
                    if (txt != null) { txt.FontSize = 8; txt.IsBold = true; }
                }
                catch { }
            }

            // SMC Trend label
            try
            {
                Color trendColor = smcTrend == SmcTrend.Bullish ? Color.LimeGreen :
                                   smcTrend == SmcTrend.Bearish ? Color.Tomato : Color.Gray;
                Chart.DrawStaticText("SMC_TREND", $"SMC: {smcTrend}", VerticalAlignment.Top, HorizontalAlignment.Right, trendColor);
            }
            catch { }
        }

        // ═══════════════════════════════════════
        //  DRAWING
        // ═══════════════════════════════════════

        private void UpdateVolumeProfileVisuals()
        {
            if (!ShowVolumeProfile || candleFootprints == null || candleFootprints.Count == 0)
            {
                // Clear all if disabled
                if (drawnVpObjects != null && drawnVpObjects.Count > 0)
                {
                    foreach (var objName in drawnVpObjects)
                        Chart.RemoveObject(objName);
                    drawnVpObjects.Clear();
                }
                return;
            }

            lastVpRedrawTime = Server.Time;

            int currentBar = Bars.Count - 1;
            int startBar = Math.Max(0, currentBar - VpLookback);

            // 1. Bin volumes by price levels
            var priceVolume = new Dictionary<double, PriceVolumeBin>();
            double priceRange = Chart.TopY - Chart.BottomY;
            if (priceRange <= 0) return;
            double stepSize = priceRange / VpBins;

            for (int bar = startBar; bar <= currentBar; bar++)
            {
                if (candleFootprints.TryGetValue(bar, out var fp))
                {
                    foreach (var level in fp.PriceLevels.Values)
                    {
                        double binnedPrice = Math.Round(level.Price / stepSize) * stepSize;
                        binnedPrice = Math.Round(binnedPrice, Symbol.Digits); // Avoid floating point inaccuracies

                        if (!priceVolume.TryGetValue(binnedPrice, out var bin))
                        {
                            bin = new PriceVolumeBin { PriceLevel = binnedPrice };
                            priceVolume[binnedPrice] = bin;
                        }

                        bin.BuyVolume += level.BuyCount;
                        bin.SellVolume += level.SellCount;
                    }
                }
            }

            if (priceVolume.Count == 0)
            {
                // Clear all if no data
                if (drawnVpObjects != null && drawnVpObjects.Count > 0)
                {
                    foreach (var objName in drawnVpObjects)
                        Chart.RemoveObject(objName);
                    drawnVpObjects.Clear();
                }
                return;
            }

            // 2. Find max volume for scaling
            long maxVolume = priceVolume.Values.Max(b => b.TotalVolume);
            if (maxVolume <= 0)
                return;

            // 2.1 Calculate POC, VAH, VAL (70% Value Area)
            var sortedBins = priceVolume.Values.OrderBy(b => b.PriceLevel).ToList();
            var pocBin = sortedBins.First(b => b.TotalVolume == maxVolume);
            currentPoc = pocBin.PriceLevel;

            long totalVolume = sortedBins.Sum(b => b.TotalVolume);
            double targetVolume = totalVolume * 0.70;

            int pocIndex = sortedBins.IndexOf(pocBin);
            int lowIndex = pocIndex;
            int highIndex = pocIndex;
            long accumulatedVolume = pocBin.TotalVolume;

            while (accumulatedVolume < targetVolume)
            {
                bool canGoLower = lowIndex > 0;
                bool canGoHigher = highIndex < sortedBins.Count - 1;

                if (!canGoLower && !canGoHigher)
                    break;

                if (canGoLower && canGoHigher)
                {
                    long lowVol = sortedBins[lowIndex - 1].TotalVolume;
                    long highVol = sortedBins[highIndex + 1].TotalVolume;

                    if (lowVol >= highVol)
                    {
                        lowIndex--;
                        accumulatedVolume += lowVol;
                    }
                    else
                    {
                        highIndex++;
                        accumulatedVolume += highVol;
                    }
                }
                else if (canGoLower)
                {
                    lowIndex--;
                    accumulatedVolume += sortedBins[lowIndex].TotalVolume;
                }
                else if (canGoHigher)
                {
                    highIndex++;
                    accumulatedVolume += sortedBins[highIndex].TotalVolume;
                }
            }

            currentVal = sortedBins[lowIndex].PriceLevel;
            currentVah = sortedBins[highIndex].PriceLevel;

            // 3. Determine target right bar anchor
            int rightBar = Chart.FirstVisibleBarIndex + Chart.MaxVisibleBars - 1;
            rightBar = Math.Max(rightBar, VpWidthBars + 2); // Avoid negative indices

            // Keep track of names we draw in this cycle
            var currentCycleObjects = new HashSet<string>();

            // 3.1 Draw Value Area lines if enabled
            if (ShowValueAreaLines)
            {
                int lineStart = rightBar - VpWidthBars;
                int lineEnd = rightBar;

                // POC (Gold Line)
                string namePoc = "VP_LINE_POC";
                currentCycleObjects.Add(namePoc);
                var pocLine = Chart.DrawTrendLine(namePoc, lineStart, currentPoc, lineEnd, currentPoc, Color.Gold, 2, LineStyle.Solid);
                if (pocLine != null)
                {
                    string namePocTxt = "VP_TXT_POC";
                    currentCycleObjects.Add(namePocTxt);
                    Chart.DrawText(namePocTxt, "POC", lineStart, currentPoc, Color.Gold);
                }

                // VAH (Red Dashed Line)
                string nameVah = "VP_LINE_VAH";
                currentCycleObjects.Add(nameVah);
                var vahLine = Chart.DrawTrendLine(nameVah, lineStart, currentVah, lineEnd, currentVah, Color.Coral, 1, LineStyle.Lines);
                if (vahLine != null)
                {
                    string nameVahTxt = "VP_TXT_VAH";
                    currentCycleObjects.Add(nameVahTxt);
                    Chart.DrawText(nameVahTxt, "VAH", lineStart, currentVah, Color.Coral);
                }

                // VAL (Blue Dashed Line)
                string nameVal = "VP_LINE_VAL";
                currentCycleObjects.Add(nameVal);
                var valLine = Chart.DrawTrendLine(nameVal, lineStart, currentVal, lineEnd, currentVal, Color.SkyBlue, 1, LineStyle.Lines);
                if (valLine != null)
                {
                    string nameValTxt = "VP_TXT_VAL";
                    currentCycleObjects.Add(nameValTxt);
                    Chart.DrawText(nameValTxt, "VAL", lineStart, currentVal, Color.SkyBlue);
                }
            }

            // 4. Render/Update dual-colored horizontal histogram bars
            for (int i = 0; i < sortedBins.Count; i++)
            {
                var bin = sortedBins[i];
                int totalWidth = (int)Math.Round((double)bin.TotalVolume / maxVolume * VpWidthBars);
                if (totalWidth <= 0)
                    continue;

                int buyWidth = (int)Math.Round((double)bin.BuyVolume / bin.TotalVolume * totalWidth);
                int sellWidth = totalWidth - buyWidth;

                double halfHeight = (stepSize / 2.0) * VpHeightMultiplier;
                double priceMin = bin.PriceLevel - halfHeight;
                double priceMax = bin.PriceLevel + halfHeight;

                // Color overrides with opacity
                Color buyColor = Color.FromArgb(VpOpacity, VpBuyColor.R, VpBuyColor.G, VpBuyColor.B);
                Color sellColor = Color.FromArgb(VpOpacity, VpSellColor.R, VpSellColor.G, VpSellColor.B);

                // Draw/Update Buy part (on the left side of the profile bar)
                if (buyWidth > 0)
                {
                    string nameBuy = $"VP_B_{i}";
                    int buyStart = rightBar - totalWidth;
                    int buyEnd = buyStart + buyWidth;

                    currentCycleObjects.Add(nameBuy);
                    var rect = Chart.DrawRectangle(nameBuy, buyStart, priceMin, buyEnd, priceMax, buyColor);
                    if (rect != null)
                    {
                        rect.IsFilled = true;
                        rect.Thickness = 1;
                    }
                }

                // Draw/Update Sell part (on the right side of the profile bar, ending at rightBar)
                if (sellWidth > 0)
                {
                    string nameSell = $"VP_S_{i}";
                    int sellStart = rightBar - totalWidth + buyWidth;
                    int sellEnd = rightBar;

                    currentCycleObjects.Add(nameSell);
                    var rect = Chart.DrawRectangle(nameSell, sellStart, priceMin, sellEnd, priceMax, sellColor);
                    if (rect != null)
                    {
                        rect.IsFilled = true;
                        rect.Thickness = 1;
                    }
                }
            }

            // 5. Remove obsolete objects that were drawn last time but not in this cycle
            foreach (var objName in drawnVpObjects)
            {
                if (!currentCycleObjects.Contains(objName))
                {
                    Chart.RemoveObject(objName);
                }
            }

            // 6. Save current names for the next cycle
            drawnVpObjects = currentCycleObjects.ToList();
        }

        private void DrawCurrentCandleBubbles(CandleFootprint fp)
        {
            foreach (var lvl in fp.PriceLevels)
            {
                int delta = lvl.Value.BuyCount - lvl.Value.SellCount;
                if (Math.Abs(delta) >= MinDeltaPerLevel && lvl.Value.TotalCount >= MinVolumePerLevel)
                    DrawFootprintBubble(fp.BarIndex, lvl.Value, delta, delta > 0);
            }
        }

        private void DrawFootprintBubble(int barIndex, PriceLevel level, int delta, bool isBuy)
        {
            int absDelta  = Math.Abs(delta);
            Color base_c  = isBuy ? Color.Green : Color.Red;
            Color color   = Color.FromArgb(BubbleOpacity, base_c.R, base_c.G, base_c.B);

            double high = Bars.HighPrices[barIndex];
            double low  = Bars.LowPrices[barIndex];
            double range = high - low;
            if (range < Symbol.PipSize) return;

            double sizePct = Math.Min(1.5, 0.05 + (absDelta / 20.0) * 1.0);
            double radius  = range * sizePct * 0.8;
            double minR    = Symbol.PipSize >= 0.1 ? range * 0.005 : Symbol.PipSize * 2;
            if (radius < minR) radius = minR;

            DateTime barTime = Bars.OpenTimes[barIndex];
            TimeSpan barDur  = barIndex > 0
                ? Bars.OpenTimes[barIndex] - Bars.OpenTimes[barIndex - 1]
                : TimeSpan.FromMinutes(1);
            TimeSpan width   = TimeSpan.FromTicks((long)(barDur.Ticks * 0.5));

            string name = $"FB_{barIndex}_{level.Price:F2}";
            try
            {
                var e = Chart.DrawEllipse(name,
                    barTime.Subtract(width), level.Price + radius,
                    barTime.Add(width),      level.Price - radius, color);
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

        private void LogDashboard()
        {
            int currentBar = Bars.Count - 1;
            double price   = Bars.ClosePrices.LastValue;

            Print("─────────────────────────────────────────────");
            Print($"  BAR #{currentBar} | {Bars.OpenTimes.LastValue:HH:mm:ss} | Price: {price:F2}");

            if (EnableSMAFilter)
            {
                double sv = sma.Result.LastValue;
                string pos = price > sv ? "ABOVE ▲" : "BELOW ▼";
                Print($"  📈 SMA({SmaPeriod}): {sv:F2} | {pos} ({(price - sv) / Symbol.PipSize:+0.0;-0.0}p)");
            }

            if (EnableHTFFilter)
            {
                string htfIcon = htfTrend == TrendDirection.Up ? "📈 UP" :
                                 htfTrend == TrendDirection.Down ? "📉 DOWN" : "➡️ NEUTRAL";
                Print($"  🕐 HTF({HTFTimeframe}): {htfIcon}");
            }

            if (EnableSMC)
            {
                string smcIcon = smcTrend == SmcTrend.Bullish ? "🟢 BULLISH" :
                                 smcTrend == SmcTrend.Bearish ? "🔴 BEARISH" : "⚪ UNDEFINED";
                Print($"  🏦 SMC: {smcIcon} | OB: {orderBlocks.Count(o => !o.IsMitigated)} | FVG: {fvgList.Count(f => !f.IsFilled)}");
            }

            int virginCount = virginClusters.Count;
            int buyZones  = clusterZones.Count(z => virginClusters.Contains(z.ZoneId) && z.Dominance == ClusterDominance.BuyDominated);
            int sellZones = clusterZones.Count(z => virginClusters.Contains(z.ZoneId) && z.Dominance == ClusterDominance.SellDominated);
            Print($"  🫧 Zones: {virginCount} virgin ({buyZones} Buy / {sellZones} Sell) | Total: {clusterZones.Count}");

            var openPos  = Positions.FindAll(BotLabel, SymbolName);
            double pnl   = Account.Equity - dailyStartBalance;
            Print($"  💰 Pos: {openPos.Length}/{MaxPositions} | Trades: {dailyTradeCount}/{MaxTradesPerDay} | P&L: {pnl:+0.00;-0.00}");
            Print($"  🧠 Markov: {currentMarketState} | Session: {(IsValidSessionToTrade() ? "✅" : "🚫")}");
            Print("─────────────────────────────────────────────");
        }

        // ═══════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════

        private int FindBarIndex(DateTime time)
        {
            int barCount = Bars.Count;
            if (barCount == 0) return 0;

            if (lastKnownBarIndex >= 0 && lastKnownBarIndex < barCount)
            {
                DateTime start = Bars.OpenTimes[lastKnownBarIndex];
                DateTime end   = lastKnownBarIndex < barCount - 1
                    ? Bars.OpenTimes[lastKnownBarIndex + 1] : Server.Time;
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

        private void PruneOldFootprints()
        {
            int lookback = Math.Max(ClusterLookback * 2, ShowVolumeProfile ? VpLookback + 5 : 0);
            int threshold = Bars.Count - 1 - lookback;
            if (threshold <= 0) return;

            var toRemove = candleFootprints.Keys.Where(k => k < threshold).ToList();
            foreach (var k in toRemove)
                candleFootprints.Remove(k);

            // BUG FIX #6: Bersihkan juga clusterZones yang terlalu tua dan tidak relevant
            int currentBar = Bars.Count - 1;
            int maxZones = 500;
            if (clusterZones.Count > maxZones)
            {
                // Hapus zona paling lama yang bukan virgin
                var toRemoveZones = clusterZones
                    .Where(z => !virginClusters.Contains(z.ZoneId))
                    .OrderBy(z => z.LastBarIndex)
                    .Take(clusterZones.Count - maxZones)
                    .ToList();

                foreach (var z in toRemoveZones)
                {
                    try { Chart.RemoveObject($"CZ_{z.ZoneId}"); Chart.RemoveObject($"V_{z.ZoneId}"); } catch { }
                    clusterZones.Remove(z);
                }
            }

            if (EnableSMC)
            {
                foreach (var ob in orderBlocks.Where(o => o.IsMitigated && currentBar - o.BarIndex > OBMaxAge * 2).ToList())
                {
                    try
                    {
                        Chart.RemoveObject($"OB_{ob.BarIndex}_{(ob.IsBullish ? "B" : "S")}");
                        Chart.RemoveObject($"OBL_{ob.BarIndex}");
                    }
                    catch { }
                }
            }
        }

        // ═══════════════════════════════════════
        //  ENUMS & DATA CLASSES
        // ═══════════════════════════════════════

        public enum TrendDirection   { Up, Down, Neutral }
        public enum ClusterDominance { BuyDominated, SellDominated, Consolidated }
        public enum SmcTrend         { Bullish, Bearish, Ranging, Undefined }
        public enum SwingType        { High, Low }
        public enum MarketState      { Bullish, Bearish, Flat }

        private class CandleFootprint
        {
            public int BarIndex { get; set; }
            public DateTime BarTime { get; set; }
            public Dictionary<double, PriceLevel> PriceLevels { get; set; } = new Dictionary<double, PriceLevel>();
            public int TotalTicks     { get; set; }
            public int TotalBuyCount  { get; set; }
            public int TotalSellCount { get; set; }
            public double LastBid { get; set; }
            public double LastAsk { get; set; }
            public bool IsFinalized   { get; set; }
        }

        private class PriceLevel
        {
            public double Price    { get; set; }
            public int BuyCount    { get; set; }
            public int SellCount   { get; set; }
            public int TotalCount  { get; set; }
        }

        private class ClusterZone
        {
            public string ZoneId          { get; set; }
            public double CenterPrice     { get; set; }
            public double PriceMin        { get; set; }
            public double PriceMax        { get; set; }
            public int FirstBarIndex      { get; set; }
            public int LastBarIndex       { get; set; }
            public int TotalBuyBubbles    { get; set; }
            public int TotalSellBubbles   { get; set; }
            public int TotalBuyVolume     { get; set; }
            public int TotalSellVolume    { get; set; }
            public double BuyPercent      { get; set; }
            public ClusterDominance Dominance { get; set; }
            public bool IsVirgin          { get; set; } = true;
        }

        private class SwingPoint
        {
            public SwingType Type  { get; set; }
            public double Price    { get; set; }
            public int BarIndex    { get; set; }
        }

        private class OrderBlock
        {
            public int BarIndex     { get; set; }
            public double PriceHigh { get; set; }
            public double PriceLow  { get; set; }
            public bool IsBullish   { get; set; }
            public bool IsMitigated { get; set; }
            public int CreatedAt    { get; set; }
        }

        private class FairValueGap
        {
            public int BarIndex     { get; set; }
            public double PriceHigh { get; set; }
            public double PriceLow  { get; set; }
            public bool IsBullish   { get; set; }
            public bool IsFilled    { get; set; }
            public int CreatedAt    { get; set; }
        }

        private class PriceVolumeBin
        {
            public double PriceLevel { get; set; }
            public long BuyVolume    { get; set; }
            public long SellVolume   { get; set; }
            public long TotalVolume => BuyVolume + SellVolume;
        }
    }
}