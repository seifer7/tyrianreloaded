/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public sealed class SdlNetTransport : INetworkTransport, IDisposable
{
    private nint _socket, _packet;
    private bool _initialized, _disposed, _bound;
    public ushort LocalPort { get; }

    public SdlNetTransport(ushort port = 1333)
    {
        SdlNative.RegisterLibraries();
        try
        {
            if (Native.Init() < 0) throw SdlNative.Error("Failed to initialize SDL2_net");
            _initialized = true;
            _socket = Native.Open(port);
            if (_socket == 0) throw SdlNative.Error("Failed to open SDL2_net UDP socket");
            _packet = Native.Allocate(NetworkSession.PacketSize);
            if (_packet == 0) throw SdlNative.Error("Failed to allocate SDL2_net UDP packet");
            var address = Native.GetAddress(_socket, -1);
            if (address == 0) throw SdlNative.Error("Failed to obtain SDL2_net local UDP address");
            var ip = Marshal.PtrToStructure<Native.Address>(address);
            LocalPort = BitConverter.IsLittleEndian ? BinaryPrimitives.ReverseEndianness(ip.Port) : ip.Port;
        }
        catch { Release(); throw; }
    }

    public void Bind(string host, ushort port)
    {
        EnsureOpen();
        ArgumentException.ThrowIfNullOrEmpty(host);
        if (Native.Resolve(out var address, host, port) != 0) throw SdlNative.Error("Failed to resolve SDL2_net peer");
        if (Native.Bind(_socket, 0, ref address) != 0) throw SdlNative.Error("Failed to bind SDL2_net peer");
        _bound = true;
    }

    public void Send(ReadOnlySpan<byte> bytes)
    {
        EnsureOpen();
        if (!_bound) throw new InvalidOperationException("SDL2_net peer is not bound.");
        if (bytes.Length is < 4 or > NetworkSession.PacketSize) throw new ArgumentOutOfRangeException(nameof(bytes));
        var packet = Marshal.PtrToStructure<Native.Packet>(_packet);
        Marshal.Copy(bytes.ToArray(), 0, packet.Data, bytes.Length);
        packet.Length = bytes.Length;
        Marshal.StructureToPtr(packet, _packet, false);
        if (Native.Send(_socket, 0, _packet) != 1) throw SdlNative.Error("Failed to send SDL2_net UDP packet");
    }

    public NetworkPacket? Receive()
    {
        EnsureOpen();
        var result = Native.Receive(_socket, _packet);
        if (result < 0) throw SdlNative.Error("Failed to receive SDL2_net UDP packet");
        if (result == 0) return null;
        var packet = Marshal.PtrToStructure<Native.Packet>(_packet);
        if (packet.Length is < 0 or > NetworkSession.PacketSize) throw new InvalidDataException("SDL2_net returned an invalid UDP packet size.");
        var bytes = new byte[packet.Length];
        Marshal.Copy(packet.Data, bytes, 0, bytes.Length);
        return new(bytes, packet.Channel);
    }
    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_disposed, this);
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; Release(); GC.SuppressFinalize(this);
    }
    private void Release()
    {
        if (_packet != 0) { Native.Free(_packet); _packet = 0; }
        if (_socket != 0) { Native.Close(_socket); _socket = 0; }
        if (_initialized) { Native.Quit(); _initialized = false; }
    }

    private static class Native
    {
        private const string Library = "OpenTyrian.SDL2_net";
        [StructLayout(LayoutKind.Sequential)]
        internal struct Address { internal uint Host; internal ushort Port; }
        [StructLayout(LayoutKind.Sequential)]
        internal struct Packet
        {
            internal int Channel;
            internal nint Data;
            internal int Length, MaximumLength, Status;
            internal Address Address;
        }
        [DllImport(Library, EntryPoint = "SDLNet_Init", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Init();
        [DllImport(Library, EntryPoint = "SDLNet_Quit", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Quit();
        [DllImport(Library, EntryPoint = "SDLNet_ResolveHost", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Resolve(out Address address, [MarshalAs(UnmanagedType.LPUTF8Str)] string host, ushort port);
        [DllImport(Library, EntryPoint = "SDLNet_UDP_Open", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint Open(ushort port);
        [DllImport(Library, EntryPoint = "SDLNet_UDP_Close", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Close(nint socket);
        [DllImport(Library, EntryPoint = "SDLNet_UDP_Bind", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Bind(nint socket, int channel, ref Address address);
        [DllImport(Library, EntryPoint = "SDLNet_UDP_GetPeerAddress", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint GetAddress(nint socket, int channel);
        [DllImport(Library, EntryPoint = "SDLNet_AllocPacket", CallingConvention = CallingConvention.Cdecl)]
        internal static extern nint Allocate(int size);
        [DllImport(Library, EntryPoint = "SDLNet_FreePacket", CallingConvention = CallingConvention.Cdecl)]
        internal static extern void Free(nint packet);
        [DllImport(Library, EntryPoint = "SDLNet_UDP_Send", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Send(nint socket, int channel, nint packet);
        [DllImport(Library, EntryPoint = "SDLNet_UDP_Recv", CallingConvention = CallingConvention.Cdecl)]
        internal static extern int Receive(nint socket, nint packet);
    }
}
