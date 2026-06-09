using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

[Table("Destroyeds")]
public class Destroyed
{
    public int ID { get; set; }

    public int PositionID { get; set; }

    public string PositionOrderNumber { get; set; } = " ";

    public DateTime DestroyDate { get; set; }

    public string Comment { get; set; } = " ";
}