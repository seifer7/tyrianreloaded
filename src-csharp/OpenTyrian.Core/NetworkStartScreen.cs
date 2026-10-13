/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public interface INetworkStartHost
{
    void LoadPicture();
    void Capture();
    void Restore();
    void Message(ReadOnlyMemory<byte> text);
    void Present();
    void FadeIn();
    void FadeBlack();
    void Connect();
    bool SelectEpisode();
    bool SelectDifficulty();
    int InitializeEpisode(int episode);
}

public interface INetworkStartChannel
{
    NetworkPacket? Packet { get; }
    bool Synchronized { get; }
    void SendDetails(ushort episode, ushort difficulty);
    void SendQuit();
    void Update();
    void Check();
    void Quit();
}

public sealed class NetworkStartChannel(NetworkSession network) : INetworkStartChannel
{
    public NetworkPacket? Packet => network.ControlPacket;
    public bool Synchronized => network.IsSynchronized;
    public void SendDetails(ushort episode, ushort difficulty)
    {
        network.Prepare(NetworkPacketType.Details);
        BinaryPrimitives.WriteUInt16BigEndian(network.Outgoing[4..], episode);
        BinaryPrimitives.WriteUInt16BigEndian(network.Outgoing[6..], difficulty);
        if (!network.Send(8)) throw new IOException("Network start could not queue episode/difficulty details.");
    }
    public void SendQuit()
    {
        network.Prepare(NetworkPacketType.Quit);
        if (!network.Send(4)) throw new IOException("Network start could not queue the quit packet.");
    }
    public void Update() => network.Update();
    public void Check() => network.Check();
    public void Quit() => throw new NetworkTerminationException(0, true);
}

public sealed class NetworkStartScreen(LegacyGameState game, ItemDatabase items, HelpTextData text, InputState input,
    INetworkStartHost host, INetworkStartChannel network)
{
    public void Run(int playerNumber, CancellationToken cancellation = default)
    {
        if (playerNumber is not 1 and not 2) throw new ArgumentOutOfRangeException(nameof(playerNumber));
        cancellation.ThrowIfCancellationRequested();
        host.LoadPicture(); host.Capture(); host.Message("Waiting for other player."u8.ToArray());
        host.Present(); host.FadeIn(); host.Connect();
        game.TwoPlayerMode = true;
        if (playerNumber == 1)
        {
            host.FadeBlack();
            if (host.SelectEpisode() && host.SelectDifficulty())
            {
                game.InitialDifficulty = game.Difficulty;
                game.Difficulty = unchecked((sbyte)(game.Difficulty + 1));
                network.SendDetails(game.Episode, unchecked((ushort)game.Difficulty));
            }
            else
            {
                network.SendQuit(); network.Quit();
                throw new InvalidOperationException("Network quit handler unexpectedly returned.");
            }
        }
        else
        {
            host.Restore(); host.Message(text.NetworkText[3]); host.Present();
            while (true)
            {
                cancellation.ThrowIfCancellationRequested(); input.ServiceEvents(false); host.Present();
                if (network.Packet?.Type == NetworkPacketType.Details) break;
                network.Update(); network.Check(); input.Delay(16);
            }
            var packet = network.Packet ?? throw new InvalidOperationException("Network start details disappeared.");
            if (packet.Data.Length < 8) throw new InvalidDataException("Network start details packet is truncated.");
            game.Episode = checked((byte)host.InitializeEpisode(BinaryPrimitives.ReadUInt16BigEndian(packet.Data.Span[4..])));
            game.Difficulty = unchecked((sbyte)BinaryPrimitives.ReadUInt16BigEndian(packet.Data.Span[6..]));
            game.InitialDifficulty = unchecked((sbyte)(game.Difficulty - 1));
            host.FadeBlack(); network.Update();
        }
        foreach (var player in game.Players) player.Cash = 0;
        game.Players[0].Items.Ship = checked((byte)items.KnownIndex(KnownItem.ShipSilverShip));
        while (!network.Synchronized)
        {
            cancellation.ThrowIfCancellationRequested(); input.ServiceEvents(false); host.Present();
            network.Check(); input.Delay(16);
        }
    }
}

public sealed class NetworkStartHost(IndexedSurface primary, IndexedSurface background, FontRenderer font,
    PaletteSystem palette, ReadOnlyMemory<PaletteColor> colors, DelayTimer timer, Action loadPicture,
    Action present, Action connect, Func<bool> selectEpisode, Func<bool> selectDifficulty,
    Func<int, int> initializeEpisode) : INetworkStartHost
{
    public void LoadPicture() => loadPicture();
    public void Capture() => Copy(primary, background);
    public void Restore() => Copy(background, primary);
    private static void Copy(IndexedSurface source, IndexedSurface destination)
    {
        if (source.Width != destination.Width || source.Height != destination.Height || source.Pitch != destination.Pitch)
            throw new InvalidDataException("Network start requires identical saved and primary surface geometry.");
        source.Pixels.Span.CopyTo(destination.Pixels.Span);
    }
    public void Message(ReadOnlyMemory<byte> text) =>
        font.DrawString(primary, font.Center(text.Span, Font.Small), 140, text.Span, Font.Small);
    public void Present() => present();
    public void FadeIn() => palette.FadePalette(colors.Span, 10, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    public void FadeBlack() => palette.FadeBlack(10, () => timer.SetDelay(1), present, timer.Wait);
    public void Connect() => connect();
    public bool SelectEpisode() => selectEpisode();
    public bool SelectDifficulty() => selectDifficulty();
    public int InitializeEpisode(int episode) => initializeEpisode(episode);
}
