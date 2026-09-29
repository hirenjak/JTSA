using System.Buffers.Binary;
using System.IO;
using System.Security.Principal;
using System.Text.Json;

namespace JtsaCalendarBridge;

// This wire contract is also shipped as source in DesktopWallManager.
internal sealed record CalendarRequest(int Version, int Year, int Month, DateTime? UpcomingFrom = null);
internal sealed record SharedCalendarEntry(long Id, DateTime Start, string Title, string Category);
internal sealed record CalendarResponse(int Version, SharedCalendarEntry[] Entries, string? Error = null, bool IsUpcoming = false);

internal static class CalendarBridgeProtocol
{
    internal const int Version = 1;
    internal const int MaxPayloadBytes = 4 * 1024 * 1024;
    internal static string PipeName => "JTSA.Calendar.v1." + WindowsIdentity.GetCurrent().User!.Value;

    internal static (DateTime From, DateTime To) GetRange(CalendarRequest request)
    {
        if (request.Version == Version && request.UpcomingFrom is DateTime upcoming)
            return (upcoming.Date, DateTime.MaxValue);
        if (request.Version != Version || request.Year < 1 || request.Year > 9999 ||
            request.Month < 1 || request.Month > 12 || (request.Year == 9999 && request.Month == 12))
            throw new InvalidDataException("対応していない要求です。");
        var from = new DateTime(request.Year, request.Month, 1);
        return (from, from.AddMonths(1));
    }

    internal static async Task WriteAsync<T>(Stream stream, T value, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(value);
        if (bytes.Length > MaxPayloadBytes) throw new InvalidDataException("予定データが大きすぎます。");
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
    }

    internal static async Task<T> ReadAsync<T>(Stream stream, CancellationToken token)
    {
        var header = new byte[4];
        await stream.ReadExactlyAsync(header, token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > MaxPayloadBytes) throw new InvalidDataException("不正なデータ長です。");
        var bytes = new byte[length];
        await stream.ReadExactlyAsync(bytes, token);
        return JsonSerializer.Deserialize<T>(bytes) ?? throw new InvalidDataException("データが空です。");
    }
}
