namespace Parcelly.Models;

public class ParcellyData
{
    public List<InstallmentPlan> Plans { get; set; } = [];
    public List<string> Cards { get; set; } = [];
    public AppSettings Settings { get; set; } = new();
}
