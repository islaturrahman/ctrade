import clr

clr.AddReference("cAlgo.API")

# Import cAlgo API types
import cAlgo.API.Indicators as Indicators

# Import trading wrapper functions
from robot_wrapper import *

class HighFrequencyTrade(object):
    def on_start(self):
        print("Robot started!")
        # To learn more about cTrader Algo visit our Help Center:
        # https://help.ctrader.com/ctrader-algo/
        
        # Initializing the MACD indicator via api (the Robot instance)
        # MACD (Moving Average Convergence Divergence)
        # Parameters: Source, LongPeriod, ShortPeriod, SignalPeriod
        self.macd = api.Indicators.MacdMain(api.Bars.ClosePrices, 26, 12, 9)
        print("MACD indicator initialized")

    def MacdCrossOver(self):
        # Indicator components (Main, Signal) are DataSeries
        # Last(0) is the current value, Last(1) is the previous value
        curr_macd = self.macd.Main.Last(0)
        curr_signal = self.macd.Signal.Last(0)
        
        prev_macd = self.macd.Main.Last(1)
        prev_signal = self.macd.Signal.Last(1)
        
        # Check for crossover
        if prev_macd <= prev_signal and curr_macd > curr_signal:
            return "Bullish Crossover"
        elif prev_macd >= prev_signal and curr_macd < curr_signal:
            return "Bearish Crossover"

        Indicators.
        
        return None

    def on_tick(self):
        # Check for MACD crossover signals
        crossover = self.MacdCrossOver()
        if crossover:
            print(f"Signal detected on {api.Symbol.Name}: {crossover}")
            print(f"Current MACD: {self.macd.Main.Last(0):.5f}, Signal: {self.macd.Signal.Last(0):.5f}")

    def on_stop(self):
        # Handle cBot stop here
        pass