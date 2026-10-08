using JTSA.Plugin.Abstractions;
using System.Reflection;
using System.Runtime.Loader;
using System.IO;

namespace JTSA.Plugins;

internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly HashSet<string> HostSharedAssemblies = new(StringComparer.OrdinalIgnoreCase)
    {
        typeof(IJtsaPlugin).Assembly.GetName().Name!,
        "Microsoft.Web.WebView2.Core",
        "Microsoft.Web.WebView2.Wpf",
        "Microsoft.Web.WebView2.WinForms"
    };

    private readonly AssemblyDependencyResolver resolver;

    public PluginLoadContext(string pluginAssemblyPath)
        : base($"JTSA.Plugin:{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: true)
    {
        resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // 契約型とUI上で相互利用する型は、ホストと同じAssemblyLoadContextを使う。
        // WebView2をプラグイン側にも読み込むと、同じ完全名でも別の型になり
        // 本体のXAML生成時にInvalidCastExceptionが発生する。
        if (assemblyName.Name is not null && HostSharedAssemblies.Contains(assemblyName.Name))
            return AssemblyLoadContext.Default.LoadFromAssemblyName(assemblyName);

        var path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}
