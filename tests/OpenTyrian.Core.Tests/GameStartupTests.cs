using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameStartupTests
{
    public static void Run()
    {
        var launchState = new GameStartupState { Christmas = true };
        var launchResult = GameStartupArguments.Parse(launchState,
            ["OpenTyrian", "--data", "assets", "--net", "peer.example:2468",
                "--net-player-name", "Tyrian", "--net-player-number", "2", "--net-port", "4567",
                "--net-delay", "5", "--record", "--no-xmas"],
            TextWriter.Null, TextWriter.Null);
        Check(launchState.Options.DataDirectory == "assets" && launchState.Options.OpponentHost == "peer.example" &&
            launchState.Options.OpponentPort == 2468 && launchState.Options.PlayerName == "Tyrian" &&
            launchState.Options.PlayerNumber == 2 && launchState.Options.PlayerPort == 4567 &&
            launchState.Options.NetworkDelay == 6 && launchState.NetworkGame && launchState.RecordDemo &&
            !launchState.Christmas && !launchState.AudioDisabled && launchResult.ExitCode is null,
            "Parsed launch settings were not retained in the startup state.");
        var priorOptions = launchState.Options;
        var failedParse = GameStartupArguments.Parse(launchState, ["OpenTyrian", "--unknown"],
            TextWriter.Null, TextWriter.Null);
        Check(failedParse.ExitCode == 1 && ReferenceEquals(launchState.Options, priorOptions) &&
            launchState.NetworkGame && !launchState.Christmas,
            "Failed launch parsing changed the active startup state.");

        var state = new GameStartupState
        {
            AudioDisabled = true,
            RecordDemo = true,
            LoadDestruct = true,
        };
        var host = new TraceHost(new Queue<bool>([true, false])) { DestructOnFirstTitle = true };
        using var output = new StringWriter();
        using var error = new StringWriter();

        Check(new GameStartup(host, networkSupported: true, releaseBuild: true).Run(state, output, error) == 0 &&
            !state.Christmas && !state.LoadDestruct && !state.PlayDemo && !state.StoppedDemo &&
            !state.GameLoaded && !state.JumpSection && !state.YouAreCheating && state.SmoothScroll &&
            host.Calls.SequenceEqual([
                "seed", "sdl", "configuration", "christmas-time", "arguments", "episodes", "items",
                "video", "keyboard", "joysticks", "palettes", "shapes:normal", "defaults",
                "extra-shapes", "help", "logos",
                "players", "high-scores", "title", "destruct", "players", "high-scores", "title", "halt:0"]) &&
            output.ToString().Contains("audio disabled", StringComparison.Ordinal) &&
            output.ToString().Contains("demo recording enabled", StringComparison.Ordinal) &&
            output.ToString().Contains(
                $"\nWelcome to... >> {GameIdentity.ProgramName} {GameIdentity.Version} <<\n",
                StringComparison.Ordinal) &&
            error.ToString().Contains("Christmas is missing", StringComparison.Ordinal),
            "Game startup did not preserve initialization, fallback, mode-loop, and shutdown order.");

        var failedSdl = new TraceHost(new Queue<bool>()) { SdlResult = -1 };
        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        Check(new GameStartup(failedSdl, networkSupported: true, releaseBuild: true)
            .Run(new GameStartupState(), output, error) == -1 &&
            failedSdl.Calls.SequenceEqual(["seed", "sdl"]) &&
            error.ToString().Contains("Failed to initialize SDL: synthetic SDL error", StringComparison.Ordinal),
            "Game startup continued after SDL initialization failed.");

        var networkUnsupported = new TraceHost(new Queue<bool>()) { NetworkRequested = true };
        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        Check(new GameStartup(networkUnsupported, networkSupported: false, releaseBuild: true)
            .Run(new GameStartupState(), output, error) == 5 &&
            networkUnsupported.Calls.TakeLast(2).SequenceEqual(["help", "halt:5"]) &&
            error.ToString().Contains("compiled without networking", StringComparison.Ordinal),
            "Game startup did not halt explicitly when network support was unavailable.");

        var networkFailure = new TraceHost(new Queue<bool>()) { NetworkRequested = true, NetworkResult = 1 };
        output.GetStringBuilder().Clear();
        error.GetStringBuilder().Clear();
        Check(new GameStartup(networkFailure, networkSupported: true, releaseBuild: true)
            .Run(new GameStartupState(), output, error) == 3 &&
            networkFailure.Calls[^1] == "halt-network:3:False" &&
            !networkFailure.Calls.Contains("logos"),
            "Network initialization failure did not halt before intro/gameplay.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TraceHost(Queue<bool> titleResults) : IGameStartupHost
    {
        private GameStartupState? _state;
        private int _titleCount;
        public List<string> Calls { get; } = [];
        public int SdlResult { get; init; }
        public int NetworkResult { get; init; }
        public bool NetworkRequested { get; init; }
        public bool DestructOnFirstTitle { get; init; }
        public string ProgramName => GameIdentity.ProgramName;
        public string Version => GameIdentity.Version;
        public string SdlError => "synthetic SDL error";

        public void SeedRandomFromClock() => Calls.Add("seed");
        public int InitializeSdl() { Calls.Add("sdl"); return SdlResult; }
        public void LoadConfiguration() => Calls.Add("configuration");
        public bool IsChristmasTime() { Calls.Add("christmas-time"); return true; }
        public void ParseArguments(GameStartupState state)
        {
            Calls.Add("arguments");
            _state = state;
            state.NetworkGame = NetworkRequested;
            state.AudioDisabled = true;
            state.RecordDemo = true;
        }
        public void ScanEpisodes() => Calls.Add("episodes");
        public void LoadItems() => Calls.Add("items");
        public void InitializeVideo() => Calls.Add("video");
        public void InitializeKeyboard() => Calls.Add("keyboard");
        public void InitializeJoysticks() => Calls.Add("joysticks");
        public bool ChristmasAssetsPresent() => false;
        public void LoadPalettes() => Calls.Add("palettes");
        public void LoadMainShapeTables(bool christmas) => Calls.Add($"shapes:{(christmas ? "christmas" : "normal")}");
        public void FreeMainShapeTables() => Calls.Add("free-shapes");
        public bool PromptChristmas() => false;
        public void SetDefaultOptions(GameStartupState state) => Calls.Add("defaults");
        public void InitializeAudio() => Calls.Add("audio");
        public void LoadMusic() => Calls.Add("music");
        public void LoadSoundEffects(bool christmas) => Calls.Add($"sounds:{christmas}");
        public void LoadExtraShapes() => Calls.Add("extra-shapes");
        public void LoadHelpText() => Calls.Add("help");
        public int InitializeNetwork() { Calls.Add("network"); return NetworkResult; }
        public int HaltNetwork(int code, bool synchronize)
        {
            Calls.Add($"halt-network:{code}:{synchronize}");
            return code;
        }
        public int Halt(int code) { Calls.Add($"halt:{code}"); return code; }
        public void ShowIntroLogos() => Calls.Add("logos");
        public void InitializePlayerData() => Calls.Add("players");
        public void SortHighScores() => Calls.Add("high-scores");
        public void StartNetworkScreen() => Calls.Add("network-screen");
        public bool ShowTitleScreen()
        {
            Calls.Add("title");
            if (_titleCount++ == 0 && DestructOnFirstTitle)
                _state!.LoadDestruct = true;
            return titleResults.TryDequeue(out var keepGoing) && keepGoing;
        }
        public void RunDestructGame() => Calls.Add("destruct");
        public void RunMainGame() => Calls.Add("main-game");
    }
}
