#nullable enable
using System;

namespace TagBites.ComponentModel.Composition;

public sealed class ContractDefinition
{
    public string? ContactName { get; }
    public Type ContactType { get; }

    public ContractDefinition(string? contactName, Type contactType)
    {
        ContactName = contactName;
        ContactType = contactType ?? throw new ArgumentNullException(nameof(contactType));
    }


    private bool Equals(ContractDefinition other)
    {
        return ContactName == other.ContactName && ContactType == other.ContactType;
    }
    public override bool Equals(object? obj)
    {
        return ReferenceEquals(this, obj) || obj is ContractDefinition other && Equals(other);
    }
    public override int GetHashCode()
    {
        unchecked
        {
            return ((ContactName != null ? ContactName.GetHashCode() : 0) * 397) ^ ContactType.GetHashCode();
        }
    }

    public static bool operator ==(ContractDefinition? left, ContractDefinition? right) => Equals(left, right);
    public static bool operator !=(ContractDefinition? left, ContractDefinition? right) => !Equals(left, right);
}
