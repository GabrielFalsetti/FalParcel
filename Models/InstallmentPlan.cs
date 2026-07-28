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

    public int PaidCount => Installments.Count(i => i.IsPaid);
    public int RemainingCount => Installments.Count(i => !i.IsPaid);
    public decimal RemainingAmount => Installments.Where(i => !i.IsPaid).Sum(i => i.Amount);
    public decimal PaidAmount => Installments.Where(i => i.IsPaid).Sum(i => i.Amount);
    public bool IsPaidOff => Installments.Count > 0 && Installments.All(i => i.IsPaid);

    public Installment? NextDue => Installments
        .Where(i => !i.IsPaid)
        .OrderBy(i => i.DueDate)
        .FirstOrDefault();

    public string StatusLabel
    {
        get
        {
            if (IsPaidOff) return "Finalizado";
            var next = NextDue;
            return next is null ? "—" : $"Até {next.DueDate:dd/MM/yyyy}";
        }
    }

    public static List<Installment> GenerateInstallments(
        decimal installmentAmount,
        int totalInstallments,
        DateOnly startDate,
        int dueDay,
        bool markPastAsPaid = true)
    {
        var list = new List<Installment>(totalInstallments);
        var year = startDate.Year;
        var month = startDate.Month;
        var today = DateOnly.FromDateTime(DateTime.Today);
        var firstOfCurrentMonth = new DateOnly(today.Year, today.Month, 1);

        for (var n = 1; n <= totalInstallments; n++)
        {
            var day = Math.Min(Math.Max(dueDay, 1), DateTime.DaysInMonth(year, month));
            var due = new DateOnly(year, month, day);
            var paid = markPastAsPaid && due < firstOfCurrentMonth;

            list.Add(new Installment
            {
                Number = n,
                Amount = installmentAmount,
                DueDate = due,
                IsPaid = paid,
                PaidDate = paid ? due : null
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
