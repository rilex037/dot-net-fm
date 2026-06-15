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
    private readonly Dictionary<string, IModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>All registered modules.</summary>
    public IReadOnlyList<IModule> Modules => _modules.Values.ToList();

    /// <summary>
    /// Registers a module. Each module is identified by its URI prefix.
    /// </summary>
    public void Register(IModule module)
    {
        _modules[module.UriPrefix] = module;
    }

    /// <summary>
    /// Finds the module that can handle the given URI.
    /// Returns the default module if no specific match is found.
    /// </summary>
    public IModule? FindByUri(ModuleUri uri)
    {
        if (!string.IsNullOrEmpty(uri.Prefix) &&
            _modules.TryGetValue(uri.Prefix, out var module))
            return module;

        return null;
    }

    /// <summary>
    /// Finds the module that can handle the given path.
    /// Checks each module's CanHandle method.
    /// </summary>
    public IModule? FindByPath(string path)
    {
        // First try to parse as a URI with prefix
        if (ModuleUri.TryParse(path, out var uri) && !string.IsNullOrEmpty(uri.Prefix))
        {
            if (_modules.TryGetValue(uri.Prefix, out var mod))
                return mod;
        }

        // Fall back to checking each module's CanHandle
        foreach (var module in _modules.Values)
        {
            if (module.CanHandle(path))
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
