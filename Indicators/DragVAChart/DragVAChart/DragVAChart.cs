using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Internals;

namespace cAlgo.Indicators
{
    // Selection mode enum
    public enum VPSelectionMode
    {
        Auto,       // Auto-analyze last N bars (no interaction needed)
        Click,      // Click 2 points on chart to define VP area
        Rectangle   // Legacy: use toolbox rectangles (original behavior)
    }

    /// <summary>
    /// DRAG VOLUME PROFILE with BUY/SELL SEPARATION + CUMULATIVE DELTA
    /// 
    /// 3 SELECTION MODES:
    /// ─────────────────
    /// 1) AUTO      → VP otomatis pada N bar terakhir, tanpa interaksi
    /// 2) CLICK     → Klik 2 titik di chart untuk menentukan area VP (RECOMMENDED)
    /// 3) RECTANGLE → Mode lama, pakai rectangle toolbox
    /// 
    /// FEATURES:
    /// - Histogram shows Buy (Green/Right) vs Sell (Red/Left) volume
    /// - Cumulative Delta histogram on the left side
    /// - Two analysis modes: Bar Analysis (fast) or Tick Data (accurate)
    /// - Displays POC (Point of Control) and Value Area (70%)
    /// 
    /// Version: 4.0 (Click Mode + Auto Mode)
    /// </summary>
    [Indicator(IsOverlay = true, AccessRights = AccessRights.None)]
    public class DragVolumeProfileBuySell : Indicator
    {
        // ═══════════════════════════════════════
        //  SELECTION MODE
        // ═══════════════════════════════════════

        [Parameter("Selection Mode", DefaultValue = VPSelectionMode.Click)]
        public VPSelectionMode SelectionMode { get; set; }

        // ═══════════════════════════════════════
        //  GENERAL PARAMETERS
        // ═══════════════════════════════════════

        [Parameter("Number of Bars (Auto Mode)", DefaultValue = 50, MinValue = 10, MaxValue = 300, Step = 5)]
        public int NumberOfBars { get; set; }

        [Parameter("Histogram Width % of Area", DefaultValue = 8, MinValue = 3, MaxValue = 50, Step = 1)]
        public int HistogramWidthPercent { get; set; }

        [Parameter("Bucket Size (pips)", DefaultValue = 90.0, MinValue = 0.1, Step = 0.1)]
        public double BucketSizePips { get; set; }

        [Parameter("Auto Bucket Size", DefaultValue = true)]
        public bool AutoBucketSize { get; set; }

        [Parameter("Show POC", DefaultValue = true)]
        public bool ShowPOC { get; set; }

        [Parameter("Show Value Area (70%)", DefaultValue = true)]
        public bool ShowValueArea { get; set; }

        [Parameter("Max VP Areas", DefaultValue = 3, MinValue = 1, MaxValue = 10)]
        public int MaxRects { get; set; }

        [Parameter("Use Tick Data (Slower but Accurate)", DefaultValue = false)]
        public bool UseTickData { get; set; }

        // ═══════════════════════════════════════
        //  COLORS
        // ═══════════════════════════════════════

        [Parameter("Buy Color", DefaultValue = "Lime")]
        public Color BuyColor { get; set; }

        [Parameter("Buy Opacity (0-255)", DefaultValue = 210, MinValue = 50, MaxValue = 255, Step = 10)]
        public int BuyOpacity { get; set; }

        [Parameter("Sell Color", DefaultValue = "Red")]
        public Color SellColor { get; set; }

        [Parameter("Sell Opacity (0-255)", DefaultValue = 210, MinValue = 50, MaxValue = 255, Step = 10)]
        public int SellOpacity { get; set; }

        [Parameter("POC Color", DefaultValue = "Yellow")]
        public Color POCColor { get; set; }

        // ═══════════════════════════════════════
        //  CUMULATIVE DELTA
        // ═══════════════════════════════════════

        [Parameter("Show Cumulative Delta", DefaultValue = true)]
        public bool ShowCumulativeDelta { get; set; }

        [Parameter("Cumulative Delta Width %", DefaultValue = 10, MinValue = 3, MaxValue = 50, Step = 1)]
        public int CumulativeDeltaWidthPercent { get; set; }

        [Parameter("Positive Delta Color", DefaultValue = "Lime")]
        public Color PositiveDeltaColor { get; set; }

        [Parameter("Negative Delta Color", DefaultValue = "Red")]
        public Color NegativeDeltaColor { get; set; }

        [Parameter("Delta Opacity (0-255)", DefaultValue = 180, MinValue = 50, MaxValue = 255, Step = 10)]
        public int DeltaOpacity { get; set; }

        // ═══════════════════════════════════════
        //  CROSSHAIR SETTINGS
        // ═══════════════════════════════════════

        [Parameter("Crosshair Color", DefaultValue = "Yellow")]
        public Color CrosshairColor { get; set; }

        [Parameter("Crosshair Opacity (0-255)", DefaultValue = 120, MinValue = 30, MaxValue = 255, Step = 10)]
        public int CrosshairOpacity { get; set; }

        // ═══════════════════════════════════════
        //  PRIVATE FIELDS
        // ═══════════════════════════════════════

        private DateTime _lastRenderTime = DateTime.MinValue;
        private DateTime _lastCleanTime = DateTime.MinValue;

        private double _pipSize;
        private HashSet<string> _processedRectNames = new HashSet<string>();
        private HashSet<string> _previousRectNames = new HashSet<string>();

        private Color _buyColor;
        private Color _sellColor;
        private Color _pocColor;

        // Click mode state
        private bool _waitingForSecondClick = false;
        private DateTime _click1Time;
        private double _click1Price;
        private int _vpAreaCounter = 0;
        private const string VP_RECT_PREFIX = "VPAREA_";

        // Crosshair state
        private bool _crosshairActive = false;
        private DateTime _lastMoveTime = DateTime.MinValue;
        private DateTime _prevClickTime = DateTime.MinValue;
        private Color _crosshairColor;

        // ═══════════════════════════════════════
        //  INITIALIZATION
        // ═══════════════════════════════════════

        protected override void Initialize()
        {
            _pipSize = Symbol.PipSize;
            _buyColor = BuyColor;
            _sellColor = SellColor;
            _pocColor = POCColor;
            _crosshairColor = Color.FromArgb(CrosshairOpacity, CrosshairColor.R, CrosshairColor.G, CrosshairColor.B);

            // CLEAN ALL OLD VP OBJECTS ON RELOAD
            CleanAllVPObjects();

            Print($"DragVolumeProfileBuySell v4.1 | Mode: {SelectionMode} | UseTickData: {UseTickData}");

            switch (SelectionMode)
            {
                case VPSelectionMode.Click:
                    // Register crosshair + click handlers
                    Chart.MouseMove += OnMouseMove;
                    Chart.MouseDown += OnChartMouseDown;
                    // Register object events for drag/resize/delete of click-created rectangles
                    Chart.ObjectsUpdated += OnObjectsUpdated;
                    Chart.ObjectsRemoved += OnObjectsRemoved;
                    _crosshairActive = false; // Start inactive — cursor clicks ignored
                    UpdateStatusText("╋ DOUBLE-CLICK chart untuk aktifkan VP Crosshair", Color.Gray);
                    Print("Crosshair Click Mode: Double-click to activate, then click 2 points.");
                    break;

                case VPSelectionMode.Auto:
                    // Auto-render on last N bars
                    UpdateStatusText("📊 AUTO MODE: VP pada " + NumberOfBars + " bar terakhir", Color.Lime);
                    RenderAutoVP();
                    Print("Auto Mode: VP on last " + NumberOfBars + " bars.");
                    break;

                case VPSelectionMode.Rectangle:
                    // Legacy rectangle mode (original behavior)
                    Chart.ObjectsAdded += OnObjectsAdded;
                    Chart.ObjectsRemoved += OnObjectsRemoved;
                    Chart.ObjectsUpdated += OnObjectsUpdated;
                    Chart.ObjectsSelectionChanged += OnObjectsSelectionChanged;
                    UpdateStatusText("🔲 RECTANGLE MODE: Gambar rectangle dari toolbox", Color.White);
                    ProcessRectangles();
                    Print("Rectangle Mode: Draw rectangles from toolbox.");
                    break;
            }
        }

        // ═══════════════════════════════════════
        //  CROSSHAIR + CLICK MODE HANDLERS
        // ═══════════════════════════════════════

        private void OnMouseMove(ChartMouseEventArgs args)
        {
            if (SelectionMode != VPSelectionMode.Click) return;
            if (!_crosshairActive) return; // ← Cursor mode: no crosshair

            // Throttle: max ~20fps to avoid lag
            var now = DateTime.Now;
            if ((now - _lastMoveTime).TotalMilliseconds < 50) return;
            _lastMoveTime = now;

            // ── LIVE CROSSHAIR LINES ──
            Chart.DrawVerticalLine("VP_xhair_v", args.TimeValue,
                _crosshairColor, 1, LineStyle.Dots);

            Chart.DrawHorizontalLine("VP_xhair_h", args.YValue,
                _crosshairColor, 1, LineStyle.Dots);

            // Price label at crosshair position
            Chart.DrawText("VP_xhair_price",
                " " + args.YValue.ToString("F" + Symbol.Digits) + " ",
                Bars.OpenTimes.GetIndexByTime(args.TimeValue) + 3, args.YValue,
                _crosshairColor);

            // ── RUBBER BAND RECTANGLE (after first click) ──
            if (_waitingForSecondClick)
            {
                var rubberband = Chart.DrawRectangle("VP_rubberband",
                    _click1Time, _click1Price,
                    args.TimeValue, args.YValue,
                    Color.FromArgb(40, CrosshairColor.R, CrosshairColor.G, CrosshairColor.B));
                rubberband.IsFilled = true;
                rubberband.IsInteractive = false;
                rubberband.LineStyle = LineStyle.Dots;
                rubberband.Thickness = 1;
            }
        }

        private void ClearCrosshairPreview()
        {
            Chart.RemoveObject("VP_xhair_v");
            Chart.RemoveObject("VP_xhair_h");
            Chart.RemoveObject("VP_xhair_price");
            Chart.RemoveObject("VP_rubberband");
            Chart.RemoveObject("VP_corner1_v");
            Chart.RemoveObject("VP_corner1_h");
        }

        private void OnChartMouseDown(ChartMouseEventArgs args)
        {
            if (SelectionMode != VPSelectionMode.Click) return;

            // ═══ DOUBLE-CLICK DETECTION ═══
            var now = DateTime.Now;
            double elapsed = (now - _prevClickTime).TotalMilliseconds;
            _prevClickTime = now;

            // Double-click = toggle crosshair VP mode (80-400ms between clicks)
            if (elapsed > 80 && elapsed < 400)
            {
                _crosshairActive = !_crosshairActive;
                _waitingForSecondClick = false;

                if (_crosshairActive)
                {
                    // ── CROSSHAIR MODE ON ──
                    UpdateStatusText("╋ VP CROSSHAIR AKTIF! Klik corner pertama", Color.Cyan);
                    Print("VP Crosshair Mode: ACTIVATED");
                }
                else
                {
                    // ── CROSSHAIR MODE OFF ──
                    ClearCrosshairPreview();
                    UpdateStatusText("╋ DOUBLE-CLICK chart untuk aktifkan VP Crosshair", Color.Gray);
                    Print("VP Crosshair Mode: DEACTIVATED");
                }
                return; // Don't process this click as a corner
            }

            // ═══ IGNORE IF CROSSHAIR NOT ACTIVE ═══
            if (!_crosshairActive) return;

            // ═══ CORNER SELECTION ═══
            if (!_waitingForSecondClick)
            {
                // ── FIRST CLICK (Corner 1) ──
                _click1Time = args.TimeValue;
                _click1Price = args.YValue;
                _waitingForSecondClick = true;

                // Draw FIXED crosshair at corner 1
                Chart.DrawVerticalLine("VP_corner1_v", _click1Time,
                    Color.FromArgb(200, CrosshairColor.R, CrosshairColor.G, CrosshairColor.B), 2, LineStyle.Solid);
                Chart.DrawHorizontalLine("VP_corner1_h", _click1Price,
                    Color.FromArgb(200, CrosshairColor.R, CrosshairColor.G, CrosshairColor.B), 2, LineStyle.Solid);

                UpdateStatusText("╋ Klik corner KEDUA... (rubber-band menunjukkan area)", Color.Yellow);
                Print($"Corner 1: Time={_click1Time}, Price={_click1Price:F5}");
            }
            else
            {
                // ── SECOND CLICK (Corner 2) → Create VP ──
                _waitingForSecondClick = false;

                // Remove crosshair preview objects
                ClearCrosshairPreview();

                // Check if we exceeded max areas → remove oldest
                EnforceMaxAreas();

                // Create VP rectangle
                string rectName = VP_RECT_PREFIX + _vpAreaCounter;
                var vpRect = Chart.DrawRectangle(
                    rectName,
                    _click1Time, _click1Price,
                    args.TimeValue, args.YValue,
                    Color.FromArgb(25, 100, 180, 255));
                vpRect.IsFilled = true;
                vpRect.IsInteractive = true;
                vpRect.LineStyle = LineStyle.Dots;
                vpRect.Thickness = 1;
                _vpAreaCounter++;

                Print($"Corner 2: Time={args.TimeValue}, Price={args.YValue:F5} → Created {rectName}");

                // Render Volume Profile
                RenderVolumeProfile(vpRect);

                UpdateStatusText("✅ VP dibuat! Klik lagi utk area baru | Double-click utk OFF", Color.Lime);
            }
        }

        private void EnforceMaxAreas()
        {
            // Count existing VP areas
            var existingAreas = Chart.Objects
                .OfType<ChartRectangle>()
                .Where(r => r.Name.StartsWith(VP_RECT_PREFIX))
                .OrderBy(r => r.Name)
                .ToList();

            // If at max, remove the oldest
            while (existingAreas.Count >= MaxRects)
            {
                var oldest = existingAreas.First();
                Print($"Max areas ({MaxRects}) reached, removing oldest: {oldest.Name}");
                RemoveVPForRect(oldest.Name);
                Chart.RemoveObject(oldest.Name);
                existingAreas.RemoveAt(0);
            }
        }

        // ═══════════════════════════════════════
        //  AUTO MODE
        // ═══════════════════════════════════════

        private void RenderAutoVP()
        {
            if (Bars.Count < 2) return;

            int endIndex = Bars.Count - 1;
            int startIndex = Math.Max(0, endIndex - NumberOfBars);

            // Find price range from the bars
            double topPrice = double.MinValue;
            double bottomPrice = double.MaxValue;

            for (int i = startIndex; i <= endIndex; i++)
            {
                topPrice = Math.Max(topPrice, Bars.HighPrices[i]);
                bottomPrice = Math.Min(bottomPrice, Bars.LowPrices[i]);
            }

            if (topPrice <= bottomPrice) return;

            // Create auto rectangle (non-interactive, just for rendering)
            string rectName = VP_RECT_PREFIX + "auto";
            var autoRect = Chart.DrawRectangle(
                rectName,
                Bars.OpenTimes[startIndex], topPrice,
                Bars.OpenTimes[endIndex], bottomPrice,
                Color.FromArgb(12, 100, 150, 255));
            autoRect.IsFilled = true;
            autoRect.IsInteractive = false;
            autoRect.LineStyle = LineStyle.Dots;
            autoRect.Thickness = 1;

            RenderVolumeProfile(autoRect);
            Print($"Auto VP rendered: bars {startIndex}-{endIndex}, range {bottomPrice:F5}-{topPrice:F5}");
        }

        // ═══════════════════════════════════════
        //  RECTANGLE MODE (LEGACY) EVENT HANDLERS
        // ═══════════════════════════════════════

        private void OnObjectsAdded(ChartObjectsAddedEventArgs args)
        {
            Print("Objects Added.");
            if (SelectionMode == VPSelectionMode.Rectangle)
                ProcessRectangles();
        }

        private void OnObjectsUpdated(ChartObjectsUpdatedEventArgs args)
        {
            Print("Objects Updated (drag/resize detected).");

            if (SelectionMode == VPSelectionMode.Click)
            {
                // Re-render VP for updated click-created rectangles
                ProcessClickRectangles();
            }
            else if (SelectionMode == VPSelectionMode.Rectangle)
            {
                ProcessRectangles();
            }
        }

        private void OnObjectsRemoved(ChartObjectsRemovedEventArgs args)
        {
            Print("Objects Removed → cleaning VP...");

            if (SelectionMode == VPSelectionMode.Click)
            {
                CleanOrphanedVP();
            }
            else if (SelectionMode == VPSelectionMode.Rectangle)
            {
                ProcessRectangles();
            }
        }

        private void OnObjectsSelectionChanged(ChartObjectsSelectionChangedEventArgs args)
        {
            if (SelectionMode == VPSelectionMode.Rectangle)
                ProcessRectangles();
        }

        // ═══════════════════════════════════════
        //  CALCULATE
        // ═══════════════════════════════════════

        public override void Calculate(int index)
        {
            if (!IsLastBar) return;

            if (SelectionMode == VPSelectionMode.Auto)
            {
                var now = Server.Time;
                if ((now - _lastRenderTime).TotalMilliseconds < 500) return;
                _lastRenderTime = now;

                // Clean and re-render auto VP on new bars
                RemoveVPForRect(VP_RECT_PREFIX + "auto");
                Chart.RemoveObject(VP_RECT_PREFIX + "auto");
                RenderAutoVP();
            }
            else if (SelectionMode == VPSelectionMode.Rectangle)
            {
                ProcessRectangles();
            }
            // Click mode: no action on Calculate
        }

        // ═══════════════════════════════════════
        //  CLICK MODE RECTANGLE PROCESSING
        // ═══════════════════════════════════════

        private void ProcessClickRectangles()
        {
            var now = Server.Time;
            if ((now - _lastRenderTime).TotalMilliseconds < 300) return;
            _lastRenderTime = now;

            var vpRects = Chart.Objects
                .OfType<ChartRectangle>()
                .Where(r => r.Name.StartsWith(VP_RECT_PREFIX))
                .Take(MaxRects)
                .ToList();

            foreach (var rect in vpRects)
            {
                // Clean old VP for this rect, then re-render
                RemoveVPForRect(rect.Name);
                RenderVolumeProfile(rect);
            }
        }

        private void CleanOrphanedVP()
        {
            // Find VP objects whose parent rect no longer exists
            var existingRectNames = Chart.Objects
                .OfType<ChartRectangle>()
                .Where(r => r.Name.StartsWith(VP_RECT_PREFIX))
                .Select(r => r.Name)
                .ToHashSet();

            var vpObjects = Chart.Objects
                .Where(o => o.Name.StartsWith("VP_"))
                .ToList();

            foreach (var vp in vpObjects)
            {
                // Check if this VP object belongs to a rect that still exists
                bool hasParent = existingRectNames.Any(rn => vp.Name.Contains(rn));
                if (!hasParent)
                {
                    Chart.RemoveObject(vp.Name);
                }
            }
        }

        // ═══════════════════════════════════════
        //  LEGACY RECTANGLE PROCESSING
        // ═══════════════════════════════════════

        private void ProcessRectangles()
        {
            var now = Server.Time;
            if ((now - _lastRenderTime).TotalMilliseconds < 300) return;
            _lastRenderTime = now;

            var currentRects = Chart.Objects
                .OfType<ChartRectangle>()
                .Where(r => r.IsInteractive)
                .OrderByDescending(r => r.Time1)
                .Take(MaxRects)
                .ToList();

            Print("Detected " + currentRects.Count + " rectangles.");

            // Periodic full clean if rectangles were removed
            if (_previousRectNames.Count > currentRects.Count && (now - _lastCleanTime).TotalMilliseconds > 100)
            {
                Print("Periodic FULL CLEAN: removing all VP...");
                CleanAllVPObjects();
                _lastCleanTime = now;
            }

            // Detect missing rectangles and remove their VP
            var missingNames = _previousRectNames
                .Where(name => !currentRects.Any(r => r.Name == name))
                .ToList();

            foreach (var missingName in missingNames)
            {
                Print($"Rect removed: {missingName} → removing VP...");
                RemoveVPForRect(missingName);
                _previousRectNames.Remove(missingName);
            }

            // Render
            if (currentRects.Any())
            {
                foreach (var rect in currentRects)
                {
                    RenderVolumeProfile(rect);
                    _previousRectNames.Add(rect.Name);
                }
            }
        }

        // ═══════════════════════════════════════
        //  HELPER: CLEAN / REMOVE
        // ═══════════════════════════════════════

        private void CleanAllVPObjects()
        {
            var allVp = Chart.Objects.Where(o => o.Name.StartsWith("VP_")).ToList();
            foreach (var obj in allVp)
            {
                Chart.RemoveObject(obj.Name);
            }
            Print($"Cleaned {allVp.Count} VP objects.");
        }

        private void RemoveVPForRect(string rectName)
        {
            var vpToRemove = Chart.Objects
                .Where(o => o.Name.StartsWith("VP_") && o.Name.Contains(rectName))
                .ToList();
            foreach (var vp in vpToRemove)
            {
                Chart.RemoveObject(vp.Name);
            }
        }

        private void UpdateStatusText(string message, Color color)
        {
            Chart.DrawStaticText("VP_status", message,
                VerticalAlignment.Top, HorizontalAlignment.Left, color);
        }

        // ═══════════════════════════════════════
        //  VOLUME CALCULATION
        // ═══════════════════════════════════════

        private void AddVolume(Dictionary<double, VolumeSplit> dict, double price, double buyVol, double sellVol)
        {
            if (dict.ContainsKey(price))
            {
                dict[price].BuyVolume += buyVol;
                dict[price].SellVolume += sellVol;
            }
            else
            {
                dict[price] = new VolumeSplit { BuyVolume = buyVol, SellVolume = sellVol };
            }
        }

        // ═══════════════════════════════════════
        //  RENDER VOLUME PROFILE
        // ═══════════════════════════════════════

        private void RenderVolumeProfile(ChartRectangle rect)
        {
            var startTime = rect.Time1;
            var endTime = rect.Time2;
            var topPrice = Math.Max(rect.Y1, rect.Y2);
            var bottomPrice = Math.Min(rect.Y1, rect.Y2);

            int startIndex = Bars.OpenTimes.GetIndexByTime(startTime);
            int endIndex = Bars.OpenTimes.GetIndexByTime(endTime);

            if (startIndex < 0 || endIndex < 0 || startIndex > endIndex) return;

            // DYNAMIC BUCKET SIZE — smooth, small bars
            double bucketSize;
            double priceRange = topPrice - bottomPrice;
            if (AutoBucketSize)
            {
                // Target: ~70 buckets for smooth, compact histogram
                int targetBuckets = 70;
                bucketSize = priceRange / targetBuckets;

                // Snap to multiples of pipSize (fine granularity)
                double bucketPips = bucketSize / _pipSize;
                if (bucketPips < 0.2) bucketPips = 0.2;
                else if (bucketPips < 0.5) bucketPips = 0.5;
                else if (bucketPips < 1) bucketPips = Math.Round(bucketPips * 2) / 2; // 0.5 steps
                else bucketPips = Math.Ceiling(bucketPips);

                bucketSize = bucketPips * _pipSize;
                Print($"Auto Bucket: {bucketPips:F1} pips | Range: {(priceRange / _pipSize):F1} pips | Buckets: ~{(int)(priceRange / bucketSize)}");
            }
            else
            {
                bucketSize = BucketSizePips * _pipSize;
            }

            var volumeDict = new Dictionary<double, VolumeSplit>();

            if (UseTickData)
                CalculateVolumeFromTicks(volumeDict, startTime, endTime, topPrice, bottomPrice, bucketSize);
            else
                CalculateVolumeFromBars(volumeDict, startIndex, endIndex, topPrice, bottomPrice, bucketSize);

            if (volumeDict.Count == 0) return;

            // Find POC
            var pocEntry = volumeDict.OrderByDescending(kv => kv.Value.TotalVolume).First();
            double pocPrice = pocEntry.Key;
            double maxVol = pocEntry.Value.TotalVolume;

            // Calculate Value Area (70%)
            var sortedBuckets = volumeDict.OrderBy(kv => kv.Key).ToList();
            double totalVol = sortedBuckets.Sum(kv => kv.Value.TotalVolume);

            double targetVa = totalVol * 0.7;
            double accumulated = maxVol;
            double vaHigh = pocPrice;
            double vaLow = pocPrice;

            int centerIdx = sortedBuckets.FindIndex(kv => Math.Abs(kv.Key - pocPrice) < _pipSize);
            int up = centerIdx + 1;
            int down = centerIdx - 1;

            while (accumulated < targetVa && (up < sortedBuckets.Count || down >= 0))
            {
                double upVol = up < sortedBuckets.Count ? sortedBuckets[up].Value.TotalVolume : 0;
                double downVol = down >= 0 ? sortedBuckets[down].Value.TotalVolume : 0;

                if (upVol >= downVol && up < sortedBuckets.Count)
                {
                    accumulated += upVol;
                    vaHigh = sortedBuckets[up++].Key;
                }
                else if (down >= 0)
                {
                    accumulated += downVol;
                    vaLow = sortedBuckets[down--].Key;
                }
                else break;
            }

            // RENDER HISTOGRAM using ChartRectangle for gap-free smooth bars
            int leftAnchorIndex = Bars.OpenTimes.GetIndexByTime(rect.Time1);
            int rightAnchorIndex = Bars.OpenTimes.GetIndexByTime(rect.Time2);

            int rectWidthInBars = Math.Max(15, rightAnchorIndex - leftAnchorIndex);

            double widthRatio = HistogramWidthPercent / 100.0;
            int histogramMaxWidth = (int)(rectWidthInBars * widthRatio);
            histogramMaxWidth = Math.Max(15, histogramMaxWidth);
            histogramMaxWidth = Math.Min(histogramMaxWidth, rectWidthInBars - 5);

            int leftStart = leftAnchorIndex;

            // Determine bar drawing times for precise positioning
            DateTime leftTime = rect.Time1 < rect.Time2 ? rect.Time1 : rect.Time2;

            foreach (var kv in sortedBuckets)
            {
                double bucketBottom = kv.Key;
                double bucketTop = bucketBottom + bucketSize;
                VolumeSplit volSplit = kv.Value;

                double totalVolAtPrice = volSplit.TotalVolume;
                double buyRatio = volSplit.BuyVolume / totalVolAtPrice;
                double sellRatio = volSplit.SellVolume / totalVolAtPrice;

                double volRatio = totalVolAtPrice / maxVol;

                // Determine opacity and width boost based on zone
                bool isPOC = Math.Abs(bucketBottom - pocPrice) < bucketSize * 0.9;
                bool isInVA = bucketBottom >= (vaLow - bucketSize * 0.5) && bucketTop <= (vaHigh + bucketSize * 0.5);

                int buyOpacity, sellOpacity;
                double widthMultiplier;

                if (isPOC)
                {
                    buyOpacity = Math.Min(255, BuyOpacity + 30);
                    sellOpacity = Math.Min(255, SellOpacity + 30);
                    widthMultiplier = 1.0;
                }
                else if (isInVA)
                {
                    buyOpacity = BuyOpacity;
                    sellOpacity = SellOpacity;
                    widthMultiplier = 0.95;
                }
                else
                {
                    buyOpacity = Math.Max(60, BuyOpacity - 50);
                    sellOpacity = Math.Max(60, SellOpacity - 50);
                    widthMultiplier = 0.85;
                }

                int totalWidth = Math.Max(2, (int)(volRatio * histogramMaxWidth * widthMultiplier));
                int buyWidth = Math.Max(1, (int)(totalWidth * buyRatio));
                int sellWidth = Math.Max(1, (int)(totalWidth * sellRatio));

                Color buyColor = Color.FromArgb(buyOpacity, _buyColor.R, _buyColor.G, _buyColor.B);
                Color sellColor = Color.FromArgb(sellOpacity, _sellColor.R, _sellColor.G, _sellColor.B);

                // ── DRAW BUY BAR (right side from left edge) ──
                if (buyWidth > 0)
                {
                    string buyBarName = "VP_buy_" + bucketBottom.ToString("F5") + "_" + rect.Name;
                    var buyRect = Chart.DrawRectangle(buyBarName,
                        leftStart, bucketBottom,
                        leftStart + buyWidth, bucketTop,
                        buyColor);
                    buyRect.IsFilled = true;
                    buyRect.IsInteractive = false;
                    buyRect.Thickness = 0;
                    buyRect.Color = buyColor;
                }

                // ── DRAW SELL BAR (overlaid, slightly offset or same position) ──
                if (sellWidth > 0)
                {
                    string sellBarName = "VP_sell_" + bucketBottom.ToString("F5") + "_" + rect.Name;
                    var sellRect = Chart.DrawRectangle(sellBarName,
                        leftStart + buyWidth, bucketBottom,
                        leftStart + buyWidth + sellWidth, bucketTop,
                        sellColor);
                    sellRect.IsFilled = true;
                    sellRect.IsInteractive = false;
                    sellRect.Thickness = 0;
                    sellRect.Color = sellColor;
                }
            }

            // POC LINE
            if (ShowPOC)
            {
                string pocName = "VP_POC_" + rect.Name;
                Chart.DrawTrendLine(pocName + "_line",
                    leftAnchorIndex, pocPrice + bucketSize * 0.5,
                    rightAnchorIndex, pocPrice + bucketSize * 0.5,
                    _pocColor, 3, LineStyle.Solid);
                Chart.DrawText(pocName + "_label",
                    "◄ POC (" + (pocPrice + bucketSize * 0.5).ToString("F" + Symbol.Digits) + ")",
                    rightAnchorIndex + 2, pocPrice + bucketSize * 0.5, _pocColor);
            }

            // VALUE AREA LINES
            if (ShowValueArea)
            {
                string vaName = "VP_VA_" + rect.Name;

                Chart.DrawTrendLine(vaName + "_VAH_line",
                    leftAnchorIndex, vaHigh + bucketSize,
                    rightAnchorIndex, vaHigh + bucketSize,
                    Color.Cyan, 2, LineStyle.DotsRare);
                Chart.DrawText(vaName + "_VAH_label",
                    "◄ VAH (" + (vaHigh + bucketSize).ToString("F" + Symbol.Digits) + ")",
                    rightAnchorIndex + 2, vaHigh + bucketSize, Color.Cyan);

                Chart.DrawTrendLine(vaName + "_VAL_line",
                    leftAnchorIndex, vaLow,
                    rightAnchorIndex, vaLow,
                    Color.Cyan, 2, LineStyle.DotsRare);
                Chart.DrawText(vaName + "_VAL_label",
                    "◄ VAL (" + vaLow.ToString("F" + Symbol.Digits) + ")",
                    rightAnchorIndex + 2, vaLow, Color.Cyan);

                // Value Area shading (subtle background)
                string vaShade = "VP_VA_shade_" + rect.Name;
                var vaRect = Chart.DrawRectangle(vaShade,
                    leftAnchorIndex, vaLow,
                    rightAnchorIndex, vaHigh + bucketSize,
                    Color.FromArgb(15, 0, 200, 255));
                vaRect.IsFilled = true;
                vaRect.IsInteractive = false;
                vaRect.Thickness = 0;
            }

            Print($"VP rendered: {rect.Name} | Method: {(UseTickData ? "Tick Data" : "Bar Analysis")} | Buckets: {sortedBuckets.Count} | Total Volume: {totalVol:F0}");

            // CUMULATIVE DELTA
            if (ShowCumulativeDelta)
            {
                RenderCumulativeDelta(rect, startIndex, endIndex, leftAnchorIndex, rectWidthInBars, topPrice, bottomPrice, bucketSize);
            }
        }

        // ═══════════════════════════════════════
        //  CUMULATIVE DELTA RENDERING
        // ═══════════════════════════════════════

        private void RenderCumulativeDelta(ChartRectangle rect, int startIndex, int endIndex, int leftAnchorIndex, int rectWidthInBars, double topPrice, double bottomPrice, double bucketSize)
        {
            var deltaByPrice = new Dictionary<double, double>();

            for (int i = startIndex; i <= endIndex; i++)
            {
                double volume = Bars.TickVolumes[i];
                if (volume <= 0) continue;

                double open = Bars.OpenPrices[i];
                double close = Bars.ClosePrices[i];
                double high = Bars.HighPrices[i];
                double low = Bars.LowPrices[i];

                double clipLow = Math.Max(low, bottomPrice);
                double clipHigh = Math.Min(high, topPrice);

                if (clipHigh <= clipLow) continue;

                double delta = close - open;
                double range = high - low;
                double buyVolume, sellVolume;

                if (range > 0)
                {
                    if (delta > 0)
                    {
                        double strength = Math.Min(delta / range, 1.0);
                        buyVolume = volume * (0.5 + strength * 0.5);
                        sellVolume = volume - buyVolume;
                    }
                    else if (delta < 0)
                    {
                        double strength = Math.Min(Math.Abs(delta) / range, 1.0);
                        sellVolume = volume * (0.5 + strength * 0.5);
                        buyVolume = volume - sellVolume;
                    }
                    else
                    {
                        buyVolume = volume * 0.5;
                        sellVolume = volume * 0.5;
                    }
                }
                else
                {
                    buyVolume = volume * 0.5;
                    sellVolume = volume * 0.5;
                }

                double barDelta = buyVolume - sellVolume;

                double priceRange2 = clipHigh - clipLow;
                int steps = Math.Max(3, (int)(priceRange2 / _pipSize));
                steps = Math.Min(steps, 15);

                for (int step = 0; step <= steps; step++)
                {
                    double p = clipLow + step * (priceRange2 / steps);
                    double bucket = Math.Floor(p / bucketSize) * bucketSize;

                    if (!deltaByPrice.ContainsKey(bucket))
                        deltaByPrice[bucket] = 0;

                    deltaByPrice[bucket] += barDelta / (steps + 1);
                }
            }

            if (deltaByPrice.Count == 0) return;

            double maxAbsDelta = deltaByPrice.Values.Max(d => Math.Abs(d));
            if (maxAbsDelta == 0) return;

            double widthRatio = CumulativeDeltaWidthPercent / 100.0;
            int maxDeltaWidth = (int)(rectWidthInBars * widthRatio);
            maxDeltaWidth = Math.Max(15, maxDeltaWidth);

            int deltaRightEdge = leftAnchorIndex;

            var sortedDelta = deltaByPrice.OrderBy(kv => kv.Key).ToList();

            foreach (var kv in sortedDelta)
            {
                double bucketBottom = kv.Key;
                double bucketTop = bucketBottom + bucketSize;
                double cumulativeDelta = kv.Value;

                double deltaRatio = cumulativeDelta / maxAbsDelta;
                int barWidth = Math.Max(2, (int)(Math.Abs(deltaRatio) * maxDeltaWidth));

                Color barColor;
                int alpha = (int)(DeltaOpacity * Math.Min(1.0, Math.Abs(deltaRatio) * 1.3 + 0.3));
                alpha = Math.Max(40, Math.Min(255, alpha));

                if (cumulativeDelta >= 0)
                    barColor = Color.FromArgb(alpha, PositiveDeltaColor.R, PositiveDeltaColor.G, PositiveDeltaColor.B);
                else
                    barColor = Color.FromArgb(alpha, NegativeDeltaColor.R, NegativeDeltaColor.G, NegativeDeltaColor.B);

                // Draw delta bar as rectangle (gap-free, extends LEFT from rect edge)
                string deltaBarName = "VP_delta_" + bucketBottom.ToString("F5") + "_" + rect.Name;
                var deltaRect = Chart.DrawRectangle(deltaBarName,
                    deltaRightEdge - barWidth, bucketBottom,
                    deltaRightEdge, bucketTop,
                    barColor);
                deltaRect.IsFilled = true;
                deltaRect.IsInteractive = false;
                deltaRect.Thickness = 0;
                deltaRect.Color = barColor;
            }

            Print($"Cumulative Delta rendered: {rect.Name} | Levels: {sortedDelta.Count}");
        }

        // ═══════════════════════════════════════
        //  BAR ANALYSIS
        // ═══════════════════════════════════════

        private void CalculateVolumeFromBars(Dictionary<double, VolumeSplit> volumeDict,
            int startIndex, int endIndex, double topPrice, double bottomPrice, double bucketSize)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                double volume = Bars.TickVolumes[i];
                if (volume <= 0) continue;

                double open = Bars.OpenPrices[i];
                double close = Bars.ClosePrices[i];
                double high = Bars.HighPrices[i];
                double low = Bars.LowPrices[i];

                double clipLow = Math.Max(low, bottomPrice);
                double clipHigh = Math.Min(high, topPrice);

                if (clipHigh <= clipLow) continue;

                double delta = close - open;
                double range = high - low;

                double buyVolume, sellVolume;

                if (range > 0)
                {
                    if (delta > 0)
                    {
                        double strength = Math.Min(delta / range, 1.0);
                        buyVolume = volume * (0.5 + strength * 0.5);
                        sellVolume = volume - buyVolume;
                    }
                    else if (delta < 0)
                    {
                        double strength = Math.Min(Math.Abs(delta) / range, 1.0);
                        sellVolume = volume * (0.5 + strength * 0.5);
                        buyVolume = volume - sellVolume;
                    }
                    else
                    {
                        buyVolume = volume * 0.5;
                        sellVolume = volume * 0.5;
                    }
                }
                else
                {
                    buyVolume = volume * 0.5;
                    sellVolume = volume * 0.5;
                }

                double priceRange = clipHigh - clipLow;
                int steps = Math.Max(5, (int)(priceRange / _pipSize));
                steps = Math.Min(steps, 20);

                double buyPerStep = buyVolume / steps;
                double sellPerStep = sellVolume / steps;

                for (int step = 0; step <= steps; step++)
                {
                    double p = clipLow + step * (priceRange / steps);
                    double bucket = Math.Floor(p / bucketSize) * bucketSize;

                    double weight = 1.0;
                    if (Math.Abs(p - high) < _pipSize || Math.Abs(p - low) < _pipSize || Math.Abs(p - close) < _pipSize)
                        weight = 1.5;

                    AddVolume(volumeDict, bucket, buyPerStep * weight, sellPerStep * weight);
                }
            }
        }

        // ═══════════════════════════════════════
        //  TICK DATA ANALYSIS
        // ═══════════════════════════════════════

        private void CalculateVolumeFromTicks(Dictionary<double, VolumeSplit> volumeDict,
            DateTime startTime, DateTime endTime, double topPrice, double bottomPrice, double bucketSize)
        {
            try
            {
                var ticks = MarketData.GetTicks();

                if (ticks == null || ticks.Count == 0)
                {
                    Print("No tick data available, falling back to bar analysis.");
                    int startIndex = Bars.OpenTimes.GetIndexByTime(startTime);
                    int endIndex = Bars.OpenTimes.GetIndexByTime(endTime);
                    CalculateVolumeFromBars(volumeDict, startIndex, endIndex, topPrice, bottomPrice, bucketSize);
                    return;
                }

                var filteredTicks = ticks.Where(t => t.Time >= startTime && t.Time <= endTime).ToList();

                if (filteredTicks.Count == 0)
                {
                    Print("No ticks in selected time range, falling back to bar analysis.");
                    int startIndex = Bars.OpenTimes.GetIndexByTime(startTime);
                    int endIndex = Bars.OpenTimes.GetIndexByTime(endTime);
                    CalculateVolumeFromBars(volumeDict, startIndex, endIndex, topPrice, bottomPrice, bucketSize);
                    return;
                }

                Print($"Processing {filteredTicks.Count} ticks...");

                Tick? previousTick = null;

                foreach (var tick in filteredTicks)
                {
                    if (tick.Bid < bottomPrice || tick.Bid > topPrice)
                        continue;

                    double price = (tick.Bid + tick.Ask) / 2.0;
                    double bucket = Math.Floor(price / bucketSize) * bucketSize;

                    bool isBuyTick = false;

                    if (previousTick.HasValue)
                    {
                        if (tick.Bid > previousTick.Value.Bid)
                            isBuyTick = true;
                        else if (tick.Bid < previousTick.Value.Bid)
                            isBuyTick = false;
                        else
                            isBuyTick = tick.Ask - price < price - tick.Bid;
                    }
                    else
                    {
                        isBuyTick = tick.Ask - price < price - tick.Bid;
                    }

                    if (isBuyTick)
                        AddVolume(volumeDict, bucket, 1.0, 0.0);
                    else
                        AddVolume(volumeDict, bucket, 0.0, 1.0);

                    previousTick = tick;
                }

                Print($"Tick analysis complete: {volumeDict.Count} price levels.");
            }
            catch (Exception ex)
            {
                Print("Error in tick analysis: " + ex.Message + " - Falling back to bar analysis");
                int startIndex = Bars.OpenTimes.GetIndexByTime(startTime);
                int endIndex = Bars.OpenTimes.GetIndexByTime(endTime);
                CalculateVolumeFromBars(volumeDict, startIndex, endIndex, topPrice, bottomPrice, bucketSize);
            }
        }
    }

    // Helper class for buy/sell volume split
    public class VolumeSplit
    {
        public double BuyVolume { get; set; }
        public double SellVolume { get; set; }
        public double TotalVolume => BuyVolume + SellVolume;
    }
}