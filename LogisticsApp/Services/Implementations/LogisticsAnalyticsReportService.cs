using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Services.Interfaces;

namespace LogisticsApp.Services.Implementations;

public class LogisticsAnalyticsReportService : ILogisticsAnalyticsReportService
{
    public Task<byte[]> GenerateTonKilometerReportAsync(DateTime startDate, DateTime endDate, List<TonKilometerReportDto> data)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Тонно-километры");

        worksheet.Cell(2, 1).Value = $"Отчет по путевым листам (тонно-километрам)";
        worksheet.Cell(3, 1).Value = $"за период с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
        worksheet.Range(2, 1, 3, 10).Style.Font.Bold = true;
        worksheet.Range(2, 1, 3, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        int row = 6;
        worksheet.Cell(row, 1).Value = "ТС";
        worksheet.Cell(row, 2).Value = "Пробег, км";
        worksheet.Cell(row, 3).Value = "Пробег с грузом, км";
        worksheet.Cell(row, 4).Value = "Вес, тн";
        worksheet.Cell(row, 5).Value = "Тн * км";
        worksheet.Cell(row, 6).Value = "Время, ч";
        worksheet.Cell(row, 7).Value = "Отопит., ч";
        worksheet.Cell(row, 8).Value = "Рефриж., ч";
        worksheet.Cell(row, 9).Value = "Заправка, л";

        var header = worksheet.Range(row, 1, row, 9);
        header.Style.Font.Bold = true;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.WrapText = true;

        row++;
        var groupedByVehicle = data.GroupBy(d => d.VehicleName).OrderBy(g => g.Key);

        foreach (var group in groupedByVehicle)
        {
            worksheet.Cell(row, 1).Value = group.Key;
            worksheet.Range(row, 1, row, 9).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 9).Style.Fill.BackgroundColor = XLColor.LightGray;

            worksheet.Cell(row, 2).Value = group.Sum(x => x.TotalMileage);
            worksheet.Cell(row, 3).Value = group.Sum(x => x.LoadedMileage);
            worksheet.Cell(row, 4).Value = group.Sum(x => x.TotalWeightTons);
            worksheet.Cell(row, 5).Value = group.Sum(x => x.TonKilometers);
            worksheet.Cell(row, 6).Value = group.Sum(x => x.WorkHours);
            worksheet.Cell(row, 7).Value = group.Sum(x => x.HeaterHours);
            worksheet.Cell(row, 8).Value = group.Sum(x => x.RefrigeratorHours);
            worksheet.Cell(row, 9).Value = group.Sum(x => x.FuelRefueled);
            row++;

            foreach (var item in group.OrderBy(i => i.DateOut))
            {
                worksheet.Cell(row, 1).Value = $"{item.DateOut:dd.MM.yyyy} - {item.DateIn:dd.MM.yyyy} ({item.DriverName})";
                worksheet.Cell(row, 2).Value = item.TotalMileage;
                worksheet.Cell(row, 3).Value = item.LoadedMileage;
                worksheet.Cell(row, 4).Value = item.TotalWeightTons;
                worksheet.Cell(row, 5).Value = item.TonKilometers;
                worksheet.Cell(row, 6).Value = item.WorkHours;
                worksheet.Cell(row, 7).Value = item.HeaterHours;
                worksheet.Cell(row, 8).Value = item.RefrigeratorHours;
                worksheet.Cell(row, 9).Value = item.FuelRefueled;
                row++;
            }
        }

        if (data.Any())
        {
            worksheet.Range(7, 1, row - 1, 9).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            worksheet.Range(7, 1, row - 1, 9).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        worksheet.Column(1).Width = 45;
        for (int i = 2; i <= 9; i++) worksheet.Column(i).Width = 12;
        worksheet.Range(7, 2, row, 9).Style.NumberFormat.Format = "#,##0.0";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> GenerateMileageReportAsync(DateTime startDate, DateTime endDate, List<MileageReportDto> data)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Отчет по пробегу");

        worksheet.Cell(2, 2).Value = "Отчет по пробегу";
        worksheet.Cell(3, 2).Value = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
        worksheet.Range(2, 2, 3, 2).Style.Font.Bold = true;

        int row = 6;
        worksheet.Cell(row, 2).Value = "Транспортное средство";
        worksheet.Cell(row, 3).Value = "Пробег за период, км";
        worksheet.Cell(row, 4).Value = "Пробег с начала экспл., км";

        row++;
        worksheet.Cell(row, 2).Value = "Гос. номер / Модель";

        var header = worksheet.Range(6, 2, 7, 4);
        header.Style.Font.Bold = true;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        row++;
        foreach (var item in data.OrderBy(d => d.VehicleRegNumber))
        {
            worksheet.Cell(row, 2).Value = $"{item.VehicleRegNumber} ({item.VehicleModel})";
            worksheet.Cell(row, 3).Value = item.PeriodMileage;
            worksheet.Cell(row, 4).Value = item.TotalMileageSinceStart;
            row++;
        }

        if (data.Any())
        {
            worksheet.Range(8, 2, row - 1, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            worksheet.Range(8, 2, row - 1, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        worksheet.Column(2).Width = 40;
        worksheet.Column(3).Width = 20;
        worksheet.Column(4).Width = 25;
        worksheet.Range(8, 3, row, 4).Style.NumberFormat.Format = "#,##0.0";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }

    public Task<byte[]> GenerateOdometerReportAsync(DateTime startDate, DateTime endDate, List<OdometerReportDto> data)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Пробег со спидометром");

        worksheet.Cell(2, 2).Value = "Отчет по пробегу со спидометром";
        worksheet.Cell(3, 2).Value = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
        worksheet.Range(2, 2, 3, 2).Style.Font.Bold = true;

        int row = 6;
        worksheet.Cell(row, 2).Value = "Транспортное средство";
        worksheet.Cell(row, 3).Value = "Значение на начало периода";
        worksheet.Cell(row, 4).Value = "Пробег за период";
        worksheet.Cell(row, 5).Value = "Пробег с начала экспл.";
        worksheet.Cell(row, 6).Value = "Значение на конец периода";

        var header = worksheet.Range(6, 2, 6, 6);
        header.Style.Font.Bold = true;
        header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        header.Style.Alignment.WrapText = true;

        row++;
        foreach (var item in data.OrderBy(d => d.VehicleRegNumber))
        {
            worksheet.Cell(row, 2).Value = $"{item.VehicleRegNumber} ({item.VehicleModel})";
            worksheet.Cell(row, 3).Value = item.OdometerStart;
            worksheet.Cell(row, 4).Value = item.PeriodMileage;
            worksheet.Cell(row, 5).Value = item.TotalMileageSinceStart;
            worksheet.Cell(row, 6).Value = item.OdometerEnd;
            row++;
        }

        if (data.Any())
        {
            worksheet.Range(7, 2, row - 1, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            worksheet.Range(7, 2, row - 1, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
        }

        worksheet.Column(2).Width = 40;
        for (int i = 3; i <= 6; i++) worksheet.Column(i).Width = 20;
        worksheet.Range(7, 3, row, 6).Style.NumberFormat.Format = "#,##0.0";

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return Task.FromResult(ms.ToArray());
    }
}