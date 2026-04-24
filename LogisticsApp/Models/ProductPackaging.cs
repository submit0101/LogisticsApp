using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class ProductPackaging
{
    [Key]
    public int PackagingID { get; set; }

    public int ProductID { get; set; }

    [ForeignKey("ProductID")]
    public virtual Product? Product { get; set; }

    public int UnitID { get; set; }

    [ForeignKey("UnitID")]
    public virtual Unit? Unit { get; set; }

    public decimal Coefficient { get; set; } = 1m;

    public double WeightKG { get; set; }

    public double VolumeM3 { get; set; }

    [MaxLength(100)]
    public string Barcode { get; set; } = string.Empty;
}