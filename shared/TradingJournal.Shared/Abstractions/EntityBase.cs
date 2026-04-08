using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TradingJournal.Shared.Abstractions;

public abstract class EntityBase<T>
{
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Key]
    [Required]
    public required T Id { get; set; } = default!;

    #region Tracking

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

    public int CreatedBy { get; set; } = 0;

    public bool IsDisabled { get; set; } = false;

    public DateTime? UpdatedDate { get; set; }

    public int? UpdatedBy { get; set; }

    #endregion
}