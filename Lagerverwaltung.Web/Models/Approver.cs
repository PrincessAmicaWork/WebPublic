using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

[Table("Approver")]
public class Approver
{
    public int Id { get; set; }

    [Required]
    [MaxLength(200)]
    public string DisplayName { get; set; } = "";

    [Required]
    [MaxLength(320)]
    public string Email { get; set; } = "";

    [Required]
    [MaxLength(320)]
    public string EmailNormalized { get; set; } = "";

    [Column("IsActive")]
    public int IsActiveValue { get; set; } = 1;

    [NotMapped]
    public bool IsActive
    {
        get => IsActiveValue == 1;
        set => IsActiveValue = value ? 1 : 0;
    }

    [MaxLength(50)]
    public string Source { get; set; } = "CSV";

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public DateTime? LastSyncedAt { get; set; }
}
