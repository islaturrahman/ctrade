using System;
using System.Collections.Generic;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo
{
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class VolumeProfilePerBar : Indicator
    {
        [Parameter("Max Bars", DefaultValue = 50, MinValue = 10, MaxValue = 100)]
        public int MaxBars { get; set; }

        [Parameter("Pip Step (Thickness)", DefaultValue = 1.0, MinValue = 0.1, Step = 0.1)]
        public double PipStep { get; set; }
        
        [Parameter("Max Width (%)", DefaultValue = 80, MinValue = 10, MaxValue = 100)]
        public int ProfileWidthPercent { get; set; }

        [Parameter("Bullish Profile Color", DefaultValue = "Green")]
        public string BullishColorStr { get; set; }

        [Parameter("Bearish Profile Color", DefaultValue = "Red")]
        public string BearishColorStr { get; set; }
        
        [Parameter("POC Color", DefaultValue = "Magenta")]
        public string PocColorStr { get; set; }

        private Color _bullishColor;
        private Color _bearishColor;
        private Color _pocColor;
        private Ticks _ticks;
        private DateTime _lastRenderTime;
        
        protected override void Initialize()
        {
            // Parse colors from string parameters
            _bullishColor = Color.FromName(BullishColorStr);
            _bearishColor = Color.FromName(BearishColorStr);
            _pocColor = Color.FromName(PocColorStr);
            
            // Load historical tick data for processing accurate volume per price
            _ticks = MarketData.GetTicks();
        }

        public override void Calculate(int index)
        {
            // Optimize: Only process the recent 'MaxBars' to avoid heavy calculations freezing the chart
            if (index < Bars.Count - MaxBars)
                return;
                
            // PERFORMANCE FIX: Mencegah lag parah saat bar live bergerak.
            // Membatasi pembaruan visual pada bar terakhir (Live Bar) maksimal 2 kali per detik (500ms).
            // Karena fungsi Calculate() dipanggil setiap milidetik saat ada transaksi masuk,
            // menggambar ratusan kotak berulang-ulang dalam hitungan milidetik akan membuat PC lag/hang.
            if (IsLastBar)
            {
                if ((Server.Time - _lastRenderTime).TotalMilliseconds < 500)
                    return;
                _lastRenderTime = Server.Time;
            }
                
            DateTime openTime = Bars.OpenTimes[index];
            DateTime closeTime;
            
            // FIX: Calculate a standardized, constant time span for this chart's timeframe.
            // Using closeTime - openTime causes massive horizontal stretching on weekend gaps.
            TimeSpan barDuration = TimeSpan.MaxValue;
            int lookback = Math.Min(10, Bars.Count);
            for (int j = Bars.Count - 1; j > Bars.Count - lookback; j--)
            {
                TimeSpan diff = Bars.OpenTimes[j] - Bars.OpenTimes[j - 1];
                if (diff < barDuration && diff.TotalSeconds > 0)
                    barDuration = diff;
            }
            if (barDuration == TimeSpan.MaxValue) barDuration = TimeSpan.FromMinutes(1);
            
            closeTime = openTime + barDuration;
            
            // Find ticks corresponding to this bar's timeframe
            int startTickIndex = GetTickIndexByTime(openTime);
            int endTickIndex = GetTickIndexByTime(closeTime);
            
            if (startTickIndex == -1 || endTickIndex == -1 || startTickIndex >= endTickIndex)
                return; 
                
            // Dictionary to store grouped buy/sell volume per price level
            Dictionary<double, LevelVolume> volumeProfile = new Dictionary<double, LevelVolume>();
            int maxVolume = 0;
            double pocPrice = 0;
            
            double priceStep = Symbol.PipSize * PipStep;
            
            double barHigh = Bars.HighPrices[index];
            double barLow = Bars.LowPrices[index];
            
            double prevPrice = startTickIndex > 0 ? _ticks[startTickIndex - 1].Bid : _ticks[startTickIndex].Bid;
            bool lastDirectionIsBuy = true;
            
            // 1. Accumulate Buy/Sell Volume per Pip Level (Tick Test Algorithm)
            for (int i = startTickIndex; i < endTickIndex; i++)
            {
                double price = _ticks[i].Bid; 
                
                // Clamp ticks to ensure they strictly belong inside the High/Low of the current bar.
                if (price > barHigh || price < barLow)
                    continue;
                
                // Group/Round the price to the nearest 'PipStep' level
                double groupedPrice = Math.Floor(price / priceStep) * priceStep;
                
                if (!volumeProfile.TryGetValue(groupedPrice, out LevelVolume levelVol))
                {
                    levelVol = new LevelVolume();
                    volumeProfile[groupedPrice] = levelVol;
                }
                
                // Determine tick direction (Bullish / Buy vs Bearish / Sell)
                if (price > prevPrice)
                {
                    levelVol.BuyVolume++;
                    lastDirectionIsBuy = true;
                }
                else if (price < prevPrice)
                {
                    levelVol.SellVolume++;
                    lastDirectionIsBuy = false;
                }
                else
                {
                    if (lastDirectionIsBuy)
                        levelVol.BuyVolume++;
                    else
                        levelVol.SellVolume++;
                }
                
                prevPrice = price;
                
                int currentTotalVol = levelVol.TotalVolume;
                if (currentTotalVol > maxVolume)
                {
                    maxVolume = currentTotalVol;
                    pocPrice = groupedPrice;
                }
            }
            
            if (maxVolume == 0) return;
            
            // Calculate max physical width on chart (as a TimeSpan)
            TimeSpan maxProfileTimeWidth = TimeSpan.FromTicks((long)(barDuration.Ticks * (ProfileWidthPercent / 100.0)));
            
            // 2. Draw Histogram Rectangles with Level-by-Level Bullish/Bearish Color
            foreach (var kvp in volumeProfile)
            {
                double price = kvp.Key;
                LevelVolume levelVol = kvp.Value;
                int volume = levelVol.TotalVolume;
                
                // Calculate width proportionally to the max volume in this bar
                double widthRatio = (double)volume / maxVolume;
                TimeSpan rectWidthTime = TimeSpan.FromTicks((long)(maxProfileTimeWidth.Ticks * widthRatio));
                DateTime endTime = openTime + rectWidthTime;
                
                double topY = price + priceStep;
                double bottomY = price;
                
                string rectName = $"vp_rect_{index}_{price}";
                
                // Determine color for this specific pip level:
                // Green (Bullish) if Buy Volume >= Sell Volume, Red (Bearish) if Sell Volume > Buy Volume
                Color levelColor = (levelVol.BuyVolume >= levelVol.SellVolume) ? _bullishColor : _bearishColor;
                Color rectColor = (price == pocPrice) ? _pocColor : levelColor;
                
                // Draw horizontal volume bar
                var rect = Chart.DrawRectangle(rectName, openTime, topY, endTime, bottomY, rectColor);
                rect.IsFilled = true;
                
                // Adding transparency (alpha) so it doesn't block background completely
                rect.Color = Color.FromArgb(140, rectColor); 
            }
            
            // 3. Draw POC Line Indicator
            if (pocPrice > 0)
            {
                double midPoc = pocPrice + (priceStep / 2);
                string pocName = $"vp_poc_{index}";
                Chart.DrawTrendLine(pocName, openTime, midPoc, openTime + maxProfileTimeWidth, midPoc, _pocColor, 2);
            }
        }
        
        // Helper to quickly find tick index via Binary Search (much faster than iterating all ticks)
        private int GetTickIndexByTime(DateTime time)
        {
            if (_ticks == null || _ticks.Count == 0) return -1;
            
            int left = 0;
            int right = _ticks.Count - 1;
            int bestMatch = -1;
            
            if (time < _ticks[0].Time) return -1;
            if (time > _ticks[_ticks.Count - 1].Time) return _ticks.Count - 1;
            
            while (left <= right)
            {
                int mid = left + (right - left) / 2;
                if (_ticks[mid].Time < time)
                {
                    bestMatch = mid;
                    left = mid + 1;
                }
                else if (_ticks[mid].Time > time)
                {
                    right = mid - 1;
                }
                else
                {
                    return mid;
                }
            }
            
            if (bestMatch != -1 && bestMatch + 1 < _ticks.Count)
                return bestMatch + 1;
                
            return _ticks.Count - 1;
        }

        private class LevelVolume
        {
            public int BuyVolume { get; set; }
            public int SellVolume { get; set; }
            public int TotalVolume => BuyVolume + SellVolume;
        }
    }
}