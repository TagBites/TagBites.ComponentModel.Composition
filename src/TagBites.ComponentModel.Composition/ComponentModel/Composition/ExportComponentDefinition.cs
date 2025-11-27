using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace TagBites.ComponentModel.Composition;

/// <summary>
/// Export component definition.
/// </summary>
public class ExportComponentDefinition
{
    private Type _valueType;

    /// <summary>
    /// Gets contract name.
    /// </summary>
    public string ContractName { get; }
    /// <summary>
    /// Gets contract type.
    /// </summary>
    public Type ContractType { get; }
    /// <summary>
    /// Gets value type.
    /// </summary>
    public Type ValueType
    {
        get
        {
            if (_valueType == null)
                _valueType = ValueTypeAssembly.GetType(ValueTypeFullName);

            return _valueType;
        }
    }
    /// <summary>
    /// Gets value type assembly.
    /// </summary>
    public Assembly ValueTypeAssembly { get; }
    /// <summary>
    /// Gets full name of value type.
    /// </summary>
    public string ValueTypeFullName { get; }
    /// <summary>
    /// Gets location.
    /// </summary>
    public Uri Location { get; }

    internal ExportComponentDefinition(string contractName, Type contractType, Type valueType, Uri location = null)
    {
        if (contractType == null)
            throw new ArgumentNullException(nameof(contractType));
        if (valueType == null)
            throw new ArgumentNullException(nameof(valueType));

        ContractName = contractName;
        ContractType = contractType;

        _valueType = valueType;
        ValueTypeAssembly = valueType.Assembly;
        ValueTypeFullName = valueType.FullName;

        Location = location ?? GetDefaultUri(contractType, contractName, valueType);
    }
    internal ExportComponentDefinition(string contractName, Type contractType, Assembly valueTypeAssembly, string valueTypeFullName, string location)
    {
        if (contractType == null)
            throw new ArgumentNullException(nameof(contractType));
        if (valueTypeAssembly == null)
            throw new ArgumentNullException(nameof(valueTypeAssembly));
        if (string.IsNullOrEmpty(valueTypeFullName))
            throw new ArgumentException("Value cannot be null or empty.", nameof(valueTypeFullName));

        ContractName = contractName;
        ContractType = contractType;

        ValueTypeAssembly = valueTypeAssembly;
        ValueTypeFullName = valueTypeFullName;

        Location = new Uri(location);
    }


    /// <inheritdoc />
    public override string ToString()
    {
        return ValueTypeFullName;
    }

    /// <summary>
    /// Gets default export URI.
    /// </summary>
    /// <param name="contractType">Contract type.</param>
    /// <param name="contractName">Contract name.</param>
    /// <param name="valueType">Value type.</param>
    /// <returns>Export URI.</returns>
    public static Uri GetDefaultUri(Type contractType, string contractName, Type valueType)
    {
        if (contractType == null)
            throw new ArgumentNullException(nameof(contractType));
        if (valueType == null)
            throw new ArgumentNullException(nameof(valueType));

        var sb = new StringBuilder(256);
        sb.Append("export:");

        // Contract Type
        sb.Append(GetTypeIdentifier(contractType));

        // Contract Name
        if (!string.IsNullOrEmpty(contractName))
        {
            sb.Append('/');
            sb.Append(Uri.EscapeDataString(contractName));
        }

        // ValueType
        sb.Append('/');
        sb.Append(GetTypeIdentifier(valueType));

        return new Uri(sb.ToString());
    }

    /// <summary>
    /// Gets type GUID.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <returns>Type GUID.</returns>
    public static Guid GetTypeGuid(Type type)
    {
        var att = type.GetCustomAttribute<GuidAttribute>(false);
        return att != null
               && !string.IsNullOrWhiteSpace(att.Value)
               && Guid.TryParse(att.Value, out var guid)
            ? guid
            : Guid.Empty;
    }
    /// <summary>
    /// Gets type identifier.
    /// </summary>
    /// <param name="type">Type.</param>
    /// <returns>Type identifier (guid or full name without assembly version).</returns>
    public static string GetTypeIdentifier(Type type)
    {
        var guid = GetTypeGuid(type);
        return guid != Guid.Empty
            ? guid.ToString("D").ToUpper()
            : $"{type.FullName},{type.Assembly.GetName().Name}";
    }
}
