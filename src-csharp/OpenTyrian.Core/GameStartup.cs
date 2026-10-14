/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameStartupState
{
    public GameOptions Options { get; private set; } = new();
    public bool Christmas { get; set; }
    public bool AudioDisabled { get; set; }
    public bool RecordDemo { get; set; }
    public bool NetworkGame { get; set; }
    public bool LoadDestruct { get; set; }
    public bool TrentWin { get; set; }
    public bool PlayDemo { get; set; }
    public bool StoppedDemo { get; set; }
    public bool GameLoaded { get; set; }
    public bool JumpSection { get; set; }
    public bool YouAreCheating { get; set; }
    public bool SmoothScroll { get; set; }

    public void ApplyOptions(GameOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        Options = options;
        Christmas = options.Christmas;
        AudioDisabled = options.AudioDisabled;
        RecordDemo = options.RecordDemo;
        NetworkGame = options.NetworkGame;
    }
}

public static class GameStartupArguments
{
    public static GameOptionResult Parse(GameStartupState state, string[] arguments, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(state);
        var result = GameOptions.Parse(arguments, output, error, state.Christmas);
        if (result.ExitCode is null)
            state.ApplyOptions(result.Options);
        return result;
    }
}

public interface IGameStartupHost
{
    string ProgramName { get; }
    string Version { get; }
    string SdlError { get; }
    int InitializeSdl();
    void SeedRandomFromClock();
    void LoadConfiguration();
    bool IsChristmasTime();
    void ParseArguments(GameStartupState state);
    void ScanEpisodes();
    void LoadItems();
    void InitializeVideo();
    void InitializeKeyboard();
    void InitializeJoysticks();
    bool ChristmasAssetsPresent();
    void LoadPalettes();
    void LoadMainShapeTables(bool christmas);
    void FreeMainShapeTables();
    bool PromptChristmas();
    void SetDefaultOptions(GameStartupState state);
    void InitializeAudio();
    void LoadMusic();
    void LoadSoundEffects(bool christmas);
    void LoadExtraShapes();
    void LoadHelpText();
    int InitializeNetwork();
    int HaltNetwork(int code, bool synchronize);
    int Halt(int code);
    void ShowIntroLogos();
    void InitializePlayerData();
    void SortHighScores();
    void StartNetworkScreen();
    bool ShowTitleScreen();
    void RunDestructGame();
    void RunMainGame();
}

public sealed class GameStartup(IGameStartupHost host, bool networkSupported, bool releaseBuild)
{
    public int Run(GameStartupState state, TextWriter output, TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        host.SeedRandomFromClock();
        output.WriteLine($"\nWelcome to... >> {host.ProgramName} {host.Version} <<\n");
        output.WriteLine("Copyright (C) 2022 The OpenTyrian Development Team\n");
        output.WriteLine("This program comes with ABSOLUTELY NO WARRANTY.");
        output.WriteLine("This is free software, and you are welcome to redistribute it");
        output.WriteLine("under certain conditions.  See the file COPYING for details.\n");

        if (host.InitializeSdl() != 0)
        {
            error.WriteLine($"Failed to initialize SDL: {host.SdlError}");
            return -1;
        }

        host.LoadConfiguration();
        state.Christmas = host.IsChristmasTime();
        host.ParseArguments(state);
        host.ScanEpisodes();
        host.LoadItems();
        host.InitializeVideo();
        host.InitializeKeyboard();
        host.InitializeJoysticks();
        output.WriteLine("assuming mouse detected");

        if (state.Christmas && !host.ChristmasAssetsPresent())
        {
            state.Christmas = false;
            error.WriteLine("warning: Christmas is missing.");
        }

        host.LoadPalettes();
        host.LoadMainShapeTables(state.Christmas);
        if (state.Christmas && !host.PromptChristmas())
        {
            state.Christmas = false;
            host.FreeMainShapeTables();
            host.LoadMainShapeTables(false);
        }

        state.YouAreCheating = false;
        state.SmoothScroll = true;
        state.LoadDestruct = false;
        host.SetDefaultOptions(state);
        if (!state.AudioDisabled)
        {
            output.WriteLine("initializing SDL audio...");
            host.InitializeAudio();
            host.LoadMusic();
            host.LoadSoundEffects(state.Christmas);
        }
        else
        {
            output.WriteLine("audio disabled");
        }

        if (state.RecordDemo)
            output.WriteLine("demo recording enabled (input limited to keyboard)");

        host.LoadExtraShapes();
        host.LoadHelpText();

        if (state.NetworkGame)
        {
            if (!networkSupported)
            {
                error.Write("OpenTyrian was compiled without networking support.");
                return host.Halt(5);
            }
            if (host.InitializeNetwork() != 0)
                return host.HaltNetwork(3, false);
        }

        if (releaseBuild && !state.NetworkGame)
            host.ShowIntroLogos();

        while (true)
        {
            host.InitializePlayerData();
            host.SortHighScores();
            state.PlayDemo = false;
            state.StoppedDemo = false;
            state.GameLoaded = false;
            state.JumpSection = false;

            if (state.NetworkGame)
                host.StartNetworkScreen();
            else if (!host.ShowTitleScreen())
                break;

            if (state.LoadDestruct)
            {
                host.RunDestructGame();
                state.LoadDestruct = false;
            }
            else
            {
                host.RunMainGame();
                if (state.TrentWin)
                    break;
            }
        }

        return host.Halt(0);
    }
}
