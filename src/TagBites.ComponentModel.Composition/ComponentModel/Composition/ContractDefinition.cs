using System;

namespace TagBites.ComponentModel.Composition
{
    public class ContractDefinition
    {
        public string ContactName { get; }
        public Type ContactType { get; }

        public ContractDefinition(string contactName, Type contactType)
        {
            if (contactType == null)
                throw new ArgumentNullException(nameof(contactType));
            ContactName = contactName;
            ContactType = contactType;
        }


        /// <inheritdoc />
        public override int GetHashCode()
        {
            return (ContactName != null ? ContactName.GetHashCode() : 0) ^ ContactType.GetHashCode();
        }
        /// <inheritdoc />
        public override bool Equals(object obj)
        {
            var contract = obj as ContractDefinition;
            return contract != null && contract.ContactName == ContactName && contract.ContactType == ContactType;
        }
        /// <inheritdoc />
        public override string ToString()
        {
            return string.IsNullOrEmpty(ContactName)
                ? ContactType.FullName ?? ContactType.Name
                : $"{ContactName} ({ContactType.FullName})";
        }

        public static bool operator ==(ContractDefinition left, ContractDefinition right)
        {
            return ReferenceEquals(left, null)
                ? ReferenceEquals(right, null)
                : (!ReferenceEquals(right, null) && left.ContactName == right.ContactName && left.ContactType == right.ContactType);
        }
        public static bool operator !=(ContractDefinition left, ContractDefinition right)
        {
            return !(left == right);
        }
    }
}
