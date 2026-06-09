using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

[Table("USER_PREFS")]
public class UserPreference
{
    public int Id { get; set; }

    public string UserEmail { get; set; } = "";
    public string UserEmailNormalized { get; set; } = "";

    public int? LastSelectedSiteId { get; set; }
    public Site? LastSelectedSite { get; set; }

    public string PreferredCulture { get; set; } = "de-CH";

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
