using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelFrameTests
{
    public static void Run()
    {
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        players[0].Lives = players[1].Lives = 1;
        players[0].IsAlive = players[1].IsAlive = true;
        players[0].X = 41;
        players[0].Y = 52;
        players[1].X = 71;
        players[1].Y = 82;
        var state = new LevelFrameState
        {
            Players = players,
            ShotAvailable = [true, false, true],
            Smoothies = Enumerable.Range(0, 9).Select(index => index is 5 or 8).ToArray(),
            NetworkGame = false,
            TwoPlayerMode = true,
            MusicFade = true,
            TemporaryVolume = 12,
            EffectsVolume = 7,
            EndLevel = true,
            LevelEnd = 1,
            LevelSong = 4,
            FirstGameOver = true,
            CurrentLocation = 5,
            MaximumEvent = 2,
            EventTimes = [2, 5, 9],
            GameScreen = 3,
            SecondaryScreen = 4,
            ScreenSegment = 8,
            Background2Over = 2,
            Wild = true,
            TopEnemyOver = true,
            SkyEnemyOverAll = true,
            Background3Over = 1,
            Background3Move = 6,
            OldMapX3Offset = 13,
            MapX2Offset = 17,
            StopBackgroundNumber = 2,
            PortConfigDone = false,
            RandomExplosions = true,
            ReturnActive = true,
            EnemyOnScreen = 0,
            FilterActive = true,
            LevelFilter = -2,
            LevelBrightness = 3,
        };
        state.MapYPositions[0] = 10;
        state.MapYPositions[1] = 20;
        state.MapYPositions[2] = 30;
        state.BackgroundWraps[0] = 10;
        state.BackgroundWraps[1] = 20;
        state.BackgroundWraps[2] = 30;
        state.BackgroundWrapTargets[0] = 110;
        state.BackgroundWrapTargets[1] = 120;
        state.BackgroundWrapTargets[2] = 130;
        var host = new TraceHost([1, 279, 179]);
        var bars = new uint[2];
        var buffer = new byte[256];
        var lastEnemy = 42;

        var keepRunning = LevelFrame.Run(state, bars, ref lastEnemy, buffer, host);

        Check(keepRunning && state.MapYPositions.SequenceEqual([110, 120, 130]) &&
            state.StarShowVgaSpecialCode == 3 && state.Screen == state.ScreenSegment &&
            state.TemporaryVolume == 11 && !state.MusicFade && state.EventLocation == 3 &&
            state.Players[0].LastXShotMove == 41 && state.Players[1].LastYShotMove == 82 &&
            state.StopBackgroundNumber == 9 && state.PortConfigDone && !state.ReturnActive &&
            host.Calls.SequenceEqual([
                "volume:11:7", "song:9", "hud", "event", "event", "smoothies", "backgrounds",
                "shot:0", "shot:2", "player-functions", "enemy-shots",
                "background3", "enemy:75", "enemy:25", "explosions", "background2:blend",
                "armor-warning", "explosion:20:279:179",                 "sounds", "jump:65535", "debug", "input", "superpixels", "filter:-2:3",
                "boss-bar", "in-game", "stars", "end-state", "chat"]),
            $"Level frame did not preserve its native state transitions, branch gates, or callback order: " +
            $"{string.Join(",", host.Calls)}; running={keepRunning}, maps={string.Join(",", state.MapYPositions)}, " +
            $"special={state.StarShowVgaSpecialCode}, screen={state.Screen}, volume={state.TemporaryVolume}, " +
            $"fade={state.MusicFade}, event={state.EventLocation}, last={state.Players[0].LastXShotMove}/" +
            $"{state.Players[1].LastYShotMove}, stop={state.StopBackgroundNumber}, port={state.PortConfigDone}, " +
            $"return={state.ReturnActive}");

        var networkPlayers = new[] { new PlayerState(0), new PlayerState(1) };
        var networkState = new LevelFrameState
        {
            Players = networkPlayers,
            ShotAvailable = [],
            Smoothies = Enumerable.Repeat(true, 9).ToArray(),
            NetworkGame = true,
            ReallyEndLevel = true,
            GameScreen = 2,
        };
        var networkHost = new TraceHost([]);
        lastEnemy = 0;
        Check(!LevelFrame.Run(networkState, bars, ref lastEnemy, buffer, networkHost) &&
            !networkState.Smoothies[8] && !networkState.Smoothies[5] &&
            networkState.Smoothies[0] && networkHost.Calls.SequenceEqual(["hud"]),
            "Network end-level frame did not clear its unsupported smoothers and return after events.");

        var inputPlayers = new[] { new PlayerState(0), new PlayerState(1) };
        var inputState = new LevelFrameState
        {
            Players = inputPlayers,
            ShotAvailable = [],
            Smoothies = new bool[9],
            GameScreen = 1,
        };
        var inputHost = new TraceHost([]) { StopAtInput = true };
        lastEnemy = 0;
        Check(LevelFrame.Run(inputState, bars, ref lastEnemy, buffer, inputHost) &&
            inputHost.Calls.SequenceEqual([
                "hud", "smoothies", "backgrounds", "enemy-shots", "explosions", "armor-warning",
                "sounds", "debug", "input"]),
            "Level frame did not return immediately when input requested a skipped presentation.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TraceHost(IEnumerable<uint> randomValues) : ILevelFrameHost
    {
        private readonly Queue<uint> _random = new(randomValues);
        public List<string> Calls { get; } = [];
        public bool StopAtInput { get; init; }

        public void SetVolume(ushort music, ushort effects) => Calls.Add($"volume:{music}:{effects}");
        public void PlaySong(int song) => Calls.Add($"song:{song}");
        public void DrawHud(uint[] oldWeaponBars) => Calls.Add("hud");
        public void ProcessEvent(LevelFrameState state)
        {
            Calls.Add("event");
            state.EventLocation++;
        }
        public void CheckSmoothies(LevelFrameState state)
        {
            Calls.Add("smoothies");
            state.AnySmoothies = state.Smoothies.Any(enabled => enabled);
        }
        public void DrawBackgrounds(LevelFrameState state, ref int lastEnemyOnScreen)
            => Calls.Add("backgrounds");
        public void ProcessPlayerShot(LevelFrameState state, int shotIndex) => Calls.Add($"shot:{shotIndex}");
        public void CollidePlayer(LevelFrameState state, int playerNumber) => Calls.Add($"collide:{playerNumber}");
        public void RunPlayerFunctions(LevelFrameState state) => Calls.Add("player-functions");
        public void DrawEnemyShots(LevelFrameState state) => Calls.Add("enemy-shots");
        public void DrawBackground3(LevelFrameState state) => Calls.Add("background3");
        public void DrawEnemy(LevelFrameState state, int priority)
        {
            Calls.Add($"enemy:{priority}");
            if (priority == 25) state.EnemyOnScreen = 0;
        }
        public void UpdateExplosions(LevelFrameState state) => Calls.Add("explosions");
        public void DrawBackground2(LevelFrameState state, bool blend) => Calls.Add($"background2:{(blend ? "blend" : "plain")}");
        public void DrawArmorWarning(LevelFrameState state) => Calls.Add("armor-warning");
        public uint NextRandomUInt32() => _random.Dequeue();
        public void SetupLargeExplosion(bool special, int size, int x, int y) => Calls.Add($"explosion:{size}:{x}:{y}");
        public void PlayLevelSounds(LevelFrameState state) => Calls.Add("sounds");
        public void JumpEvent(LevelFrameState state, ushort target) => Calls.Add($"jump:{target}");
        public void DrawDebugAndTimer(LevelFrameState state, byte[] buffer) => Calls.Add("debug");
        public bool HandleInput(LevelFrameState state)
        {
            Calls.Add("input");
            return StopAtInput;
        }
        public void UpdateNetwork(LevelFrameState state) => Calls.Add("network");
        public void DrawSuperpixels(LevelFrameState state) => Calls.Add("superpixels");
        public void FilterScreen(LevelFrameState state, int filter, int brightness) => Calls.Add($"filter:{filter}:{brightness}");
        public void DrawBossBar(LevelFrameState state) => Calls.Add("boss-bar");
        public void DrawInGameDisplays(LevelFrameState state) => Calls.Add("in-game");
        public void ShowStars(LevelFrameState state) => Calls.Add("stars");
        public void UpdateEndState(LevelFrameState state) => Calls.Add("end-state");
        public void HandleChat(LevelFrameState state) => Calls.Add("chat");
    }
}
