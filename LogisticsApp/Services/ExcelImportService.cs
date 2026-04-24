using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using ClosedXML.Excel;
using LogisticsApp.Data;
using LogisticsApp.Models;
using LogisticsApp.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace LogisticsApp.Services;

public partial class ExcelImportService
{
    private readonly IDbContextFactory<LogisticsDbContext> _dbFactory;

    public ExcelImportService(IDbContextFactory<LogisticsDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(кг|г|гр)(?:\W|$)")]
    private static partial Regex WeightRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(л|мл)(?:\W|$)")]
    private static partial Regex VolumeRegex();

    [GeneratedRegex(@"(?:^|\W)(фас|шт|упак|порц|банк[аи]?|бутылк[аи]?|кор)\.?")]
    private static partial Regex PcsRegex();

    [GeneratedRegex(@"(?:^|\W)(вес|весов)\.?")]
    private static partial Regex KgRegex();

    [GeneratedRegex(@"(?:^|\W)(литр)\.?")]
    private static partial Regex LitreRegex();

    [GeneratedRegex(@"\d+шт")]
    private static partial Regex DigitsPcsRegex();

    [GeneratedRegex(@"(?<=\s|^)(г\.|ул\.|обл\.|р-н|пос\.|п\.|с\.|д\.|просп\.|пр-кт|ш\.|деревня|пер\.|мкр\.)", RegexOptions.IgnoreCase)]
    private static partial Regex AddressMarkerRegex();

    [GeneratedRegex(@"[^a-zA-Zа-яА-Я0-9]")]
    private static partial Regex CleanRegNumberRegex();

    [GeneratedRegex(@"(?<![A-Z0-9])[A-HJ-NPR-Z0-9]{17}(?![A-Z0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex SmartVinRegex();

    [GeneratedRegex(@"(?<![A-ZА-Я0-9])([АВЕКМНОРСТУХABEKMHOPCTYX]\s*\d{3}\s*[АВЕКМНОРСТУХABEKMHOPCTYX]{2}\s*\d{2,3}|[АВЕКМНОРСТУХABEKMHOPCTYX]{2}\s*\d{3,4}\s*\d{2,3}|\d{4}\s*[АВЕКМНОРСТУХABEKMHOPCTYX]{2}\s*\d{2,3})(?![A-ZА-Я0-9])", RegexOptions.IgnoreCase)]
    private static partial Regex SmartRegRegex();

    [GeneratedRegex(@"\b(19[9]\d|20[0-3]\d)\s*(г\.?в\.?|год|г\.?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SmartYearWithContextRegex();

    [GeneratedRegex(@"\b(19[9]\d|20[0-3]\d)\b")]
    private static partial Regex SmartYearRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(кг|т|тн|тонн)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SmartKgRegex();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*(м3|куб|м\.куб)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SmartM3Regex();

    [GeneratedRegex(@"\b(реф|рефрижератор|хоу|холод)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SmartFridgeRegex();

    private static string MapLatinToCyrillic(string input)
    {
        var map = new Dictionary<char, char>
        {
            {'A', 'А'}, {'B', 'В'}, {'E', 'Е'}, {'K', 'К'}, {'M', 'М'}, {'H', 'Н'},
            {'O', 'О'}, {'P', 'Р'}, {'C', 'С'}, {'T', 'Т'}, {'Y', 'У'}, {'X', 'Х'}
        };
        return new string(input.Select(c => map.TryGetValue(c, out var cyr) ? cyr : c).ToArray());
    }

    private (int? UnitId, double? Weight, double? Volume) ParseMetrics(string name, int kgId, int pcsId, int lId)
    {
        int? unitId = null;
        double? weight = null;
        double? volume = null;
        string lowerName = name.ToLowerInvariant();
        var weightMatch = WeightRegex().Match(lowerName);
        if (weightMatch.Success)
        {
            if (double.TryParse(weightMatch.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double w))
            {
                weight = weightMatch.Groups[2].Value == "кг" ? w : w / 1000.0;
            }
        }
        var volMatch = VolumeRegex().Match(lowerName);
        if (volMatch.Success)
        {
            if (double.TryParse(volMatch.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double v))
            {
                volume = volMatch.Groups[2].Value == "л" ? v : v / 1000.0;
            }
        }
        if (PcsRegex().IsMatch(lowerName) || DigitsPcsRegex().IsMatch(lowerName))
        {
            unitId = pcsId;
        }
        else if (KgRegex().IsMatch(lowerName))
        {
            unitId = kgId;
        }
        else if (volume.HasValue || LitreRegex().IsMatch(lowerName))
        {
            unitId = lId;
        }
        else if (weight.HasValue)
        {
            unitId = pcsId;
        }
        return (unitId, weight, volume);
    }

    public async Task<(int Added, int Updated, int Errors, string ErrorDetails)> ImportNomenclatureAsync(string filePath)
    {
        int added = 0;
        int updated = 0;
        int errors = 0;
        var errorDetails = new StringBuilder();
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            context.ChangeTracker.AutoDetectChangesEnabled = false;
            var defaultKg = await context.Units.FirstOrDefaultAsync(u => u.Name == "кг" || u.Name == "КГ");
            if (defaultKg == null)
            {
                defaultKg = new Unit { Name = "кг", FullName = "Килограмм", Code = "166" };
                context.Units.Add(defaultKg);
                await context.SaveChangesAsync();
            }
            var defaultPcs = await context.Units.FirstOrDefaultAsync(u => u.Name == "шт" || u.Name == "ШТ");
            if (defaultPcs == null)
            {
                defaultPcs = new Unit { Name = "шт", FullName = "Штука", Code = "796" };
                context.Units.Add(defaultPcs);
                await context.SaveChangesAsync();
            }
            var defaultL = await context.Units.FirstOrDefaultAsync(u => u.Name == "л" || u.Name == "Л");
            if (defaultL == null)
            {
                defaultL = new Unit { Name = "л", FullName = "Литр", Code = "112" };
                context.Units.Add(defaultL);
                await context.SaveChangesAsync();
            }
            var defaultUpak = await context.Units.FirstOrDefaultAsync(u => u.Name == "упак" || u.Name == "УПАК");
            if (defaultUpak == null)
            {
                defaultUpak = new Unit { Name = "упак", FullName = "Упаковка", Code = "778" };
                context.Units.Add(defaultUpak);
                await context.SaveChangesAsync();
            }
            int kgId = defaultKg.UnitID;
            int pcsId = defaultPcs.UnitID;
            int lId = defaultL.UnitID;
            int upakId = defaultUpak.UnitID;
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed();
            int headerRowNumber = -1;
            foreach (var row in rows)
            {
                string col2 = row.Cell(2).GetString().Trim();
                string col3 = row.Cell(3).GetString().Trim();
                if (col2.Equals("Код", StringComparison.OrdinalIgnoreCase) ||
                    col3.Equals("Наименование", StringComparison.OrdinalIgnoreCase))
                {
                    headerRowNumber = row.RowNumber();
                    break;
                }
            }
            if (headerRowNumber == -1)
            {
                return (0, 0, 1, "Header not found");
            }
            ProductGroup? currentGroup = null;
            var dbGroups = await context.ProductGroups.ToListAsync();
            var existingGroups = dbGroups.GroupBy(g => g.Name).ToDictionary(g => g.Key, g => g.First());
            var dbProducts = await context.Products
                .Include(p => p.Prices)
                .Include(p => p.Packagings)
                .ToListAsync();
            var existingProducts = dbProducts.GroupBy(p => p.SKU).ToDictionary(g => g.Key, g => g.First());
            var existingProductsByName = dbProducts.Where(p => !string.IsNullOrWhiteSpace(p.Name)).GroupBy(p => p.Name).ToDictionary(g => g.Key, g => g.First());
            int defaultWarehouseId = await new InventoryService().GetDefaultWarehouseIdAsync(context);
            foreach (var row in rows.Where(r => r.RowNumber() > headerRowNumber))
            {
                try
                {
                    var skuCell = row.Cell(2);
                    var nameCell = row.Cell(3);
                    var priceCell = row.Cell(4);
                    string sku = skuCell.GetString().Trim();
                    string name = nameCell.GetString().Trim();
                    string priceStr = priceCell.GetString().Trim();
                    if (string.IsNullOrWhiteSpace(sku) && string.IsNullOrWhiteSpace(name))
                        continue;
                    bool isBold = false;
                    try
                    {
                        isBold = nameCell.Style.Font.Bold || skuCell.Style.Font.Bold || row.Style.Font.Bold;
                    }
                    catch { }
                    bool isCategory = string.IsNullOrWhiteSpace(priceStr) && (isBold || string.IsNullOrWhiteSpace(sku) || string.IsNullOrWhiteSpace(name));
                    if (isCategory)
                    {
                        string groupName = !string.IsNullOrWhiteSpace(name) ? name : sku;
                        if (!string.IsNullOrWhiteSpace(groupName))
                        {
                            if (!existingGroups.TryGetValue(groupName, out currentGroup))
                            {
                                currentGroup = new ProductGroup { Name = groupName };
                                context.ProductGroups.Add(currentGroup);
                                existingGroups[groupName] = currentGroup;
                            }
                        }
                        continue;
                    }
                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        if (string.IsNullOrWhiteSpace(sku))
                        {
                            if (existingProductsByName.TryGetValue(name, out var existingByName))
                            {
                                sku = existingByName.SKU;
                            }
                            else
                            {
                                sku = "GEN-" + Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
                            }
                        }
                        string shelfLife = row.Cell(5).GetString().Trim();
                        string storage = row.Cell(6).GetString().Trim();
                        string barcode = row.Cell(7).GetString().Trim();
                        decimal newPrice = 0;
                        if (priceCell.DataType == XLDataType.Number)
                        {
                            newPrice = (decimal)priceCell.GetDouble();
                        }
                        else if (!string.IsNullOrWhiteSpace(priceStr))
                        {
                            decimal.TryParse(priceStr.Replace(',', '.').Replace(" ", ""),
                                System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out newPrice);
                        }
                        var metrics = ParseMetrics(name, kgId, pcsId, lId);
                        bool isFallback = !metrics.UnitId.HasValue && !metrics.Weight.HasValue && !metrics.Volume.HasValue;
                        int finalUnitId = metrics.UnitId ?? upakId;
                        double pkgWeight = metrics.Weight ?? (isFallback ? 0.2 : 0);
                        double pkgVolume = metrics.Volume ?? 0;
                        bool isNewProduct = false;
                        if (existingProducts.TryGetValue(sku, out var product))
                        {
                            product.Name = name;
                            product.ShelfLife = shelfLife;
                            product.StorageConditions = storage;
                            product.Barcode = barcode;
                            product.Group = currentGroup;
                            product.BaseUnitID = finalUnitId;
                            if (pkgWeight > 0 || pkgVolume > 0 || isFallback)
                            {
                                int pkgUnitId = isFallback ? upakId : finalUnitId;
                                var existingPkg = product.Packagings.FirstOrDefault(p => p.UnitID == pkgUnitId && p.Coefficient == 1m);
                                if (existingPkg != null)
                                {
                                    existingPkg.WeightKG = pkgWeight;
                                    existingPkg.VolumeM3 = pkgVolume;
                                }
                                else
                                {
                                    product.Packagings.Add(new ProductPackaging
                                    {
                                        UnitID = pkgUnitId,
                                        Coefficient = 1m,
                                        WeightKG = pkgWeight,
                                        VolumeM3 = pkgVolume
                                    });
                                }
                            }
                            context.Entry(product).State = EntityState.Modified;
                            updated++;
                        }
                        else
                        {
                            product = new Product
                            {
                                SKU = sku,
                                Name = name,
                                ShelfLife = shelfLife,
                                StorageConditions = storage,
                                Barcode = barcode,
                                Group = currentGroup,
                                BaseUnitID = finalUnitId,
                                Packagings = new List<ProductPackaging>()
                            };
                            if (pkgWeight > 0 || pkgVolume > 0 || isFallback)
                            {
                                int pkgUnitId = isFallback ? upakId : finalUnitId;
                                product.Packagings.Add(new ProductPackaging
                                {
                                    UnitID = pkgUnitId,
                                    Coefficient = 1m,
                                    WeightKG = pkgWeight,
                                    VolumeM3 = pkgVolume
                                });
                            }
                            context.Products.Add(product);
                            existingProducts[sku] = product;
                            existingProductsByName[name] = product;
                            isNewProduct = true;
                            added++;
                        }
                        if (newPrice > 0)
                        {
                            if (isNewProduct)
                            {
                                product.Prices.Add(new ProductPrice { Period = DateTime.Today, Value = newPrice });
                            }
                            else
                            {
                                var currentActivePrice = product.Prices
                                    .Where(p => p.Period <= DateTime.Today)
                                    .OrderByDescending(p => p.Period)
                                    .FirstOrDefault();
                                if (currentActivePrice == null || currentActivePrice.Value != newPrice)
                                {
                                    var todayPrice = product.Prices.FirstOrDefault(p => p.Period.Date == DateTime.Today);
                                    if (todayPrice != null)
                                    {
                                        todayPrice.Value = newPrice;
                                    }
                                    else
                                    {
                                        product.Prices.Add(new ProductPrice { Period = DateTime.Today, Value = newPrice });
                                    }
                                }
                            }
                        }
                        if (isNewProduct)
                        {
                            context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                Timestamp = DateTime.Now,
                                Product = product,
                                WarehouseID = defaultWarehouseId,
                                Quantity = 1000,
                                IsReserve = false,
                                SourceDocument = "Import",
                                SourceDocumentID = 0
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    errorDetails.AppendLine($"Row {row.RowNumber()}: {ex.Message}");
                }
            }
            context.ChangeTracker.AutoDetectChangesEnabled = true;
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            errors++;
            errorDetails.AppendLine($"Fatal: {ex.Message}");
        }
        return (added, updated, errors, errorDetails.ToString());
    }

    public async Task<(int Added, int Updated, int Errors, string ErrorDetails)> ImportCustomersAsync(string filePath)
    {
        int added = 0;
        int updated = 0;
        int errors = 0;
        var errorDetails = new StringBuilder();
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed();
            int innCol = -1, nameCol = -1, fullNameCol = -1;
            int headerRowNumber = -1;
            foreach (var row in rows.Take(10))
            {
                int maxCol = row.LastCellUsed()?.Address.ColumnNumber ?? 0;
                for (int i = 1; i <= maxCol; i++)
                {
                    string cellValue = row.Cell(i).GetString().Trim().ToLower();
                    if (cellValue == "инн") innCol = i;
                    if (cellValue == "наименование") nameCol = i;
                    if (cellValue == "полное наименование") fullNameCol = i;
                }
                if (innCol != -1 || nameCol != -1)
                {
                    headerRowNumber = row.RowNumber();
                    break;
                }
            }
            if (innCol == -1) innCol = 3;
            if (nameCol == -1) nameCol = 2;
            if (fullNameCol == -1) fullNameCol = 4;
            if (headerRowNumber == -1) headerRowNumber = 1;
            var dbCustomers = await context.Customers.ToListAsync();
            var existingCustomers = dbCustomers.GroupBy(c => c.INN).ToDictionary(g => g.Key, g => g.First());
            foreach (var row in rows.Where(r => r.RowNumber() > headerRowNumber))
            {
                try
                {
                    string rawInn = row.Cell(innCol).GetString().Trim();
                    string rawName = row.Cell(nameCol).GetString().Trim();
                    string fullName = fullNameCol != -1 ? row.Cell(fullNameCol).GetString().Trim() : "";
                    if (string.IsNullOrWhiteSpace(rawInn) || string.IsNullOrWhiteSpace(rawName))
                        continue;
                    string inn = Regex.Replace(rawInn, @"[^\d]", "");
                    if (string.IsNullOrWhiteSpace(inn))
                        continue;
                    string name = rawName;
                    string address = string.Empty;
                    var addressMarkerMatch = AddressMarkerRegex().Match(rawName);
                    if (addressMarkerMatch.Success)
                    {
                        int addressIndex = addressMarkerMatch.Index;
                        name = rawName.Substring(0, addressIndex).Trim(' ', ',', '-');
                        address = rawName.Substring(addressIndex).Trim();
                    }
                    if (existingCustomers.TryGetValue(inn, out var customer))
                    {
                        customer.Name = name;
                        customer.Address = address;
                        if (!string.IsNullOrWhiteSpace(fullName))
                            customer.FullName = fullName;
                        updated++;
                    }
                    else
                    {
                        customer = new Customer
                        {
                            INN = inn,
                            Name = name,
                            FullName = string.IsNullOrWhiteSpace(fullName) ? null : fullName,
                            Address = address,
                            Type = inn.Length == 12 ? CustomerType.Entrepreneur : CustomerType.LegalEntity
                        };
                        context.Customers.Add(customer);
                        existingCustomers[inn] = customer;
                        added++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    errorDetails.AppendLine($"Строка {row.RowNumber()}: {ex.Message}");
                }
            }
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            errors++;
            errorDetails.AppendLine($"Критический сбой: {ex.Message}");
        }
        return (added, updated, errors, errorDetails.ToString());
    }

    public async Task<bool> ExportCustomersAsync(string filePath)
    {
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            var customers = await context.Customers.AsNoTracking().ToListAsync();
            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Покупатели");
            worksheet.Cell(1, 1).Value = "Код";
            worksheet.Cell(1, 2).Value = "Наименование";
            worksheet.Cell(1, 3).Value = "ИНН";
            worksheet.Cell(1, 4).Value = "Полное наименование";
            var headerRange = worksheet.Range("A1:D1");
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.LightGray;
            int row = 2;
            foreach (var customer in customers)
            {
                worksheet.Cell(row, 1).Value = customer.CustomerID;
                string combinedName = string.IsNullOrWhiteSpace(customer.Address)
                    ? customer.Name
                    : $"{customer.Name} {customer.Address}";
                worksheet.Cell(row, 2).Value = combinedName;
                worksheet.Cell(row, 3).Value = customer.INN;
                worksheet.Cell(row, 4).Value = customer.FullName ?? "";
                row++;
            }
            worksheet.Columns().AdjustToContents();
            workbook.SaveAs(filePath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<(int Added, int Updated, int Errors, string ErrorDetails)> ImportVehiclesAsync(string filePath)
    {
        int added = 0;
        int updated = 0;
        int errors = 0;
        var errorDetails = new StringBuilder();
        try
        {
            using var context = await _dbFactory.CreateDbContextAsync();
            using var workbook = new XLWorkbook(filePath);
            var worksheet = workbook.Worksheet(1);
            var rows = worksheet.RowsUsed();

            var dbVehicles = await context.Vehicles.ToListAsync();
            var existingVehicles = dbVehicles.GroupBy(v => CleanRegNumberRegex().Replace(v.RegNumber.ToUpper(), "")).ToDictionary(g => g.Key, g => g.First());
            var existingByVin = dbVehicles.Where(v => !string.IsNullOrWhiteSpace(v.VIN)).GroupBy(v => v.VIN.ToUpper()).ToDictionary(g => g.Key, g => g.First());

            foreach (var row in rows)
            {
                try
                {
                    // Собираем все непустые ячейки строки в единый текст для интеллектуального анализа
                    string fullRowText = string.Join(" ", row.CellsUsed().Select(c => c.GetString().Trim()));
                    if (string.IsNullOrWhiteSpace(fullRowText)) continue;

                    string upperText = fullRowText.ToUpper();

                    // 1. Поиск VIN
                    string vin = "";
                    var vinMatch = SmartVinRegex().Match(upperText);
                    if (vinMatch.Success)
                    {
                        vin = vinMatch.Value;
                        // Вырезаем найденный VIN, чтобы он не сбивал поиск других параметров (например, года или модели)
                        fullRowText = fullRowText.Replace(vinMatch.Value, " ", StringComparison.OrdinalIgnoreCase);
                        upperText = upperText.Replace(vinMatch.Value, " ");
                    }

                    // 2. Поиск Гос. Номера
                    string rawReg = "";
                    string cleanReg = "";
                    var regMatch = SmartRegRegex().Match(upperText);
                    if (regMatch.Success)
                    {
                        rawReg = Regex.Replace(regMatch.Value, @"\s+", "").ToUpper();
                        rawReg = MapLatinToCyrillic(rawReg);
                        cleanReg = CleanRegNumberRegex().Replace(rawReg, "");
                        fullRowText = fullRowText.Replace(regMatch.Value, " ", StringComparison.OrdinalIgnoreCase);
                    }

                    // Если нет ни VIN, ни Номера - скорее всего это строка заголовков или мусор
                    if (string.IsNullOrWhiteSpace(cleanReg) && string.IsNullOrWhiteSpace(vin))
                        continue;

                    // 3. Поиск Года Выпуска
                    int year = DateTime.Today.Year;
                    var yearMatch = SmartYearWithContextRegex().Match(fullRowText);
                    if (!yearMatch.Success) yearMatch = SmartYearRegex().Match(fullRowText);
                    if (yearMatch.Success)
                    {
                        year = int.Parse(yearMatch.Groups[1].Value);
                        fullRowText = fullRowText.Replace(yearMatch.Value, " ");
                    }

                    // 4. Поиск Грузоподъемности (Авто-конвертация тонн в кг)
                    int capKg = 1500;
                    var kgMatch = SmartKgRegex().Match(fullRowText);
                    if (kgMatch.Success)
                    {
                        if (double.TryParse(kgMatch.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double val))
                        {
                            capKg = kgMatch.Groups[2].Value.ToLower().StartsWith("т") ? (int)(val * 1000) : (int)val;
                        }
                        fullRowText = fullRowText.Replace(kgMatch.Value, " ");
                    }

                    // 5. Поиск Объема
                    double capM3 = 10.0;
                    var m3Match = SmartM3Regex().Match(fullRowText);
                    if (m3Match.Success)
                    {
                        if (double.TryParse(m3Match.Groups[1].Value.Replace(',', '.'), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double m3val))
                        {
                            capM3 = m3val;
                        }
                        fullRowText = fullRowText.Replace(m3Match.Value, " ");
                    }

                    // 6. Поиск Рефрижератора
                    bool isFridge = SmartFridgeRegex().IsMatch(fullRowText);
                    if (isFridge) fullRowText = SmartFridgeRegex().Replace(fullRowText, " ");

                    // 7. Поиск Модели (Все, что осталось после очистки строки от метрик)
                    string model = Regex.Replace(fullRowText, @"[^\w\sА-Яа-я-]", " ").Trim();
                    model = Regex.Replace(model, @"\s+", " ").Trim();
                    if (string.IsNullOrWhiteSpace(model) || model.Length < 2)
                    {
                        model = "Неизвестная модель";
                    }
                    if (model.Length > 100) model = model.Substring(0, 100).Trim();

                    Vehicle? vehicle = null;

                    // Поиск машины в кэше по нормализованному номеру или VIN
                    if (!string.IsNullOrWhiteSpace(cleanReg) && existingVehicles.TryGetValue(cleanReg, out var vReg))
                        vehicle = vReg;
                    else if (!string.IsNullOrWhiteSpace(vin) && existingByVin.TryGetValue(vin.ToUpper(), out var vVin))
                        vehicle = vVin;

                    if (vehicle != null)
                    {
                        if (model != "Неизвестная модель") vehicle.Model = model;
                        if (!string.IsNullOrWhiteSpace(rawReg)) vehicle.RegNumber = rawReg;
                        if (!string.IsNullOrWhiteSpace(vin)) vehicle.VIN = vin;
                        if (yearMatch.Success) vehicle.Year = year;
                        if (kgMatch.Success) vehicle.CapacityKG = capKg;
                        if (m3Match.Success) vehicle.CapacityM3 = capM3;
                        if (isFridge) vehicle.IsFridge = true;

                        updated++;
                    }
                    else
                    {
                        vehicle = new Vehicle
                        {
                            RegNumber = string.IsNullOrWhiteSpace(rawReg) ? "Б/Н" : rawReg,
                            Model = model,
                            VIN = vin,
                            Year = year,
                            CapacityKG = capKg,
                            CapacityM3 = capM3,
                            IsFridge = isFridge,
                            FuelType = FuelType.DT,
                            BaseFuelConsumption = 12.0,
                            Status = VehicleStatus.Active,
                            SanitizationDate = DateTime.Today,
                            CurrentFuelLevel = 0,
                            FuelCapacity = 100.0,
                            CargoFuelBonus = 1.3,
                            WinterFuelBonus = 10.0
                        };
                        context.Vehicles.Add(vehicle);
                        if (!string.IsNullOrWhiteSpace(cleanReg)) existingVehicles[cleanReg] = vehicle;
                        if (!string.IsNullOrWhiteSpace(vin)) existingByVin[vin.ToUpper()] = vehicle;
                        added++;
                    }
                }
                catch (Exception ex)
                {
                    errors++;
                    errorDetails.AppendLine($"Строка {row.RowNumber()}: {ex.Message}");
                }
            }
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            errors++;
            errorDetails.AppendLine($"Критическая ошибка: {ex.Message}");
        }
        return (added, updated, errors, errorDetails.ToString());
    }
}