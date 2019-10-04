using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Reflection;
using TagBites.Collections;

namespace TagBites.ComponentModel.Composition
{
    /// <summary>
    /// Thread safe.
    /// </summary>
    public class ExportComponentManager
    {
        #region Events

        private readonly EventHandlerList _events = new EventHandlerList();

        public event EventHandler<ExportCollectionChangedEventArgs> ExportCollectionChanged;

        #endregion

        #region Members

        private readonly object _locker = new object();
        private readonly HashSet<Assembly> _loadedAssemblies = new HashSet<Assembly>();
        private readonly Dictionary<Uri, IExportData> _exports = new Dictionary<Uri, IExportData>();
        private readonly MultiDoubleDictionary<Type, string, List<IExportData>, IExportData> _exportTree = new MultiDoubleDictionary<Type, string, List<IExportData>, IExportData>();

        private readonly Func<string, Type, object> _deserializeFromFile;
        private readonly Action<string, object> _serializeToFile;

        private bool _loadedAssemblyOutsideOfCache;
        private bool _lastAssemblyCacheInfoNotFound;
        private CacheInfoModel _lastAssemblyCacheInfo;

        public string AssemblyCacheDirectory { get; }

        #endregion

        #region Constructor

        public ExportComponentManager()
        { }
        public ExportComponentManager(string assemblyCacheDirectory, Func<string, Type, object> deserializeFromFile, Action<string, object> serializeToFile)
        {
            AssemblyCacheDirectory = assemblyCacheDirectory;
            _deserializeFromFile = deserializeFromFile;
            _serializeToFile = serializeToFile;
        }

        #endregion


        #region Get Exports

        public T GetExportInstance<T>(Uri location)
        {
            var export = GetExport<T>(location);
            return export != null
                ? export.Instance
                : default;
        }
        public T TryCreateExportInstance<T>(Uri location)
        {
            var export = GetExport<T>(location);
            return export != null && !export.IsDynamic
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
            return export != null
                ? export.Instance
                : null;
        }
        public object TryCreateExportInstance(Uri location)
        {
            var export = GetExport(location);
            return export != null && !export.IsDynamic
                ? export.CreateInstance()
                : null;
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
        public IEnumerable<T> TryCreateExportInstances<T>()
        {
            return TryCreateExportInstances<T>(null);
        }
        public IEnumerable<T> TryCreateExportInstances<T>(string contractName)
        {
            foreach (var export in GetExports<T>(contractName))
                if (!export.IsDynamic)
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
        public IEnumerable<object> TryCreateExportInstances(ContractDefinition contract)
        {
            return TryCreateExportInstances(contract.ContactName, contract.ContactType);
        }
        public IEnumerable<object> TryCreateExportInstances(Type contractType)
        {
            return TryCreateExportInstances(null, contractType);
        }
        public IEnumerable<object> TryCreateExportInstances(string contractName, Type contractType)
        {
            foreach (var export in GetExports(contractName, contractType))
                if (!export.IsDynamic)
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
                var exports = _exportTree.TryGetValueDefault(contractType, contractName ?? string.Empty);

                if (exports != null)
                    foreach (var export in exports)
                        yield return export.Component;
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
                if (!export.IsDynamic)
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
                if (!export.IsDynamic)
                    yield return export.CreateInstance();
        }
        public IEnumerable<object> GetManyExportInstances(string[] contractNames, Type contractType)
        {
            foreach (var export in GetManyExports(contractNames, contractType))
                yield return export.Instance;
        }
        public IEnumerable<ExportComponent> GetManyExports(string[] contractNames, Type contractType)
        {
            lock (_locker)
            {
                if (contractNames == null || contractNames.Length == 0)
                    yield break;

                var names = new HashSet<string>();

                // ReSharper disable once ForCanBeConvertedToForeach
                for (var i = 0; i < contractNames.Length; i++)
                {
                    var name = contractNames[i] ?? string.Empty;
                    if (names.Add(name))
                    {
                        var exports = _exportTree.TryGetValueDefault(contractType, name);

                        if (exports != null)
                            foreach (var export in exports)
                                yield return export.Component;
                    }
                }
            }
        }

        public IEnumerable<ExportComponent> GetExports(Assembly assembly)
        {
            lock (_locker)
            {
                foreach (var exports in _exportTree.Values)
                    foreach (var export in exports)
                    {
                        var component = export.Component;
                        if (component.OriginAssembly == assembly)
                            yield return component;
                    }
            }
        }
        public IEnumerable<ExportComponentDefinition> GetExportsDefinitions(Assembly assembly)
        {
            lock (_locker)
            {
                foreach (var exports in _exportTree.Values)
                    foreach (var export in exports)
                        if (export.Definition.ValueType.GetTypeInfo().Assembly == assembly)
                            yield return export.Definition;
            }
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

        //public void LoadDirectory(string directory, bool compileScripts = true)
        //{
        //    lock (m_locker)
        //    {
        //        // Load Assemblies
        //        foreach (var item in Directory.GetFiles("*.dll"))
        //            try
        //            {
        //                var assembly = Assembly.LoadFile(item);
        //                LoadAssembly(assembly);
        //            }
        //            catch { }

        //        // Load and compile files
        //        if (compileScripts)
        //        {
        //            foreach (var item in Directory.GetFiles("*.cs"))
        //                try
        //                {
        //                    var source = File.ReadAllText(item);
        //                    var dynamicAssembly = DynamicAssemblyCompiler.Compile(new[] { source });

        //                    LoadAssembly(dynamicAssembly.Assembly);
        //                }
        //                catch { }
        //        }
        //    }
        //}

        public void LoadAssembly(Type typeInRequestedAssembly)
        {
            LoadAssembly(typeInRequestedAssembly.GetTypeInfo().Assembly);
        }
        public void LoadAssembly(Assembly assembly)
        {
            lock (_locker)
            {
                if (!_loadedAssemblies.Add(assembly))
                    return;

                var items = new List<ExportComponentDefinition>();
                var loadDirectly = true;

                // Load form cache
                var cache = GetAssemblyCacheModel(assembly);
                if (cache != null)
                {
                    loadDirectly = false;
                    var map = new Dictionary<string, Type>();
                    try
                    {
                        foreach (var export in cache.Exports)
                        {
                            if (!map.TryGetValue(export.ContractType, out var contractType))
                            {
                                contractType = Type.GetType(export.ContractType);
                                if (contractType == null)
                                {
                                    loadDirectly = true;
                                    break;
                                }

                                map.Add(export.ContractType, contractType);
                            }

                            var definition = new ExportComponentDefinition(export.ContractName ?? string.Empty, contractType, assembly, export.ValueType, export.Location);
                            items.Add(definition);
                        }
                    }
                    catch
                    {
                        loadDirectly = true;
                    }
                }

                // Load from assembly
                if (loadDirectly)
                {
                    items.Clear();

                    var types = assembly.GetTypes();

                    foreach (var valueType in types)
                        if (!valueType.IsInterface && !valueType.IsAbstract)
                            foreach (var exportInfo in valueType.GetCustomAttributes<ExportAttribute>(false))
                            {
                                if (!HasDefaultConstructor(valueType))
                                    continue;

                                var contractType = exportInfo.ContractType ?? valueType;
                                if (!contractType.GetTypeInfo().IsAssignableFrom(valueType))
                                    continue;

                                var definition = new ExportComponentDefinition(exportInfo.ContractName, contractType, valueType, null);
                                items.Add(definition);
                            }
                }

                // Apply changes
                var changedContractTypes = new HashSet<Type>();

                lock (_locker)
                {
                    foreach (var definition in items)
                    {
                        if (_exports.ContainsKey(definition.Location))
                            continue;

                        var data = new ExportData(definition);

                        _exports.Add(definition.Location, data);
                        _exportTree.Add(definition.ContractType, definition.ContractName ?? string.Empty, data);

                        changedContractTypes.Add(definition.ContractType);
                    }

                    if (loadDirectly)
                        _loadedAssemblyOutsideOfCache = true;
                }

                RaiseExportCollectionChanged(changedContractTypes.ToArray());
            }
        }
        public void UnloadAssembly(Assembly assembly)
        {
            var changedContractTypes = new HashSet<Type>();

            lock (_locker)
            {
                if (!_loadedAssemblies.Remove(assembly))
                    return;

                foreach (var collection in _exportTree.Values)
                {
                    for (var i = collection.Count - 1; i >= 0; i--)
                        if (collection[i].OriginAssembly == assembly)
                        {
                            changedContractTypes.Add(collection[i].Definition.ContractType);
                            _exports.Remove(collection[i].Definition.Location);
                            collection.RemoveAt(i);
                        }
                }
            }

            RaiseExportCollectionChanged(changedContractTypes.ToArray());
        }

        public void Register<T>(ExportComponent<T> component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));
            if (component.Location == null)
                throw new ArgumentNullException(nameof(component) + "." + nameof(component.Location));

            lock (_locker)
            {
                var data = new RegisteredExportData(component);
                if (_exports.ContainsKey(component.Location))
                    throw new Exception(string.Format("Component with the same url ({0}) is already registered.", component.Location));

                _exports.Add(component.Location, data);
                _exportTree.Add(component.ContractType, component.ContractName ?? string.Empty, data);
            }

            RaiseExportCollectionChanged(new[] { component.ContractType });
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
                    var collection = _exportTree.TryGetValueDefault(data.Definition.ContractType, data.Definition.ContractName ?? string.Empty);
                    if (collection != null)
                    {
                        for (var i = collection.Count - 1; i >= 0; i--)
                            if (collection[i] == data)
                            {
                                collection.RemoveAt(i);
                                break;
                            }
                    }

                    _exports.Remove(location);
                    contractType = data.Definition.ContractType;
                }
            }

            if (contractType != null)
            {
                RaiseExportCollectionChanged(new[] { contractType });
                return true;
            }

            return false;
        }
        public bool Unregister(ExportComponent component)
        {
            if (component == null)
                throw new ArgumentNullException(nameof(component));

            lock (_locker)
            {
                var collection = _exportTree.TryGetValueDefault(component.ContractType, component.ContractName ?? string.Empty);
                if (collection != null)
                {
                    for (var i = collection.Count - 1; i >= 0; i--)
                        if (collection[i].IsRegistered && collection[i].Component == component)
                        {
                            _exports.Remove(collection[i].Definition.Location);
                            collection.RemoveAt(i);
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

        #endregion

        #region Cache

        private string GetCacheFileName()
        {
            var directory = AssemblyCacheDirectory;
            if (string.IsNullOrEmpty(directory))
                return null;

            return Path.Combine(directory, "cache-info.json");
        }
        private string GetAssemblyCacheFileName(Assembly assembly)
        {
            var directory = AssemblyCacheDirectory;
            if (string.IsNullOrEmpty(directory))
                return null;

            var name = assembly.GetName();
            return Path.Combine(directory, $"{name.Name} v. {name.Version}.json");
        }

        private CacheInfoModel GetCacheModel(bool forceLoaded)
        {
            if (!forceLoaded)
            {
                if (_lastAssemblyCacheInfoNotFound)
                    return null;

                if (_lastAssemblyCacheInfo != null)
                    return _lastAssemblyCacheInfo;
            }

            _lastAssemblyCacheInfo = null;

            var fullName = GetCacheFileName();
            if (File.Exists(fullName))
            {
                try
                {
                    lock (_locker)
                        _lastAssemblyCacheInfo = _deserializeFromFile(fullName, typeof(CacheInfoModel)) as CacheInfoModel;// ModelManager<CacheInfoModel>.FromFile(fullName);                      
                }
                catch
                {
                    // Ignored
                }
            }

            _lastAssemblyCacheInfoNotFound = _lastAssemblyCacheInfo == null;
            return _lastAssemblyCacheInfo;
        }
        private AssemblyCacheModel GetAssemblyCacheModel(Assembly assembly)
        {
            try
            {
                if (assembly.IsDynamic)
                    return null;

                var cacheInfo = GetCacheModel(false);
                if (cacheInfo == null)
                    return null;

                var file = GetAssemblyCacheFileName(assembly);
                if (!File.Exists(file))
                    return null;

                var current = CreateAssemblyCacheInfo(assembly);
                var cached = cacheInfo.Assemblies.FirstOrDefault(x => x.Equals(current));
                if (cached == null)
                    return null;

                return _deserializeFromFile(file, typeof(AssemblyCacheModel)) as AssemblyCacheModel; // ModelManager<AssemblyCacheModel>.FromFile(fullName);
            }
            catch
            {
                // Ignored
            }

            return null;
        }

        public void PrepareCache()
        {
            string cacheInfoFileName;
            CacheInfoModel oldCacheInfo;
            List<ExportComponentDefinition> values;
            List<Assembly> loadedAssemblies;

            lock (_locker)
            {
                if (!_loadedAssemblyOutsideOfCache)
                    return;
                _loadedAssemblyOutsideOfCache = false;

                cacheInfoFileName = GetCacheFileName();
                if (string.IsNullOrEmpty(cacheInfoFileName))
                    throw new InvalidOperationException("AssemblyCacheDirectory is not set.");

                oldCacheInfo = GetCacheModel(true);

                // Create cache directory
                var directory = Path.GetDirectoryName(cacheInfoFileName);
                if (!string.IsNullOrEmpty(directory))
                    Directory.CreateDirectory(directory);

                // Copy components
                values = _exportTree.Values.SelectMany(x => x).Select(x => x.Definition).ToList();
                loadedAssemblies = _loadedAssemblies.ToList();
            }

            // Create Cache
            var assemblies = values.GroupBy(x => x.ValueTypeAssembly).ToDictionary(x => x.Key, x => x.ToList());
            foreach (var loadedAssembly in loadedAssemblies)
                if (!assemblies.ContainsKey(loadedAssembly))
                    assemblies.Add(loadedAssembly, new List<ExportComponentDefinition>());

            var cacheInfo = new CacheInfoModel();

            foreach (var assembly in assemblies)
                if (!assembly.Key.IsDynamic)
                {
                    var current = CreateAssemblyCacheInfo(assembly.Key);
                    var cached = oldCacheInfo?.Assemblies.FirstOrDefault(x => x.Equals(current));
                    var fileName = GetAssemblyCacheFileName(assembly.Key);

                    if (cached == null || !File.Exists(fileName))
                    {
                        var assemblyModel = new AssemblyCacheModel();
                        foreach (var data in assembly.Value)
                        {
                            var exportModel = new AssemblyExportModel()
                            {
                                ContractName = data.ContractName,
                                ContractType = data.ContractType.AssemblyQualifiedName,
                                ValueType = data.ValueTypeFullName,
                                Location = data.Location.ToString()
                            };
                            assemblyModel.Exports.Add(exportModel);
                        }

                        _serializeToFile(fileName, assemblyModel); // ModelManager.ToFile(assemblyModel, fileName);
                    }

                    cacheInfo.Assemblies.Add(current);
                }

            _serializeToFile(cacheInfoFileName, cacheInfo); // ModelManager.ToFile(cacheInfo, cacheInfoFileName);
        }
        private AssemblyCacheInfoModel CreateAssemblyCacheInfo(Assembly assembly)
        {
            var fileInfo = new FileInfo(assembly.Location);
            var name = assembly.GetName();

            return new AssemblyCacheInfoModel
            {
                Name = name.Name,
                Version = name.Version.ToString(),
                ModifyTime = fileInfo.LastWriteTimeUtc,
                Size = fileInfo.Length
            };
        }

        #endregion

        #region IExportData classes

        private interface IExportData
        {
            ExportComponentDefinition Definition { get; }
            ExportComponent Component { get; }
            Assembly OriginAssembly { get; }
            bool IsRegistered { get; }
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
            public Assembly OriginAssembly => Definition.ValueType.GetTypeInfo().Assembly;
            public bool IsRegistered => false;

            public ExportData(ExportComponentDefinition definition)
            {
                Definition = definition;
            }
        }
        private class RegisteredExportData : IExportData
        {
            public ExportComponentDefinition Definition { get; }
            public ExportComponent Component { get; }
            public Assembly OriginAssembly => Component.OriginAssembly;
            public bool IsRegistered => true;

            public RegisteredExportData(ExportComponent component)
            {
                if (component == null)
                    throw new ArgumentNullException(nameof(component));

                Definition = new ExportComponentDefinition(component.ContractName, component.ContractType, component.ValueType, component.Metadata);
                Component = component;
            }
        }

        private class CacheInfoModel
        {
            public IList<AssemblyCacheInfoModel> Assemblies { get; } = new List<AssemblyCacheInfoModel>();
        }
        private sealed class AssemblyCacheInfoModel
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public long Size { get; set; }
            public DateTime ModifyTime { get; set; }


            private bool Equals(AssemblyCacheInfoModel other)
            {
                return string.Equals(Name, other.Name)
                       && string.Equals(Version, other.Version)
                       && Size == other.Size
                       && ModifyTime.Equals(other.ModifyTime);
            }
            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj))
                    return false;
                if (ReferenceEquals(this, obj))
                    return true;
                return obj is AssemblyCacheInfoModel other && Equals(other);
            }
            public override int GetHashCode()
            {
                // ReSharper disable once NonReadonlyMemberInGetHashCode
                return Name?.GetHashCode() ?? 0;
            }
            public override string ToString()
            {
                return $"{Name} v. {Version}";
            }
        }
        private class AssemblyCacheModel
        {
            public IList<AssemblyExportModel> Exports { get; } = new List<AssemblyExportModel>();
        }
        private class AssemblyExportModel
        {
            public string ContractName { get; set; }
            public string ContractType { get; set; }
            public string ValueType { get; set; }
            public string Location { get; set; }
        }

        #endregion

        #region Helpers

        private static bool HasDefaultConstructor(Type type)
        {
            return type.GetTypeInfo().DeclaredConstructors.Any(x => x.IsPublic && x.GetParameters().Length == 0);
        }

        #endregion
    }
}
