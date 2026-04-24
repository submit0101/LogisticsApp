using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogisticsApp.Models.DTOs.Reports;

namespace LogisticsApp.Services.Interfaces;

public interface ILogisticsAnalyticsReportService
{
    Task<byte[]> GenerateTonKilometerReportAsync(DateTime startDate, DateTime endDate, List<TonKilometerReportDto> data);
    Task<byte[]> GenerateMileageReportAsync(DateTime startDate, DateTime endDate, List<MileageReportDto> data);
    Task<byte[]> GenerateOdometerReportAsync(DateTime startDate, DateTime endDate, List<OdometerReportDto> data);
}