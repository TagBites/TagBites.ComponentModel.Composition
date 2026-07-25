#nullable enable
using System.ComponentModel;

namespace TagBites.ComponentModel.Composition;

public sealed class ContractDefinition(string? contractName, Type contractType)
{
    public string? ContractName { get; } = contractName;
    public Type ContractType { get; } = contractType ?? throw new ArgumentNullException(nameof(contractType));


    private bool Equals(ContractDefinition other)
    {
        return ContractName == other.ContractName && ContractType == other.ContractType;
    }
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is ContractDefinition other && Equals(other);
    }
    public override int GetHashCode()
    {
        unchecked
        {
            return ((ContractName != null ? ContractName.GetHashCode() : 0) * 397) ^ ContractType.GetHashCode();
        }
    }

    public static bool operator ==(ContractDefinition? left, ContractDefinition? right) => Equals(left, right);
    public static bool operator !=(ContractDefinition? left, ContractDefinition? right) => !Equals(left, right);

    [Obsolete("Use ContractName instead."), EditorBrowsable(EditorBrowsableState.Never)]
    public string? ContactName => ContractName;
    [Obsolete("Use ContractType instead."), EditorBrowsable(EditorBrowsableState.Never)]
    public Type ContactType => ContractType;
}
