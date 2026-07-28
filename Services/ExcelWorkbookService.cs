using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Parcelly.Models;

namespace Parcelly.Services;

/// <summary>
/// Gera e lê .xlsx simples (Open XML) sem dependências externas — funciona offline no WASM.
/// </summary>
public static class ExcelWorkbookService
{
    private static readonly XNamespace Ss = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace Pk = "http://schemas.openxmlformats.org/package/2006/relationships";
    private static readonly XNamespace Ct = "http://schemas.openxmlformats.org/package/2006/content-types";

    public static readonly string[] Headers =
    [
        "Cartao",
        "Compra",
        "Qtd Parcelas",
        "Valor Parcela",
        "Valor Total",
        "Mes Inicio",
        "Mes Final",
        "Finalizou"
    ];

    public static byte[] CreateEmptyTemplate()
    {
        var rows = new List<string[]> { Headers };
        // Algumas linhas em branco para facilitar o preenchimento no Excel.
        for (var i = 0; i < 15; i++)
            rows.Add(new string[Headers.Length]);
        return BuildXlsx(rows);
    }

    public static byte[] ExportPlans(IEnumerable<InstallmentPlan> plans, PaymentMode mode = PaymentMode.Mixed)
    {
        var rows = new List<string[]> { Headers };
        foreach (var plan in plans.OrderBy(p => p.Card).ThenBy(p => p.StartDate))
        {
            var finished = plan.IsPaidOff(mode)
                ? "Finalizado"
                : $"Ate {PaymentRules.FormatMonth(plan.EndDate ?? plan.StartDate)}";

            rows.Add(
            [
                plan.Card,
                plan.Name,
                plan.TotalInstallments.ToString(CultureInfo.InvariantCulture),
                plan.InstallmentAmount.ToString("0.00", CultureInfo.InvariantCulture),
                plan.TotalAmount.ToString("0.00", CultureInfo.InvariantCulture),
                plan.StartDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                (plan.EndDate ?? plan.StartDate).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                finished
            ]);
        }

        return BuildXlsx(rows);
    }

    public static List<InstallmentPlan> ImportPlans(byte[] xlsxBytes)
    {
        using var ms = new MemoryStream(xlsxBytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);

        var shared = ReadSharedStrings(zip);
        var sheetEntry = zip.GetEntry("xl/worksheets/sheet1.xml")
                         ?? zip.Entries.FirstOrDefault(e => e.FullName.Contains("worksheets/sheet", StringComparison.OrdinalIgnoreCase))
                         ?? throw new InvalidOperationException("Planilha não encontrada no arquivo Excel.");

        using var sheetStream = sheetEntry.Open();
        var doc = XDocument.Load(sheetStream);
        var sheetData = doc.Root?.Element(Ss + "sheetData")
                        ?? throw new InvalidOperationException("Conteúdo da planilha inválido.");

        var table = new List<string[]>();
        foreach (var row in sheetData.Elements(Ss + "row"))
        {
            var cells = row.Elements(Ss + "c").ToList();
            if (cells.Count == 0) continue;

            var values = new string[Headers.Length];
            foreach (var cell in cells)
            {
                var refer = cell.Attribute("r")?.Value ?? "";
                var col = ColumnIndex(refer);
                if (col < 0 || col >= Headers.Length) continue;
                values[col] = GetCellValue(cell, shared);
            }

            if (values.All(string.IsNullOrWhiteSpace)) continue;
            table.Add(values);
        }

        if (table.Count == 0) return [];

        // Pula cabeçalho se a primeira linha parecer header.
        var start = LooksLikeHeader(table[0]) ? 1 : 0;
        var plans = new List<InstallmentPlan>();

        for (var i = start; i < table.Count; i++)
        {
            var cols = table[i];
            var card = (cols.ElementAtOrDefault(0) ?? "").Trim();
            var name = (cols.ElementAtOrDefault(1) ?? "").Trim();
            if (string.IsNullOrWhiteSpace(card) && string.IsNullOrWhiteSpace(name))
                continue;
            if (string.IsNullOrWhiteSpace(card) || string.IsNullOrWhiteSpace(name))
                continue;

            var qty = ParseInt(cols.ElementAtOrDefault(2));
            var parcel = ParseDecimal(cols.ElementAtOrDefault(3));
            var total = ParseDecimal(cols.ElementAtOrDefault(4));
            var startDate = ParseMonth(cols.ElementAtOrDefault(5)) ?? DateOnly.FromDateTime(DateTime.Today);
            var finishedText = cols.ElementAtOrDefault(7) ?? "";
            var finished = finishedText.Contains("Finalizado", StringComparison.OrdinalIgnoreCase);

            if (qty <= 0 || parcel <= 0)
            {
                if (qty <= 0 && total > 0 && parcel > 0)
                    qty = (int)Math.Round(total / parcel);
                if (parcel <= 0 && total > 0 && qty > 0)
                    parcel = Math.Round(total / qty, 2);
            }

            if (qty <= 0 || parcel <= 0) continue;
            if (total <= 0) total = Math.Round(parcel * qty, 2);

            var installments = InstallmentPlan.GenerateInstallments(parcel, qty, startDate, 1);
            if (finished)
            {
                foreach (var inst in installments)
                {
                    inst.IsPaid = true;
                    inst.PaidDate = inst.DueDate;
                }
            }

            plans.Add(new InstallmentPlan
            {
                Card = card,
                Name = name,
                TotalInstallments = qty,
                InstallmentAmount = parcel,
                TotalAmount = total,
                StartDate = startDate,
                DueDay = 1,
                Installments = installments
            });
        }

        return plans;
    }

    private static bool LooksLikeHeader(string[] row) =>
        row.Any(c => c.Contains("Cartao", StringComparison.OrdinalIgnoreCase)
                  || c.Contains("Cartão", StringComparison.OrdinalIgnoreCase)
                  || c.Contains("Compra", StringComparison.OrdinalIgnoreCase));

    private static byte[] BuildXlsx(List<string[]> rows)
    {
        var shared = new List<string>();
        var sharedIndex = new Dictionary<string, int>(StringComparer.Ordinal);

        int AddShared(string value)
        {
            if (sharedIndex.TryGetValue(value, out var idx)) return idx;
            idx = shared.Count;
            shared.Add(value);
            sharedIndex[value] = idx;
            return idx;
        }

        foreach (var row in rows)
        foreach (var cell in row)
            if (!string.IsNullOrEmpty(cell))
                AddShared(cell);

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "[Content_Types].xml",
                $"""
                 <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                 <Types xmlns="{Ct}">
                   <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                   <Default Extension="xml" ContentType="application/xml"/>
                   <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                   <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                   <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
                   <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                 </Types>
                 """);

            WriteEntry(zip, "_rels/.rels",
                $"""
                 <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                 <Relationships xmlns="{Pk}">
                   <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                 </Relationships>
                 """);

            WriteEntry(zip, "xl/workbook.xml",
                $"""
                 <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                 <workbook xmlns="{Ss}" xmlns:r="{R}">
                   <sheets>
                     <sheet name="Parcelamentos" sheetId="1" r:id="rId1"/>
                   </sheets>
                 </workbook>
                 """);

            WriteEntry(zip, "xl/_rels/workbook.xml.rels",
                $"""
                 <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                 <Relationships xmlns="{Pk}">
                   <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                   <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings" Target="sharedStrings.xml"/>
                   <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                 </Relationships>
                 """);

            WriteEntry(zip, "xl/styles.xml",
                $"""
                 <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                 <styleSheet xmlns="{Ss}">
                   <fonts count="1"><font><sz val="11"/><name val="Calibri"/></font></fonts>
                   <fills count="1"><fill><patternFill patternType="none"/></fill></fills>
                   <borders count="1"><border/></borders>
                   <cellStyleXfs count="1"><xf/></cellStyleXfs>
                   <cellXfs count="1"><xf/></cellXfs>
                 </styleSheet>
                 """);

            var sbShared = new StringBuilder();
            sbShared.Append($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><sst xmlns="{Ss}" count="{shared.Count}" uniqueCount="{shared.Count}">""");
            foreach (var s in shared)
            {
                sbShared.Append("<si><t>")
                    .Append(System.Security.SecurityElement.Escape(s))
                    .Append("</t></si>");
            }
            sbShared.Append("</sst>");
            WriteEntry(zip, "xl/sharedStrings.xml", sbShared.ToString());

            var sbSheet = new StringBuilder();
            sbSheet.Append($"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="{Ss}"><sheetData>""");
            for (var r = 0; r < rows.Count; r++)
            {
                var rowNum = r + 1;
                sbSheet.Append($"<row r=\"{rowNum}\">");
                for (var c = 0; c < Headers.Length; c++)
                {
                    var value = rows[r].ElementAtOrDefault(c) ?? "";
                    if (string.IsNullOrEmpty(value)) continue;
                    var refer = $"{ColumnName(c)}{rowNum}";
                    var idx = sharedIndex[value];
                    sbSheet.Append($"<c r=\"{refer}\" t=\"s\"><v>{idx}</v></c>");
                }
                sbSheet.Append("</row>");
            }
            sbSheet.Append("</sheetData></worksheet>");
            WriteEntry(zip, "xl/worksheets/sheet1.xml", sbSheet.ToString());
        }

        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive zip, string path, string content)
    {
        var entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static List<string> ReadSharedStrings(ZipArchive zip)
    {
        var entry = zip.GetEntry("xl/sharedStrings.xml");
        if (entry is null) return [];

        using var stream = entry.Open();
        var doc = XDocument.Load(stream);
        var list = new List<string>();
        foreach (var si in doc.Root?.Elements(Ss + "si") ?? [])
        {
            var t = si.Element(Ss + "t");
            if (t is not null)
            {
                list.Add(t.Value);
                continue;
            }

            var parts = si.Elements(Ss + "r").Select(r => r.Element(Ss + "t")?.Value ?? "");
            list.Add(string.Concat(parts));
        }

        return list;
    }

    private static string GetCellValue(XElement cell, List<string> shared)
    {
        var type = cell.Attribute("t")?.Value;
        var v = cell.Element(Ss + "v")?.Value;
        if (type == "s" && int.TryParse(v, out var idx) && idx >= 0 && idx < shared.Count)
            return shared[idx];
        if (type == "inlineStr")
            return cell.Element(Ss + "is")?.Element(Ss + "t")?.Value ?? "";
        return v ?? "";
    }

    private static int ColumnIndex(string cellRef)
    {
        var col = 0;
        foreach (var ch in cellRef)
        {
            if (!char.IsLetter(ch)) break;
            col = col * 26 + (char.ToUpperInvariant(ch) - 'A' + 1);
        }
        return col - 1;
    }

    private static string ColumnName(int index)
    {
        var n = index + 1;
        var name = "";
        while (n > 0)
        {
            n--;
            name = (char)('A' + n % 26) + name;
            n /= 26;
        }
        return name;
    }

    private static int ParseInt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        if (int.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var i)) return i;
        if (decimal.TryParse(value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d)) return (int)d;
        if (decimal.TryParse(value.Trim(), NumberStyles.Any, new CultureInfo("pt-BR"), out d)) return (int)d;
        return 0;
    }

    private static decimal ParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;
        value = value.Trim().Replace("R$", "", StringComparison.OrdinalIgnoreCase).Trim();
        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var inv)) return inv;
        if (decimal.TryParse(value, NumberStyles.Any, new CultureInfo("pt-BR"), out var br)) return br;
        return 0;
    }

    private static DateOnly? ParseMonth(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();

        if (DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
            return new DateOnly(d.Year, d.Month, 1);
        if (DateOnly.TryParse(value, new CultureInfo("pt-BR"), DateTimeStyles.None, out d))
            return new DateOnly(d.Year, d.Month, 1);
        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return new DateOnly(dt.Year, dt.Month, 1);
        if (DateTime.TryParse(value, new CultureInfo("pt-BR"), DateTimeStyles.None, out dt))
            return new DateOnly(dt.Year, dt.Month, 1);
        if (double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var serial) && serial > 20000)
        {
            var oa = DateTime.FromOADate(serial);
            return new DateOnly(oa.Year, oa.Month, 1);
        }

        return null;
    }
}
