using JTSA.Plugin.Abstractions;

namespace JTSA.Plugins;

internal sealed record LoadedPlugin(IJtsaPlugin Instance, PluginLoadContext LoadContext);
