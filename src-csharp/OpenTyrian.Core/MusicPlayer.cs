/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IMusicPlayer : IMusicSynthesizer
{
    void Load(ReadOnlySpan<byte> song);
    void Rewind();
    void Fade(byte speed);
}

public sealed class MusicArchive
{
    private readonly byte[][] _songs;
    public int Count => _songs.Length;

    private MusicArchive(byte[][] songs) => _songs = songs;

    public static MusicArchive Load(Stream stream)
    {
        Span<ushort> count = stackalloc ushort[1];
        BinaryFile.ReadUInt16(stream, count);
        var positions = new uint[count[0] + 1];
        BinaryFile.ReadUInt32(stream, positions.AsSpan(0, count[0]));
        if (stream.Length > uint.MaxValue) throw new InvalidDataException("Music archive exceeds the native 32-bit directory size.");
        positions[^1] = (uint)stream.Length;
        var songs = new byte[count[0]][];
        for (var i = 0; i < songs.Length; i++)
        {
            if (positions[i] > positions[i + 1] || positions[i + 1] > stream.Length ||
                positions[i + 1] - positions[i] > int.MaxValue)
                throw new InvalidDataException("Music offsets are unordered, oversized, or outside the archive.");
            songs[i] = new byte[positions[i + 1] - positions[i]];
            stream.Position = positions[i];
            BinaryFile.ReadExactly(stream, songs[i]);
        }
        return new MusicArchive(songs);
    }

    public ReadOnlyMemory<byte> Song(uint index)
        => index < _songs.Length ? _songs[index] : throw new InvalidDataException($"warning: failed to load song {(ulong)index + 1}");
}

public sealed class MusicController
{
    private readonly IMusicPlayer _player;
    private readonly MusicArchive _archive;
    private uint _songPlaying;
    public AudioMixer Mixer { get; }
    public uint SongPlaying { get { uint result = 0; Mixer.ChangeMusic(() => result = _songPlaying); return result; } }

    public MusicController(MusicArchive archive, int sampleRate, IMusicPlayer player)
        : this(archive, new AudioMixer(sampleRate, player), player)
    {
    }

    public MusicController(MusicArchive archive, AudioMixer mixer, IMusicPlayer player)
    {
        ArgumentNullException.ThrowIfNull(archive);
        ArgumentNullException.ThrowIfNull(mixer);
        ArgumentNullException.ThrowIfNull(player);
        _archive = archive;
        _player = player;
        Mixer = mixer;
    }

    public void PlaySong(uint index)
    {
        Mixer.ChangeMusic(() =>
        {
            if (Mixer.AudioDisabled) return;
            if (index != _songPlaying)
            {
                Mixer.MusicStopped = true;
                _player.Load(_archive.Song(index).Span);
                _songPlaying = index;
            }
            Mixer.MusicStopped = false;
        });
    }

    public void Restart()
    {
        Mixer.ChangeMusic(() =>
        {
            if (Mixer.AudioDisabled) return;
            _player.Rewind();
            Mixer.MusicStopped = false;
        });
    }

    public void Stop()
    {
        Mixer.ChangeMusic(() => { if (!Mixer.AudioDisabled) Mixer.MusicStopped = true; });
    }

    public void Fade()
    {
        Mixer.ChangeMusic(() => { if (!Mixer.AudioDisabled) _player.Fade(1); });
    }
}
