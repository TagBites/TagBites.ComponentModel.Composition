using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using TagBites.Utils;

namespace TagBites.ComponentModel.Composition
{
    public class ExportComponent
    {
        internal static readonly IDictionary<string, object> EmptyMetadata = new ReadOnlyDictionary<string, object>(new Dictionary<string, object>());

        private string _name;
        private Type _valueType;
        private readonly object _initInstance;
        private WeakReference _instance;
        private Uri _location;

        public string Name => _name ??= ValueType.FullName;
        public string FullName =>
            Name == ValueType.FullName
                ? string.Format("{0} - {1}", Name, OriginName)
                : string.Format("{0} - {1} - {2}", Name, ValueType.FullName, OriginName);

        public string ContractName { get; }
        public Type ContractType { get; }
        public Type ValueType
        {
            get
            {
                if (_valueType == null)
                    _valueType = Instance.GetType();

                return _valueType;
            }
        }
        public object Instance
        {
            get
            {
                if (_initInstance != null)
                    return _initInstance;
                object target = null;

                if (_instance != null)
                    target = _instance.Target;

                if (target == null)
                {
                    target = Activator.CreateInstance(ValueType);
                    _instance = new WeakReference(target);
                }

                return target;
            }
        }
        public IDictionary<string, object> Metadata { get; }

        public Assembly OriginAssembly { get; }
        public string OriginName => AssemblyUtils.GetFullFriendlyName(OriginAssembly);
        public Uri Location
        {
            get
            {
                if (_location == null && OriginAssembly == ValueType.GetTypeInfo().Assembly)
                    _location = ExportComponentDefinition.GetDefaultUri(ContractType, ContractName, ValueType);

                return _location;
            }
        }
        public bool IsDynamic { get; }

        internal ExportComponent(ExportComponentDefinition definition)
            : this(definition.ContractName, definition.ContractType, definition.ValueType)
        {
            Metadata = definition.Metadata;
            _location = definition.Location;
        }
        public ExportComponent(string contractName, Type contractType, Type valueType)
            : this(contractName, contractType, valueType, valueType.GetTypeInfo().Assembly, null)
        { }
        public ExportComponent(string contractName, Type contractType, Type valueType, string name)
            : this(contractName, contractType, valueType, valueType.GetTypeInfo().Assembly, name)
        { }
        public ExportComponent(string contractName, Type contractType, Type valueType, Assembly originAssembly, string name)
        {
            if (contractType == null)
                throw new ArgumentNullException(nameof(contractType));
            if (valueType == null)
                throw new ArgumentNullException(nameof(valueType));
            if (originAssembly == null)
                throw new ArgumentNullException(nameof(originAssembly));

            _name = name;

            ContractName = contractName;
            ContractType = contractType;
            _valueType = valueType;

            OriginAssembly = originAssembly;
        }
        public ExportComponent(string contractName, Type contractType, object instance)
            : this(contractName, contractType, instance, instance.GetType().GetTypeInfo().Assembly, null)
        { }
        public ExportComponent(string contractName, Type contractType, object instance, string name)
            : this(contractName, contractType, instance, instance.GetType().GetTypeInfo().Assembly, name)
        { }
        public ExportComponent(string contractName, Type contractType, object instance, Assembly originAssembly, string name)
        {
            if (contractType == null)
                throw new ArgumentNullException(nameof(contractType));
            if (instance == null)
                throw new ArgumentNullException(nameof(instance));
            if (originAssembly == null)
                throw new ArgumentNullException(nameof(originAssembly));

            _name = name;

            ContractName = contractName;
            ContractType = contractType;
            _initInstance = instance;

            OriginAssembly = originAssembly;
            IsDynamic = true;
        }


        public object CreateInstance()
        {
            if (IsDynamic)
                throw new InvalidOperationException("Can not create instance of dynamic component.");

            return Activator.CreateInstance(ValueType);
        }

        /// <inheritdoc />
        public override string ToString()
        {
            return Name;
        }
    }

    public sealed class ExportComponent<T> : ExportComponent
    {
        public new T Instance => (T)base.Instance;

        public ExportComponent(ExportComponentDefinition definition)
            : base(definition)
        { }
        public ExportComponent(string contractName, Type valueType)
            : base(contractName, typeof(T), valueType)
        { }
        public ExportComponent(string contractName, Type valueType, string name)
            : base(contractName, typeof(T), valueType, name)
        { }
        public ExportComponent(string contractName, Type valueType, Assembly originAssembly, string name)
            : base(contractName, typeof(T), valueType, originAssembly, name)
        { }
        public ExportComponent(string contractName, T instance)
            : base(contractName, typeof(T), instance)
        { }
        public ExportComponent(string contractName, T instance, string name)
            : base(contractName, typeof(T), instance, name)
        { }
        public ExportComponent(string contractName, T instance, Assembly originAssembly, string name)
            : base(contractName, typeof(T), instance, originAssembly, name)
        { }


        public new T CreateInstance()
        {
            return (T)base.CreateInstance();
        }
    }
}
