using System;

namespace LogisticsApp.Models.DTOs;

public class CustomerDebtDto
{
    public int CustomerID { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string INN { get; set; } = string.Empty;
    public decimal TotalShipped { get; set; }
    public decimal TotalPaid { get; set; }

    public decimal Balance => TotalShipped - TotalPaid;
    public decimal CurrentDebt => Balance > 0 ? Balance : 0;
    public decimal AdvanceAmount => Balance < 0 ? Math.Abs(Balance) : 0;

    public bool IsDebt => Balance > 0;
    public bool IsAdvance => Balance < 0;
    public bool IsSettled => Balance == 0;
}