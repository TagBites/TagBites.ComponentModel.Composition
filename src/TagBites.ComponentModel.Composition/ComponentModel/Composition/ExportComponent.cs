using System;
using System.Reflection;

namespace TagBites.ComponentModel.Composition;

public class ExportComponent
{
    private readonly Func<object> _instanceProvider;
    private WeakReference _instance;
    private readonly Assembly _originAssembly;

    /// <summary>
    /// Export definition.
    /// </summary>
    internal ExportComponentDefinition Definition { get; }
    /// <summary>
    /// Gets contract name.
    /// </summary>
    public string ContractName => Definition.ContractName;
    /// <summary>
    /// Gets contract type.
    /// </summary>
    public Type ContractType => Definition.ContractType;
    /// <summary>
    /// Gets value type.
    /// </summary>
    public Type ValueType => Definition.ValueType;
    /// <summary>
    /// Gets value type assembly.
    /// </summary>
    public Assembly ValueTypeAssembly => Definition.ValueTypeAssembly;
    /// <summary>
    /// Gets full name of value type.
    /// </summary>
    public string ValueTypeFullName => Definition.ValueTypeFullName;
    /// <summary>
    /// Gets location.
    /// </summary>
    public Uri Location => Definition.Location;

    /// <summary>
    /// Gets origin assembly.
    /// </summary>
    public Assembly OriginAssembly => _originAssembly ?? ValueTypeAssembly;

    /// <summary>
    /// Gets instance.
    /// </summary>
    public object Instance
    {
        get
        {
            object target = null;

            if (_instance != null)
                target = _instance.Target;

            if (target == null)
            {
                target = CreateInstance();
                _instance = new WeakReference(target);
            }

            return target;
        }
    }

    internal ExportComponent(ExportComponentDefinition definition)
    {
        Definition = definition ?? throw new ArgumentNullException(nameof(definition));
    }
    internal ExportComponent(string contractName, Type contractType, Type valueType, Uri location = null)
        : this(new ExportComponentDefinition(contractName, contractType, valueType, location))
    { }
    internal ExportComponent(string contractName, Type contractType, Type valueType, Uri location, Func<object> instanceProvider, Assembly originAssembly)
        : this(new ExportComponentDefinition(contractName, contractType, valueType, location))
    {
        _instanceProvider = instanceProvider;
        _originAssembly = originAssembly;
    }


    /// <summary>
    /// Creates instance.
    /// </summary>
    /// <returns>New instance.</returns>
    public object CreateInstance()
    {
        return _instanceProvider != null
            ? _instanceProvider()
            : Activator.CreateInstance(ValueType);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return ValueTypeFullName;
    }
}

public sealed class ExportComponent<T> : ExportComponent
{
    /// <summary>
    /// Creates instance.
    /// </summary>
    /// <returns>New instance.</returns>
    public new T Instance => (T)base.Instance;

    public ExportComponent(ExportComponentDefinition definition)
        : base(definition)
    { }
    public ExportComponent(string contractName, Type contractType, Type valueType, Uri location = null)
        : base(contractName, contractType, valueType, location)
    { }
    public ExportComponent(string contractName, Type contractType, Type valueType, Uri location, Func<T> instanceProvider, Assembly originAssembly)
        : base(contractName, contractType, valueType, location, () => instanceProvider(), originAssembly)
    {
        if (instanceProvider == null)
            throw new ArgumentNullException(nameof(instanceProvider));
    }


    /// <summary>
    /// Creates instance.
    /// </summary>
    /// <returns>New instance.</returns>
    public new T CreateInstance()
    {
        return (T)base.CreateInstance();
    }
}
