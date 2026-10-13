using System.Diagnostics;
using System.Text;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class LevelFlagsInitializerTests
{
    public static void Run(string? executable = null)
    {
        var definitions = new ItemDefinitions();
        definitions.ParseBundle(Encoding.Latin1.GetBytes(LevelEventTests.Fixture()), "level-flags.tyrr");
        var items = ItemDatabase.FromDefinitions(definitions);
        var players = new PlayerState[] { new(0), new(1) };
        var effects = new SceneEffects(new byte[8]);
        using var warnings = new StringWriter();
        var enemies = new EnemySystem(items, warnings);
        var backgrounds = new BackgroundRenderer();
        var level = new LevelEvents(items, enemies, players, backgrounds, effects);
        var combat = new PlayerCombat(players, effects, new IndexedSurface(320, 200, 320, TextWriter.Null));
        var input = new InputState(new NoOpInputPlatform(), new VideoLayout());
        var shots = new PlayerShots([], [], players, new NoOpShotEffects());
        var movement = new PlayerMovement(players, combat, level, shots, items);
        var weapons = new PlayerWeapons(shots, items, combat);
        var playerInput = new PlayerInput(input, new JoystickInput([], () => 0, () => { }, _ => { }), [],
            [], movement, combat, level, new NetworkRequests());
        var special = new PlayerSpecialWeapons(items, shots, weapons, combat, enemies, effects, level, backgrounds);
        var frame = new PlayerFrame(players, combat, level, movement, playerInput, weapons, special, shots, items, effects, enemies);
        var pickups = new PlayerPickups(players, enemies, combat, shots, items, level, effects, [], []);
        var warning = new WarningBars(new IndexedSurface(320, 200, 320, TextWriter.Null), new DelayTimer(input),
            () => { }, _ => { });
        var armorWarning = new ArmorWarning(players, combat, warning, effects, new NoOpArmorWarningHost());
        var initializer = new LevelFlagsInitializer(input, combat, movement, level, backgrounds, effects,
            enemies, frame, pickups, warning, armorWarning);
        var trace = new StringBuilder();

        for (var mode = 0; mode < 16384; mode++)
        {
            Seed(mode);
            initializer.Initialize();
            trace.AppendLine($"case:{mode}");
            trace.AppendLine(FormattableString.Invariant($"core:{Bit(input.NewKey)},{Bit(input.NewMouse)},{combat.LevelEnd},{movement.LevelEndWarp},{combat.LevelEndFxWait},{warning.Color},{warning.ColorChange},{warning.SoundDelay},{armorWarning.ShipDelay}"));
            trace.AppendLine(FormattableString.Invariant($"level:{Bit(pickups.BonusLevel)},{Bit(level.ReadyToEndLevel)},{Bit(level.FirstGameOver)},{level.EventLocation},{level.Location},{Bit(level.EnemiesActive)},{Bit(level.StopBackgrounds)},{level.StopBackgroundNumber},{Bit(level.ThirdAtFirstX)},{Bit(level.ThirdAtFirstXB)},{level.ThirdOver},{level.SecondOver},{Bit(level.TopEnemyOver)},{Bit(level.SkyOverAll)},{Bit(level.SmallEnemyAdjust)},{Bit(level.StarsActive)},{Bit(enemies.ContinualDamage)},{level.EnemyFrequency},{Bit(level.QuitRequested)},{Bit(level.ForceEvents)},{enemies.Enemy254Jump},{Bit(level.FilterActive)},{Bit(level.SecondNotTransparent)}"));
            trace.AppendLine(FormattableString.Invariant($"background:{backgrounds.First.Move},{backgrounds.Second.Move},{backgrounds.Third.Move},{effects.ExplosionMove},{Bit(backgrounds.FilterFade)},{Bit(backgrounds.FilterFadeStart)},{Bit(frame.Buttons[0])},{Bit(frame.Buttons[1])},{Bit(frame.Buttons[2])},{backgrounds.LevelFilter},{backgrounds.Brightness},{backgrounds.BrightnessChange}"));
            trace.AppendLine(FormattableString.Invariant($"boss:{enemies.BossBars[0].LinkNumber},{enemies.BossBars[0].Armor},{enemies.BossBars[0].Color},{enemies.BossBars[1].LinkNumber},{enemies.BossBars[1].Armor},{enemies.BossBars[1].Color}"));
        }

        if (executable is null) return;
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = true, RedirectStandardError = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start level flags reference.");
        var output = process.StandardOutput.ReadToEndAsync();
        var errors = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit(30000))
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("Level flags reference timed out.");
        }
        var expected = output.GetAwaiter().GetResult().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var actual = trace.ToString().Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (process.ExitCode != 0 || !expected.SequenceEqual(actual))
        {
            var index = Enumerable.Range(0, Math.Min(expected.Length, actual.Length))
                .FirstOrDefault(i => expected[i] != actual[i], -1);
            throw new InvalidOperationException($"Level flag initialization differs at {index}: " +
                $"{expected.ElementAtOrDefault(index)} / {actual.ElementAtOrDefault(index)}; {errors.GetAwaiter().GetResult()}");
        }

        void Seed(int mode)
        {
            input.NewKey = input.NewMouse = pickups.BonusLevel = level.ReadyToEndLevel =
                level.FirstGameOver = level.EnemiesActive = level.StopBackgrounds = (mode & 1) != 0;
            level.ThirdAtFirstX = level.ThirdAtFirstXB = level.TopEnemyOver = level.SkyOverAll =
                level.SmallEnemyAdjust = level.StarsActive = (mode & 2) != 0;
            enemies.ContinualDamage = level.QuitRequested = level.ForceEvents = level.FilterActive =
                backgrounds.FilterFade = backgrounds.FilterFadeStart = (mode & 4) != 0;
            level.SecondNotTransparent = (mode & 8) != 0;
            combat.LevelEnd = (byte)mode;
            movement.LevelEndWarp = unchecked((sbyte)(mode * 17));
            combat.LevelEndFxWait = unchecked((ushort)(mode * 19));
            warning.Color = (byte)(mode * 23);
            warning.ColorChange = unchecked((sbyte)(mode * 29));
            warning.SoundDelay = (byte)(mode * 31);
            armorWarning.ShipDelay = (ushort)(mode * 37);
            level.EventLocation = (ushort)(mode * 41);
            level.Location = (ushort)(mode * 43);
            backgrounds.First.Move = (ushort)(mode * 47);
            backgrounds.Second.Move = (ushort)(mode * 53);
            backgrounds.Third.Move = (ushort)(mode * 59);
            effects.ExplosionMove = (ushort)(mode * 61);
            level.StopBackgroundNumber = (byte)(mode * 67);
            level.ThirdOver = (byte)(mode * 71);
            level.SecondOver = (byte)(mode * 73);
            level.EnemyFrequency = (ushort)(mode * 79);
            enemies.Enemy254Jump = (ushort)(mode * 83);
            backgrounds.LevelFilter = unchecked((sbyte)(mode * 3));
            backgrounds.Brightness = unchecked((sbyte)(mode * 5));
            backgrounds.BrightnessChange = unchecked((sbyte)(mode * 7));
            for (var i = 0; i < frame.Buttons.Length; i++) frame.Buttons[i] = ((mode + i) & 2) != 0;
            for (var i = 0; i < enemies.BossBars.Length; i++)
            {
                enemies.BossBars[i].LinkNumber = (byte)(mode + i + 1);
                enemies.BossBars[i].Armor = (byte)(mode + i + 10);
                enemies.BossBars[i].Color = (byte)(mode + i + 20);
            }
        }

        static int Bit(bool value) => value ? 1 : 0;
    }

    private sealed class NoOpShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) { }
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => (0, 0);
        public void Explosion(int x, int y, byte type) { }
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) { }
    }

    private sealed class NoOpInputPlatform : IInputPlatform
    {
        public bool PollEvent(out InputEvent input) { input = default; return false; }
        public uint Ticks => 0;
        public void Delay(uint milliseconds) { }
        public void SetCursorVisible(bool visible) { }
        public void SetRelativeMouseMode(bool enabled) { }
        public void EnableSystemMouseScaling() { }
        public void ToggleFullscreen() { }
        public void WindowResized() { }
        public void Quit() { }
        public void PushJoysticksAsKeyboard() { }
        public void PollJoysticks() { }
        public bool JoystickDown => false;
        public bool IsNetworkGame => false;
        public void CheckNetwork() { }
    }

    private sealed class NoOpArmorWarningHost : IArmorWarningHost
    {
        public short CreateShip() => 0;
        public void ConfigureShip(short ship, int dropId, ushort backgroundMove) { }
        public void Fill(int left, int top, int right, int bottom, byte color) { }
        public void Label(byte brightness) { }
    }
}
