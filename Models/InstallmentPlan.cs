namespace Parcelly.Models;

public class InstallmentPlan
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    /// <summary>Cartão (ex: C6, Santander) — coluna A da planilha.</summary>
    public string Card { get; set; } = string.Empty;

    /// <summary>Compra / descrição — coluna B da planilha.</summary>
    public string Name { get; set; } = string.Empty;

    public decimal TotalAmount { get; set; }
    public decimal InstallmentAmount { get; set; }
    public int TotalInstallments { get; set; }

    /// <summary>Mês início (dia 1 na planilha).</summary>
    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Dia do vencimento no cartão (na planilha costuma ser dia 1).</summary>
    public int DueDay { get; set; } = 1;

    public string Notes { get; set; } = string.Empty;
    public List<Installment> Installments { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateOnly? EndDate => Installments.Count == 0
        ? null
        : Installments.Max(i => i.DueDate);

    public int PaidCount(PaymentMode mode) =>
        Installments.Count(i => i.IsEffectivelyPaid(mode));

    public int RemainingCount(PaymentMode mode) =>
        Installments.Count(i => !i.IsEffectivelyPaid(mode));

    public decimal RemainingAmount(PaymentMode mode) =>
        Installments.Where(i => !i.IsEffectivelyPaid(mode)).Sum(i => i.Amount);

    public decimal PaidAmount(PaymentMode mode) =>
        Installments.Where(i => i.IsEffectivelyPaid(mode)).Sum(i => i.Amount);

    public bool IsPaidOff(PaymentMode mode) =>
        Installments.Count > 0 && Installments.All(i => i.IsEffectivelyPaid(mode));

    public Installment? NextDue(PaymentMode mode) => Installments
        .Where(i => !i.IsEffectivelyPaid(mode))
        .OrderBy(i => i.DueDate)
        .FirstOrDefault();

    public string StatusLabel(PaymentMode mode)
    {
        if (IsPaidOff(mode)) return "Finalizado";
        var next = NextDue(mode);
        return next is null ? "—" : $"Até {PaymentRules.FormatMonth(next.DueDate)}";
    }

    public static List<Installment> GenerateInstallments(
        decimal installmentAmount,
        int totalInstallments,
        DateOnly startDate,
        int dueDay = 1)
    {
        var list = new List<Installment>(totalInstallments);
        var year = startDate.Year;
        var month = startDate.Month;

        for (var n = 1; n <= totalInstallments; n++)
        {
            // Sempre dia 1 — o controle é só por mês.
            list.Add(new Installment
            {
                Number = n,
                Amount = installmentAmount,
                DueDate = new DateOnly(year, month, 1),
                IsPaid = false,
                PaidDate = null
            });

            month++;
            if (month > 12)
            {
                month = 1;
                year++;
            }
        }

        return list;
    }
}
