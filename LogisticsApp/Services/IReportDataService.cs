using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using LogisticsApp.Models.DTOs.Reports;

namespace LogisticsApp.Services;

public interface IReportDataService
{
    Task<List<WaybillRegistryDto>> GetWaybillsRegistryAsync(DateTime from, DateTime to, CancellationToken ct = default);
}