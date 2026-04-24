using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using ClosedXML.Excel;
using LogisticsApp.Models.DTOs;
using Microsoft.Win32;

namespace LogisticsApp.Services;

public class ExcelExportService
{
    private readonly NotificationService _notify;

    public ExcelExportService(NotificationService notify)
    {
        _notify = notify;
    }

    public void Export<T>(IEnumerable<T> data, string filename = "Export")
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Workbook|*.xlsx",
            FileName = $"{filename}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (saveFileDialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Data");
            var properties = typeof(T).GetProperties();

            int col = 1;
            foreach (var prop in properties)
            {
                if (prop.GetMethod?.IsVirtual == true && prop.PropertyType.Name != "String") continue;
                var displayName = prop.Name;
                var attribute = prop.GetCustomAttribute<DisplayNameAttribute>();
                if (attribute != null) displayName = attribute.DisplayName;

                worksheet.Cell(1, col).Value = displayName;
                worksheet.Cell(1, col).Style.Font.Bold = true;
                worksheet.Cell(1, col).Style.Fill.BackgroundColor = XLColor.LightGray;
                col++;
            }

            int row = 2;
            foreach (var item in data)
            {
                col = 1;
                foreach (var prop in properties)
                {
                    if (prop.GetMethod?.IsVirtual == true && prop.PropertyType.Name != "String") continue;
                    var value = prop.GetValue(item);
                    if (value is DateTime dt) worksheet.Cell(row, col).Value = dt.ToShortDateString();
                    else if (value is bool b) worksheet.Cell(row, col).Value = b ? "Да" : "Нет";
                    else worksheet.Cell(row, col).Value = value?.ToString();
                    col++;
                }
                row++;
            }

            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(saveFileDialog.FileName);
            _notify.Success($"Данные успешно выгружены в {Path.GetFileName(saveFileDialog.FileName)}");

            new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true } }.Start();
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка экспорта: {ex.Message}");
        }
    }

    public void ExportNomenclaturePriceList(IEnumerable<ProductListDto> data, string filename)
    {
        var saveFileDialog = new SaveFileDialog
        {
            Filter = "Excel Workbook|*.xlsx",
            FileName = $"{filename}_{DateTime.Now:dd_MM_yyyy}.xlsx"
        };

        if (saveFileDialog.ShowDialog() != true) return;

        try
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("TDSheet");

            ws.Cell("D1").Value = "Прайс-лист ООО ТД \"БМК\"";
            ws.Cell("D1").Style.Font.Bold = true;
            ws.Cell("D1").Style.Font.FontSize = 14;

            ws.Cell("D2").Value = $"от {DateTime.Now:dd MMMM yyyy г.}";
            ws.Cell("D2").Style.Font.Italic = true;

            ws.Cell(4, 2).Value = "Код";
            ws.Cell(4, 3).Value = "Наименование";
            ws.Cell(4, 4).Value = "Отпускная цена";
            ws.Cell(4, 5).Value = "Срок годности";
            ws.Cell(4, 6).Value = "Условия хранения";
            ws.Cell(4, 7).Value = "Штрих-код\nEAN13";

            var headerRange = ws.Range(4, 2, 4, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            headerRange.Style.Alignment.WrapText = true;
            headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            int row = 6;

            var groupedData = data.GroupBy(p => string.IsNullOrWhiteSpace(p.GroupName) ? "Без группы" : p.GroupName).OrderBy(g => g.Key);

            foreach (var group in groupedData)
            {
                var groupCell = ws.Cell(row, 2);
                groupCell.Value = group.Key;
                groupCell.Style.Font.Bold = true;
                groupCell.Style.Font.FontSize = 11;

                var rowRange = ws.Range(row, 2, row, 7);
                rowRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F3F4F6");
                rowRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                row++;

                foreach (var item in group.OrderBy(i => i.Name))
                {
                    ws.Cell(row, 2).Value = item.SKU;
                    ws.Cell(row, 3).Value = item.Name;

                    var priceCell = ws.Cell(row, 4);
                    priceCell.Value = item.CurrentPrice;
                    priceCell.Style.NumberFormat.Format = "#,##0.00";

                    ws.Cell(row, 5).Value = item.ShelfLife;
                    ws.Cell(row, 6).Value = item.StorageConditions;

                    ws.Cell(row, 7).Value = $"'{item.Barcode}";

                    var itemRange = ws.Range(row, 2, row, 7);
                    itemRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    itemRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                    row++;
                }
            }

            ws.Column(1).Width = 3;
            ws.Column(2).Width = 14;
            ws.Column(3).Width = 55;
            ws.Column(4).Width = 16;
            ws.Column(5).Width = 15;
            ws.Column(6).Width = 18;
            ws.Column(7).Width = 18;

            workbook.SaveAs(saveFileDialog.FileName);
            _notify.Success($"Прайс-лист успешно выгружен в {Path.GetFileName(saveFileDialog.FileName)}");

            new System.Diagnostics.Process { StartInfo = new System.Diagnostics.ProcessStartInfo(saveFileDialog.FileName) { UseShellExecute = true } }.Start();
        }
        catch (Exception ex)
        {
            _notify.Error($"Ошибка экспорта прайс-листа: {ex.Message}");
        }
    }
}