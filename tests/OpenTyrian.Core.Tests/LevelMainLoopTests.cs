using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelMainLoopTests
{
    public static void Run()
    {
        var state = new LevelMainLoopState
        {
            MainLevel = 3,
            Power = 489,
            NetworkGame = true,
            TwoPlayerMode = true,
            RecordDemo = true,
            EndLevel = true,
            ReallyEndLevel = true,
            PlayerEndLevel = true,
            ExtraGame = true,
            DoNotSaveBackup = true,
        };
        var host = new TraceHost(new Queue<bool>([true, false]), new Queue<bool>([true, false]));

        LevelMainLoop.Run(state, host);

        Check(state.LastPower == 48 && !state.EndLevel && !state.ReallyEndLevel &&
            !state.PlayerEndLevel && !state.ExtraGame && host.FrameCount == 3 &&
            host.Calls.SequenceEqual([
                "volume", "map", "relative:true", "fade-song", "players", "map-state",
                "flags", "status", "clear-keyboard", "clear-special", "seed:32402394",
                "starfield", "speed", "volume", "save:22", "record-demo", "combat",
                "frame:0", "frame:1", "prepare", "volume", "map", "relative:true",
                "fade-song", "players", "map-state", "flags", "status", "clear-keyboard",
                "clear-special", "seed:32402394", "starfield", "speed", "volume", "save:22",
                "record-demo", "combat", "frame:2", "prepare"]),
            "Level main loop did not preserve startup/reset/save/frame and next-level ordering.");

        var titleReturnState = new LevelMainLoopState { MainLevel = 1, PlayDemo = true };
        var titleReturnHost = new TraceHost(new Queue<bool>(), new Queue<bool>());
        titleReturnHost.SetMainLevelAfterMap = 0;
        LevelMainLoop.Run(titleReturnState, titleReturnHost);
        Check(titleReturnHost.Calls.SequenceEqual(["volume", "map"]),
            "Level main loop continued initialization after the map loader returned to the title.");

        var demoState = new LevelMainLoopState { MainLevel = 1, PlayDemo = true, RecordDemo = true };
        var demoHost = new TraceHost(new Queue<bool>([false]), new Queue<bool>([false]));
        LevelMainLoop.Run(demoState, demoHost);
        Check(demoHost.Calls.SequenceEqual([
                "volume", "map", "fade-song", "players", "map-state", "flags", "status",
                "clear-keyboard", "starfield", "speed", "volume", "combat", "frame:0", "prepare"]),
            "Level main loop did not preserve demo-specific input, save, network, and recording gates.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TraceHost(Queue<bool> frames, Queue<bool> levels)
        : ILevelMainLoopHost
    {
        public List<string> Calls { get; } = [];
        public int FrameCount { get; private set; }
        public byte? SetMainLevelAfterMap { get; set; }

        public bool PrepareNextLevel()
        {
            Calls.Add("prepare");
            return levels.TryDequeue(out var hasLevel) && hasLevel;
        }

        public void SetVolume() => Calls.Add("volume");
        public void LoadMap(LevelMainLoopState levelState)
        {
            Calls.Add("map");
            levelState.MainLevel = SetMainLevelAfterMap ?? levelState.MainLevel;
        }
        public void SetMouseRelative(bool enabled) => Calls.Add($"relative:{enabled.ToString().ToLowerInvariant()}");
        public void FadeSong() => Calls.Add("fade-song");
        public void InitializePlayersAndDisplay() => Calls.Add("players");
        public void InitializeMapAndShipState() => Calls.Add("map-state");
        public void InitializeFlags() => Calls.Add("flags");
        public void InitializeStatusDisplay() => Calls.Add("status");
        public void ClearKeyboard() => Calls.Add("clear-keyboard");
        public void ClearSpecialRequests() => Calls.Add("clear-special");
        public void SeedRandom(uint seed) => Calls.Add($"seed:{seed}");
        public void InitializeStarfield() => Calls.Add("starfield");
        public void SetNewGameSpeed() => Calls.Add("speed");
        public void SaveGame(int slot, string description) => Calls.Add($"save:{slot}");
        public void RecordLevelDemo() => Calls.Add("record-demo");
        public void InitializeCombatState() => Calls.Add("combat");
        public bool RunLevelFrame(uint[] oldWeaponBars, ref int lastEnemyOnScreen, byte[] buffer)
        {
            Calls.Add($"frame:{FrameCount++}");
            return frames.TryDequeue(out var moreFrames) && moreFrames;
        }
    }
}
