using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class InventoryDocumentItem
{
    [Key]
    public int ItemID { get; set; }

    [Required]
    public int DocumentID { get; set; }

    [ForeignKey(nameof(DocumentID))]
    public InventoryDocument? Document { get; set; }

    [Required]
    public int ProductID { get; set; }

    [ForeignKey(nameof(ProductID))]
    public Product? Product { get; set; }

    public int? PackagingID { get; set; }

    [ForeignKey(nameof(PackagingID))]
    public ProductPackaging? Packaging { get; set; }

    [Required]
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CostPrice { get; set; }
}