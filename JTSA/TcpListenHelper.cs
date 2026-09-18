using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace JTSA;

static class TcpListenHelper
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;
    private const int TcpTableOwnerPidListener = 3;
    private const uint ErrorInsufficientBuffer = 122;
    private const int TcpRowOwnerPidSize = 24;
    private const int Tcp6RowOwnerPidSize = 56;

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable, ref int size, bool order, int addressFamily, int tableClass, uint reserved);

    public static bool HasListeningPort(int port) => GetListeningPids(port).Count > 0;

    public static List<int> GetListeningPids(int port)
    {
        if (port is < 1 or > 65535) return [];

        var pids = new HashSet<int>();
        Collect(AfInet, port, pids);
        Collect(AfInet6, port, pids);
        return [.. pids];
    }

    public static bool TryKillListening(int port, out string error)
    {
        error = "";
        var killed = false;
        foreach (var pid in GetListeningPids(port))
        {
            try
            {
                using var process = Process.GetProcessById(pid);
                if (process.HasExited) continue;
                process.Kill(entireProcessTree: true);
                killed = true;
            }
            catch (ArgumentException)
            {
            }
            catch (Exception ex)
            {
                error = $"ポート {port} (PID {pid}) を停止できませんでした: {ex.Message}";
                return killed;
            }
        }

        return killed;
    }

    private static void Collect(int addressFamily, int port, HashSet<int> pids)
    {
        var size = 0;
        var status = GetExtendedTcpTable(IntPtr.Zero, ref size, true, addressFamily, TcpTableOwnerPidListener, 0);
        if (status != 0 && status != ErrorInsufficientBuffer) return;

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            status = GetExtendedTcpTable(buffer, ref size, true, addressFamily, TcpTableOwnerPidListener, 0);
            if (status != 0) return;

            var count = Marshal.ReadInt32(buffer);
            var rowSize = addressFamily == AfInet ? TcpRowOwnerPidSize : Tcp6RowOwnerPidSize;
            var portOffset = addressFamily == AfInet ? 8 : 20;
            var pidOffset = rowSize - 4;
            var row = IntPtr.Add(buffer, 4);
            for (var i = 0; i < count; i++)
            {
                var localPort = PortFromNetworkDword((uint)Marshal.ReadInt32(row, portOffset));
                var pid = Marshal.ReadInt32(row, pidOffset);
                if (localPort == port && pid > 0) pids.Add(pid);
                row = IntPtr.Add(row, rowSize);
            }
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int PortFromNetworkDword(uint value)
    {
        var network = unchecked((short)(value & 0xFFFF));
        return unchecked((ushort)IPAddress.NetworkToHostOrder(network));
    }
}
