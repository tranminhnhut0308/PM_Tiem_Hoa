namespace ShopHoa.Models;

public sealed class GenericResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public object? Data { get; set; }
}
