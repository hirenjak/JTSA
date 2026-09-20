using JTSA.Plugin.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;

namespace JTSA.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base($"JTSA.Plugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: true)
    {
        resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 契約型はホストと同じインスタンスを使わないと IJtsaPlugin の判定に失敗する。
        if (assemblyName.Name == typeof(IJtsaPlugin).Assembly.GetName().Name)
            return null;

        var path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
