using System.Buffers.Binary;
using System.IO;
using JtsaCalendarBridge;
using Xunit;

namespace JTSA.Tests;

public class CalendarBridgeProtocolTests
{
    [Fact]
    public async Task RoundTripPreservesJapaneseTitlesIdsAndLocalStartTimes()
    {
        var expected = new CalendarResponse(1, [new(42, new DateTime(2026, 9, 27, 23, 30, 0), "配信\nコラボ 🎮", "ゲーム")]);
        using var stream = new MemoryStream();
        await CalendarBridgeProtocol.WriteAsync(stream, expected, CancellationToken.None);
        stream.Position = 0;
        var actual = await CalendarBridgeProtocol.ReadAsync<CalendarResponse>(stream, CancellationToken.None);
        Assert.Equal(expected.Version, actual.Version);
        Assert.Equal(expected.Entries, actual.Entries);
        Assert.Equal(DateTimeKind.Unspecified, actual.Entries[0].Start.Kind);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4194305)]
    public async Task InvalidLengthsAreRejectedBeforeAllocatingPayload(int length)
    {
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, length);
        using var stream = new MemoryStream(header);
        await Assert.ThrowsAsync<InvalidDataException>(() => CalendarBridgeProtocol.ReadAsync<CalendarRequest>(stream, CancellationToken.None));
    }

    [Fact]
    public async Task TruncatedMessagesAreRejected()
    {
        using var stream = new MemoryStream(new byte[] { 8, 0, 0, 0, 123 });
        await Assert.ThrowsAsync<EndOfStreamException>(() => CalendarBridgeProtocol.ReadAsync<CalendarRequest>(stream, CancellationToken.None));
    }

    [Theory]
    [InlineData(2, 2026, 9)]
    [InlineData(1, 0, 9)]
    [InlineData(1, 2026, 13)]
    [InlineData(1, 9999, 12)]
    public void UnsupportedRequestsAreRejected(int version, int year, int month)
        => Assert.Throws<InvalidDataException>(() => CalendarBridgeProtocol.GetRange(new(version, year, month)));

    [Fact]
    public void MonthRangeIncludesLeapDayAndExcludesNextMonth()
    {
        var (from, to) = CalendarBridgeProtocol.GetRange(new(1, 2024, 2));
        Assert.Equal(new DateTime(2024, 2, 1), from);
        Assert.Equal(new DateTime(2024, 3, 1), to);
        Assert.Equal(29, (to - from).Days);
    }
}
