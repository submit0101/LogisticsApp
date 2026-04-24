using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LogisticsApp.Models;

public class ProductPrice
{
    [Key]
    public int PriceID { get; set; }

    public int ProductID { get; set; }

    [ForeignKey("ProductID")]
    public virtual Product? Product { get; set; }

    public DateTime Period { get; set; }

    public decimal Value { get; set; }
}