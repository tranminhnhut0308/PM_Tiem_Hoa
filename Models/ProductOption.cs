using System;

namespace ShopHoa.Models;

public sealed class ProductOption : IEquatable<ProductOption>
{
    public uint Id { get; init; }
    public string Name { get; init; } = "";

    public override string ToString() => Name;

    public bool Equals(ProductOption? other)
    {
        if (other is null)
            return false;
        if (ReferenceEquals(this, other))
            return true;
        if (Id > 0 || other.Id > 0)
            return Id == other.Id;
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj) => Equals(obj as ProductOption);

    public override int GetHashCode() => Id > 0
        ? Id.GetHashCode()
        : StringComparer.OrdinalIgnoreCase.GetHashCode(Name ?? "");
}

