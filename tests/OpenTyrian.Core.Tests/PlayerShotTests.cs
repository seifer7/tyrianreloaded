using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class PlayerShotTests
{
    public static void Run(string? executable = null)
    {
        var fixture = Fixture(0);
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(fixture), "fixture.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var effects = new Effects(new StringBuilder());
        var shots = Setup(items, effects);
        Check(shots.Create(ushort.MaxValue, 0, 0, 0, 0, 0, 1, 1) == PlayerShots.Capacity &&
            shots.Create(0, 0, 0, 0, 0, 0, 0, 1) == PlayerShots.Capacity, "Projectile native bounds differ.");
        var typedPort = (ushort)items.Index(ItemType.WeaponFront, "late");
        var typedShot = (ushort)items.Index(ItemType.Shot, "late");
        shots.Power = 0;
        Check(shots.CreateTyped(typedPort, PlayerShotSlot.Front, 0, 0, 0, 0, typedShot, 1) == PlayerShots.Capacity,
            "Typed player-shot slot did not route to the native front slot.");
        try
        {
            shots.CreateTyped(typedPort, (PlayerShotSlot)byte.MaxValue, 0, 0, 0, 0, typedShot, 1);
            throw new InvalidOperationException("Invalid typed player-shot slot was accepted.");
        }
        catch (ArgumentOutOfRangeException) { }
        shots.Power = 0;
        Check(shots.Create((ushort)items.Index(ItemType.WeaponFront, "late"), 0, 60, 60, 70, 50,
            (ushort)items.Index(ItemType.Shot, "late"), 1) == PlayerShots.Capacity, "Insufficient projectile power accepted.");
        shots.Availability[80] = 1;
        var result = new PlayerShotResult { X = 42, Y = 21, IsSpecial = true };
        Check(shots.MoveAndDraw(80, ref result) && result.X == 42 && result.Y == 21 && result.IsSpecial &&
            shots.Availability[80] == 0, "Reserved projectile modified collision output.");
        shots.Availability[0] = 1;
        shots.Shots[0].PlayerNumber = 1;
        shots.Shots[0].X = 290;
        shots.Shots[0].XM = 1;
        Check(!shots.MoveAndDraw(0, ref result) && result.X == 291 && shots.Availability[0] == 0,
            "Projectile off-screen boundary or partial output differs.");
        if (executable is null) return;
        var path = Path.Combine(Path.GetTempPath(), $"opentyrian-projectiles-{Guid.NewGuid():N}.tyrr");
        try
        {
            for (var mode = 0; mode < 6; mode++)
            {
                File.WriteAllBytes(path, Encoding.Latin1.GetBytes(Fixture(mode)));
                var managed = Execute(path, mode);
                var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
                start.ArgumentList.Add(path);
                start.ArgumentList.Add(mode.ToString(CultureInfo.InvariantCulture));
                using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start projectile oracle.");
                var output = process.StandardOutput.ReadToEndAsync();
                var errors = process.StandardError.ReadToEndAsync();
                if (!process.WaitForExit(30000))
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException("Projectile oracle timed out.");
                }
                var actual = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal);
                var difference = actual.Split('\n').Zip(managed.Split('\n')).FirstOrDefault(pair => pair.First != pair.Second);
                Check(process.ExitCode == 0 && actual == managed,
                    $"Projectile mode {mode} differs: {difference.First} / {difference.Second} {errors.GetAwaiter().GetResult()}");
            }
        }
        finally { File.Delete(path); }
    }

    private static string Fixture(int mode) => ItemDatabaseTests.Fixture(0) +
        "[weapon:late]\npoweruse=7\n[weapon:27]\n[weapon:28]\n[weapon:30]\n[weapon:22]\n" +
        "[shot:late]\nmax=3\nmulti=3\nattack=2 3 4\nchain=0 147 148\niceticks=0 20 40\ninfinite=0 1 0\n" +
        "shotrepeat=8\nweapani=2\ntx=3\nty=4\naim=7\nacceleration=1\naccelerationx=0\n" +
        "sx=101 110 -3\nsy=102 98 5\nbx=1 -1 0\nby=0 2 -2\n" +
        $"del={(mode == 1 ? "10 12 9" : "99 100 121")}\nsg={(mode == 2 ? "0 0 0" : "501 60001 1005")}\n" +
        "circlesize=42\nsound=5\ntrail=98\nshipblastfilter=3\n";

    private static PlayerShots Setup(ItemDatabase items, IPlayerShotEffects effects)
    {
        var players = new[] { new PlayerState(0), new PlayerState(1) };
        players[0].DeltaXShotMove = 2;
        players[0].DeltaYShotMove = -3;
        players[1].DeltaXShotMove = -4;
        players[1].DeltaYShotMove = 5;
        var shots = new PlayerShots(items.Shots, items.Weapons, players, effects) { Power = 100, BackgroundTwo = true };
        shots.Enemies[0].Availability = 0; shots.Enemies[0].X = 70; shots.Enemies[0].Y = 60;
        shots.Enemies[1].Availability = 2; shots.Enemies[1].X = 65; shots.Enemies[1].Y = 65; shots.Enemies[1].ScoreItem = true;
        shots.Enemies[99].Availability = 2; shots.Enemies[99].X = 65; shots.Enemies[99].Y = 65;
        return shots;
    }

    private static string Execute(string path, int mode)
    {
        var definitions = new ItemDefinitions();
        definitions.LoadBundle(path);
        var items = ItemDatabase.FromDefinitions(definitions);
        var output = new StringBuilder();
        var shots = Setup(items, new Effects(output));
        if (mode == 3) Array.Fill(shots.Availability, (byte)10);
        var id = shots.Create((ushort)items.Index(ItemType.WeaponFront, "late"), 3,
            mode == 5 ? ushort.MaxValue : (ushort)60, mode == 5 ? ushort.MaxValue : (ushort)60, 70, 50,
            (ushort)items.Index(ItemType.Shot, "late"), 2);
        output.Append($"created:{id}\n");
        if (mode == 4)
        {
            shots.Availability[79] = 1;
            shots.Shots[79].X = 10; shots.Shots[79].Y = 20;
            shots.Shots[79].XM = short.MaxValue; shots.Shots[79].XC = 1;
            shots.Shots[79].PlayerNumber = 1; shots.Shots[79].Trail = 255;
        }
        shots.Availability[80] = 3;
        Dump(shots, output);
        for (var frame = 0; frame < 12 && mode != 3; frame++)
        {
            if (frame == 4) shots.Enemies[99].Availability = 1;
            if (mode == 1) shots.Simulate();
            else for (var i = 0; i < PlayerShots.Capacity; i++)
            {
                if (shots.Availability[i] == 0) continue;
                var result = new PlayerShotResult
                {
                    IsSpecial = true,
                    X = -20,
                    Y = -30,
                    Damage = -2,
                    BlastFilter = 4,
                    PlayerNumber = 5,
                    Chain = 6,
                    RadiusWidth = 99,
                    RadiusHeight = 88
                };
                var active = shots.MoveAndDraw(i, ref result);
                output.Append($"R:{i}:{(active ? 1 : 0)}:{(result.IsSpecial ? 1 : 0)}:{result.X}:{result.Y}:" +
                    $"{result.Damage}:{result.BlastFilter}:{result.Chain}:{result.PlayerNumber}:{result.RadiusWidth}:{result.RadiusHeight}\n");
            }
            Dump(shots, output);
        }
        var weaponIds = new[] { "27", "28", "30", "22" };
        for (var wi = 0; wi < 4; wi++)
        {
            for (var angle = 0; angle < 17; angle++)
            {
                shots.Shots[0].YM = 7;
                shots.SetDirection(0, items.Index(ItemType.WeaponFront, weaponIds[wi]), (float)(angle * (2 * Math.PI / 16)));
                var shot = shots.Shots[0];
                output.Append($"direction:{wi}:{angle}={shot.XM},{shot.YM},{shot.YC},{shot.Graphic}\n");
            }
        }
        return output.ToString();
    }

    private static void Dump(PlayerShots state, StringBuilder output)
    {
        output.Append($"state:{state.Power}");
        for (var i = 0; i < 11; i++) output.Append($",{state.MultiPosition[i]},{state.Repeat[i]}");
        foreach (var sound in state.SoundQueue) output.Append($",{sound}");
        output.Append('\n');
        for (var i = 0; i < PlayerShots.Capacity; i++)
        {
            var shot = state.Shots[i];
            output.Append($"{i}:{state.Availability[i]}={shot.X},{shot.Y},{shot.XM},{shot.YM},{shot.XC},{shot.YC}," +
                $"{(shot.Complicated ? 1 : 0)},{shot.DevX},{shot.DirX},{shot.DevY},{shot.DirY},{shot.CircleSizeX},{shot.CircleSizeY}," +
                $"{shot.Trail},{shot.Graphic},{shot.Animation},{shot.AnimationMax},{shot.Chain},{shot.Damage},{shot.BlastFilter}," +
                $"{shot.PlayerNumber},{shot.AimAtEnemy},{shot.AimDelay},{shot.AimDelayMax},{shot.IceTicks},{shot.Infinite}\n");
        }
    }

    private sealed class Effects(StringBuilder output) : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) => output.Append($"D:{sheet}:{x}:{y}:{frame}:{(darken ? 1 : 0)}\n");
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame)
        {
            output.Append($"S:{x}:{y}:{frame}\n");
            return (13, 17);
        }
        public void Explosion(int x, int y, byte type) => output.Append($"E:{x}:{y}:{type}\n");
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) => output.Append($"P:{x}:{y}:{count}:{width}:{color}\n");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
