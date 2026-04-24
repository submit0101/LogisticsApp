using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Services.Interfaces;

namespace LogisticsApp.Services.Implementations;

public class OrderReportService : IOrderReportService
{
    public Task<byte[]> GenerateOrdersByCustomerReportAsync(DateTime startDate, DateTime endDate, List<CustomerOrdersGroupDto> data)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Отчет по заказам");

        worksheet.Cell(1, 1).Value = $"ОТЧЕТ ПО ЗАКАЗАМ ПОКУПАТЕЛЕЙ ЗА ПЕРИОД С {startDate:dd.MM.yyyy} ПО {endDate:dd.MM.yyyy}";
        worksheet.Range(1, 1, 1, 8).Merge();
        var titleStyle = worksheet.Cell(1, 1).Style;
        titleStyle.Font.Bold = true;
        titleStyle.Font.FontSize = 14;
        titleStyle.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleStyle.Font.FontColor = XLColor.Black;

        worksheet.Cell(2, 1).Value = $"Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}";
        worksheet.Range(2, 1, 2, 8).Merge();
        worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Cell(2, 1).Style.Font.Italic = true;

        int currentRow = 4;
        worksheet.Cell(currentRow, 1).Value = "№ Заказа";
        worksheet.Cell(currentRow, 2).Value = "Дата";
        worksheet.Cell(currentRow, 3).Value = "Код";
        worksheet.Cell(currentRow, 4).Value = "Номенклатура";
        worksheet.Cell(currentRow, 5).Value = "Кол-во";
        worksheet.Cell(currentRow, 6).Value = "Цена, ₽";
        worksheet.Cell(currentRow, 7).Value = "Сумма, ₽";
        worksheet.Cell(currentRow, 8).Value = "Вес, кг";

        var headerRange = worksheet.Range(currentRow, 1, currentRow, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Font.FontColor = XLColor.Black;
        headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        currentRow++;

        decimal grandTotalSum = 0;
        decimal grandTotalWeight = 0;

        foreach (var group in data.OrderBy(g => g.CustomerName))
        {
            worksheet.Cell(currentRow, 1).Value = $"Контрагент: {group.CustomerName}";
            worksheet.Range(currentRow, 1, currentRow, 4).Merge();
            worksheet.Cell(currentRow, 7).Value = group.TotalGroupSum;
            worksheet.Cell(currentRow, 8).Value = group.TotalGroupWeight;

            var groupHeaderRange = worksheet.Range(currentRow, 1, currentRow, 8);
            groupHeaderRange.Style.Font.Bold = true;
            groupHeaderRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            groupHeaderRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            currentRow++;

            foreach (var item in group.Items.OrderBy(i => i.OrderDate).ThenBy(i => i.ProductName))
            {
                worksheet.Cell(currentRow, 1).Value = item.OrderID;
                worksheet.Cell(currentRow, 2).Value = item.OrderDate.ToString("dd.MM.yyyy");
                worksheet.Cell(currentRow, 3).Value = item.ProductSKU;
                worksheet.Cell(currentRow, 4).Value = item.ProductName;
                worksheet.Cell(currentRow, 5).Value = item.Quantity;
                worksheet.Cell(currentRow, 6).Value = item.Price;
                worksheet.Cell(currentRow, 7).Value = item.TotalSum;
                worksheet.Cell(currentRow, 8).Value = item.TotalWeight;

                var dataRowRange = worksheet.Range(currentRow, 1, currentRow, 8);
                dataRowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                dataRowRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                currentRow++;
            }

            grandTotalSum += group.TotalGroupSum;
            grandTotalWeight += group.TotalGroupWeight;
        }

        var totalsRow = worksheet.Range(currentRow, 1, currentRow, 8);
        totalsRow.Style.Font.Bold = true;
        totalsRow.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        totalsRow.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        worksheet.Cell(currentRow, 1).Value = "ОБЩИЙ ИТОГ:";
        worksheet.Range(currentRow, 1, currentRow, 6).Merge();
        worksheet.Cell(currentRow, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Cell(currentRow, 7).Value = grandTotalSum;
        worksheet.Cell(currentRow, 8).Value = grandTotalWeight;

        worksheet.Column(1).Width = 12;
        worksheet.Column(2).Width = 12;
        worksheet.Column(3).Width = 15;
        worksheet.Column(4).Width = 45;
        worksheet.Column(5).Width = 12;
        worksheet.Column(6).Width = 12;
        worksheet.Column(7).Width = 15;
        worksheet.Column(8).Width = 12;

        worksheet.Range(5, 5, currentRow, 8).Style.NumberFormat.Format = "#,##0.00";

        using var memoryStream = new MemoryStream();
        workbook.SaveAs(memoryStream);
        return Task.FromResult(memoryStream.ToArray());
    }
}