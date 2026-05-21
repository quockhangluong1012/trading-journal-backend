using TradingJournal.Shared.Common.Enum;
using TradingJournal.Shared.Dtos;

namespace TradingJournal.Shared.Interfaces;

public interface ITradeRiskAssessmentService
{
    Task<TradeRiskAssessmentDto> AssessAsync(
        int userId,
        string asset,
        decimal entryPrice,
        decimal stopLossPrice,
        decimal targetPrice,
        TradeStatus tradeStatus,
        int? existingTradeId = null,
        CancellationToken cancellationToken = default);
}
