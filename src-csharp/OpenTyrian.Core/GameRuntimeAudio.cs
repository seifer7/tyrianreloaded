/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameRuntimeAudio : IDisposable
{
    public MusicArchive Music { get; }
    public SoundBank Sounds { get; }
    private bool _disposed;

    private GameRuntimeAudio(MusicArchive music, SoundBank sounds)
    {
        Music = music;
        Sounds = sounds;
    }

    public static GameRuntimeAudio Load(DataFiles files, bool christmas, IAudioSampleConverter converter)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(converter);

        using var music = files.OpenRequired(files.Directory, "music.mus");
        var musicArchive = MusicArchive.Load(music);
        var sounds = new SoundBank();
        try
        {
            sounds.Load(files, christmas, converter);
            return new(musicArchive, sounds);
        }
        catch
        {
            sounds.Clear();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Sounds.Clear();
        GC.SuppressFinalize(this);
    }
}
