using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    [Indicator(IsOverlay = true, TimeZone = TimeZones.UTC, AccessRights = AccessRights.None)]
    public class OrderFlowBigTraders : Indicator
    {
        [Parameter("Min Delta Per Level", DefaultValue = 3, MinValue = 1)]
        public int MinDeltaPerLevel { get; set; }

        [Parameter("Min Volume Per Level", DefaultValue = 1, MinValue = 1)]
        public int MinVolumePerLevel { get; set; }

        [Parameter("Show Delta Labels", DefaultValue = true)]
        public bool ShowDeltaLabels { get; set; }

        [Parameter("Bubble Opacity (%)", DefaultValue = 127, MinValue = 10, MaxValue = 255)]
        public int BubbleOpacity { get; set; }

        [Parameter("Buy Bubble Color", DefaultValue = "Green")]
        public string BuyBubbleColor { get; set; }

        [Parameter("Sell Bubble Color", DefaultValue = "Red")]  
        public string SellBubbleColor { get; set; }

        [Parameter("Bubble Size Multiplier", DefaultValue = 0.8, MinValue = 0.01, MaxValue = 2.0)]
        public double BubbleSizeMultiplier { get; set; }

        private Dictionary<int, CandleFootprint> candleFootprints;
        private Ticks ticks;
        private int bubblesDrawn = 0;
        private double maxDeltaPerLevel = 0;
        private int maxVolumePerLevel = 0;  // Track max volume for normalization

        protected override void Initialize()
        {
            candleFootprints = new Dictionary<int, CandleFootprint>();
            ticks = MarketData.GetTicks();

            Print("═══════════════════════════════════════");
            Print("  FOOTPRINT BUBBLE CHART");
            Print("═══════════════════════════════════════");
            Print("Symbol: " + SymbolName);
            Print("Min Delta/Level: " + MinDeltaPerLevel);
            Print("Min Volume/Level: " + MinVolumePerLevel);
            Print("═══════════════════════════════════════");

            // Subscribe to new ticks
            ticks.Tick += OnNewTick;

            // Process existing ticks
            ProcessHistoricalTicks();
        }

        private void OnNewTick(TicksTickEventArgs obj)
        {
            if (ticks.Count > 0)
            {
                var latestTick = ticks.Last();
                ProcessSingleTick(latestTick);
                
                // 🔥 FORCE REDRAW IMMEDIATELY!
                int currentBarIndex = Bars.Count - 1;
                if (candleFootprints.ContainsKey(currentBarIndex))
                {
                    var currentFootprint = candleFootprints[currentBarIndex];
                    
                    // Redraw current candle bubbles SETIAP TICK
                    DrawCurrentCandleBubbles(currentFootprint);
                }
            }
        }

        private void ProcessHistoricalTicks()
        {
            int tickCount = ticks.Count;
            Print("Processing " + tickCount + " historical ticks...");

            if (tickCount == 0)
            {
                Print("⚠️ No tick data available");
                return;
            }

            // Process last 20000 ticks
            int start = Math.Max(0, tickCount - 20000);

            for (int i = start; i < tickCount; i++)
            {
                ProcessSingleTick(ticks[i]);

                if ((i - start) % 5000 == 0 && i > start)
                {
                    Print("Progress: " + (i - start) + " / " + (tickCount - start));
                }
            }

            // Finalize all candles
            FinalizeAllCandles();

            Print("✓ Processing complete!");
            Print("✓ Bubbles drawn: " + bubblesDrawn);
        }

        private void ProcessSingleTick(Tick tick)
        {
            // Find which bar this tick belongs to
            int barIndex = FindBarIndex(tick.Time);
            if (barIndex < 0)
                return;

            // Create footprint if doesn't exist
            if (!candleFootprints.ContainsKey(barIndex))
            {
                candleFootprints[barIndex] = new CandleFootprint
                {
                    BarIndex = barIndex,
                    BarTime = Bars.OpenTimes[barIndex]
                };
            }

            CandleFootprint footprint = candleFootprints[barIndex];

            // Skip if already finalized
            if (footprint.IsFinalized)
                return;

            // Determine tick direction
            bool isBuyTick = false;
            bool isSellTick = false;

            // Uptick rule: Ask increased = Buy
            if (footprint.LastAsk > 0 && tick.Ask > footprint.LastAsk)
            {
                isBuyTick = true;
            }
            // Downtick rule: Bid decreased = Sell
            else if (footprint.LastBid > 0 && tick.Bid < footprint.LastBid)
            {
                isSellTick = true;
            }
            // Zero tick: use bid/ask spread
            else if (footprint.LastAsk > 0)
            {
                double midLast = (footprint.LastBid + footprint.LastAsk) / 2.0;
                double midNow = (tick.Bid + tick.Ask) / 2.0;

                if (midNow > midLast)
                    isBuyTick = true;
                else if (midNow < midLast)
                    isSellTick = true;
            }

            // Round price to nearest pip for grouping
            double price = isBuyTick ? tick.Ask : tick.Bid;
            double roundedPrice = RoundToPip(price);

            // Create price level if doesn't exist
            if (!footprint.PriceLevels.ContainsKey(roundedPrice))
            {
                footprint.PriceLevels[roundedPrice] = new PriceLevel
                {
                    Price = roundedPrice
                };
            }

            PriceLevel level = footprint.PriceLevels[roundedPrice];

            // Update counts
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

            level.TotalCount++;
            footprint.TotalTicks++;
            footprint.LastBid = tick.Bid;
            footprint.LastAsk = tick.Ask;
            footprint.LastTime = tick.Time;
        }



        private HashSet<int> drawnCandles = new HashSet<int>();  // Track which candles have been drawn
        
        private Dictionary<int, int> lastDrawnTickCountPerCandle = new Dictionary<int, int>();

        public override void Calculate(int index)
        {
            CheckCompletedCandles();
            DrawVisibleHistoricalCandles();
            
            int currentBarIndex = Bars.Count - 1;
            if (candleFootprints.ContainsKey(currentBarIndex))
            {
                var currentFootprint = candleFootprints[currentBarIndex];
                int currentTicks = currentFootprint.TotalTicks;
                
                // Get last drawn count for THIS candle specifically
                int lastCount = 0;
                if (lastDrawnTickCountPerCandle.ContainsKey(currentBarIndex))
                    lastCount = lastDrawnTickCountPerCandle[currentBarIndex];
                
                // Redraw if new ticks arrived
                if (currentTicks > lastCount)
                {
                    DrawCurrentCandleBubbles(currentFootprint);
                    lastDrawnTickCountPerCandle[currentBarIndex] = currentTicks;
                }
            }
        }
         



         private void DrawVisibleHistoricalCandles()
        {
            int lastVisibleIndex = Bars.Count - 1;
            int firstVisibleIndex = Math.Max(0, lastVisibleIndex - 200);
            
            foreach (var kvp in candleFootprints)
            {
                int barIndex = kvp.Key;
                var footprint = kvp.Value;
                
                // Skip current candle (handled by real-time logic)
                if (barIndex == Bars.Count - 1)
                    continue;
                
                // Skip already drawn OR not visible
                if (drawnCandles.Contains(barIndex) || 
                    barIndex < firstVisibleIndex)
                    continue;
                
                // Draw completed candles IMMEDIATELY (remove IsFinalized check!)
                foreach (var levelKvp in footprint.PriceLevels)
                {
                    PriceLevel level = levelKvp.Value;
                    int delta = level.BuyCount - level.SellCount;
                    int absDelta = Math.Abs(delta);

                    if (absDelta >= MinDeltaPerLevel && level.TotalCount >= MinVolumePerLevel)
                    {
                        DrawFootprintBubble(barIndex, level, delta, level.BuyCount > level.SellCount);
                    }
                }
                
                drawnCandles.Add(barIndex);
            }
        }

        private void CheckCompletedCandles()
        {
            DateTime now = Server.Time;
            int currentBarIndex = Bars.Count - 1;

            var completedCandles = candleFootprints
                .Where(kvp => kvp.Key < currentBarIndex && !kvp.Value.IsFinalized)
                .ToList();

            foreach (var kvp in completedCandles)
            {
                FinalizeCandle(kvp.Value);
            }
        }

        private void FinalizeAllCandles()
        {
            var allCandles = candleFootprints
                .Where(kvp => !kvp.Value.IsFinalized)
                .OrderBy(kvp => kvp.Key)
                .ToList();

            Print("Finalizing " + allCandles.Count + " candles (lazy mode - no draw yet)...");

            foreach (var kvp in allCandles)
            {
                // Mark as finalized but DON'T draw yet (lazy loading!)
                kvp.Value.IsFinalized = true;
            }
            
            Print("✓ Candles finalized (bubbles will draw on-demand when visible)");
        }

        private void FinalizeCandle(CandleFootprint footprint)
        {
            footprint.IsFinalized = true;

            // Skip if not enough data
            if (footprint.TotalTicks < 10)
                return;

            // Process each price level
            foreach (var levelKvp in footprint.PriceLevels)
            {
                PriceLevel level = levelKvp.Value;

                // Calculate delta
                int delta = level.BuyCount - level.SellCount;
                int absDelta = Math.Abs(delta);
                bool isBuyPressure = delta > 0;

                // Check if meets criteria
                if (absDelta < MinDeltaPerLevel || level.TotalCount < MinVolumePerLevel)
                    continue;

                // Draw bubble for this price level
                DrawFootprintBubble(footprint.BarIndex, level, delta, isBuyPressure);

                bubblesDrawn++;
                if (absDelta > maxDeltaPerLevel)
                    maxDeltaPerLevel = absDelta;
                if (level.TotalCount > maxVolumePerLevel)
                    maxVolumePerLevel = level.TotalCount;
            }

            // Log significant candles
            int totalDelta = footprint.TotalBuyCount - footprint.TotalSellCount;
            if (Math.Abs(totalDelta) > 50)
            {
                string dir = totalDelta > 0 ? "BUY" : "SELL";
                Print($"💎 {dir} Candle | Δ{totalDelta:+#;-#;0} | Levels:{footprint.PriceLevels.Count} | {footprint.BarTime:HH:mm:ss}");
            }
        }

        private void DrawCurrentCandleBubbles(CandleFootprint footprint)
        {
            // Clear previous bubbles
            string prefix = $"Footprint_{footprint.BarIndex}_";
            var objectsToRemove = Chart.Objects
                .Where(obj => obj.Name.StartsWith(prefix))
                .ToList();
            
            foreach (var obj in objectsToRemove)
            {
                Chart.RemoveObject(obj.Name);
            }
            
            // Draw SEMUA levels yang memenuhi kriteria (TANPA cek IsFinalized!)
            foreach (var levelKvp in footprint.PriceLevels)
            {
                PriceLevel level = levelKvp.Value;
                int delta = level.BuyCount - level.SellCount;
                int absDelta = Math.Abs(delta);
                bool isBuyPressure = delta > 0;

                if (absDelta >= MinDeltaPerLevel && level.TotalCount >= MinVolumePerLevel)
                {
                    DrawFootprintBubble(footprint.BarIndex, level, delta, isBuyPressure);
                }
            }
        }

        private void DrawFootprintBubble(int barIndex, PriceLevel level, int delta, bool isBuy)
        {
            int absDelta = Math.Abs(delta);
            
            // Use flat configurable opacity for all bubbles
            Color baseColor = isBuy ? GetColorFromString(BuyBubbleColor) : GetColorFromString(SellBubbleColor);
            
            // Flat opacity - same for all bubbles
            Color color = Color.FromArgb(BubbleOpacity, baseColor.R, baseColor.G, baseColor.B);

            // Get candle data
            double candleHigh = Bars.HighPrices[barIndex];
            double candleLow = Bars.LowPrices[barIndex];
            double candleOpen = Bars.OpenPrices[barIndex];
            double candleClose = Bars.ClosePrices[barIndex];
            double candleRange = candleHigh - candleLow;
            
            // PURE PRICE-BASED SIZING for zoom adaptivity!
            // All bubble dimensions calculated as % of candle body HEIGHT (price range)
            // This makes bubbles ALWAYS proportional to visual candle size
            
            // Define bubble size tiers (% of candle range)
            // Δ1-5:   5-15% of candle = TINY dots inside
            // Δ6-10:  15-35% of candle = Small inside  
            // Δ11-15: 35-60% of candle = Medium, approaching edge
            // Δ16-20: 60-100% of candle = Large, at/near edge
            // Δ21+:   100-150% of candle = HUGE, exceeding candle
            
            double bubbleSizePercent;
            
            // Smooth scaling based on delta - INCREASED FOR ZOOM-OUT VISIBILITY
            if (absDelta <= 5)
            {
                // TINY: 5% to 20% of candle (was 2-8%) - 2.5x bigger!
                bubbleSizePercent = 0.05 + (absDelta / 5.0) * 0.15;
            }
            else if (absDelta <= 10)
            {
                // SMALL: 20% to 50% of candle (was 15-35%) - More visible!
                bubbleSizePercent = 0.20 + ((absDelta - 5) / 5.0) * 0.30;
            }
            else if (absDelta <= 15)
            {
                // MEDIUM: 50% to 80% of candle (was 35-60%)
                bubbleSizePercent = 0.50 + ((absDelta - 10) / 5.0) * 0.30;
            }
            else if (absDelta <= 20)
            {
                // LARGE: 80% to 120% of candle (was 60-100%)
                bubbleSizePercent = 0.80 + ((absDelta - 15) / 5.0) * 0.40;
            }
            else
            {
                // HUGE: 120% to 180% of candle (was 100-150%) - VERY PROMINENT!
                double excess = Math.Min(10, absDelta - 20); // Cap at Δ30
                bubbleSizePercent = 1.20 + (excess / 10.0) * 0.60;
            }
            
            // Apply user multiplier (but cap the result)
            bubbleSizePercent *= BubbleSizeMultiplier;
            bubbleSizePercent = Math.Min(1.8, bubbleSizePercent); // Max 180% of candle
            
            // Volume boost (small)
            if (level.TotalCount > 30)
            {
                double volumeBoost = Math.Min(0.10, (level.TotalCount - 30) / 400.0);
                bubbleSizePercent *= (1.0 + volumeBoost);
            }
            
            // Calculate bubble radius in PRICE terms
            double bubbleRadius = candleRange * bubbleSizePercent;
            
            // Ensure minimum visibility - dynamic based on instrument
            double minRadius;
            if (Symbol.PipSize >= 0.1)  // Crypto/commodities
            {
                // Use % of candle range instead of pip size
                minRadius = candleRange * 0.005;  // 0.5% of candle minimum
            }
            else  // Forex
            {
                minRadius = Symbol.PipSize * 2;  // 2 pips minimum
            }
            
            if (bubbleRadius < minRadius)
                bubbleRadius = minRadius;
            
            // Get bar time and duration
            DateTime barTime = Bars.OpenTimes[barIndex];
            TimeSpan barDuration = barIndex > 0 ?
                Bars.OpenTimes[barIndex] - Bars.OpenTimes[barIndex - 1] :
                TimeSpan.FromMinutes(1);
            
            // DYNAMIC WIDTH for circular appearance
            // Small bubbles: narrow width (30%)
            // Large bubbles: much wider (up to 150%!) - EXCEED candle width!
            double widthFraction;
            
            if (bubbleSizePercent <= 0.20)
                widthFraction = 0.30;  // Tiny: 30% width
            else if (bubbleSizePercent <= 0.40)
                widthFraction = 0.50;  // Small: 50% width
            else if (bubbleSizePercent <= 0.60)
                widthFraction = 0.75;  // Medium: 75% width
            else if (bubbleSizePercent <= 0.80)
                widthFraction = 1.00;  // Large: 100% width (flush with candle)
            else if (bubbleSizePercent <= 1.00)
                widthFraction = 1.25;  // Very large: 125% (exceed!)
            else
                widthFraction = 1.50;  // HUGE: 150% (DRAMATIC!) ⭕
            
            TimeSpan bubbleWidthTime = TimeSpan.FromTicks((long)(barDuration.Ticks * widthFraction));

            // Bubble center is at the exact price level
            double bubbleCenter = level.Price;

            // Draw bubble ellipse (FILLED)
            string name = $"Footprint_{barIndex}_{level.Price.ToString("F5").Replace(".", "_")}";

            try
            {
                var ellipse = Chart.DrawEllipse(name + "_bubble",
                    barTime.Subtract(bubbleWidthTime), bubbleCenter + bubbleRadius,
                    barTime.Add(bubbleWidthTime), bubbleCenter - bubbleRadius,
                    color);

                if (ellipse != null)
                {
                    ellipse.IsFilled = true;
                    ellipse.Thickness = 1;
                }
            }
            catch { }

            // Delta label - Show on all bubbles that are large enough to read
            // Only hide for extremely tiny bubbles (Δ < 3) where text won't fit
            if (ShowDeltaLabels && absDelta >= 3)
            {
                string label = $"{delta:+#;-#;0}";  // Show delta value (e.g., +12, -8)
                var text = Chart.DrawText(name + "_label", label, barIndex, bubbleCenter, Color.White);
                if (text != null)
                {
                    // Adaptive font size based on delta/bubble size
                    if (absDelta >= 20)
                        text.FontSize = 9;      // HUGE bubbles - big text
                    else if (absDelta >= 15)
                        text.FontSize = 8;      // Large bubbles
                    else if (absDelta >= 10)
                        text.FontSize = 7;      // Medium bubbles
                    else if (absDelta >= 6)
                        text.FontSize = 6;      // Small bubbles
                    else
                        text.FontSize = 5;      // Tiny bubbles - very small text
                    
                    text.HorizontalAlignment = HorizontalAlignment.Center;
                    text.VerticalAlignment = VerticalAlignment.Center;
                    text.IsBold = absDelta >= 15;  // Bold for significant deltas
                }
            }
        }

        private double RoundToPip(double price)
        {
            // Smart price grouping that works for both Forex and Crypto
            double pipSize = Symbol.PipSize;
            
            // For high-value instruments (BTCUSD, Gold, etc.) where pip=1.0
            // We need to group into larger buckets to avoid too many levels
            if (pipSize >= 0.1)  // Crypto, indices, commodities
            {
                // Determine grouping size based on price magnitude
                double groupSize;
                
                if (price >= 10000)       // BTCUSD ($40k+)
                    groupSize = 50;       // Group into $50 buckets
                else if (price >= 1000)   // Gold, smaller crypto
                    groupSize = 10;       // Group into $10 buckets
                else if (price >= 100)    
                    groupSize = 5;        // Group into $5 buckets
                else
                    groupSize = 1;        // Group into $1 buckets
                
                return Math.Round(price / groupSize) * groupSize;
            }
            else  // Forex (pipSize = 0.0001 or 0.01)
            {
                // Standard pip rounding for forex
                return Math.Round(price / pipSize) * pipSize;
            }
        }

        private int FindBarIndex(DateTime time)
        {
            for (int i = Bars.Count - 1; i >= 0; i--)
            {
                DateTime barStart = Bars.OpenTimes[i];
                DateTime barEnd = i < Bars.Count - 1 ? Bars.OpenTimes[i + 1] : Server.Time;

                if (time >= barStart && time < barEnd)
                    return i;
            }

            return Bars.Count - 1;
        }

        private Color GetColorFromString(string colorName)
        {
            switch (colorName.ToLower())
            {
                case "red": return Color.Red;
                case "green": return Color.Green;
                case "blue": return Color.Blue;
                case "yellow": return Color.Yellow;
                case "orange": return Color.Orange;
                case "purple": return Color.Purple;
                case "cyan": return Color.Cyan;
                case "white": return Color.White;
                case "lime": return Color.Lime;
                case "magenta": return Color.Magenta;
                default: return Color.Green;
            }
        }


        // Data structures
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
        }

        private class PriceLevel
        {
            public double Price { get; set; }
            public int BuyCount { get; set; }
            public int SellCount { get; set; }
            public int TotalCount { get; set; }
        }
    }
}