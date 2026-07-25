#nullable enable
using System.ComponentModel;

namespace TagBites.ComponentModel.Composition;

/// <summary>
/// Contract of an export: a contract type with an optional contract name.
/// </summary>
/// <param name="contractName">Contract name, or <c>null</c> for the contract without a name.</param>
/// <param name="contractType">Contract type.</param>
public sealed class ContractDefinition(string? contractName, Type contractType)
{
    /// <summary>
    /// Gets the contract name, or <c>null</c> when the contract has no name.
    /// </summary>
    public string? ContractName { get; } = contractName;
    /// <summary>
    /// Gets the contract type.
    /// </summary>
    public Type ContractType { get; } = contractType ?? throw new ArgumentNullException(nameof(contractType));


    private bool Equals(ContractDefinition other)
    {
        return ContractName == other.ContractName && ContractType == other.ContractType;
    }
    /// <inheritdoc />
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is ContractDefinition other && Equals(other);
    }
    /// <inheritdoc />
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
