namespace JTSA.MultiPlatformPlugin;

internal sealed record PlatformCategory(string Id, string Name)
{
    public override string ToString() => $"{Name}（{Id}）";
}
