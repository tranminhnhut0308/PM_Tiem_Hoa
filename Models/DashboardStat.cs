namespace ShopHoa.Models;

public sealed class DashboardStat
{
    public string Title { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
}
