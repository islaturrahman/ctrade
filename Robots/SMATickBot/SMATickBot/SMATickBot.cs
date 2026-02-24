using System;
using System.Collections.Generic;
using System.Linq;
using cAlgo.API;
using cAlgo.API.Indicators;

namespace cAlgo.Robots
{
    [Robot(AccessRights = AccessRights.None, AddIndicators = true)]
    public class SMATickBot : Robot
    {
        // ── HMM Parameters ──────────────────────────────────────────
        [Parameter("HMM Training Max Iter", Group = "HMM", DefaultValue = 50, MinValue = 10)]
        public int HmmMaxIterations { get; set; }

        [Parameter("HMM Training Window (Ticks)", Group = "HMM", DefaultValue = 400, MinValue = 50)]
        public int HmmTrainingWindow { get; set; }

        [Parameter("HMM Retrain Interval (Ticks)", Group = "HMM", DefaultValue = 100, MinValue = 10)]
        public int HmmRetrainInterval { get; set; }

        // ── Micro Scalper Exits ──────────────────────────────────────
        [Parameter("Target Win ($)", Group = "Micro Scalper Exits", DefaultValue = 1.0, MinValue = 0.1, Step = 0.1)]
        public double TargetCashWin { get; set; }

        [Parameter("Max Loss ($)", Group = "Micro Scalper Exits", DefaultValue = 1.0, MinValue = 0.1, Step = 0.1)]
        public double MaxCashLoss { get; set; }

        [Parameter("Pips Safety Stop", Group = "Micro Scalper Exits", DefaultValue = 50, MinValue = 1)]
        public int PipsSafetyStop { get; set; }

        // ── Sniper Entry Filters ────────────────────────────────────
        [Parameter("Tick BB Period", Group = "Sniper Filters", DefaultValue = 100, MinValue = 10)]
        public int TickBbPeriod { get; set; }

        [Parameter("Tick BB Deviation", Group = "Sniper Filters", DefaultValue = 3.0, MinValue = 1.0, Step = 0.1)]
        public double TickBbDeviation { get; set; }

        [Parameter("Tick RSI Period", Group = "Sniper Filters", DefaultValue = 14, MinValue = 5)]
        public int TickRsiPeriod { get; set; }

        [Parameter("Tick RSI Overbought", Group = "Sniper Filters", DefaultValue = 85, MinValue = 50)]
        public int TickRsiOverbought { get; set; }

        [Parameter("Tick RSI Oversold", Group = "Sniper Filters", DefaultValue = 15, MaxValue = 50)]
        public int TickRsiOversold { get; set; }

        // ── HMM & HTF Parameters ────────────────────────────────────
        [Parameter("Regime Persistence (Ticks)", Group = "HMM Filter", DefaultValue = 3, MinValue = 1)]
        public int HmmPersistence { get; set; }

        [Parameter("HTF Timeframe", Group = "HTF Filter", DefaultValue = "Minute5")]
        public TimeFrame HtfTimeframe { get; set; }

        [Parameter("HTF SMA Period", Group = "HTF Filter", DefaultValue = 200, MinValue = 10)]
        public int HtfSmaPeriod { get; set; }

        // ── Constants & Enums ───────────────────────────────────────
        private const string BotLabel = "SMATickBot";
        private const int NumStates = 3;  // Hidden regimes
        private const int NumSymbols = 3; // 0: Up, 1: Down, 2: Flat

        public enum MarketRegime
        {
            Bullish,
            Bearish,
            Ranging,
            Unknown
        }

        // ── Internal ────────────────────────────────────────────────
        private readonly List<double> _tickPrices = new List<double>();
        private readonly List<int> _tickSymbols = new List<int>(); // The sequence of observations
        private int _ticksSinceLastTraining = 0;

        // Visuals & Filters
        private DiscreteHMM _hmm;
        private int _bullishHMMState = -1;
        private int _bearishHMMState = -1;
        private MarketRegime _lastRegime = MarketRegime.Unknown;
        private int _consecutiveRegimeTicks = 0;
        private bool _hasTradedThisRegime = false;

        // Indicators
        private Bars _htfBars;
        private SimpleMovingAverage _htfSma;

        protected override void OnStart()
        {
            _htfBars = MarketData.GetBars(HtfTimeframe);
            _htfSma = Indicators.SimpleMovingAverage(_htfBars.ClosePrices, HtfSmaPeriod);

            // Initialize HMM
            _hmm = new DiscreteHMM(NumStates, NumSymbols);
            
            Print("HMM Bot Started — 3 Regimes | Window: {0} | Retrain: {1} | Persistence: {2} | HTF SMA: {3}",
                HmmTrainingWindow, HmmRetrainInterval, HmmPersistence, HtfSmaPeriod);
        }

        protected override void OnTick()
        {
            double price = (Symbol.Bid + Symbol.Ask) / 2.0;

            if (_tickPrices.Count > 0)
            {
                double prevPrice = _tickPrices[_tickPrices.Count - 1];
                int symbol;
                if (price > prevPrice) symbol = 0;      // Up
                else if (price < prevPrice) symbol = 1; // Down
                else symbol = 2;                        // Flat
                
                _tickSymbols.Add(symbol);
            }
            
            _tickPrices.Add(price);

            // Buffer management
            if (_tickSymbols.Count > HmmTrainingWindow * 2)
            {
                _tickPrices.RemoveRange(0, _tickPrices.Count - HmmTrainingWindow - 10);
                _tickSymbols.RemoveRange(0, _tickSymbols.Count - HmmTrainingWindow - 10);
            }

            // Not enough data to train yet
            if (_tickSymbols.Count < HmmTrainingWindow)
            {
                if (_tickSymbols.Count % 50 == 0)
                    Print("Gathering HMM data: {0}/{1} ticks", _tickSymbols.Count, HmmTrainingWindow);
                return;
            }

            _ticksSinceLastTraining++;

            // 1. Train the HMM periodically
            if (_ticksSinceLastTraining >= HmmRetrainInterval)
            {
                _ticksSinceLastTraining = 0;
                TrainModel();
            }

            // If we haven't mapped the states yet, skip trading
            if (_bullishHMMState == -1 || _bearishHMMState == -1)
                return;

            // ── Micro Scalping Cash Exits ───────────────────────────
            foreach (var position in Positions.FindAll(BotLabel, SymbolName))
            {
                if (position.GrossProfit >= TargetCashWin)
                {
                    ClosePosition(position);
                    Print("+$ Target Hit: {0:F2}", position.GrossProfit);
                }
                else if (position.GrossProfit <= -MaxCashLoss)
                {
                    ClosePosition(position);
                    Print("-$ Stop Hit: {0:F2}", position.GrossProfit);
                }
            }

            // 2. Decode current regime using Viterbi
            // Decode the last 20 ticks for immediate responsiveness
            int decodeWindow = 20;
            int[] recentObs = _tickSymbols.Skip(Math.Max(0, _tickSymbols.Count - decodeWindow)).ToArray();
            int[] hiddenPath = _hmm.DecodeViterbi(recentObs);
            int currentHiddenState = hiddenPath[hiddenPath.Length - 1]; // Current regime

            MarketRegime currentRegime = MarketRegime.Unknown;
            if (currentHiddenState == _bullishHMMState) currentRegime = MarketRegime.Bullish;
            else if (currentHiddenState == _bearishHMMState) currentRegime = MarketRegime.Bearish;
            else currentRegime = MarketRegime.Ranging;

            // Update persistence tracking
            if (currentRegime == _lastRegime)
            {
                _consecutiveRegimeTicks++;
            }
            else
            {
                _lastRegime = currentRegime;
                _consecutiveRegimeTicks = 1;
                _hasTradedThisRegime = false;
            }

            // ── Execute Logic (Micro Momentum Breakout) ─────────────
            int htfIndex = _htfBars.Count - 1;
            if (htfIndex < 1 || double.IsNaN(_htfSma.Result[htfIndex]))
                return;

            double htfClose = _htfBars.ClosePrices[htfIndex];
            double htfSmaValue = _htfSma.Result[htfIndex];
            bool htfBullish = htfClose > htfSmaValue;
            bool htfBearish = htfClose < htfSmaValue;

            bool isPersistenceMet = _consecutiveRegimeTicks >= HmmPersistence;

            // Compute BB Calculate
            if (_tickPrices.Count < Math.Max(TickBbPeriod, TickRsiPeriod + 1)) 
                return;

            int bbStartIndex = _tickPrices.Count - TickBbPeriod;
            double bbMean = _tickPrices.Skip(bbStartIndex).Average();
            double sumSq = _tickPrices.Skip(bbStartIndex).Sum(p => Math.Pow(p - bbMean, 2));
            double bbStdDev = Math.Sqrt(sumSq / (TickBbPeriod - 1));

            double upperBand = bbMean + (TickBbDeviation * bbStdDev);
            double lowerBand = bbMean - (TickBbDeviation * bbStdDev);

            // Compute Tick RSI Calculate
            double gain = 0;
            double loss = 0;
            for (int i = _tickPrices.Count - TickRsiPeriod; i < _tickPrices.Count; i++)
            {
                double change = _tickPrices[i] - _tickPrices[i - 1];
                if (change > 0) gain += change;
                else loss -= change;
            }
            double avgGain = gain / TickRsiPeriod;
            double avgLoss = loss / TickRsiPeriod;
            double rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
            double currentRsi = avgLoss == 0 ? 100 : 100 - (100 / (1 + rs));

            double currentPrice = _tickPrices[_tickPrices.Count - 1];

            // ── SNIPER BUY: HTF Up + Extreme Crash + HMM Recovers ──
            if (currentRegime == MarketRegime.Bullish)
            {
                if (HasOpenPosition(TradeType.Sell))
                {
                    ClosePositions(TradeType.Sell);
                    Print("⊘ HMM Trend Reversed — closing SELL");
                }

                if (!HasOpenPosition(TradeType.Buy) && isPersistenceMet && htfBullish && !_hasTradedThisRegime)
                {
                    // Confirm exhaustion (either BB pierced or RSI extreme)
                    if (currentPrice < lowerBand || currentRsi <= TickRsiOversold)
                    {
                        _hasTradedThisRegime = true;
                        double volume = Symbol.VolumeInUnitsMin;
                        ExecuteMarketOrder(TradeType.Buy, SymbolName, volume, BotLabel, PipsSafetyStop, null);
                        Print("▲ BUY SNIPER | RSI: {0:F1} | P < BB: {1}", currentRsi, currentPrice < lowerBand);
                    }
                }
            }
            // ── SNIPER SELL: HTF Down + Extreme Pump + HMM Recovers ──
            else if (currentRegime == MarketRegime.Bearish)
            {
                if (HasOpenPosition(TradeType.Buy))
                {
                    ClosePositions(TradeType.Buy);
                    Print("⊘ HMM Trend Reversed — closing BUY");
                }

                if (!HasOpenPosition(TradeType.Sell) && isPersistenceMet && htfBearish && !_hasTradedThisRegime)
                {
                    // Confirm exhaustion
                    if (currentPrice > upperBand || currentRsi >= TickRsiOverbought)
                    {
                        _hasTradedThisRegime = true;
                        double volume = Symbol.VolumeInUnitsMin;
                        ExecuteMarketOrder(TradeType.Sell, SymbolName, volume, BotLabel, PipsSafetyStop, null);
                        Print("▼ SELL SNIPER | RSI: {0:F1} | P > BB: {1}", currentRsi, currentPrice > upperBand);
                    }
                }
            }
        }

        private void TrainModel()
        {
            // Extract latest window of observations
            int[] obs = _tickSymbols.Skip(Math.Max(0, _tickSymbols.Count - HmmTrainingWindow)).ToArray();
            
            // Run Baum-Welch
            _hmm.TrainBaumWelch(obs, HmmMaxIterations);

            // Map the hidden states based on emission probabilities
            // State emitting the most '0' (Up) is Bullish
            // State emitting the most '1' (Down) is Bearish
            
            double maxUpProb = -1.0;
            double maxDownProb = -1.0;
            int bullState = -1;
            int bearState = -1;

            for (int s = 0; s < NumStates; s++)
            {
                double pUp = _hmm.Emissions[s, 0];
                double pDown = _hmm.Emissions[s, 1];

                if (pUp > maxUpProb)
                {
                    maxUpProb = pUp;
                    bullState = s;
                }
                if (pDown > maxDownProb)
                {
                    maxDownProb = pDown;
                    bearState = s;
                }
            }

            // Fallback collision resolution
            if (bullState == bearState)
            {
                // If one state heavily dominates both, the model hasn't cleanly separated them
                // We pick the second best for whichever is weaker
                if (maxUpProb > maxDownProb)
                {
                    // Find alternative for bear
                    double secondMaxDown = -1;
                    for (int s = 0; s < NumStates; s++)
                    {
                        if (s == bullState) continue;
                        if (_hmm.Emissions[s, 1] > secondMaxDown)
                        {
                            secondMaxDown = _hmm.Emissions[s, 1];
                            bearState = s;
                        }
                    }
                }
                else
                {
                    // Find alternative for bull
                    double secondMaxUp = -1;
                    for (int s = 0; s < NumStates; s++)
                    {
                        if (s == bearState) continue;
                        if (_hmm.Emissions[s, 0] > secondMaxUp)
                        {
                            secondMaxUp = _hmm.Emissions[s, 0];
                            bullState = s;
                        }
                    }
                }
            }

            _bullishHMMState = bullState;
            _bearishHMMState = bearState;

            Print("🧠 HMM Trained! States → Bullish: {0} (P_Up={1:P1}) | Bearish: {2} (P_Down={3:P1})",
                bullState, maxUpProb, bearState, _hmm.Emissions[bearState, 1]);
        }

        // ═══════════════════════════════════════════════════════════
        //  DISCRETE HIDDEN MARKOV MODEL ENGINE (PURE C#)
        // ═══════════════════════════════════════════════════════════
        public class DiscreteHMM
        {
            public int NumStates { get; }
            public int NumSymbols { get; }

            // A: Transition Matrix [state, next_state]
            public double[,] Transitions { get; private set; }
            // B: Emission Matrix [state, symbol]
            public double[,] Emissions { get; private set; }
            // Pi: Initial State Probabilities
            public double[] Initial { get; private set; }

            private readonly Random _rand = new Random(42);

            public DiscreteHMM(int numStates, int numSymbols)
            {
                NumStates = numStates;
                NumSymbols = numSymbols;
                InitializeRandomly();
            }

            private void InitializeRandomly()
            {
                Transitions = new double[NumStates, NumStates];
                Emissions = new double[NumStates, NumSymbols];
                Initial = new double[NumStates];

                for (int i = 0; i < NumStates; i++)
                {
                    Initial[i] = _rand.NextDouble() + 0.1;
                    for (int j = 0; j < NumStates; j++) Transitions[i, j] = _rand.NextDouble() + 0.1;
                    for (int k = 0; k < NumSymbols; k++) Emissions[i, k] = _rand.NextDouble() + 0.1;
                    
                    Normalize(Initial);
                    NormalizeRow(Transitions, i);
                    NormalizeRow(Emissions, i);
                }
            }

            private void Normalize(double[] arr)
            {
                double sum = arr.Sum();
                for (int i = 0; i < arr.Length; i++) arr[i] /= sum;
            }

            private void NormalizeRow(double[,] matrix, int row)
            {
                double sum = 0;
                int cols = matrix.GetLength(1);
                for (int j = 0; j < cols; j++) sum += matrix[row, j];
                for (int j = 0; j < cols; j++) matrix[row, j] /= sum;
            }

            /// <summary>
            /// Forward-Backward (Baum-Welch) Algorithm for unsupervised training
            /// </summary>
            public void TrainBaumWelch(int[] observations, int maxIter)
            {
                int T = observations.Length;
                double scaleMin = 1e-10;

                for (int iter = 0; iter < maxIter; iter++)
                {
                    // α (Forward) variables scaling factors
                    double[,] alpha = new double[T, NumStates];
                    double[] c = new double[T];

                    // 1. Forward Pass
                    for (int i = 0; i < NumStates; i++)
                    {
                        alpha[0, i] = Initial[i] * Emissions[i, observations[0]];
                        c[0] += alpha[0, i];
                    }
                    if (c[0] < scaleMin) c[0] = scaleMin; // prevent div/0
                    c[0] = 1.0 / c[0];
                    for (int i = 0; i < NumStates; i++) alpha[0, i] *= c[0];

                    for (int t = 1; t < T; t++)
                    {
                        for (int i = 0; i < NumStates; i++)
                        {
                            double sum = 0;
                            for (int j = 0; j < NumStates; j++)
                                sum += alpha[t - 1, j] * Transitions[j, i];
                            alpha[t, i] = sum * Emissions[i, observations[t]];
                            c[t] += alpha[t, i];
                        }

                        if (c[t] < scaleMin) c[t] = scaleMin;
                        c[t] = 1.0 / c[t];
                        for (int i = 0; i < NumStates; i++) alpha[t, i] *= c[t];
                    }

                    // 2. Backward Pass (β)
                    double[,] beta = new double[T, NumStates];
                    for (int i = 0; i < NumStates; i++)
                        beta[T - 1, i] = 1.0 * c[T - 1];

                    for (int t = T - 2; t >= 0; t--)
                    {
                        for (int i = 0; i < NumStates; i++)
                        {
                            double sum = 0;
                            for (int j = 0; j < NumStates; j++)
                            {
                                sum += Transitions[i, j] * Emissions[j, observations[t + 1]] * beta[t + 1, j];
                            }
                            beta[t, i] = sum * c[t];
                        }
                    }

                    // 3. Compute γ and ξ
                    double[,] gamma = new double[T, NumStates];
                    double[,,] xi = new double[T - 1, NumStates, NumStates];

                    for (int t = 0; t < T - 1; t++)
                    {
                        for (int i = 0; i < NumStates; i++)
                        {
                            double gSum = 0;
                            for (int j = 0; j < NumStates; j++)
                            {
                                double prob = alpha[t, i] * Transitions[i, j] * Emissions[j, observations[t + 1]] * beta[t + 1, j];
                                xi[t, i, j] = prob;
                                gSum += prob;
                            }
                            gamma[t, i] = gSum;
                        }
                    }
                    
                    // Special case for T-1
                    for (int i = 0; i < NumStates; i++)
                        gamma[T - 1, i] = alpha[T - 1, i];

                    // 4. M-Step: Update Model Parameters
                    // Initial
                    for (int i = 0; i < NumStates; i++)
                        Initial[i] = gamma[0, i];
                    Normalize(Initial);

                    // Transitions
                    for (int i = 0; i < NumStates; i++)
                    {
                        double denom = 0;
                        for (int t = 0; t < T - 1; t++) denom += gamma[t, i];

                        if (denom > 0)
                        {
                            for (int j = 0; j < NumStates; j++)
                            {
                                double num = 0;
                                for (int t = 0; t < T - 1; t++) num += xi[t, i, j];
                                Transitions[i, j] = num / denom;
                            }
                        }
                    }

                    // Emissions
                    for (int i = 0; i < NumStates; i++)
                    {
                        double denom = 0;
                        for (int t = 0; t < T; t++) denom += gamma[t, i];

                        if (denom > 0)
                        {
                            for (int k = 0; k < NumSymbols; k++)
                            {
                                double num = 0;
                                for (int t = 0; t < T; t++)
                                {
                                    if (observations[t] == k) num += gamma[t, i];
                                }
                                Emissions[i, k] = num / denom;
                            }
                        }
                    }

                    // Force stochasticity to handle precisions issues
                    for (int i=0; i<NumStates; i++)
                    {
                        NormalizeRow(Transitions, i);
                        NormalizeRow(Emissions, i);
                    }
                }
            }

            /// <summary>
            /// Viterbi Algorithm to find the most probable hidden state path
            /// </summary>
            public int[] DecodeViterbi(int[] observations)
            {
                int T = observations.Length;
                if (T == 0) return new int[0];

                double[,] v = new double[T, NumStates];
                int[,] ptr = new int[T, NumStates];

                // Initialize base cases in log space to prevent underflow
                for (int i = 0; i < NumStates; i++)
                {
                    v[0, i] = Math.Log(Math.Max(1e-10, Initial[i])) + Math.Log(Math.Max(1e-10, Emissions[i, observations[0]]));
                    ptr[0, i] = 0;
                }

                // Run Viterbi for t > 0
                for (int t = 1; t < T; t++)
                {
                    for (int j = 0; j < NumStates; j++)
                    {
                        double maxTrProb = double.NegativeInfinity;
                        int maxPtr = 0;

                        for (int i = 0; i < NumStates; i++)
                        {
                            double trProb = v[t - 1, i] + Math.Log(Math.Max(1e-10, Transitions[i, j]));
                            if (trProb > maxTrProb)
                            {
                                maxTrProb = trProb;
                                maxPtr = i;
                            }
                        }

                        v[t, j] = maxTrProb + Math.Log(Math.Max(1e-10, Emissions[j, observations[t]]));
                        ptr[t, j] = maxPtr;
                    }
                }

                // Backtracking
                int[] path = new int[T];
                double maxFinalProb = double.NegativeInfinity;
                int bestFinalState = 0;

                for (int i = 0; i < NumStates; i++)
                {
                    if (v[T - 1, i] > maxFinalProb)
                    {
                        maxFinalProb = v[T - 1, i];
                        bestFinalState = i;
                    }
                }

                path[T - 1] = bestFinalState;
                for (int t = T - 2; t >= 0; t--)
                {
                    path[t] = ptr[t + 1, path[t + 1]];
                }

                return path;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  HELPERS
        // ═══════════════════════════════════════════════════════════


        private void ClosePositions(TradeType tradeType)
        {
            foreach (var position in Positions.FindAll(BotLabel, SymbolName))
            {
                if (position.TradeType == tradeType)
                {
                    ClosePosition(position);
                    Print("⊘ Closed {0} @ {1:F5}", tradeType, position.EntryPrice);
                }
            }
        }

        private bool HasOpenPosition(TradeType tradeType)
        {
            foreach (var position in Positions.FindAll(BotLabel, SymbolName))
            {
                if (position.TradeType == tradeType)
                    return true;
            }
            return false;
        }

        protected override void OnStop()
        {
            Print("SMATickBot stopped — Total Ticks buffered: {0}", _tickPrices.Count);
        }
    }
}