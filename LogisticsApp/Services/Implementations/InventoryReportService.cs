using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Services.Interfaces;

namespace LogisticsApp.Services.Implementations;

public class InventoryReportService : IInventoryReportService
{
    public Task<byte[]> GenerateInventoryBalanceReportAsync(DateTime startDate, DateTime endDate, List<InventoryBalanceDto> data)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Оборотно-сальдовая ведомость");

            worksheet.Cell(1, 1).Value = $"ОБОРОТНО-САЛЬДОВАЯ ВЕДОМОСТЬ ПО СКЛАДАМ ЗА ПЕРИОД С {startDate:dd.MM.yyyy} ПО {endDate:dd.MM.yyyy}";
            worksheet.Range(1, 1, 1, 6).Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(2, 1).Value = $"Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}";
            worksheet.Range(2, 1, 2, 6).Merge();
            worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Cell(2, 1).Style.Font.Italic = true;

            int row = 4;
            worksheet.Cell(row, 1).Value = "Артикул";
            worksheet.Cell(row, 2).Value = "Номенклатура";
            worksheet.Cell(row, 3).Value = "Нач. остаток";
            worksheet.Cell(row, 4).Value = "Приход (+)";
            worksheet.Cell(row, 5).Value = "Расход (-)";
            worksheet.Cell(row, 6).Value = "Кон. остаток";

            var header = worksheet.Range(row, 1, row, 6);
            header.Style.Font.Bold = true;
            header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;

            foreach (var group in data.GroupBy(d => d.WarehouseName))
            {
                worksheet.Cell(row, 1).Value = $"Склад: {group.Key}";
                worksheet.Range(row, 1, row, 6).Merge();
                worksheet.Range(row, 1, row, 6).Style.Font.Bold = true;
                worksheet.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.LightGray;
                worksheet.Range(row, 1, row, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                row++;

                foreach (var item in group.OrderBy(i => i.ProductName))
                {
                    worksheet.Cell(row, 1).Value = item.SKU;
                    worksheet.Cell(row, 2).Value = item.ProductName;
                    worksheet.Cell(row, 3).Value = item.InitialBalance;
                    worksheet.Cell(row, 4).Value = item.ReceiptQuantity;
                    worksheet.Cell(row, 5).Value = item.ExpenseQuantity;
                    worksheet.Cell(row, 6).Value = item.FinalBalance;

                    worksheet.Range(row, 1, row, 6).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    worksheet.Range(row, 1, row, 6).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    row++;
                }
            }

            worksheet.Column(1).Width = 15;
            worksheet.Column(2).Width = 45;
            worksheet.Column(3).Width = 15;
            worksheet.Column(4).Width = 15;
            worksheet.Column(5).Width = 15;
            worksheet.Column(6).Width = 15;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        });
    }

    public Task<byte[]> GenerateDeficitReportAsync(List<DeficitAnalysisDto> data)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Анализ дефицита");

            worksheet.Cell(1, 1).Value = $"АНАЛИЗ ДЕФИЦИТА ТОВАРОВ ДЛЯ ОБЕСПЕЧЕНИЯ ЗАКАЗОВ (НА {DateTime.Now:dd.MM.yyyy HH:mm})";
            worksheet.Range(1, 1, 1, 5).Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 3;
            worksheet.Cell(row, 1).Value = "Артикул";
            worksheet.Cell(row, 2).Value = "Номенклатура";
            worksheet.Cell(row, 3).Value = "Требуется по заказам";
            worksheet.Cell(row, 4).Value = "Доступно на складах";
            worksheet.Cell(row, 5).Value = "Дефицит (нехватка)";

            var header = worksheet.Range(row, 1, row, 5);
            header.Style.Font.Bold = true;
            header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;

            foreach (var item in data.OrderByDescending(d => d.DeficitQuantity))
            {
                worksheet.Cell(row, 1).Value = item.SKU;
                worksheet.Cell(row, 2).Value = item.ProductName;
                worksheet.Cell(row, 3).Value = item.RequiredQuantity;
                worksheet.Cell(row, 4).Value = item.AvailableQuantity;
                worksheet.Cell(row, 5).Value = item.DeficitQuantity;

                var dataRow = worksheet.Range(row, 1, row, 5);
                dataRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                dataRow.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                // Подсвечиваем дефицит красным
                worksheet.Cell(row, 5).Style.Font.FontColor = XLColor.DarkRed;
                worksheet.Cell(row, 5).Style.Font.Bold = true;

                row++;
            }

            worksheet.Column(1).Width = 15;
            worksheet.Column(2).Width = 45;
            worksheet.Column(3).Width = 22;
            worksheet.Column(4).Width = 22;
            worksheet.Column(5).Width = 22;

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        });
    }
}