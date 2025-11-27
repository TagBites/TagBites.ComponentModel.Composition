using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.Reflection;
#if NETCOREAPP
using System.Runtime.Loader;
#endif

namespace TagBites.ComponentModel.Composition;

/// <summary>
/// Thread safe.
/// </summary>
[PublicAPI]
public class ExportComponentManager
{
    #region Events

    private readonly EventHandlerList _events = new();

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

    public string AssemblyCacheDirectory { get; private set; }

    #endregion


    #region Get Exports

    public T GetExportInstance<T>(Uri location)
    {
        var export = GetExport<T>(location);
        return export != null
            ? export.Instance
            : default;
    }
    public T CreateExportInstance<T>(Uri location)
    {
        var export = GetExport<T>(location);
        return export != null
            ? export.CreateInstance()
            : default;
    }
    public ExportComponent<T> GetExport<T>(Uri location)
    {
        return GetExport(location) as ExportComponent<T>;
    }

    public object GetExportInstance(Uri location)
    {
        var export = GetExport(location);
        return export?.Instance;
    }
    public object CreateExportInstance(Uri location)
    {
        var export = GetExport(location);
        return export?.CreateInstance();
    }
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

    public IEnumerable<T> GetExportInstances<T>()
    {
        return GetExportInstances<T>(null);
    }
    public IEnumerable<T> GetExportInstances<T>(string contractName)
    {
        foreach (var export in GetExports<T>(contractName))
            yield return export.Instance;
    }
    public IEnumerable<T> CreateExportInstances<T>()
    {
        return CreateExportInstances<T>(null);
    }
    public IEnumerable<T> CreateExportInstances<T>(string contractName)
    {
        foreach (var export in GetExports<T>(contractName))
            yield return export.CreateInstance();
    }
    public IEnumerable<ExportComponent<T>> GetExports<T>()
    {
        foreach (var component in GetExports(null, typeof(T)))
            yield return (ExportComponent<T>)component;
    }
    public IEnumerable<ExportComponent<T>> GetExports<T>(string contractName)
    {
        foreach (var component in GetExports(contractName, typeof(T)))
            yield return (ExportComponent<T>)component;
    }

    public IEnumerable<object> GetExportInstances(ContractDefinition contract)
    {
        return GetExportInstances(contract.ContactName, contract.ContactType);
    }
    public IEnumerable<object> GetExportInstances(Type contractType)
    {
        return GetExportInstances(null, contractType);
    }
    public IEnumerable<object> GetExportInstances(string contractName, Type contractType)
    {
        foreach (var export in GetExports(contractName, contractType))
            yield return export.Instance;
    }
    public IEnumerable<object> CreateExportInstances(ContractDefinition contract)
    {
        return CreateExportInstances(contract.ContactName, contract.ContactType);
    }
    public IEnumerable<object> CreateExportInstances(Type contractType)
    {
        return CreateExportInstances(null, contractType);
    }
    public IEnumerable<object> CreateExportInstances(string contractName, Type contractType)
    {
        foreach (var export in GetExports(contractName, contractType))
            yield return export.CreateInstance();
    }
    public IEnumerable<ExportComponent> GetExports(ContractDefinition contract)
    {
        if (contract == null)
            throw new ArgumentNullException(nameof(contract));

        return GetExports(contract.ContactName, contract.ContactType);
    }
    public IEnumerable<ExportComponent> GetExports(Type contractType)
    {
        return GetExports(null, contractType);
    }
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

    public IEnumerable<T> GetManyExportInstances<T>(string[] contractNames)
    {
        foreach (var export in GetManyExports<T>(contractNames))
            yield return export.Instance;
    }
    public IEnumerable<T> TryCreateManyExportInstances<T>(string[] contractNames)
    {
        foreach (var export in GetManyExports<T>(contractNames))
            yield return export.CreateInstance();
    }
    public IEnumerable<ExportComponent<T>> GetManyExports<T>(string[] contractNames)
    {
        foreach (var component in GetManyExports(contractNames, typeof(T)))
            yield return (ExportComponent<T>)component;
    }

    public IEnumerable<object> TryCreateManyExportInstances(string[] contractNames, Type contractType)
    {
        foreach (var export in GetManyExports(contractNames, contractType))
            yield return export.CreateInstance();
    }
    public IEnumerable<object> GetManyExportInstances(string[] contractNames, Type contractType)
    {
        foreach (var export in GetManyExports(contractNames, contractType))
            yield return export.Instance;
    }
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

    #endregion

    #region Load/Unload Assembly, Register/Unregister Component

    public Assembly[] GetLoadedAssemblies()
    {
        lock (_locker)
        {
            return _loadedAssemblies.ToArray();
        }
    }

    public void LoadAssembly(Type typeInRequestedAssembly)
    {
        LoadAssembly(typeInRequestedAssembly.Assembly);
    }
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

                // Load form cache
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

            var removed = _removedExports.FirstOrDefault(x => x.Assembly == assembly).Removed;
            if (removed != null)
                foreach (var data in removed)
                {
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

    public void Register<T>(ExportComponent<T> component) => Register((ExportComponent)component);
    private void Register(ExportComponent component, bool skipExisting = false, bool skipEvent = false)
    {
        if (component == null)
            throw new ArgumentNullException(nameof(component));
        if (component.Location == null)
            throw new ArgumentNullException(nameof(component) + "." + nameof(component.Location));

        lock (_locker)
        {
            var data = new RegisteredExportData(component);
            if (_exports.ContainsKey(component.Location))
            {
                if (skipExisting)
                    return;

                throw new Exception(string.Format("Component with the same url ({0}) is already registered.", component.Location));
            }

            _exports.Add(component.Location, data);
            AddCore(component.ContractType, component.ContractName, data);
        }

        if (skipEvent)
            RaiseExportCollectionChanged([component.ContractType]);
    }
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
    public bool Unregister(ExportComponent component) => UnregisterCore(component, false);
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

    public void AddNotify(Type contractType, EventHandler handler)
    {
        lock (_events)
            _events.AddHandler(contractType, handler);
    }
    public void RemoveNotify(Type contractType, EventHandler handler)
    {
        lock (_events)
            _events.RemoveHandler(contractType, handler);
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
                    neh = (EventHandler)_events[contractType];

                if (neh != null)
                    neh.Invoke(this, EventArgs.Empty);
            }
        }
    }

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

    public void UseCache(string assemblyCacheDirectory, Func<string, Type, object> deserializeFromFile, Action<string, object> serializeToFile)
    {
        AssemblyCacheDirectory = assemblyCacheDirectory ?? throw new ArgumentNullException(nameof(assemblyCacheDirectory));
        _deserializeFromFile = deserializeFromFile ?? throw new ArgumentNullException(nameof(deserializeFromFile));
        _serializeToFile = serializeToFile ?? throw new ArgumentNullException(nameof(serializeToFile));
    }
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
            }
        }
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
