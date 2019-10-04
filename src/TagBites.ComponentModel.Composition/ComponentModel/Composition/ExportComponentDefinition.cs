using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using TagBites.Utils;

namespace TagBites.ComponentModel.Composition
{
    public class ExportComponentDefinition
    {
        private Type _valueType;
        private IDictionary<string, object> _metadata;

        public string ContractName { get; }
        public Type ContractType { get; }
        public Type ValueType
        {
            get
            {
                if (_valueType == null)
                    _valueType = ValueTypeAssembly.GetType(ValueTypeFullName);

                return _valueType;
            }
        }
        public Assembly ValueTypeAssembly { get; }
        public string ValueTypeFullName { get; }
        public Uri Location { get; }

        public IDictionary<string, object> Metadata
        {
            get
            {
                if (_metadata == null)
                {
                    var attributes = ValueType.GetCustomAttributes(typeof(ExportMetadataAttribute), false);
                    if (attributes.Length == 0)
                        _metadata = ExportComponent.EmptyMetadata;
                    else
                    {
                        var metadata = new Dictionary<string, object>();

                        foreach (ExportMetadataAttribute item in attributes)
                            if (!metadata.ContainsKey(item.Name))
                                metadata.Add(item.Name, item.Value);

                        _metadata = new ReadOnlyDictionary<string, object>(metadata);
                    }
                }

                return _metadata;
            }
        }

        private ExportComponentDefinition(string contractName, Type contractType, IDictionary<string, object> metadata)
        {
            if (contractType == null)
                throw new ArgumentNullException(nameof(contractType));

            ContractName = contractName;
            ContractType = contractType;

            _metadata = metadata;
            if (_metadata != null && !_metadata.IsReadOnly)
                _metadata = new ReadOnlyDictionary<string, object>(_metadata);
        }
        internal ExportComponentDefinition(string contractName, Type contractType, Type valueType, IDictionary<string, object> metadata)
            : this(contractName, contractType, metadata)
        {
            if (valueType == null)
                throw new ArgumentNullException(nameof(valueType));

            _valueType = valueType;
            ValueTypeAssembly = valueType.Assembly;
            ValueTypeFullName = valueType.FullName;
            Location = GetDefaultUri(contractType, contractName, ValueType);
        }
        internal ExportComponentDefinition(string contractName, Type contractType, Assembly valueTypeAssembly, string valueTypeFullName, string location)
            : this(contractName, contractType, null)
        {
            if (valueTypeAssembly == null)
                throw new ArgumentNullException(nameof(valueTypeAssembly));
            if (string.IsNullOrEmpty(valueTypeFullName))
                throw new ArgumentException("Value cannot be null or empty.", nameof(valueTypeFullName));

            ValueTypeAssembly = valueTypeAssembly;
            ValueTypeFullName = valueTypeFullName;
            Location = new Uri(location);
        }


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

        public static Guid? GetTypeGuid(Type type)
        {
            var att = type.GetTypeInfo().GetCustomAttribute<GuidAttribute>(false);
            return att != null
                   && !string.IsNullOrWhiteSpace(att.Value)
                   && Guid.TryParse(att.Value, out var guid)
                   && guid != Guid.Empty
                ? (Guid?)guid
                : null;
        }
        public static string GetTypeIdentifier(Type type)
        {
            return GetTypeGuid(type)?.ToString("D").ToUpper()
                   ?? $"{type.FullName},{AssemblyUtils.GetName(type.GetTypeInfo().Assembly)}";
        }
    }
}
