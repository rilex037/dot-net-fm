using System.IO;
using System.Reflection;

namespace dot_net_fm;

/// <summary>
/// Registry that discovers and manages modules. Scans assemblies for
/// IModule implementations and provides lookup by URI prefix.
/// At runtime, discovers module DLLs automatically from the output directory.
/// </summary>
public sealed class ModuleRegistry
{
    private readonly Dictionary<string, IModule> _prefixMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IModule> _orderedModules = new();

    /// <summary>All registered modules (deduplicated, in registration order).</summary>
    public IReadOnlyList<IModule> Modules => _orderedModules;

    /// <summary>The default module for no-argument startup (first registered module).</summary>
    public IModule DefaultModule => _orderedModules.Count > 0
        ? _orderedModules[0]
        : throw new InvalidOperationException("No modules registered.");

    /// <summary>
    /// Registers a module. Reads its URI prefixes and builds a prefix→module mapping.
    /// </summary>
    public void Register(IModule module)
    {
        _orderedModules.Add(module);

        foreach (var prefix in module.UriPrefixes)
            _prefixMap[prefix] = module;
    }

    /// <summary>
    /// Finds the module by URI prefix — O(1) dictionary lookup.
    /// </summary>
    public IModule? FindByUri(ModuleUri uri)
    {
        if (!string.IsNullOrEmpty(uri.Prefix) &&
            _prefixMap.TryGetValue(uri.Prefix, out var module))
            return module;

        return null;
    }

    /// <summary>
    /// Finds the module for the given path.
    /// 1. URI scheme → direct prefix map lookup.
    /// 2. Bare string → check if it starts with any registered prefix.
    /// </summary>
    public IModule? FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        // URI-style: "windows://C:\Users" → parse scheme, direct lookup
        if (ModuleUri.TryParse(path, out var uri) && !string.IsNullOrEmpty(uri.Prefix))
        {
            if (_prefixMap.TryGetValue(uri.Prefix, out var mod))
                return mod;
        }

        // Bare string: match against registered prefixes
        foreach (var (prefix, module) in _prefixMap)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return module;
        }

        return null;
    }

    /// <summary>
    /// Scans the given assembly for IModule implementations and registers them.
    /// </summary>
    public void ScanAndRegister(Assembly assembly)
    {
        Type[] moduleTypes;
        try
        {
            moduleTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException)
        {
            return; // Skip assemblies that can't be loaded
        }

        foreach (var type in moduleTypes)
        {
            if (type is { IsClass: true, IsAbstract: false } && typeof(IModule).IsAssignableFrom(type))
            {
                if (Activator.CreateInstance(type) is IModule module)
                    Register(module);
            }
        }
    }

    /// <summary>
    /// Scans multiple assemblies for IModule implementations.
    /// </summary>
    public void ScanAndRegister(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
            ScanAndRegister(assembly);
    }

    /// <summary>
    /// Discovers and registers all module DLLs from the application output directory.
    /// Each module is its own separate DLL (e.g. DotNetFM.Module.Windows.dll,
    /// DotNetFM.Module.FTP.dll, DotNetFM.Module.Cloud.dll).
    /// Modules are discovered by naming convention: DotNetFM.Module.*.dll.
    /// Just drop any compatible module DLL into the output folder — 
    /// no compile-time references needed, no source-level coupling.
    /// </summary>
    public void ScanAndRegisterAll()
    {
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        if (string.IsNullOrEmpty(appDir)) return;

        try
        {
            var moduleDlls = Directory.GetFiles(appDir, "DotNetFM.Module.*.dll");

            foreach (var dllPath in moduleDlls)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dllPath);
                    ScanAndRegister(assembly);
                }
                catch
                {
                    // Skip DLLs that can't be loaded (version mismatch, etc.)
                }
            }
        }
        catch
        {
            // Directory scan failed — no modules discovered
        }
    }
}
