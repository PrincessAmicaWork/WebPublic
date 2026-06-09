using System.ComponentModel.DataAnnotations.Schema;

namespace Lagerverwaltung.Web.Models;

[Table("Deleted")]
public class Deleted
{
    public int ID { get; set; }

    public DateTime DeletedDate { get; set; }

    public string Comment { get; set; } = " ";

    public string OrderNumber { get; set; } = " ";
}