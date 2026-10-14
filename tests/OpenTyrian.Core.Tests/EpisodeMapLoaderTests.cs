using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class EpisodeMapLoaderTests
{
    public static void Run()
    {
        var state = new EpisodeMapLoadState
        {
            MainLevel = 1,
            CubeMaximum = 2,
            NetworkGame = true,
        };
        var host = new TraceHost(() => MakeEpisode(
            "* section one",
            "plain text",
            Command('A'),
            GalaxyCommand(),
            CubeListCommand(),
            Command('!', (4, "3")),
            Command('+', (4, "4")),
            Command('s'),
            Command('b'),
            Command('@'),
            Command('i', (3, "7")),
            Command('I'),
            "shop 1", "shop 2", "shop 3", "shop 4", "shop 5",
            "shop 6", "shop 7", "shop 8", "shop 9",
            Command('h'),
            "skipped hard-difficulty line",
            Command('S'),
            Command('M', (3, "8")),
            LevelCommand()), state);

        Check(new EpisodeMapLoader(state, host, 3, 4, 2).Run() &&
            state.LastCubeMaximum == 2 && state.CubeMaximum == 4 &&
            state.MapOrigin == 3 && state.MapPlanetCount == 2 &&
            state.MapPlanets.Take(2).SequenceEqual(new byte[] { 4, 5 }) &&
            state.MapSections.Take(2).SequenceEqual(new byte[] { 2, 3 }) &&
            state.CubeList.Take(2).SequenceEqual(new ushort[] { 7, 9 }) &&
            state.SaveLevel == 1 && state.SongBuy == 6 && state.UseLastBank &&
            state.NextLevel == 2 && state.LevelName.Take(10).SequenceEqual(
                Encoding.ASCII.GetBytes("LevelName\0")) && state.LevelSong == 6 &&
            state.LevelFileNumber == 3 && state.BonusLevel && state.NormalBonusLevel &&
            !state.GameJustLoaded && host.Calls.SequenceEqual([
                "animation:tyrend.anm:0:7", "save:11", "items", "network-text", "song:7",
                "fade:50", "load-map"]),
            "Episode map loader did not preserve command parsing, fixed-width fields, or load order.");

        var jumpState = new EpisodeMapLoadState { MainLevel = 1 };
        var jumpHost = new TraceHost(() => MakeEpisode(
            "* first section", Command('J', (3, "2")), "* second section", LevelCommand()), jumpState)
        {
            JumpOnLevelCommand = true,
        };
        Check(new EpisodeMapLoader(jumpState, jumpHost, 3, 2, 2).Run() &&
            jumpState.MainLevel == 2 && jumpHost.OpenCount == 2 &&
            jumpHost.Calls.SequenceEqual(["jump:J", "fade:50", "load-map"]),
            $"Episode section jump did not reopen the file at its destination section: level={jumpState.MainLevel} " +
            $"opens={jumpHost.OpenCount} calls={string.Join(",", jumpHost.Calls)}");

        var retryState = new EpisodeMapLoadState { MainLevel = 1 };
        var retryHost = new TraceHost(() => MakeEpisode(
            "* first section", Command('I'), "row 1", "row 2", "row 3", "row 4", "row 5",
            "row 6", "row 7", "row 8", "row 9", LevelCommand()), retryState)
        { MarkGameLoadedAfterItems = true };
        Check(new EpisodeMapLoader(retryState, retryHost, 3, 2, 2).Run() &&
            retryHost.OpenCount == 2 &&
            retryHost.Calls.SequenceEqual(["items", "items", "fade:50", "load-map"]),
            $"Episode game-loaded restart did not reinitialize and reopen the episode: " +
            $"opens={retryHost.OpenCount} calls={string.Join(",", retryHost.Calls)}");

        var titleState = new EpisodeMapLoadState();
        var titleHost = new TraceHost(() => MakeEpisode(
            Command('I'), "row 1", "row 2", "row 3", "row 4", "row 5",
            "row 6", "row 7", "row 8", "row 9"), titleState)
        { MarkGameLoadedAfterItems = true };
        Check(!new EpisodeMapLoader(titleState, titleHost, 3, 2, 2).Run() &&
            titleHost.Calls.SequenceEqual(["items"]),
            "Episode loader failed to return to the title after an item-screen quit.");

        var presentationState = new EpisodeMapLoadState { MainLevel = 1 };
        var presentationHost = new TraceHost(() => MakeEpisode(
            "* section one", Command('Q')), presentationState)
        { ReturnFromPresentation = true };
        Check(!new EpisodeMapLoader(presentationState, presentationHost, 3, 2, 2).Run() &&
            presentationHost.Calls.SequenceEqual(["presentation:Q"]),
            "Episode presentation return was not propagated from the map loader.");

        var demoState = new EpisodeMapLoadState { PlayDemo = true };
        var demoHost = new TraceHost(() => throw new InvalidOperationException("Demo must not open episode data."), demoState)
        { DemoLoads = true };
        Check(new EpisodeMapLoader(demoState, demoHost, 3, 2, 2).Run() &&
            demoHost.Calls.SequenceEqual(["load-demo", "load-map"]),
            "Demo map loading used episode commands or skipped map selection.");
    }

    private static byte[] Command(char command, params (int Offset, string Value)[] fields)
    {
        var line = Enumerable.Repeat((byte)' ', 40).ToArray();
        line[0] = (byte)']';
        line[1] = (byte)command;
        foreach (var (offset, value) in fields)
            Encoding.ASCII.GetBytes(value).CopyTo(line, offset);
        return line;
    }

    private static byte[] GalaxyCommand()
    {
        var line = Command('G', (4, "3"), (7, "2"), (9, "4"), (12, "2"), (17, "5"), (20, "3"));
        return line;
    }

    private static byte[] CubeListCommand()
        => Command('?', (4, "2"), (7, "7"), (11, "9"));

    private static byte[] LevelCommand()
    {
        var line = Command('L', (9, "2"), (13, "LevelNameX"), (22, "6"), (25, "3"));
        line[27] = (byte)'$';
        line[28] = (byte)'$';
        return line;
    }

    private static MemoryStream MakeEpisode(params object[] lines)
    {
        var stream = new MemoryStream();
        Span<byte> encrypted = stackalloc byte[255];
        ReadOnlySpan<byte> key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        foreach (var line in lines)
        {
            var bytes = line switch
            {
                string text => Encoding.ASCII.GetBytes(text),
                byte[] record => record,
                _ => throw new InvalidOperationException("Unsupported episode fixture line."),
            };
            stream.WriteByte(checked((byte)bytes.Length));
            byte previous = 0;
            for (var index = 0; index < bytes.Length; index++)
            {
                encrypted[index] = (byte)(bytes[index] ^ key[index % key.Length] ^ previous);
                previous = encrypted[index];
            }
            stream.Write(encrypted[..bytes.Length]);
        }
        stream.Position = 0;
        return stream;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TraceHost(Func<Stream> openEpisode, EpisodeMapLoadState state) : IEpisodeMapLoaderHost
    {
        private int _items;
        public List<string> Calls { get; } = [];
        public int OpenCount { get; private set; }
        public bool JumpOnLevelCommand { get; init; }
        public bool MarkGameLoadedAfterItems { get; init; }
        public bool ReturnFromPresentation { get; init; }
        public bool DemoLoads { get; init; }

        public Stream OpenEpisodeFile()
        {
            OpenCount++;
            return openEpisode();
        }
        public void PlayAnimation(string filename, int firstFrame, int lastFrame)
            => Calls.Add($"animation:{filename}:{firstFrame}:{lastFrame}");
        public void ConfigureMode(EpisodeMapLoadState state, char command) => Calls.Add($"mode:{command}");
        public void HandleLevelJump(EpisodeMapLoadState state, char command, ReadOnlyMemory<byte> section)
        {
            if (JumpOnLevelCommand)
            {
                state.MainLevel = 2;
                state.JumpSection = true;
            }
            Calls.Add($"jump:{command}");
        }
        public void SaveGame(int slot, string description) => Calls.Add($"save:{slot}");
        public void ShowItemScreen(Stream episode, ReadOnlyMemory<byte> section)
        {
            Calls.Add("items");
            Span<byte> row = stackalloc byte[256];
            for (var index = 0; index < 9; index++)
                EncryptedText.ReadPascalString(episode, row);
            if (MarkGameLoadedAfterItems && _items++ == 0)
                state.GameLoaded = true;
        }
        public bool HandlePresentation(char command, Stream episode, ReadOnlyMemory<byte> section,
            Memory<byte> buffer, Memory<byte> pictureBuffer)
        {
            Calls.Add($"presentation:{command}");
            return ReturnFromPresentation;
        }
        public bool LoadNextDemo()
        {
            Calls.Add("load-demo");
            return DemoLoads;
        }
        public void FadeBlack(int steps) => Calls.Add($"fade:{steps}");
        public void PlaySong(int song) => Calls.Add($"song:{song}");
        public void ReadNetworkText() => Calls.Add("network-text");
        public void LoadSelectedLevelMap(EpisodeMapLoadState state)
        {
            Calls.Add("load-map");
        }
    }
}
