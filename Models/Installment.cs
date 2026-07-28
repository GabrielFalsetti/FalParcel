namespace Parcelly.Models;

public class Installment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public int Number { get; set; }
    public decimal Amount { get; set; }
    public DateOnly DueDate { get; set; }
    public bool IsPaid { get; set; }
    public DateOnly? PaidDate { get; set; }

    public bool IsDueThisMonth
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            return !IsPaid && DueDate.Year == today.Year && DueDate.Month == today.Month;
        }
    }
}
