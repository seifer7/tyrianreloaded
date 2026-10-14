/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameRuntimeResources : IDisposable
{
    public GameRuntimeAssets Assets { get; }
    public GameRuntimeAudio? Audio { get; }
    public GameRuntimeAudioSession? AudioSession { get; }
    private bool _disposed;

    private GameRuntimeResources(GameRuntimeAssets assets, GameRuntimeAudio? audio,
        GameRuntimeAudioSession? audioSession = null)
    {
        Assets = assets;
        Audio = audio;
        AudioSession = audioSession;
    }

    public static GameRuntimeResources Load(string workingDirectory, string? dataDirectory,
        string userDirectory, bool christmas, TextWriter diagnostics, IAudioSampleConverter converter)
        => Load(workingDirectory, dataDirectory, userDirectory, christmas, diagnostics, converter, null);

    public static GameRuntimeResources Load(string workingDirectory, GameStartupState startup,
        string userDirectory, TextWriter diagnostics, IAudioSampleConverter? converter)
    {
        ArgumentNullException.ThrowIfNull(startup);
        return Load(workingDirectory, startup.Options.DataDirectory, userDirectory, startup.Christmas,
            diagnostics, converter, startup, !startup.AudioDisabled);
    }

    public static GameRuntimeResources LoadForStartup(string workingDirectory, GameStartupState startup,
        string userDirectory, TextWriter diagnostics, Func<uint>? nextRandom = null)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentNullException.ThrowIfNull(diagnostics);
        var assets = GameRuntimeAssets.Load(workingDirectory, startup.Options.DataDirectory,
            userDirectory, startup.Christmas, diagnostics);
        GameRuntimeAudioSession? audioSession = null;
        try
        {
            startup.Christmas = assets.ChristmasEnabled;
            var audioDisabled = startup.AudioDisabled;
            if (!audioDisabled)
                GameRuntimeAudioSession.TryInitialize(assets.Files, assets.ChristmasEnabled,
                    ref audioDisabled, diagnostics, out audioSession, nextRandom);
            startup.AudioDisabled = audioDisabled;
            return new(assets, null, audioSession);
        }
        catch
        {
            try { audioSession?.Dispose(); }
            finally { assets.Dispose(); }
            throw;
        }
    }

    private static GameRuntimeResources Load(string workingDirectory, string? dataDirectory,
        string userDirectory, bool christmas, TextWriter diagnostics, IAudioSampleConverter? converter,
        GameStartupState? startup, bool audioEnabled = true)
    {
        var assets = GameRuntimeAssets.Load(workingDirectory, dataDirectory, userDirectory, christmas, diagnostics);
        try
        {
            if (startup is not null) startup.Christmas = assets.ChristmasEnabled;
            GameRuntimeAudio? audio = null;
            if (audioEnabled)
                audio = GameRuntimeAudio.Load(assets.Files, assets.ChristmasEnabled,
                    converter ?? throw new ArgumentNullException(nameof(converter)));
            return new(assets, audio);
        }
        catch
        {
            assets.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { AudioSession?.Dispose(); }
        finally
        {
            try { Audio?.Dispose(); }
            finally { Assets.Dispose(); }
        }
        GC.SuppressFinalize(this);
    }
}
