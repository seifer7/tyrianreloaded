/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Text;

namespace OpenTyrian.Core;

public interface IJukeboxAudio
{
    bool Disabled { get; }
    bool Playing { get; }
    bool Looped { get; }
    uint Song { get; }
    void SetVolume(byte music, byte effects);
    void Play(uint song);
    void Stop();
    void Restart();
    void Sample(byte number);
}

public sealed class JukeboxAudio(MusicController music, Func<(bool Playing, bool Looped)> status,
    Action<byte> sample) : IJukeboxAudio
{
    private (bool Playing, bool Looped) State
    {
        get
        {
            (bool Playing, bool Looped) state = default;
            music.Mixer.ChangeMusic(() => state = status());
            return state;
        }
    }
    public bool Disabled => music.Mixer.AudioDisabled;
    public bool Playing => State.Playing;
    public bool Looped => State.Looped;
    public uint Song => music.SongPlaying;
    public void SetVolume(byte volume, byte effects) => music.Mixer.SetVolume(volume, effects);
    public void Play(uint song) => music.PlaySong(song);
    public void Stop() => music.Stop();
    public void Restart() => music.Restart();
    public void Sample(byte number) => sample(number);
}

public interface IJukeboxPresentation
{
    void Initialize();
    void Stars();
    void DrawLine(int y, ReadOnlyMemory<byte> text, sbyte brightness);
    void FadeStep(int steps);
    void FadeBlack();
    void Present();
}

public sealed class JukeboxPresentation(IndexedSurface screen, PaletteSystem palette, ReadOnlyMemory<PaletteColor> colors,
    FontRenderer font, JukeboxStarfield stars, InputState input, Action present) : IJukeboxPresentation
{
    private int[,] _fade = new int[256, 3];
    public void Initialize()
    {
        _fade = palette.InitializeFade(colors.Span, 0, 255);
        stars.Initialize();
    }
    public void Stars()
    {
        screen.Pixels.Span.Clear();
        stars.Update(screen, input);
    }
    public void DrawLine(int y, ReadOnlyMemory<byte> text, sbyte brightness) =>
        font.DrawHue(screen, screen.Width / 2, y, text.Span, Font.Small, FontAlignment.Centered, 1, brightness);
    public void FadeStep(int steps) => palette.StepFade(_fade, steps, 0, 255);
    public void FadeBlack() => _fade = palette.InitializeSolidFade(new(0, 0, 0), 0, 255);
    public void Present() => present();
}

public sealed class Jukebox(InputState input, DelayTimer timer, IJukeboxAudio audio,
    IJukeboxPresentation presentation, Action pushJoysticks)
{
    public void Run(byte musicVolume, byte effectsVolume) => Run(musicVolume, effectsVolume,
        JukeboxTitles.Music, JukeboxTitles.Sounds);

    public void Run(byte musicVolume, byte effectsVolume, IReadOnlyList<string> musicTitles, IReadOnlyList<string> soundTitles)
    {
        if (musicTitles.Count != 41 || soundTitles.Count != 38)
            throw new ArgumentException("Jukebox requires the native 41 music and 38 sound titles.");
        ArgumentNullException.ThrowIfNull(pushJoysticks);
        var triggerQuit = false;
        var quitting = false;
        var hideText = false;
        var fadeLooped = true;
        var fading = false;
        var stopped = false;
        var effects = false;
        var effect = 0;
        var fadeSteps = 15;
        presentation.Initialize();
        var volume = (int)musicVolume;
        try
        {
            for (; ; )
            {
                if (!stopped && !audio.Disabled)
                {
                    if (audio.Looped && fadeLooped) fading = true;
                    if (fading)
                    {
                        if (volume > 5) volume -= 2;
                        else { volume = musicVolume; fading = false; }
                        audio.SetVolume((byte)volume, effectsVolume);
                    }

                    if (!audio.Playing || (audio.Looped && fadeLooped && !fading))
                        audio.Play(MersenneTwister.NextUInt32() % 41);
                }
                timer.SetDelay(1);
                presentation.Stars();
                pushJoysticks();
                input.ServiceEvents(true);
                if (!hideText)
                {
                    var song = audio.Song;
                    if (!effects && song >= musicTitles.Count) throw new InvalidDataException("Jukebox song index exceeds its title table.");
                    var title = effects ? $"{effect + 1} {soundTitles[effect]}" : $"{song + 1} {musicTitles[(int)song]}";
                    var text = Encoding.Latin1.GetBytes(title);
                    // snprintf's native 60-byte buffer includes its terminator.
                    if (text.Length > 59) text = text[..59];
                    presentation.DrawLine(170, "Press ESC to quit the jukebox."u8.ToArray(), 0);
                    presentation.DrawLine(180, "Arrow keys change the song being played."u8.ToArray(), 0);
                    presentation.DrawLine(190, text, 4);
                }
                if (fadeSteps > 0) presentation.FadeStep(fadeSteps--);
                presentation.Present();
                timer.Wait();
                if (input.MousePosition().Button > 0) triggerQuit = true;
                if (input.NewKey)
                {
                    switch (input.LastScancode)
                    {
                        case 41:
                        case 20: triggerQuit = true; break;
                        case 44: hideText = !hideText; break;
                        case 9: fading = !fading; break;
                        case 17: fadeLooped = !fadeLooped; break;
                        case 56: effects = !effects; break;
                        case 54: if (effects && --effect < 0) effect = soundTitles.Count - 1; break;
                        case 55: if (effects && ++effect >= soundTitles.Count) effect = 0; break;
                        case 51: if (effects) audio.Sample((byte)(effect + 1)); break;
                        case 80:
                        case 82: audio.Play((audio.Song > 0 ? audio.Song : 41) - 1); stopped = false; break;
                        case 40:
                        case 88:
                        case 79:
                        case 81: audio.Play((audio.Song + 1) % 41); stopped = false; break;
                        case 22: audio.Stop(); stopped = true; break;
                        case 21: audio.Restart(); stopped = false; break;
                    }
                }
                if (triggerQuit && !quitting)
                {
                    fadeSteps = 15;
                    presentation.FadeBlack();
                    quitting = true;
                }
                if (quitting && fadeSteps == 0) break;
            }
        }
        finally { audio.SetVolume(musicVolume, effectsVolume); }
    }
}

public static class JukeboxTitles
{
    public static IReadOnlyList<string> Music { get; } = Array.AsReadOnly(new[]
    {
        "Asteroid Dance Part 2", "Asteroid Dance Part 1", "Buy/Sell Music", "CAMANIS", "CAMANISE",
        "Deli Shop Quartet", "Deli Shop Quartet No. 2", "Ending Number 1", "Ending Number 2", "End of Level",
        "Game Over Solo", "Gryphons of the West", "Somebody pick up the Gryphone", "Gyges, Will You Please Help Me?",
        "I speak Gygese", "Halloween Ramble", "Tunneling Trolls", "Tyrian, The Level", "The MusicMan", "The Navigator",
        "Come Back to Me, Savara", "Come Back again to Savara", "Space Journey 1", "Space Journey 2", "The final edge",
        "START5", "Parlance", "Torm - The Gathering", "TRANSON", "Tyrian: The Song", "ZANAC3", "ZANACS",
        "Return me to Savara", "High Score Table", "One Mustn't Fall", "Sarah's Song", "A Field for Mag",
        "Rock Garden", "Quest for Peace", "Composition in Q", "BEER"
    });
    public static IReadOnlyList<string> Sounds { get; } = Array.AsReadOnly(new[]
    {
        "SCALEDN2", "F2", "TEMP10", "EXPLSM", "PASS3", "TEMP2", "BYPASS1", "EXP1RT", "EXPLLOW", "TEMP13",
        "EXPRETAP", "MT2BOOM", "TEMP3", "LAZB", "LAZGUN2", "SPRING", "WARNING", "ITEM", "HIT2", "MACHNGUN",
        "HYPERD2", "EXPLHUG", "CLINK1", "CLICK", "SCALEDN1", "TEMP11", "TEMP16", "SMALL1", "POWERUP",
        "VOICE1", "VOICE2", "VOICE3", "VOICE4", "VOICE5", "VOICE6", "VOICE7", "VOICE8", "VOICE9"
    });
}
