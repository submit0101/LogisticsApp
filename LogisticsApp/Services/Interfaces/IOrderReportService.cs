using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using LogisticsApp.Models.DTOs.Reports;

namespace LogisticsApp.Services.Interfaces;

public interface IOrderReportService
{
    Task<byte[]> GenerateOrdersByCustomerReportAsync(DateTime startDate, DateTime endDate, List<CustomerOrdersGroupDto> data);
}