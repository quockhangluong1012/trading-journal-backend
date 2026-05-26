using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Modules.Backtest.Domain;

/// <summary>
/// Stores chart drawings for a backtest session as a JSON column.
/// One row per session - the DrawingsJson contains the full array of drawing objects.
/// </summary>
[Table("ChartDrawings", Schema = "Backtest")]
public sealed class ChartDrawing : EntityBase<int>
{
    public int SessionId { get; set; }

    /// <summary>
    /// Serialized JSON array of drawing objects.
    /// Each drawing is anchored to (time, price) coordinates for timeframe-independent scaling.
    /// </summary>
    [Column(TypeName = "nvarchar(max)")]
    public string DrawingsJson { get; set; } = "[]";

    [ForeignKey(nameof(SessionId))]
    public BacktestSession Session { get; set; } = null!;
}
