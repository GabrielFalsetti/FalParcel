namespace Parcelly.Models;

public static class PaymentRules
{
    public static DateOnly Today => DateOnly.FromDateTime(DateTime.Today);

    /// <summary>Chave ano-mês para comparar só por mês (ignora o dia).</summary>
    public static int MonthKey(DateOnly date) => date.Year * 12 + date.Month;

    public static bool MonthReached(DateOnly dueMonth, DateOnly? asOf = null)
    {
        var today = asOf ?? Today;
        return MonthKey(dueMonth) <= MonthKey(today);
    }

    public static bool IsEffectivelyPaid(Installment installment, PaymentMode mode, DateOnly? asOf = null)
    {
        return mode switch
        {
            PaymentMode.Manual => installment.IsPaid,
            PaymentMode.Automatic => MonthReached(installment.DueDate, asOf),
            PaymentMode.Mixed => installment.IsPaid || MonthReached(installment.DueDate, asOf),
            _ => installment.IsPaid
        };
    }

    /// <summary>Quando true, o checkbox não pode ser alterado (regido pelo mês).</summary>
    public static bool IsLockedByDate(Installment installment, PaymentMode mode, DateOnly? asOf = null)
    {
        return mode switch
        {
            PaymentMode.Automatic => true,
            PaymentMode.Mixed => MonthReached(installment.DueDate, asOf),
            _ => false
        };
    }

    public static string Describe(PaymentMode mode) => mode switch
    {
        PaymentMode.Manual => "Manual — você marca cada parcela como paga.",
        PaymentMode.Automatic => "Automático — no mês da parcela ela já conta como paga.",
        PaymentMode.Mixed => "Misto — pode marcar antes; se o mês chegar, também conta como paga.",
        _ => ""
    };

    public static string FormatMonth(DateOnly date) => date.ToString("MMM/yyyy");
}
