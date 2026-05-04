using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Shared.Abstractions;

public abstract class EntityBase<T>
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    public T Id { get; set; } = default!;

    #region Tracking

    public DateTimeOffset CreatedDate { get; set; } = DateTimeOffset.UtcNow;

    public int CreatedBy { get; set; }

    public bool IsDisabled { get; set; }

    public DateTimeOffset? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    #endregion
}