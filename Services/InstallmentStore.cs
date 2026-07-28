using System.Globalization;
using System.Text.Json;
using Parcelly.Models;

namespace Parcelly.Services;

public class InstallmentStore(LocalStorageService storage)
{
    private const string StorageKey = "parcelly.data.v3";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    private ParcellyData? _cache;
    public event Action? Changed;

    public async Task EnsureLoadedAsync()
    {
        if (_cache is not null) return;

        var raw = await storage.GetAsync(StorageKey);
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Migra v2 (sem seed). Começa vazio se não houver dados anteriores.
            var legacy = await storage.GetAsync("parcelly.data.v2")
                         ?? await storage.GetAsync("parcelly.data.v1");

            _cache = string.IsNullOrWhiteSpace(legacy)
                ? new ParcellyData()
                : JsonSerializer.Deserialize<ParcellyData>(legacy, JsonOptions) ?? new ParcellyData();

            EnsureCardsFromPlans();
            await PersistAsync();
            return;
        }

        _cache = JsonSerializer.Deserialize<ParcellyData>(raw, JsonOptions) ?? new ParcellyData();
        EnsureCardsFromPlans();
    }

    public async Task<PaymentMode> GetPaymentModeAsync()
    {
        await EnsureLoadedAsync();
        return _cache!.Settings.PaymentMode;
    }

    public async Task SavePaymentModeAsync(PaymentMode mode)
    {
        await EnsureLoadedAsync();
        _cache!.Settings.PaymentMode = mode;
        await PersistAsync();
    }

    public async Task<IReadOnlyList<InstallmentPlan>> GetPlansAsync()
    {
        await EnsureLoadedAsync();
        var mode = _cache!.Settings.PaymentMode;
        return _cache.Plans
            .OrderBy(p => p.IsPaidOff(mode))
            .ThenBy(p => p.Card)
            .ThenBy(p => p.NextDue(mode)?.DueDate ?? DateOnly.MaxValue)
            .ToList();
    }

    public async Task<InstallmentPlan?> GetPlanAsync(string id)
    {
        await EnsureLoadedAsync();
        return _cache!.Plans.FirstOrDefault(p => p.Id == id);
    }

    public async Task<IReadOnlyList<string>> GetCardsAsync()
    {
        await EnsureLoadedAsync();
        return _cache!.Cards
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    public async Task AddCardAsync(string name)
    {
        await EnsureLoadedAsync();
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        if (_cache!.Cards.Any(c => c.Equals(name, StringComparison.OrdinalIgnoreCase)))
            return;

        _cache.Cards.Add(name);
        await PersistAsync();
    }

    public async Task RenameCardAsync(string oldName, string newName)
    {
        await EnsureLoadedAsync();
        oldName = oldName.Trim();
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(oldName) || string.IsNullOrWhiteSpace(newName)) return;

        var idx = _cache!.Cards.FindIndex(c => c.Equals(oldName, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        if (_cache.Cards.Any(c => !c.Equals(oldName, StringComparison.OrdinalIgnoreCase)
                                  && c.Equals(newName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Já existe um cartão com esse nome.");

        _cache.Cards[idx] = newName;
        foreach (var plan in _cache.Plans.Where(p => p.Card.Equals(oldName, StringComparison.OrdinalIgnoreCase)))
            plan.Card = newName;

        await PersistAsync();
    }

    public async Task DeleteCardAsync(string name)
    {
        await EnsureLoadedAsync();
        name = name.Trim();
        _cache!.Cards.RemoveAll(c => c.Equals(name, StringComparison.OrdinalIgnoreCase));
        await PersistAsync();
    }

    public async Task SavePlanAsync(InstallmentPlan plan)
    {
        await EnsureLoadedAsync();
        plan.UpdatedAt = DateTime.UtcNow;
        var idx = _cache!.Plans.FindIndex(p => p.Id == plan.Id);
        if (idx >= 0)
            _cache.Plans[idx] = plan;
        else
            _cache.Plans.Add(plan);

        if (!string.IsNullOrWhiteSpace(plan.Card)
            && !_cache.Cards.Any(c => c.Equals(plan.Card, StringComparison.OrdinalIgnoreCase)))
            _cache.Cards.Add(plan.Card.Trim());

        await PersistAsync();
    }

    public async Task DeletePlanAsync(string id)
    {
        await EnsureLoadedAsync();
        _cache!.Plans.RemoveAll(p => p.Id == id);
        await PersistAsync();
    }

    public async Task ToggleInstallmentAsync(string planId, string installmentId, bool paid)
    {
        await EnsureLoadedAsync();
        var mode = _cache!.Settings.PaymentMode;
        var plan = _cache.Plans.FirstOrDefault(p => p.Id == planId);
        var item = plan?.Installments.FirstOrDefault(i => i.Id == installmentId);
        if (plan is null || item is null) return;
        if (PaymentRules.IsLockedByDate(item, mode)) return;

        item.IsPaid = paid;
        item.PaidDate = paid ? DateOnly.FromDateTime(DateTime.Today) : null;
        plan.UpdatedAt = DateTime.UtcNow;
        await PersistAsync();
    }

    public async Task AddPlansAsync(IEnumerable<InstallmentPlan> plans)
    {
        await EnsureLoadedAsync();
        foreach (var plan in plans)
        {
            _cache!.Plans.Add(plan);
            if (!string.IsNullOrWhiteSpace(plan.Card)
                && !_cache.Cards.Any(c => c.Equals(plan.Card, StringComparison.OrdinalIgnoreCase)))
                _cache.Cards.Add(plan.Card.Trim());
        }

        await PersistAsync();
    }

    public async Task DownloadEmptyExcelAsync()
    {
        var bytes = ExcelWorkbookService.CreateEmptyTemplate();
        await storage.DownloadBytesAsync("falparcel-modelo.xlsx", bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task DownloadDataExcelAsync()
    {
        var plans = await GetPlansAsync();
        var mode = await GetPaymentModeAsync();
        var bytes = ExcelWorkbookService.ExportPlans(plans, mode);
        await storage.DownloadBytesAsync($"falparcel-{DateTime.Now:yyyyMMdd-HHmm}.xlsx", bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    public async Task<(bool Ok, string Message)> ImportExcelAsync(byte[] bytes)
    {
        try
        {
            var plans = ExcelWorkbookService.ImportPlans(bytes);
            if (plans.Count == 0)
                return (false, "Nenhuma compra preenchida no Excel.");

            await AddPlansAsync(plans);
            return (true, $"{plans.Count} compra(s) inseridas no sistema.");
        }
        catch (Exception ex)
        {
            return (false, $"Erro ao importar: {ex.Message}");
        }
    }

    public DashboardSummary GetSummary(IEnumerable<InstallmentPlan> plans, PaymentMode mode, int? year = null)
    {
        var list = plans.ToList();
        var open = list.SelectMany(p => p.Installments.Where(i => !i.IsEffectivelyPaid(mode))).ToList();
        var today = PaymentRules.Today;
        var y = year ?? today.Year;

        var monthTotals = Enumerable.Range(1, 12)
            .Select(m => new MonthTotal
            {
                Year = y,
                Month = m,
                Amount = list.SelectMany(p => p.Installments)
                    .Where(i => i.DueDate.Year == y && i.DueDate.Month == m)
                    .Sum(i => i.Amount),
                OpenAmount = list.SelectMany(p => p.Installments)
                    .Where(i => !i.IsEffectivelyPaid(mode) && i.DueDate.Year == y && i.DueDate.Month == m)
                    .Sum(i => i.Amount)
            })
            .ToList();

        return new DashboardSummary
        {
            ActivePlans = list.Count(p => !p.IsPaidOff(mode)),
            PaidOffPlans = list.Count(p => p.IsPaidOff(mode)),
            RemainingDebt = open.Sum(i => i.Amount),
            DueThisMonth = open.Where(i => i.DueDate.Year == today.Year && i.DueDate.Month == today.Month).Sum(i => i.Amount),
            OpenInstallmentCount = open.Count,
            Year = y,
            MonthTotals = monthTotals,
            ByCard = list
                .GroupBy(p => string.IsNullOrWhiteSpace(p.Card) ? "Sem cartão" : p.Card.Trim())
                .Select(g => new CardSummary
                {
                    Card = g.Key,
                    ActivePlans = g.Count(p => !p.IsPaidOff(mode)),
                    RemainingAmount = g.Sum(p => p.RemainingAmount(mode)),
                    DueThisMonth = g.SelectMany(p => p.Installments)
                        .Where(i => !i.IsEffectivelyPaid(mode) && i.DueDate.Year == today.Year && i.DueDate.Month == today.Month)
                        .Sum(i => i.Amount)
                })
                .OrderByDescending(c => c.RemainingAmount)
                .ToList(),
            Upcoming = list
                .Select(p => new { Plan = p, Next = p.NextDue(mode) })
                .Where(x => !x.Plan.IsPaidOff(mode) && x.Next is not null)
                .Select(x => new UpcomingItem
                {
                    PlanId = x.Plan.Id,
                    PlanName = x.Plan.Name,
                    Card = x.Plan.Card,
                    InstallmentNumber = x.Next!.Number,
                    TotalInstallments = x.Plan.TotalInstallments,
                    Amount = x.Next.Amount,
                    DueDate = x.Next.DueDate
                })
                .OrderBy(u => u.DueDate)
                .Take(10)
                .ToList()
        };
    }

    private void EnsureCardsFromPlans()
    {
        foreach (var card in _cache!.Plans
                     .Select(p => p.Card.Trim())
                     .Where(c => c.Length > 0))
        {
            if (!_cache.Cards.Any(c => c.Equals(card, StringComparison.OrdinalIgnoreCase)))
                _cache.Cards.Add(card);
        }
    }

    private async Task PersistAsync()
    {
        var json = JsonSerializer.Serialize(_cache, JsonOptions);
        await storage.SetAsync(StorageKey, json);
        Changed?.Invoke();
    }
}

public class DashboardSummary
{
    public int ActivePlans { get; set; }
    public int PaidOffPlans { get; set; }
    public decimal RemainingDebt { get; set; }
    public decimal DueThisMonth { get; set; }
    public int OpenInstallmentCount { get; set; }
    public int Year { get; set; }
    public List<MonthTotal> MonthTotals { get; set; } = [];
    public List<CardSummary> ByCard { get; set; } = [];
    public List<UpcomingItem> Upcoming { get; set; } = [];
}

public class MonthTotal
{
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal Amount { get; set; }
    public decimal OpenAmount { get; set; }
    public string Label => CultureInfo.GetCultureInfo("pt-BR").DateTimeFormat.GetAbbreviatedMonthName(Month);
}

public class CardSummary
{
    public string Card { get; set; } = string.Empty;
    public int ActivePlans { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal DueThisMonth { get; set; }
}

public class UpcomingItem
{
    public string PlanId { get; set; } = string.Empty;
    public string PlanName { get; set; } = string.Empty;
    public string Card { get; set; } = string.Empty;
    public int InstallmentNumber { get; set; }
    public int TotalInstallments { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
}
