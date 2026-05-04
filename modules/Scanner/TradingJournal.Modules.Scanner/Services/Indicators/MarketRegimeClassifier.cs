using System;
using System.Collections.Generic;
using System.Linq;
using TradingJournal.Modules.Scanner.Common.Enums;
using TradingJournal.Modules.Scanner.Services.ICTAnalysis;
using TradingJournal.Modules.Scanner.Services.LiveData;

namespace TradingJournal.Modules.Scanner.Services.Indicators;

public static class MarketRegimeClassifier
{
    public static MarketRegime Classify(List<CandleData> candles, int period = 14)
    {
        if (candles == null || candles.Count < period * 2)
            return MarketRegime.Transitional;

        // Calculate ATR
        var trueRanges = new List<decimal>();
        for (int i = 1; i < candles.Count; i++)
        {
            var tr = Math.Max(candles[i].High - candles[i].Low,
                Math.Max(Math.Abs(candles[i].High - candles[i - 1].Close),
                         Math.Abs(candles[i].Low - candles[i - 1].Close)));
            trueRanges.Add(tr);
        }

        var atr = new List<decimal>();
        decimal currentAtr = trueRanges.Take(period).Average();
        atr.Add(currentAtr);

        for (int i = period; i < trueRanges.Count; i++)
        {
            currentAtr = ((currentAtr * (period - 1)) + trueRanges[i]) / period;
            atr.Add(currentAtr);
        }

        // Calculate +DM and -DM
        var plusDM = new List<decimal>();
        var minusDM = new List<decimal>();

        for (int i = 1; i < candles.Count; i++)
        {
            var upMove = candles[i].High - candles[i - 1].High;
            var downMove = candles[i - 1].Low - candles[i].Low;

            if (upMove > downMove && upMove > 0)
            {
                plusDM.Add(upMove);
                minusDM.Add(0);
            }
            else if (downMove > upMove && downMove > 0)
            {
                plusDM.Add(0);
                minusDM.Add(downMove);
            }
            else
            {
                plusDM.Add(0);
                minusDM.Add(0);
            }
        }

        // Calculate Smoothed +DM and -DM
        var smoothedPlusDM = new List<decimal>();
        var smoothedMinusDM = new List<decimal>();

        decimal currentPlusDM = plusDM.Take(period).Sum();
        decimal currentMinusDM = minusDM.Take(period).Sum();
        
        smoothedPlusDM.Add(currentPlusDM);
        smoothedMinusDM.Add(currentMinusDM);

        for (int i = period; i < plusDM.Count; i++)
        {
            currentPlusDM = currentPlusDM - (currentPlusDM / period) + plusDM[i];
            currentMinusDM = currentMinusDM - (currentMinusDM / period) + minusDM[i];
            
            smoothedPlusDM.Add(currentPlusDM);
            smoothedMinusDM.Add(currentMinusDM);
        }

        // Calculate +DI and -DI
        var plusDI = new List<decimal>();
        var minusDI = new List<decimal>();

        for (int i = 0; i < smoothedPlusDM.Count; i++)
        {
            if (atr[i] == 0)
            {
                plusDI.Add(0);
                minusDI.Add(0);
            }
            else
            {
                plusDI.Add(100 * smoothedPlusDM[i] / atr[i]);
                minusDI.Add(100 * smoothedMinusDM[i] / atr[i]);
            }
        }

        // Calculate DX
        var dx = new List<decimal>();
        for (int i = 0; i < plusDI.Count; i++)
        {
            var sum = plusDI[i] + minusDI[i];
            if (sum == 0) dx.Add(0);
            else dx.Add(100 * Math.Abs(plusDI[i] - minusDI[i]) / sum);
        }

        // Calculate ADX
        var adx = new List<decimal>();
        if (dx.Count >= period)
        {
            decimal currentAdx = dx.Take(period).Average();
            adx.Add(currentAdx);

            for (int i = period; i < dx.Count; i++)
            {
                currentAdx = ((currentAdx * (period - 1)) + dx[i]) / period;
                adx.Add(currentAdx);
            }
        }

        if (adx.Count == 0) return MarketRegime.Transitional;

        decimal latestAdx = adx.Last();
        decimal latestPlusDI = plusDI.Last();
        decimal latestMinusDI = minusDI.Last();
        decimal latestAtr = atr.Last();
        
        decimal medianAtr = atr.OrderBy(x => x).ElementAt(atr.Count / 2);

        if (latestAdx > 25)
        {
            if (latestPlusDI > latestMinusDI) return MarketRegime.TrendingUp;
            return MarketRegime.TrendingDown;
        }
        else if (latestAdx < 20)
        {
            if (latestAtr < medianAtr) return MarketRegime.RangeBound;
            return MarketRegime.HighVolatility;
        }

        return MarketRegime.Transitional;
    }
}
