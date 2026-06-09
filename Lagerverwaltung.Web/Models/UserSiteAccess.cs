using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

[Table("USER_SITE_ACCESS")]
public class UserSiteAccess
{
    public int Id { get; set; }

    public string UserEmail { get; set; } = "";
    public string UserEmailNormalized { get; set; } = "";

    public int SiteId { get; set; }
    public Site? Site { get; set; }

    public int CanOrderValue { get; set; } = 1;
    public int CanFulfillValue { get; set; } = 0;
    public int IsAdminValue { get; set; } = 0;
    public int IsDefaultValue { get; set; } = 0;
    public int IsActiveValue { get; set; } = 1;

    [NotMapped]
    public bool CanOrder
    {
        get => CanOrderValue == 1;
        set => CanOrderValue = value ? 1 : 0;
    }

    [NotMapped]
    public bool CanFulfill
    {
        get => CanFulfillValue == 1;
        set => CanFulfillValue = value ? 1 : 0;
    }

    [NotMapped]
    public bool IsAdmin
    {
        get => IsAdminValue == 1;
        set => IsAdminValue = value ? 1 : 0;
    }

    [NotMapped]
    public bool IsDefault
    {
        get => IsDefaultValue == 1;
        set => IsDefaultValue = value ? 1 : 0;
    }

    [NotMapped]
    public bool IsActive
    {
        get => IsActiveValue == 1;
        set => IsActiveValue = value ? 1 : 0;
    }
}
