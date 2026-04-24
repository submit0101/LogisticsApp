using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogisticsApp.Models.DTOs.Reports;

namespace LogisticsApp.Services.Interfaces;

public interface IFinanceReportService
{
    Task<byte[]> GenerateReconciliationReportAsync(DateTime startDate, DateTime endDate, CustomerReconciliationDto data);
    Task<byte[]> GenerateCashFlowReportAsync(DateTime startDate, DateTime endDate, List<CashFlowReportDto> data);
}