using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ClosedXML.Excel;
using LogisticsApp.Models.DTOs.Reports;
using LogisticsApp.Services.Interfaces;

namespace LogisticsApp.Services.Implementations;

public class FinanceReportService : IFinanceReportService
{
    public Task<byte[]> GenerateReconciliationReportAsync(DateTime startDate, DateTime endDate, CustomerReconciliationDto data)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Акт сверки");

            worksheet.Cell(1, 1).Value = $"АКТ СВЕРКИ ВЗАИМОРАСЧЕТОВ";
            worksheet.Range(1, 1, 1, 4).Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(2, 1).Value = $"Период: с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy}";
            worksheet.Range(2, 1, 2, 4).Merge();
            worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            worksheet.Cell(2, 1).Style.Font.Italic = true;

            worksheet.Cell(4, 1).Value = $"Контрагент: {data.CustomerName} (ИНН: {data.INN})";
            worksheet.Range(4, 1, 4, 4).Merge();
            worksheet.Cell(4, 1).Style.Font.Bold = true;

            int row = 6;
            worksheet.Cell(row, 1).Value = "Дата операции";
            worksheet.Cell(row, 2).Value = "Документ / Основание";
            worksheet.Cell(row, 3).Value = "Дебет (Отгрузка)";
            worksheet.Cell(row, 4).Value = "Кредит (Оплата)";

            var header = worksheet.Range(row, 1, row, 4);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;
            header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;

            // Сальдо начальное
            worksheet.Cell(row, 1).Value = startDate.ToString("dd.MM.yyyy");
            worksheet.Cell(row, 2).Value = "САЛЬДО НА НАЧАЛО ПЕРИОДА";
            if (data.InitialBalance > 0)
            {
                worksheet.Cell(row, 3).Value = data.InitialBalance;
            }
            else if (data.InitialBalance < 0)
            {
                worksheet.Cell(row, 4).Value = Math.Abs(data.InitialBalance);
            }
            worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(row, 1, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            row++;

            // Операции
            decimal totalDebit = 0;
            decimal totalCredit = 0;

            foreach (var item in data.Items.OrderBy(i => i.Date))
            {
                worksheet.Cell(row, 1).Value = item.Date.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cell(row, 2).Value = item.Document;

                if (item.DebtIncrease > 0)
                {
                    worksheet.Cell(row, 3).Value = item.DebtIncrease;
                    totalDebit += item.DebtIncrease;
                }

                if (item.DebtDecrease > 0)
                {
                    worksheet.Cell(row, 4).Value = item.DebtDecrease;
                    totalCredit += item.DebtDecrease;
                }

                worksheet.Range(row, 1, row, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                worksheet.Range(row, 1, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                row++;
            }

            // Обороты за период
            worksheet.Cell(row, 2).Value = "ОБОРОТЫ ЗА ПЕРИОД:";
            worksheet.Cell(row, 3).Value = totalDebit;
            worksheet.Cell(row, 4).Value = totalCredit;
            worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(row, 1, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

            row++;

            // Сальдо конечное
            worksheet.Cell(row, 1).Value = endDate.ToString("dd.MM.yyyy");
            worksheet.Cell(row, 2).Value = "САЛЬДО НА КОНЕЦ ПЕРИОДА";
            if (data.FinalBalance > 0)
            {
                worksheet.Cell(row, 3).Value = data.FinalBalance;
            }
            else if (data.FinalBalance < 0)
            {
                worksheet.Cell(row, 4).Value = Math.Abs(data.FinalBalance);
            }
            worksheet.Range(row, 1, row, 4).Style.Font.Bold = true;
            worksheet.Range(row, 1, row, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            worksheet.Range(row, 1, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // Настройка колонок
            worksheet.Column(1).Width = 18;
            worksheet.Column(2).Width = 50;
            worksheet.Column(3).Width = 20;
            worksheet.Column(4).Width = 20;

            worksheet.Range(7, 3, row, 4).Style.NumberFormat.Format = "#,##0.00";

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        });
    }

    public Task<byte[]> GenerateCashFlowReportAsync(DateTime startDate, DateTime endDate, List<CashFlowReportDto> data)
    {
        return Task.Run(() =>
        {
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Реестр поступлений ДС");

            worksheet.Cell(1, 1).Value = $"РЕЕСТР ПОСТУПЛЕНИЙ ДЕНЕЖНЫХ СРЕДСТВ ЗА ПЕРИОД С {startDate:dd.MM.yyyy} ПО {endDate:dd.MM.yyyy}";
            worksheet.Range(1, 1, 1, 4).Merge();
            worksheet.Cell(1, 1).Style.Font.Bold = true;
            worksheet.Cell(1, 1).Style.Font.FontSize = 14;
            worksheet.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            worksheet.Cell(2, 1).Value = $"Сформирован: {DateTime.Now:dd.MM.yyyy HH:mm}";
            worksheet.Range(2, 1, 2, 4).Merge();
            worksheet.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Cell(2, 1).Style.Font.Italic = true;

            int row = 4;
            worksheet.Cell(row, 1).Value = "Дата платежа";
            worksheet.Cell(row, 2).Value = "Плательщик (Контрагент)";
            worksheet.Cell(row, 3).Value = "Назначение платежа";
            worksheet.Cell(row, 4).Value = "Сумма (₽)";

            var header = worksheet.Range(row, 1, row, 4);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.LightGray;
            header.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            header.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            row++;

            decimal grandTotal = 0;

            foreach (var item in data.OrderBy(d => d.PaymentDate))
            {
                worksheet.Cell(row, 1).Value = item.PaymentDate.ToString("dd.MM.yyyy HH:mm");
                worksheet.Cell(row, 2).Value = item.CustomerName;
                worksheet.Cell(row, 3).Value = item.Description;
                worksheet.Cell(row, 4).Value = item.Amount;

                worksheet.Range(row, 1, row, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                worksheet.Range(row, 1, row, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;

                grandTotal += item.Amount;
                row++;
            }

            worksheet.Cell(row, 3).Value = "ИТОГО ПОСТУПИЛО:";
            worksheet.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            worksheet.Cell(row, 4).Value = grandTotal;

            var footer = worksheet.Range(row, 1, row, 4);
            footer.Style.Font.Bold = true;
            footer.Style.Fill.BackgroundColor = XLColor.LightYellow;
            footer.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            footer.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            worksheet.Column(1).Width = 18;
            worksheet.Column(2).Width = 40;
            worksheet.Column(3).Width = 50;
            worksheet.Column(4).Width = 20;

            worksheet.Range(5, 4, row, 4).Style.NumberFormat.Format = "#,##0.00";

            using var ms = new MemoryStream();
            workbook.SaveAs(ms);
            return ms.ToArray();
        });
    }
}