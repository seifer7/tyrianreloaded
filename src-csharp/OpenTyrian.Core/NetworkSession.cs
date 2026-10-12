/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public enum NetworkPacketType : ushort
{
    Acknowledge = 0, KeepAlive = 1, Connect = 0x10, Details = 0x11,
    Quit = 0x20, Waiting = 0x21, Busy = 0x22, GameQuit = 0x30, GamePause = 0x31,
    GameMenu = 0x32, StateResend = 0x40, State = 0x41, StateXor = 0x42
}

public sealed class NetworkPacket
{
    internal readonly byte[] Buffer;
    public ReadOnlyMemory<byte> Data => Buffer;
    public int Channel { get; }
    public NetworkPacketType Type => (NetworkPacketType)BinaryPrimitives.ReadUInt16BigEndian(Buffer);
    public ushort Sequence => BinaryPrimitives.ReadUInt16BigEndian(Buffer.AsSpan(2));
    public NetworkPacket(ReadOnlySpan<byte> bytes, int channel = 0)
    {
        if (bytes.Length > NetworkSession.PacketSize) throw new InvalidDataException("Network packet exceeds 256 bytes.");
        Buffer = bytes.ToArray();
        Channel = channel;
    }
    internal NetworkPacket Copy() => new(Buffer, Channel);
}

public interface INetworkTransport
{
    void Send(ReadOnlySpan<byte> packet);
    NetworkPacket? Receive();
}

public sealed class NetworkTerminationException(int code, bool attemptSync) : IOException(Messages[code])
{
    public int Code { get; } = code;
    public bool AttemptSync { get; } = attemptSync;
    public static IReadOnlyList<string> Messages { get; } = Array.AsReadOnly(new[]
    {
        "Quitting...", "Other player quit the game.", "Network connection was lost.",
        "Network connection failed.", "Network version mismatch.", "Network delay mismatch.",
        "Network player number conflict."
    });
}

public sealed class NetworkSession
{
    public const int PacketSize = 256, QueueSize = 16, RetryMilliseconds = 640,
        ResendMilliseconds = 320, KeepAliveMilliseconds = 1600, TimeoutMilliseconds = 16000;
    private readonly INetworkTransport _transport;
    private readonly InputState _input;
    private readonly TextWriter _errors;
    private readonly byte[] _outgoing = new byte[PacketSize];
    private readonly NetworkPacket?[] _incoming = new NetworkPacket?[QueueSize], _pending = new NetworkPacket?[QueueSize],
        _statesIn = new NetworkPacket?[QueueSize], _xorIn = new NetworkPacket?[QueueSize], _statesOut = new NetworkPacket?[QueueSize];
    private ushort _lastOut, _queueIn, _queueOut, _lastAck, _lastStateIn, _lastStateOut;
    private uint _lastInTick, _lastOutTick, _lastStateInTick, _keepAliveTick, _resendTick;
    private bool _quit;
    public int DelayFrames { get; }
    public bool Connected { get; set; }
    public NetworkRequests Requests { get; } = new();
    public Span<byte> Outgoing => _outgoing;
    public NetworkPacket? ControlPacket => _incoming[0];
    public NetworkPacket? IncomingState => _statesIn[0];
    public IReadOnlyList<NetworkPacket?> OutgoingStates { get; }
    public ReadOnlyMemory<byte> OpponentName { get; private set; }
    public Span<byte> OutgoingState => (_statesOut[0] ?? throw new InvalidOperationException("No network state is prepared.")).Buffer;
    // This native predicate uses promoted signed subtraction, not modular subtraction.
    public bool IsSynchronized => (int)_queueOut - _lastAck == 1;
    public bool StateIsReset => _lastStateOut < DelayFrames;
    public bool IsAlive => Elapsed(_lastInTick) < TimeoutMilliseconds || Elapsed(_lastStateInTick) < TimeoutMilliseconds;

    public NetworkSession(INetworkTransport transport, InputState input, int delayFrames, TextWriter errors)
    {
        if (delayFrames is < 1 or > 7) throw new ArgumentOutOfRangeException(nameof(delayFrames), "Network delay must fit the native 16-packet queues.");
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(errors);
        _transport = transport; _input = input; DelayFrames = delayFrames; _errors = errors;
        OutgoingStates = Array.AsReadOnly(_statesOut);
    }

    private uint Elapsed(uint previous) => unchecked(_input.Ticks - previous);
    private static ushort Distance(int first, int second) => unchecked((ushort)(first - second));
    private static void Write(Span<byte> packet, int offset, ushort value) => BinaryPrimitives.WriteUInt16BigEndian(packet[offset..], value);
    private static void ShiftUp(NetworkPacket?[] packets)
    {
        Array.Copy(packets, 1, packets, 0, QueueSize - 1); packets[^1] = null;
    }
    private static void ShiftDown(NetworkPacket?[] packets)
    {
        Array.Copy(packets, 0, packets, 1, QueueSize - 1); packets[0] = null;
    }

    public void Prepare(NetworkPacketType type)
    {
        Write(_outgoing, 0, (ushort)type); Write(_outgoing, 2, _lastOut);
    }
    public void SendUnacknowledged(int length)
    {
        if (length is < 4 or > PacketSize) throw new ArgumentOutOfRangeException(nameof(length));
        _transport.Send(_outgoing.AsSpan(0, length));
    }
    public bool Send(int length)
    {
        SendUnacknowledged(length);
        var index = Distance(_lastOut, _queueOut);
        if (index >= QueueSize)
        {
            _errors.WriteLine("warning: outbound packet queue overflow");
            return false;
        }
        _pending[index] = new(_outgoing.AsSpan(0, length));
        _lastOut = unchecked((ushort)(_lastOut + 1));
        if (IsSynchronized) _lastOutTick = _input.Ticks;
        return true;
    }
    private void Acknowledge(ushort sequence)
    {
        Write(_outgoing, 0, (ushort)NetworkPacketType.Acknowledge); Write(_outgoing, 2, sequence);
        SendUnacknowledged(4);
    }

    public int Check()
    {
        if (Connected)
        {
            if (!IsAlive && !_quit) Terminate(2, false);
            if (Elapsed(_keepAliveTick) > KeepAliveMilliseconds)
            {
                Prepare(NetworkPacketType.KeepAlive); SendUnacknowledged(4); _keepAliveTick = _input.Ticks;
            }
        }
        if (_pending[0] is { } pending && Elapsed(_lastOutTick) > RetryMilliseconds)
        {
            _transport.Send(pending.Data.Span); _lastOutTick = _input.Ticks;
        }
        var packet = _transport.Receive();
        if (packet == null || packet.Channel != 0) return 0;
        if (packet.Data.Length < 4)
        {
            _errors.WriteLine("warning: short network packet ignored"); return 0;
        }
        Validate(packet);
        var sequence = packet.Sequence;
        switch (packet.Type)
        {
            case NetworkPacketType.Acknowledge:
                if (Distance(sequence, _lastAck) < QueueSize) _lastAck = sequence;
                var acknowledged = Distance(sequence, _queueOut);
                if (acknowledged < QueueSize) _pending[acknowledged] = null;
                while (_pending[0] == null && Distance(_lastAck, _queueOut) < QueueSize)
                {
                    ShiftUp(_pending); _queueOut = unchecked((ushort)(_queueOut + 1));
                }
                _lastInTick = _input.Ticks;
                break;
            case NetworkPacketType.Connect:
                _queueIn = sequence; Array.Clear(_incoming);
                goto case NetworkPacketType.Details;
            case NetworkPacketType.Details:
            case NetworkPacketType.Waiting:
            case NetworkPacketType.Busy:
            case NetworkPacketType.GameQuit:
            case NetworkPacketType.GamePause:
            case NetworkPacketType.GameMenu:
                var index = Distance(sequence, _queueIn);
                if (index < QueueSize) _incoming[index] = packet.Copy();
                Acknowledge(sequence); _lastInTick = _input.Ticks;
                break;
            case NetworkPacketType.KeepAlive:
                _lastInTick = _input.Ticks;
                break;
            case NetworkPacketType.Quit:
                if (!_quit) { Prepare(NetworkPacketType.Quit); Send(4); }
                Acknowledge(sequence);
                if (!_quit) Terminate(1, true);
                break;
            case NetworkPacketType.State:
                var stateIndex = Distance(sequence + 1, _lastStateIn);
                if (stateIndex < QueueSize) _statesIn[stateIndex] = packet.Copy();
                break;
            case NetworkPacketType.StateXor:
                var xorIndex = Distance(sequence + 1, _lastStateIn);
                if (xorIndex < QueueSize)
                {
                    if (_xorIn[xorIndex] == null) _xorIn[xorIndex] = packet.Copy();
                    else if (_xorIn[xorIndex]!.Type != NetworkPacketType.StateXor)
                    {
                        Xor(_xorIn[xorIndex]!, packet);
                        Write(_xorIn[xorIndex]!.Buffer, 0, (ushort)NetworkPacketType.StateXor);
                    }
                }
                break;
            case NetworkPacketType.StateResend:
                var resend = Distance(_lastStateOut, sequence);
                if (resend is > 0 and < QueueSize && _statesOut[resend] is { } state) _transport.Send(state.Data.Span);
                break;
            default:
                _errors.WriteLine($"warning: bad packet {(ushort)packet.Type} received"); return 0;
        }
        return 1;
    }

    private static void Validate(NetworkPacket packet)
    {
        var length = packet.Data.Length;
        if (packet.Type is NetworkPacketType.State or NetworkPacketType.StateXor && length != 28)
            throw new InvalidDataException("Network state packet must contain 28 bytes.");
        if (packet.Type == NetworkPacketType.Details && length < 8)
            throw new InvalidDataException("Network details packet is truncated.");
        if (packet.Type == NetworkPacketType.Connect && (length < 13 || !packet.Data.Span[12..].Contains((byte)0)))
            throw new InvalidDataException("Network connect packet has a truncated or unterminated player name.");
    }
    private static void Xor(NetworkPacket destination, NetworkPacket source)
    {
        if (destination.Data.Length != source.Data.Length) throw new InvalidDataException("Network XOR states have inconsistent lengths.");
        for (var i = 4; i < destination.Buffer.Length; i++) destination.Buffer[i] ^= source.Buffer[i];
    }
    public bool Update()
    {
        if (_incoming[0] == null) return false;
        ShiftUp(_incoming); _queueIn = unchecked((ushort)(_queueIn + 1)); return true;
    }
    public void PrepareState()
    {
        if (_statesOut[0] != null) _errors.WriteLine("warning: state packet overwritten (previous packet remains unsent)");
        else _statesOut[0] = new(new byte[28]);
        Write(OutgoingState, 0, (ushort)NetworkPacketType.State); Write(OutgoingState, 2, _lastStateOut);
        OutgoingState[4..].Clear();
    }
    public void SendState()
    {
        var state = _statesOut[0] ?? throw new InvalidOperationException("No network state is prepared.");
        _transport.Send(state.Data.Span);
        if (DelayFrames > 1 && (_lastStateOut + 1) % DelayFrames == 0 && _statesOut[DelayFrames - 1] != null)
        {
            var parity = state.Copy(); Write(parity.Buffer, 0, (ushort)NetworkPacketType.StateXor);
            for (var i = 1; i < DelayFrames; i++)
                Xor(parity, _statesOut[i] ?? throw new InvalidOperationException("Network state history has a gap."));
            _transport.Send(parity.Data.Span);
        }
        ShiftDown(_statesOut); _lastStateOut = unchecked((ushort)(_lastStateOut + 1));
    }
    public bool UpdateState(CancellationToken cancellationToken = default)
    {
        if (StateIsReset) return false;
        ShiftUp(_statesIn); ShiftUp(_xorIn); _lastStateIn = unchecked((ushort)(_lastStateIn + 1));
        var parityIndex = DelayFrames - ((int)_lastStateIn - 1) % DelayFrames - 1;
        while (_statesIn[0] == null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_xorIn[parityIndex] is { Type: NetworkPacketType.StateXor } parity &&
                Enumerable.Range(1, parityIndex).All(i => _statesIn[i] != null))
            {
                var recovered = parity.Copy();
                for (var i = 1; i <= parityIndex; i++) Xor(recovered, _statesIn[i]!);
                _statesIn[0] = recovered; break;
            }
            if (Elapsed(_lastStateInTick) > ResendMilliseconds && Elapsed(_resendTick) > ResendMilliseconds)
            {
                Write(_outgoing, 0, (ushort)NetworkPacketType.StateResend);
                Write(_outgoing, 2, unchecked((ushort)(_lastStateIn - 1)));
                SendUnacknowledged(4); _resendTick = _input.Ticks;
            }
            if (Check() == 0) _input.Delay(1);
        }
        if (DelayFrames > 1)
        {
            if (_xorIn[parityIndex] == null) _xorIn[parityIndex] = _statesIn[0]!.Copy();
            else Xor(_xorIn[parityIndex]!, _statesIn[0]!);
        }
        _lastStateInTick = _input.Ticks; return true;
    }
    public void ResetState()
    {
        _lastStateIn = _lastStateOut = 0;
        Array.Clear(_statesIn); Array.Clear(_xorIn); Array.Clear(_statesOut);
        _lastStateInTick = _input.Ticks;
    }
    public void Connect(Span<bool> episodes, ushort playerNumber, ReadOnlySpan<byte> playerName,
        Action pushJoysticks, CancellationToken cancellationToken = default)
    {
        if (episodes.Length != 5) throw new ArgumentException("Network connection requires the five native episode flags.", nameof(episodes));
        if (playerNumber is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        ArgumentNullException.ThrowIfNull(pushJoysticks);
        ushort available = 0;
        for (var i = episodes.Length - 1; i >= 0; i--) available = (ushort)((available << 1) | (episodes[i] ? 1 : 0));
        var terminator = playerName.IndexOf((byte)0);
        var name = playerName[..Math.Min(20, terminator < 0 ? playerName.Length : terminator)].ToArray();
        for (; ; )
        {
            SendConnect(available, playerNumber, name);
            for (; ; )
            {
                cancellationToken.ThrowIfCancellationRequested();
                pushJoysticks(); _input.ServiceEvents(false);
                if (_input.NewKey && _input.LastScancode == 41) Terminate(0, false);
                _lastInTick = _input.Ticks;
                if (ControlPacket?.Type == NetworkPacketType.Connect) break;
                Update(); Check(); _input.Delay(16);
            }
            ApplyConnect(episodes, playerNumber);
            var retry = false;
            while (!IsSynchronized)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _input.ServiceEvents(false);
                if (ControlPacket?.Type == NetworkPacketType.Connect) { ApplyConnect(episodes, playerNumber); continue; }
                Check();
                if (Elapsed(_lastOutTick) > RetryMilliseconds) { retry = true; break; }
                _input.Delay(16);
            }
            if (retry) continue;
            SendConnect(available, playerNumber, name); Connected = true; return;
        }
    }
    private void SendConnect(ushort episodes, ushort playerNumber, ReadOnlySpan<byte> name)
    {
        Prepare(NetworkPacketType.Connect);
        Write(_outgoing, 4, 2); Write(_outgoing, 6, (ushort)DelayFrames);
        Write(_outgoing, 8, episodes); Write(_outgoing, 10, playerNumber);
        name.CopyTo(_outgoing.AsSpan(12)); _outgoing[12 + name.Length] = 0;
        if (!Send(13 + name.Length)) throw new IOException("Network connect packet queue overflow.");
    }
    private void ApplyConnect(Span<bool> episodes, ushort playerNumber)
    {
        var packet = ControlPacket ?? throw new InvalidOperationException("No connection packet is queued.");
        var bytes = packet.Data.Span;
        if (BinaryPrimitives.ReadUInt16BigEndian(bytes[4..]) != 2)
        {
            _errors.WriteLine("error: network version did not match opponent's"); Terminate(4, true);
        }
        if (BinaryPrimitives.ReadUInt16BigEndian(bytes[6..]) != DelayFrames)
        {
            _errors.WriteLine("error: network delay did not match opponent's"); Terminate(5, true);
        }
        if (BinaryPrimitives.ReadUInt16BigEndian(bytes[10..]) == playerNumber)
        {
            _errors.WriteLine("error: player number conflicts with opponent's"); Terminate(6, true);
        }
        var available = BinaryPrimitives.ReadUInt16BigEndian(bytes[8..]);
        for (var i = 0; i < episodes.Length; i++) { episodes[i] &= (available & 1) != 0; available >>= 1; }
        OpponentName = bytes[12..(12 + bytes[12..].IndexOf((byte)0))].ToArray();
        Update();
    }
    public void Shutdown(uint code, bool attemptSync, INetworkShutdownPresentation presentation,
        Action releaseTransport, Action<int> haltGame, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        ArgumentNullException.ThrowIfNull(releaseTransport);
        ArgumentNullException.ThrowIfNull(haltGame);
        _quit = true;
        if (code >= NetworkTerminationException.Messages.Count) code = 0;
        try
        {
            cancellation.ThrowIfCancellationRequested();
            presentation.FadeBlack();
            presentation.ShowMessage(NetworkTerminationException.Messages[(int)code]);
            presentation.Present();
            presentation.FadeIn();
            if (attemptSync)
            {
                while (!IsSynchronized && IsAlive)
                {
                    cancellation.ThrowIfCancellationRequested();
                    _input.ServiceEvents(false);
                    Check();
                    _input.Delay(16);
                }
            }
            if (code != 0)
            {
                while (!_input.AnyButton())
                {
                    cancellation.ThrowIfCancellationRequested();
                    _input.Delay(16);
                }
            }
            presentation.FadeBlack();
        }
        finally { releaseTransport(); }
        haltGame(5);
    }

    public void Terminate(int code, bool attemptSync)
    {
        _quit = true;
        if (code < 0 || code >= NetworkTerminationException.Messages.Count) code = 0;
        throw new NetworkTerminationException(code, attemptSync);
    }
}
