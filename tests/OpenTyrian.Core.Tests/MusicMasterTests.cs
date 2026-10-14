using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class MusicMasterTests
{
    public static void Run()
    {
        MusicMaster.SongBuy = MusicMaster.DefaultSongBuy;
        MusicMaster.MusicFade = false;

        var mapState = new EpisodeMapLoadState();
        mapState.SongBuy = 17;
        Check(MusicMaster.SongBuy == 17 && mapState.SongBuy == 17,
            "Episode map song selection must use the shared music-master state.");

        var players = new[] { new PlayerState(0), new PlayerState(1) };
        var frameState = new LevelFrameState
        {
            Players = players,
            ShotAvailable = [],
            Smoothies = new bool[9]
        };
        frameState.MusicFade = true;
        Check(MusicMaster.MusicFade, "Level frame fade state must use the shared music-master state.");

        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(EnemySystemTests.Fixture()), "music-master.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var levelEvents = new LevelEvents(items, new EnemySystem(items, TextWriter.Null), players,
            new BackgroundRenderer(), new SceneEffects(new byte[8]));
        Check(levelEvents.MusicFade, "Level event fade state must observe the shared music-master state.");
        levelEvents.MusicFade = false;
        Check(!frameState.MusicFade && !MusicMaster.MusicFade,
            "Clearing music fade in level events must update the shared state.");

        MusicMaster.SongBuy = MusicMaster.DefaultSongBuy;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
