using System.Linq.Expressions;
using System.Reflection;

namespace JTSA.NizimaLivePlugin;

internal sealed class NizimaExpansionSubscription : IDisposable
{
    private readonly object target;
    private readonly EventInfo eventInfo;
    private readonly Delegate handler;

    private NizimaExpansionSubscription(object target, EventInfo eventInfo, Delegate handler)
    {
        this.target = target;
        this.eventInfo = eventInfo;
        this.handler = handler;
    }

    public static bool TrySubscribe(object context, Action<string, string> onTrigger, out NizimaExpansionSubscription? subscription)
    {
        subscription = null;
        var eventInfo = context.GetType().GetEvent("ExpansionTriggered");
        var handlerType = eventInfo?.EventHandlerType;
        var invoke = handlerType?.GetMethod("Invoke");
        var parameters = invoke?.GetParameters();
        if (eventInfo is null || handlerType is null || parameters is not { Length: 1 })
            return false;

        var info = Expression.Parameter(parameters[0].ParameterType, "info");
        var call = Expression.Call(
            Expression.Constant(onTrigger),
            typeof(Action<string, string>).GetMethod("Invoke")!,
            Expression.Property(info, "TriggerType"),
            Expression.Property(info, "Value"));
        var handler = Expression.Lambda(handlerType, call, info).Compile();
        eventInfo.AddEventHandler(context, handler);
        subscription = new NizimaExpansionSubscription(context, eventInfo, handler);
        return true;
    }

    public void Dispose() => eventInfo.RemoveEventHandler(target, handler);
}
