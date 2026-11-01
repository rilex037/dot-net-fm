using System.IO;
using System.Reflection;

namespace DotNetFM;

public sealed class ModuleRegistry
{
    private readonly Dictionary<string, IModule> _prefixMap = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IModule> _orderedModules = new();

    public IReadOnlyList<IModule> Modules => _orderedModules;

    public IModule DefaultModule => _orderedModules.Count > 0
        ? _orderedModules[0]
        : throw new InvalidOperationException("No modules registered.");

    public void Register(IModule module)
    {
        _orderedModules.Add(module);

        foreach (var prefix in module.UriPrefixes)
            _prefixMap[prefix] = module;
    }

    public IModule? FindByUri(ModuleUri uri)
    {
        if (!string.IsNullOrEmpty(uri.Prefix) &&
            _prefixMap.TryGetValue(uri.Prefix, out var module))
            return module;

        return null;
    }

    public IModule? FindByPath(string path)
    {
        if (string.IsNullOrEmpty(path))
            return null;

        if (ModuleUri.TryParse(path, out var uri) && !string.IsNullOrEmpty(uri.Prefix))
        {
            if (_prefixMap.TryGetValue(uri.Prefix, out var mod))
                return mod;
        }

        foreach (var (prefix, module) in _prefixMap)
        {
            if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return module;
        }

        return null;
    }

    public void ScanAndRegister(Assembly assembly)
    {
        Type[] moduleTypes;
        try
        {
            moduleTypes = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException)
        {
            return;
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

    public void ScanAndRegister(params Assembly[] assemblies)
    {
        foreach (var assembly in assemblies)
            ScanAndRegister(assembly);
    }

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
                }
            }
        }
        catch
        {
        }
    }
}
