namespace Parcelly.Models;

public class Installment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Number { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateOnly? PaidDate { get; set; }

    public bool IsEffectivelyPaid(PaymentMode mode, DateOnly? asOf = null) =>
        PaymentRules.IsEffectivelyPaid(this, mode, asOf);

    public bool IsDueThisMonth(PaymentMode mode, DateOnly? asOf = null)
    {
        var today = asOf ?? PaymentRules.Today;
        return !IsEffectivelyPaid(mode, today)
               && DueDate.Year == today.Year
               && DueDate.Month == today.Month;
    }
}
