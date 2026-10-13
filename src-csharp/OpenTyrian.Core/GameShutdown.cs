/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameTerminationException(int exitCode) : Exception($"Game terminated with exit code {exitCode}.")
{
    public int ExitCode { get; } = exitCode;
}

public interface IGameShutdownHost
{
    TextWriter Output { get; }
    void DeinitializeAudio();
    void DeinitializeVideo();
    void DeinitializeJoysticks();
    void ClearMainShapes();
    void ClearShopShapes();
    void ClearExplosionShapes();
    void ClearDestructShapes();
    void ClearSounds();
    void SaveConfiguration();
    void QuitSdl();
    void Exit(int code);
}

public sealed class GameShutdown(IGameShutdownHost host)
{
    public void Halt(byte code, bool trentWin = false)
    {
        try
        {
            ReleaseResources();
            if (code != 9) host.SaveConfiguration();
            if (trentWin)
                host.Output.Write("\n\n\n\nSleep well, Trent, you deserve the rest.\n" +
                    "You now have permission to borrow my ship on your next mission.\n\n" +
                    "Also, you might want to try out the YESXMAS parameter.\n" +
                    "  Type: File0001 YESXMAS\n\nYou'll need the 2.1 patch, though!\n\n");
        }
        finally { host.QuitSdl(); }
        host.Exit(code == 5 ? 0 : code);
        throw new InvalidOperationException("Game exit unexpectedly returned.");
    }

    private void ReleaseResources()
    {
        try { host.DeinitializeAudio(); }
        finally
        {
            try { host.DeinitializeVideo(); }
            finally
            {
                try { host.DeinitializeJoysticks(); }
                finally
                {
                    host.ClearMainShapes();
                    host.ClearShopShapes();
                    host.ClearExplosionShapes();
                    host.ClearDestructShapes();
                    host.ClearSounds();
                }
            }
        }
    }
}

public sealed class SdlGameShutdownHost(SdlAudioDevice? audio, SdlVideoDevice video,
    SdlJoysticks joysticks, SdlInputPlatform input, MainShapeTables shapes,
    SpriteSheet shop, SpriteSheet explosions, SpriteSheet destruct, SoundBank sounds,
    GameSettingsStore settings, string directory, TextWriter output, LdsPlayer? music = null) : IGameShutdownHost
{
    public TextWriter Output => output;
    public void DeinitializeAudio()
    {
        if (audio is null) return;
        try { audio.Dispose(); }
        finally { music?.Free(); }
    }
    public void DeinitializeVideo() => video.Dispose();
    public void DeinitializeJoysticks() => joysticks.Dispose();
    public void ClearMainShapes() => shapes.Clear();
    public void ClearShopShapes() => shop.Clear();
    public void ClearExplosionShapes() => explosions.Clear();
    public void ClearDestructShapes() => destruct.Clear();
    public void ClearSounds() => sounds.Clear();
    public void SaveConfiguration() => settings.Save(directory);
    public void QuitSdl()
    {
        try { input.Dispose(); }
        finally { SdlNative.Quit(); }
    }
    public void Exit(int code) => throw new GameTerminationException(code);
}
