using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogisticsApp.Models.DTOs.Reports;

namespace LogisticsApp.Services.Interfaces;

public interface IInventoryReportService
{
    Task<byte[]> GenerateInventoryBalanceReportAsync(DateTime startDate, DateTime endDate, List<InventoryBalanceDto> data);
    Task<byte[]> GenerateDeficitReportAsync(List<DeficitAnalysisDto> data);
}