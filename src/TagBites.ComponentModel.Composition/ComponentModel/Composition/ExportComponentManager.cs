using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reflection;
using System.Text.RegularExpressions;

namespace TagBites.ComponentModel.Composition;

/// <summary>
/// Container of exported components. Discovers types marked with <see cref="ExportAttribute"/> and creates their instances on demand.
/// </summary>
/// <remarks>All members are thread safe.</remarks>
[PublicAPI]
public class ExportComponentManager
{
    #region Events

    private readonly Dictionary<Type, EventHandler> _events = new();

    /// <summary>
    /// Occurs when exports are added or removed.
    /// </summary>
    public event EventHandler<ExportCollectionChangedEventArgs> ExportCollectionChanged;

    #endregion

    #region Members

    private readonly object _locker = new();
    private readonly HashSet<Assembly> _loadedAssemblies = [];
    private readonly List<(Assembly Assembly, List<IExportData> Removed)> _removedExports = [];
    private readonly Dictionary<Uri, IExportData> _exports = new();
    private readonly Dictionary<(Type, string), List<IExportData>> _exportTree = [];

    private Func<string, Type> _typeResolver = Type.GetType;
    private Func<string, Type, object> _deserializeFromFile;
    private Action<string, object> _serializeToFile;

    private readonly HashSet<string> _assemblyWithoutCache = [];

    /// <summary>
    /// Gets the directory with assembly cache files, or <c>null</c> when the cache is not used.
    /// </summary>
    public string AssemblyCacheDirectory { get; private set; }

    #endregion


    #region Get Exports

    /// <summary>
    /// Gets the shared instance of the export with the given location.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="location">Export URI.</param>
    /// <returns>Shared instance, or the default value of <typeparamref name="T"/> when no such export exists.</returns>
    public T GetExportInstance<T>(Uri location)
    {
        var export = GetExport<T>(location);
        return export != null
            ? export.Instance
            : default;
    }
    /// <summary>
    /// Creates a new instance of the export with the given location.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="location">Export URI.</param>
    /// <returns>New instance, or the default value of <typeparamref name="T"/> when no such export exists.</returns>
    public T CreateExportInstance<T>(Uri location)
    {
        var export = GetExport<T>(location);
        return export != null
            ? export.CreateInstance()
            : default;
    }
    /// <summary>
    /// Gets the export with the given location.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="location">Export URI.</param>
    /// <returns>Export component, or <c>null</c> when no such export exists or its contract type differs.</returns>
    public ExportComponent<T> GetExport<T>(Uri location)
    {
        return GetExport(location) as ExportComponent<T>;
    }

    /// <inheritdoc cref="GetExportInstance{T}"/>
    public object GetExportInstance(Uri location)
    {
        var export = GetExport(location);
        return export?.Instance;
    }
    /// <inheritdoc cref="CreateExportInstance{T}"/>
    public object CreateExportInstance(Uri location)
    {
        var export = GetExport(location);
        return export?.CreateInstance();
    }
    /// <summary>
    /// Gets the export with the given location. Follows the override chain, so the last export registered for the location wins.
    /// </summary>
    /// <param name="location">Export URI.</param>
    /// <returns>Export component, or <c>null</c> when no such export exists.</returns>
    public ExportComponent GetExport(Uri location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        lock (_locker)
        {
            _exports.TryGetValue(location, out var data);
            if (data == null)
                return null;

            while (data.OverrideBy != null)
                data = data.OverrideBy;

            return data.Component;
        }
    }

    /// <summary>
    /// Gets the shared instances of all exports of the given contract.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <returns>Shared instances of exports without a contract name.</returns>
    public IEnumerable<T> GetExportInstances<T>()
    {
        return GetExportInstances<T>(null);
    }
    /// <summary>
    /// Gets the shared instances of all exports of the given contract.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="contractName">Contract name, or <c>null</c> for exports without a contract name.</param>
    /// <returns>Shared instances.</returns>
    public IEnumerable<T> GetExportInstances<T>(string contractName)
    {
        foreach (var export in GetExports<T>(contractName))
            yield return export.Instance;
    }
    /// <summary>
    /// Creates a new instance of every export of the given contract.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <returns>New instances of exports without a contract name.</returns>
    public IEnumerable<T> CreateExportInstances<T>()
    {
        return CreateExportInstances<T>(null);
    }
    /// <summary>
    /// Creates a new instance of every export of the given contract.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="contractName">Contract name, or <c>null</c> for exports without a contract name.</param>
    /// <returns>New instances.</returns>
    public IEnumerable<T> CreateExportInstances<T>(string contractName)
    {
        foreach (var export in GetExports<T>(contractName))
            yield return export.CreateInstance();
    }
    /// <summary>
    /// Gets all exports of the given contract.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <returns>Exports without a contract name.</returns>
    public IEnumerable<ExportComponent<T>> GetExports<T>()
    {
        foreach (var component in GetExports(null, typeof(T)))
            yield return (ExportComponent<T>)component;
    }
    /// <summary>
    /// Gets all exports of the given contract.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="contractName">Contract name, or <c>null</c> for exports without a contract name.</param>
    /// <returns>Exports.</returns>
    public IEnumerable<ExportComponent<T>> GetExports<T>(string contractName)
    {
        foreach (var component in GetExports(contractName, typeof(T)))
            yield return (ExportComponent<T>)component;
    }

    /// <inheritdoc cref="GetExportInstances{T}(string)"/>
    public IEnumerable<object> GetExportInstances(ContractDefinition contract)
    {
        return GetExportInstances(contract.ContractName, contract.ContractType);
    }
    /// <inheritdoc cref="GetExportInstances{T}(string)"/>
    public IEnumerable<object> GetExportInstances(Type contractType)
    {
        return GetExportInstances(null, contractType);
    }
    /// <inheritdoc cref="GetExportInstances{T}(string)"/>
    public IEnumerable<object> GetExportInstances(string contractName, Type contractType)
    {
        foreach (var export in GetExports(contractName, contractType))
            yield return export.Instance;
    }
    /// <inheritdoc cref="CreateExportInstances{T}(string)"/>
    public IEnumerable<object> CreateExportInstances(ContractDefinition contract)
    {
        return CreateExportInstances(contract.ContractName, contract.ContractType);
    }
    /// <inheritdoc cref="CreateExportInstances{T}(string)"/>
    public IEnumerable<object> CreateExportInstances(Type contractType)
    {
        return CreateExportInstances(null, contractType);
    }
    /// <inheritdoc cref="CreateExportInstances{T}(string)"/>
    public IEnumerable<object> CreateExportInstances(string contractName, Type contractType)
    {
        foreach (var export in GetExports(contractName, contractType))
            yield return export.CreateInstance();
    }
    /// <inheritdoc cref="GetExports{T}(string)"/>
    public IEnumerable<ExportComponent> GetExports(ContractDefinition contract)
    {
        if (contract == null)
            throw new ArgumentNullException(nameof(contract));

        return GetExports(contract.ContractName, contract.ContractType);
    }
    /// <inheritdoc cref="GetExports{T}(string)"/>
    public IEnumerable<ExportComponent> GetExports(Type contractType)
    {
        return GetExports(null, contractType);
    }
    /// <inheritdoc cref="GetExports{T}(string)"/>
    public IEnumerable<ExportComponent> GetExports(string contractName, Type contractType)
    {
        lock (_locker)
        {
            if (!_exportTree.TryGetValue((contractType, contractName ?? string.Empty), out var exports))
                return [];

            var items = new ExportComponent[exports.Count];

            for (var i = 0; i < exports.Count; i++)
            {
                var export = exports[i];
                items[i] = export.Component;
            }

            return items;
        }
    }

    /// <summary>
    /// Gets the shared instances of all exports of the given contract names.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="contractNames">Contract names. A <c>null</c> item means exports without a contract name; duplicates are ignored.</param>
    /// <returns>Shared instances.</returns>
    public IEnumerable<T> GetManyExportInstances<T>(string[] contractNames)
    {
        foreach (var export in GetManyExports<T>(contractNames))
            yield return export.Instance;
    }
    /// <summary>
    /// Creates a new instance of every export of the given contract names.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="contractNames">Contract names. A <c>null</c> item means exports without a contract name; duplicates are ignored.</param>
    /// <returns>New instances.</returns>
    public IEnumerable<T> CreateManyExportInstances<T>(string[] contractNames)
    {
        foreach (var export in GetManyExports<T>(contractNames))
            yield return export.CreateInstance();
    }
    /// <summary>
    /// Gets all exports of the given contract names.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="contractNames">Contract names. A <c>null</c> item means exports without a contract name; duplicates are ignored.</param>
    /// <returns>Exports.</returns>
    public IEnumerable<ExportComponent<T>> GetManyExports<T>(string[] contractNames)
    {
        foreach (var component in GetManyExports(contractNames, typeof(T)))
            yield return (ExportComponent<T>)component;
    }

    /// <inheritdoc cref="CreateManyExportInstances{T}"/>
    public IEnumerable<object> CreateManyExportInstances(string[] contractNames, Type contractType)
    {
        foreach (var export in GetManyExports(contractNames, contractType))
            yield return export.CreateInstance();
    }
    /// <inheritdoc cref="GetManyExportInstances{T}"/>
    public IEnumerable<object> GetManyExportInstances(string[] contractNames, Type contractType)
    {
        foreach (var export in GetManyExports(contractNames, contractType))
            yield return export.Instance;
    }
    /// <inheritdoc cref="GetManyExports{T}"/>
    public IList<ExportComponent> GetManyExports(string[] contractNames, Type contractType)
    {
        if (contractNames == null || contractNames.Length == 0)
            return Array.Empty<ExportComponent>();

        List<ExportComponent> items = null;

        lock (_locker)
        {
            for (var i = 0; i < contractNames.Length; i++)
            {
                var name = contractNames[i] ?? string.Empty;

                // Check for duplicate
                var duplicate = false;
                for (var j = 0; j < i; j++)
                {
                    if (name == (contractNames[j] ?? string.Empty))
                    {
                        duplicate = true;
                        break;
                    }
                }

                // Add
                if (!duplicate)
                {
                    if (_exportTree.TryGetValue((contractType, name), out var exports))
                    {
                        items ??= new List<ExportComponent>(exports.Count);

                        // ReSharper disable once ForCanBeConvertedToForeach
                        // ReSharper disable once LoopCanBeConvertedToQuery
                        for (var j = 0; j < exports.Count; j++)
                            items.Add(exports[j].Component);
                    }
                }
            }
        }

        return items ?? [];
    }

    /// <summary>
    /// Gets all exports that come from the given assembly.
    /// </summary>
    /// <param name="assembly">Origin assembly.</param>
    /// <returns>Exports.</returns>
    public IList<ExportComponent> GetExports(Assembly assembly)
    {
        var items = new List<ExportComponent>();

        lock (_locker)
        {
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var exports in _exportTree.Values)
            {
                // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                foreach (var export in exports)
                {
                    var component = export.Component;
                    if (component.OriginAssembly == assembly)
                        items.Add(component);
                }
            }
        }

        return items;
    }
    /// <summary>
    /// Gets the definitions of all exports whose value type comes from the given assembly.
    /// </summary>
    /// <param name="assembly">Value type assembly.</param>
    /// <returns>Export definitions.</returns>
    /// <remarks>Resolves the value type of every export, which loads types that the cache left unresolved.</remarks>
    public IList<ExportComponentDefinition> GetExportsDefinitions(Assembly assembly)
    {
        var items = new List<ExportComponentDefinition>();

        lock (_locker)
        {
            // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
            foreach (var exports in _exportTree.Values)
            {
                // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                foreach (var export in exports)
                    if (export.Definition.ValueType.Assembly == assembly)
                        items.Add(export.Definition);
            }
        }

        return items;
    }

    [Obsolete("Use CreateManyExportInstances instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnumerable<T> TryCreateManyExportInstances<T>(string[] contractNames) => CreateManyExportInstances<T>(contractNames);

    [Obsolete("Use CreateManyExportInstances instead.")]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public IEnumerable<object> TryCreateManyExportInstances(string[] contractNames, Type contractType) => CreateManyExportInstances(contractNames, contractType);

    #endregion

    #region Load/Unload Assembly, Register/Unregister Component

    /// <summary>
    /// Gets the assemblies loaded into the container.
    /// </summary>
    /// <returns>Loaded assemblies.</returns>
    public Assembly[] GetLoadedAssemblies()
    {
        lock (_locker)
        {
            return _loadedAssemblies.ToArray();
        }
    }

    /// <inheritdoc cref="LoadAssembly(Assembly)"/>
    /// <param name="typeInRequestedAssembly">Any type from the assembly to load.</param>
    public void LoadAssembly(Type typeInRequestedAssembly)
    {
        LoadAssembly(typeInRequestedAssembly.Assembly);
    }
    /// <summary>
    /// Loads all exports of the given assembly. Does nothing when the assembly is already loaded.
    /// </summary>
    /// <param name="assembly">Assembly to load.</param>
    /// <remarks>
    /// Reads the export definitions from the cache when a matching cache file exists, otherwise reflects over all types of the assembly.
    /// Either all exports of the assembly become available, or none: a failure unloads the assembly and rethrows.
    /// </remarks>
    public void LoadAssembly(Assembly assembly)
    {
        var changedContractTypes = new HashSet<Type>();
        var duplicateUriHandling = assembly.GetCustomAttribute<AssemblyExportSettingsAttribute>()?.DuplicateUriHandling ?? ExportDuplicateUriHandling.SkipCurrent;

        lock (_locker)
        {
            if (!_loadedAssemblies.Add(assembly))
                return;

            try
            {
                var items = new List<ExportComponentDefinition>();

                // Load from cache
                var cache = TryGetAssemblyCacheModel(assembly);
                var loadFromCache = cache != null;

                if (loadFromCache)
                {
                    try
                    {
                        foreach (var item in cache)
                        {
                            var contractType = _typeResolver(item.Key);
                            if (contractType == null)
                            {
                                loadFromCache = false;
                                break;
                            }

                            // ReSharper disable once ForCanBeConvertedToForeach
                            // ReSharper disable once LoopCanBeConvertedToQuery
                            for (var i = 0; i < item.Value.Count; i++)
                            {
                                var export = item.Value[i];
                                var definition = new ExportComponentDefinition(
                                    export.ContractName ?? string.Empty,
                                    contractType,
                                    assembly,
                                    export.ValueType,
                                    export.Location);

                                items.Add(definition);
                            }
                        }
                    }
                    catch
                    {
                        loadFromCache = false;
                    }
                }

                // Load from assembly
                if (!loadFromCache)
                {
                    items.Clear();

                    var types = assembly.GetTypes();

                    foreach (var valueType in types)
                        if (!valueType.IsInterface && !valueType.IsAbstract)
                            foreach (var exportInfo in valueType.GetCustomAttributes<ExportAttribute>(false))
                            {
                                var contractType = exportInfo.ContractType ?? valueType;

                                if (!contractType.IsAssignableFrom(valueType))
                                    continue;

                                var definition = new ExportComponentDefinition(exportInfo.ContractName, contractType, valueType);
                                items.Add(definition);
                            }
                }

                // Apply changes
                lock (_locker)
                {
                    List<IExportData> removed = null;

                    foreach (var definition in items)
                    {
                        if (_exports.TryGetValue(definition.Location, out var existing) && duplicateUriHandling == ExportDuplicateUriHandling.SkipCurrent)
                            continue;

                        var data = new ExportData(definition);

                        if (existing == null)
                            _exports.Add(definition.Location, data);
                        else if (duplicateUriHandling == ExportDuplicateUriHandling.OverrideExisting)
                        {
                            while (existing.OverrideBy != null)
                                existing = existing.OverrideBy;

                            existing.OverrideBy = data;
                        }
                        else
                        {
                            removed ??= new List<IExportData>(2);
                            removed.Add(existing);

                            UnregisterCore(existing.Component, true);

                            while (_exports.TryGetValue(definition.Location, out existing))
                                UnregisterCore(existing.Component, true);

                            _exports.Add(definition.Location, data);
                        }

                        AddCore(definition.ContractType, definition.ContractName, data);

                        changedContractTypes.Add(definition.ContractType);
                    }

                    if (removed != null)
                        _removedExports.Add((assembly, removed));

                    if (!loadFromCache && !string.IsNullOrEmpty(AssemblyCacheDirectory))
                        _assemblyWithoutCache.Add(assembly.GetName().Name);
                }
            }
            catch
            {
                UnloadAssembly(assembly);
                throw;
            }
        }

        RaiseExportCollectionChanged(changedContractTypes.ToArray());
    }
    /// <summary>
    /// Removes all exports of the given assembly and restores the exports it replaced. Does nothing when the assembly is not loaded.
    /// </summary>
    /// <param name="assembly">Assembly to unload.</param>
    public void UnloadAssembly(Assembly assembly)
    {
        var changedContractTypes = new HashSet<Type>();

        lock (_locker)
        {
            if (!_loadedAssemblies.Remove(assembly))
                return;

            _assemblyWithoutCache.Remove(assembly.GetName().Name);

            foreach (var collection in _exportTree.Values)
            {
                for (var i = collection.Count - 1; i >= 0; i--)
                {
                    var item = collection[i];
                    if (item.OriginAssembly == assembly)
                    {
                        changedContractTypes.Add(item.Definition.ContractType);
                        collection.RemoveAt(i);

                        RemoveLocation(item);
                    }
                }
            }

            var removedIndex = _removedExports.FindIndex(x => x.Assembly == assembly);
            if (removedIndex >= 0)
            {
                var removed = _removedExports[removedIndex].Removed;
                _removedExports.RemoveAt(removedIndex);

                foreach (var data in removed)
                    if (_loadedAssemblies.Contains(data.OriginAssembly))
                    {
                        Register(data.Component, true, true);
                        changedContractTypes.Add(data.Definition.ContractType);
                    }
            }
        }

        RaiseExportCollectionChanged(changedContractTypes.ToArray());
    }
    private void RemoveLocation(IExportData item)
    {
        var location = item.Definition.Location;
        if (_exports.TryGetValue(location, out var data))
            if (data == item)
            {
                if (data.OverrideBy != null)
                    _exports[location] = data.OverrideBy;
                else
                    _exports.Remove(location);
            }
            else
            {
                for (; data.OverrideBy != null; data = data.OverrideBy)
                    if (data.OverrideBy == item)
                    {
                        data.OverrideBy = data.OverrideBy.OverrideBy;
                        break;
                    }
            }
    }

    /// <summary>
    /// Registers a component that no assembly declares.
    /// </summary>
    /// <typeparam name="T">Contract type.</typeparam>
    /// <param name="component">Component to register.</param>
    /// <exception cref="InvalidOperationException">An export with the same location is already registered.</exception>
    public void Register<T>(ExportComponent<T> component) => Register((ExportComponent)component);
    private void Register(ExportComponent component, bool skipExisting = false, bool skipEvent = false)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));
        if (component.Location == null)
            throw new ArgumentException("Component location cannot be null.", nameof(component));

        lock (_locker)
        {
            var data = new RegisteredExportData(component);
            if (_exports.ContainsKey(component.Location))
            {
                if (skipExisting)
                    return;

                throw new InvalidOperationException($"Component with the same uri ({component.Location}) is already registered.");
            }

            _exports.Add(component.Location, data);
            AddCore(component.ContractType, component.ContractName, data);
        }

        if (!skipEvent)
            RaiseExportCollectionChanged([component.ContractType]);
    }
    /// <summary>
    /// Removes the export with the given location.
    /// </summary>
    /// <param name="location">Export URI.</param>
    /// <returns><c>true</c> when an export was removed.</returns>
    public bool Unregister(Uri location)
    {
        if (location == null)
            throw new ArgumentNullException(nameof(location));

        Type contractType = null;

        lock (_locker)
        {
            _exports.TryGetValue(location, out var data);
            if (data != null)
            {
                var key = (data.Definition.ContractType, data.Definition.ContractName ?? string.Empty);

                if (_exportTree.TryGetValue(key, out var collection))
                {
                    for (var i = collection.Count - 1; i >= 0; i--)
                        if (collection[i] == data)
                        {
                            collection.RemoveAt(i);
                            break;
                        }

                    if (collection.Count == 0)
                        _exportTree.Remove(key);
                }

                RemoveLocation(data);
                contractType = data.Definition.ContractType;
            }
        }

        if (contractType != null)
        {
            RaiseExportCollectionChanged([contractType]);
            return true;
        }

        return false;
    }
    /// <summary>
    /// Removes a registered component. Exports that come from an assembly stay untouched.
    /// </summary>
    /// <param name="component">Component to remove.</param>
    /// <returns><c>true</c> when the component was removed.</returns>
    public bool Unregister(ExportComponent component)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));

        if (!UnregisterCore(component, false))
            return false;

        RaiseExportCollectionChanged([component.ContractType]);
        return true;
    }
    /// <summary>
    /// Removes a component without raising <see cref="ExportCollectionChanged"/>.
    /// </summary>
    /// <param name="component">Component to remove.</param>
    /// <param name="force"><c>true</c> to remove the component even when it comes from an assembly.</param>
    /// <returns><c>true</c> when the component was removed.</returns>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public bool UnregisterCore(ExportComponent component, bool force)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));

        lock (_locker)
        {
            var key = (component.ContractType, component.ContractName ?? string.Empty);
            if (_exportTree.TryGetValue(key, out var collection))
            {
                for (var i = collection.Count - 1; i >= 0; i--)
                    if (collection[i].Component == component && (force || collection[i].IsRegistered))
                    {
                        RemoveLocation(collection[i]);
                        collection.RemoveAt(i);

                        if (collection.Count == 0)
                            _exportTree.Remove(key);

                        return true;
                    }
            }
        }

        return false;
    }

    /// <summary>
    /// Adds a handler called when the exports of the given contract type change.
    /// </summary>
    /// <param name="contractType">Contract type to watch.</param>
    /// <param name="handler">Handler to add.</param>
    public void AddNotify(Type contractType, EventHandler handler)
    {
        lock (_events)
        {
            _events.TryGetValue(contractType, out var handlers);
            _events[contractType] = (EventHandler)Delegate.Combine(handlers, handler);
        }
    }
    /// <summary>
    /// Removes a handler added by <see cref="AddNotify"/>.
    /// </summary>
    /// <param name="contractType">Watched contract type.</param>
    /// <param name="handler">Handler to remove.</param>
    public void RemoveNotify(Type contractType, EventHandler handler)
    {
        lock (_events)
        {
            if (_events.TryGetValue(contractType, out var handlers))
            {
                handlers = (EventHandler)Delegate.Remove(handlers, handler);

                if (handlers != null)
                    _events[contractType] = handlers;
                else
                    _events.Remove(contractType);
            }
        }
    }

    private void RaiseExportCollectionChanged(IList<Type> changedContractsTypes)
    {
        if (changedContractsTypes.Count > 0)
        {
            var eh = ExportCollectionChanged;
            if (eh != null)
                eh(this, new ExportCollectionChangedEventArgs(new ReadOnlyCollection<Type>(changedContractsTypes)));

            foreach (var contractType in changedContractsTypes)
            {
                EventHandler neh;
                lock (_events)
                    _events.TryGetValue(contractType, out neh);

                if (neh != null)
                    neh.Invoke(this, EventArgs.Empty);
            }
        }
    }

    /// <summary>
    /// Replaces the resolver that turns a contract type name from a cache file into a type.
    /// </summary>
    /// <param name="typeResolver">Resolver called with an assembly qualified type name. Default: <see cref="Type.GetType(string)"/>.</param>
    /// <remarks>A resolver that returns <c>null</c> makes the container ignore the cache file and reflect over the assembly.</remarks>
    public void UseCustomTypeResolver(Func<string, Type> typeResolver)
    {
        _typeResolver = typeResolver ?? throw new ArgumentNullException(nameof(typeResolver));
    }

    private void AddCore(Type contractType, string contractName, IExportData data)
    {
        var key = (contractType, contractName ?? string.Empty);

        if (!_exportTree.TryGetValue(key, out var collection))
        {
            collection = [];
            _exportTree.Add(key, collection);
        }

        collection.Add(data);
    }

    #endregion

    #region Cache

    /// <summary>
    /// Turns on the assembly cache, which replaces reflection over all types with reading one file per assembly.
    /// </summary>
    /// <param name="assemblyCacheDirectory">Directory with the cache files.</param>
    /// <param name="deserializeFromFile">Reads an object of the given type from a file.</param>
    /// <param name="serializeToFile">Writes an object to a file.</param>
    /// <remarks>
    /// Call before the first <see cref="LoadAssembly(Assembly)"/>. A cache file belongs to one build of an assembly, so a rebuilt
    /// assembly is read through reflection again. An unreadable file falls back to reflection as well. A process that can write to
    /// the directory decides which types the container creates, so keep the directory out of reach of untrusted users.
    /// </remarks>
    public void UseCache(string assemblyCacheDirectory, Func<string, Type, object> deserializeFromFile, Action<string, object> serializeToFile)
    {
        AssemblyCacheDirectory = assemblyCacheDirectory ?? throw new ArgumentNullException(nameof(assemblyCacheDirectory));
        _deserializeFromFile = deserializeFromFile ?? throw new ArgumentNullException(nameof(deserializeFromFile));
        _serializeToFile = serializeToFile ?? throw new ArgumentNullException(nameof(serializeToFile));
    }
    /// <summary>
    /// Writes cache files for the assemblies that were loaded through reflection and deletes the files of their earlier builds.
    /// </summary>
    /// <exception cref="InvalidOperationException"><see cref="UseCache"/> was not called.</exception>
    /// <remarks>Call after startup, for example from a background task.</remarks>
    public void PrepareCache()
    {
        if (string.IsNullOrEmpty(AssemblyCacheDirectory))
            throw new InvalidOperationException("AssemblyCacheDirectory is not set.");

        Dictionary<Assembly, List<ExportComponentDefinition>> assemblies;

        // Prepare data
        lock (_locker)
        {
            if (_assemblyWithoutCache.Count == 0)
                return;

            // Copy components
            var values = _exportTree.Values
                .SelectMany(x => x)
                .Select(x => x.Definition)
                .ToList();
            var loadedAssemblies = _loadedAssemblies.ToList();

            // Create Cache
            assemblies = values
                .GroupBy(x => x.ValueTypeAssembly)
                .Where(x => _assemblyWithoutCache.Contains(x.Key.GetName().Name))
                .ToDictionary(x => x.Key, x => x.ToList());

            foreach (var loadedAssembly in loadedAssemblies)
                if (_assemblyWithoutCache.Contains(loadedAssembly.GetName().Name) && !assemblies.ContainsKey(loadedAssembly))
                    assemblies.Add(loadedAssembly, []);

            _assemblyWithoutCache.Clear();
        }

        // Serialize to files
        var directoryPrepared = false;
        Dictionary<string, string> savedFiles = null;

        foreach (var assemblyGroup in assemblies)
        {
            var assembly = assemblyGroup.Key;
            if (assembly.IsDynamic)
                continue;

            var exports = assemblyGroup.Value;
            var fileName = GetAssemblyCacheFileName(assembly);

            if (!File.Exists(fileName))
            {
                // Create model
                var model = exports.GroupBy(x => x.ContractType)
                    .ToDictionary(
                        x => x.Key.AssemblyQualifiedName?.Replace(", Culture=neutral, PublicKeyToken=null", string.Empty),
                        x =>
                        {
                            return x.Select(data => new AssemblyExportModel
                            {
                                ContractName = data.ContractName,
                                ValueType = data.ValueTypeFullName,
                                Location = data.Location.ToString()
                            }).ToList();
                        });

                // Create cache directory
                if (!directoryPrepared)
                {
                    Directory.CreateDirectory(AssemblyCacheDirectory);
                    directoryPrepared = true;
                }

                // Save
                _serializeToFile(fileName, model);

                savedFiles ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                savedFiles[assembly.GetName().Name] = fileName;
            }
        }

        // Clean up
        if (savedFiles != null)
            RemoveStaleCacheFiles(savedFiles);
    }

    private string GetAssemblyCacheFileName(Assembly assembly)
    {
        var name = assembly.GetName();
        var moduleId = assembly.ManifestModule.ModuleVersionId.ToString("N");

        var directory = AssemblyCacheDirectory ?? throw new InvalidOperationException();
        return Path.Combine(directory, $"{name.Name}-{name.Version}-{moduleId}.json");
    }
    private Dictionary<string, List<AssemblyExportModel>> TryGetAssemblyCacheModel(Assembly assembly)
    {
        if (assembly.IsDynamic)
            return null;

        if (string.IsNullOrEmpty(AssemblyCacheDirectory))
            return null;

        try
        {
            var file = GetAssemblyCacheFileName(assembly);
            if (!File.Exists(file))
                return null;

            var cache = _deserializeFromFile(file, typeof(Dictionary<string, List<AssemblyExportModel>>)) as Dictionary<string, List<AssemblyExportModel>>;
            return cache;
        }
        catch
        {
            // Ignored
        }

        return null;
    }

    private void RemoveStaleCacheFiles(Dictionary<string, string> currentFileNames)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(AssemblyCacheDirectory, "*.json"))
            {
                var match = Regex.Match(Path.GetFileNameWithoutExtension(file), @"^(.+)-\d+(\.\d+){1,3}-[0-9a-f]{32}$");
                if (!match.Success)
                    continue;

                if (currentFileNames.TryGetValue(match.Groups[1].Value, out var currentFileName)
                    && !string.Equals(file, currentFileName, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(file);
                }
            }
        }
        catch { /* ignored */ }
    }

    #endregion

    #region IExportData classes

    private interface IExportData
    {
        ExportComponentDefinition Definition { get; }
        ExportComponent Component { get; }
        Assembly OriginAssembly { get; }
        bool IsRegistered { get; }

        IExportData OverrideBy { get; set; }
    }
    private class ExportData : IExportData
    {
        private ExportComponent _component;

        public ExportComponentDefinition Definition { get; }
        public ExportComponent Component
        {
            get
            {
                if (_component == null)
                {
                    _component = (ExportComponent)Activator.CreateInstance(
                        typeof(ExportComponent<>).MakeGenericType(Definition.ContractType),
                        Definition);
                }

                return _component;
            }
        }
        public Assembly OriginAssembly => Definition.ValueTypeAssembly;
        public bool IsRegistered => false;
        public IExportData OverrideBy { get; set; }

        public ExportData(ExportComponentDefinition definition)
        {
            Definition = definition;
        }
    }
    private class RegisteredExportData : IExportData
    {
        public ExportComponentDefinition Definition => Component.Definition;
        public ExportComponent Component { get; }
        public Assembly OriginAssembly => Component.OriginAssembly;
        public bool IsRegistered => true;
        public IExportData OverrideBy { get; set; }

        public RegisteredExportData(ExportComponent component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            Component = component;
        }
    }

    #endregion

    #region Cache classes

    private class AssemblyExportModel
    {
        public string ContractName { get; set; }
        public string ValueType { get; set; }
        public string Location { get; set; }
    }

    #endregion
}
