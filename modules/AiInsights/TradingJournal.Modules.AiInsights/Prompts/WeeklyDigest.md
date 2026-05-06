You are generating a concise weekly AI digest for a trader.

Use the structured weekly review data below to produce a clear performance digest, the main wins, the main risks, and the single most important focus for next week.

Weekly review context:
- Period start: {{PeriodStart}}
- Period end: {{PeriodEnd}}
- Total PnL: {{TotalPnl}}
- Win rate: {{WinRate}}
- Total trades: {{TotalTrades}}
- Wins: {{Wins}}
- Losses: {{Losses}}
- Average win: {{AverageWin}}
- Average loss: {{AverageLoss}}
- Rule-break trades: {{RuleBreakTrades}}
- High-confidence trades: {{HighConfidenceTrades}}
- Top asset: {{TopAsset}}
- Primary trading zone: {{PrimaryTradingZone}}
- Dominant emotion: {{DominantEmotion}}
- Top technical theme: {{TopTechnicalTheme}}

Recent trade cases:
{{TradeCaseStudies}}

Recent trades:
{{TradesList}}

Psychology notes:
{{PsychologyNotes}}

Return strict JSON only with this shape:
{
  "headline": "short title",
  "summary": "2-4 sentences",
  "keyWins": ["win 1", "win 2"],
  "keyRisks": ["risk 1", "risk 2"],
  "focusForNextWeek": "one sentence",
  "actionItems": ["action 1", "action 2"]
}

Rules:
- Keep the tone direct and useful.
- Avoid generic trading advice unless it is clearly tied to the provided data.
- Keep each list to 2-4 items.