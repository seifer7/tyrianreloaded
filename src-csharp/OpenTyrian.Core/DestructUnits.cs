namespace OpenTyrian.Core;

public enum DestructShot
{
    Invalid = -1,
    Tracer,
    Small,
    Large,
    Micro,
    Super,
    Demo,
    SmallNuke,
    LargeNuke,
    SmallDirt,
    LargeDirt,
    Magnet,
    MiniLaser,
    MegaLaser,
    LaserTracer,
    MegaBlast,
    Mini,
    Bomb
}

public enum DestructMode
{
    FiveCardWar,
    Traditional,
    HeliAssault,
    HeliDefense,
    Outgunned,
    Custom
}

public enum DestructTickState
{
    Continue,
    Reload,
    ReturnToModeSelect
}

public enum DestructExplosionType
{
    None,
    Magnet,
    Dirt,
    Normal
}

[Flags]
public enum DestructMapFlags : uint
{
    Normal = 0,
    Walls = 0x01,
    Rings = 0x02,
    Holes = 0x04,
    Fuzzy = 0x08,
    Tall = 0x10
}

public sealed class DestructPlayerState
{
    public bool IsCpu { get; set; }
    public uint UnitsRemaining { get; set; }
    public uint UnitSelected { get; set; }
    public uint ShotDelay { get; set; }
    public uint Score { get; set; }
    public bool[] Actions { get; } = new bool[8];
    public int[,] KeyConfig { get; } = new int[8, 4];
    public DestructAiState Ai { get; } = new();
    public IList<DestructUnitState> Units { get; } = new List<DestructUnitState>();
}

public sealed class DestructUnitRoster
{
    public uint InstallationCount { get; set; }
    public DestructUnit[] TypeChoices { get; } = new DestructUnit[10];
}

public sealed class DestructConfigState
{
    public uint MaximumShots { get; set; } = 40;
    public uint MaximumExplosions { get; set; } = 40;
    public uint MinimumWalls { get; set; } = 20;
    public uint MaximumWalls { get; set; } = 20;
    public bool AlwaysAlias { get; set; }
    public bool TracerLaser { get; set; }
    public bool AllowCustom { get; set; }
    public bool[] IsCpu { get; } = [true, false];
    public bool[] JumperStraight { get; } = [true, false];
}

public sealed class DestructAiState
{
    public int Angle { get; set; }
    public int Power { get; set; }
    public int Fire { get; set; }
    public uint NoDown { get; set; }
}

public sealed class DestructMatchState
{
    public DestructPlayerState[] Players { get; } = [new(), new()];
    public uint[] SoundQueue { get; } = new uint[8];
    public IList<DestructExplosionState> Explosions { get; } = new List<DestructExplosionState>();
    public uint MaximumExplosions { get; set; }
    public uint ExplosionSoundChannel { get; set; }
    public DestructMapFlags MapFlags { get; set; }
    public DestructMode Mode { get; set; }
    public bool FirstTick { get; set; } = true;
    public uint EndDelay { get; set; }
}

public sealed class DestructGameSession(
    DestructConfigState configuration,
    DestructMatchState match,
    IList<DestructShotState> shots,
    IList<DestructExplosionState> explosions,
    IList<DestructWallState> walls,
    int maximumInstallations,
    IndexedSurface videoScreen,
    IndexedSurface temporaryScreen)
{
    public DestructConfigState Configuration { get; } = configuration;
    public DestructMatchState Match { get; } = match;
    public IList<DestructShotState> Shots { get; } = shots;
    public IList<DestructExplosionState> Explosions { get; } = explosions;
    public IList<DestructWallState> Walls { get; } = walls;
    public int MaximumInstallations { get; } = maximumInstallations;
    public IndexedSurface VideoScreen { get; } = videoScreen;
    public IndexedSurface TemporaryScreen { get; } = temporaryScreen;
}

public sealed class DestructUnitState
{
    public int Health { get; set; }
    public float Angle { get; set; }
    public float Power { get; set; }
    public float YMovement { get; set; }
    public bool IsInAir { get; set; }
    public DestructUnit UnitType { get; set; }
    public DestructShot ShotType { get; set; }
    public uint AnimationFrame { get; set; }
    public uint X { get; set; }
    public float Y { get; set; }
    public int LastMove { get; set; }

    public void RaiseAngle()
    {
        Angle += 0.01f;
        var maximum = (float)(Math.PI / 2 - 0.01);
        if (Angle > maximum)
            Angle = maximum;
    }

    public void LowerAngle()
    {
        Angle -= 0.01f;
        if (Angle < 0)
            Angle = 0;
    }

    public void RaisePower()
    {
        Power += 0.05f;
        if (Power > 5)
            Power = 5;
    }

    public void LowerPower()
    {
        Power -= 0.05f;
        if (Power < 1)
            Power = 1;
    }
}

public sealed class DestructAvailabilityState
{
    public bool IsAvailable { get; set; }
}

public sealed class DestructShotState
{
    public bool IsAvailable { get; set; } = true;
    public float X { get; set; }
    public float Y { get; set; }
    public float XMovement { get; set; }
    public float YMovement { get; set; }
    public bool HasGravity { get; set; }
    public DestructShot ShotType { get; set; }
    public uint[] TrailX { get; } = new uint[4];
    public uint[] TrailY { get; } = new uint[4];
    public uint[] TrailColor { get; } = new uint[4];
}

public sealed class DestructWallState
{
    public bool Exists { get; set; }
    public uint X { get; set; }
    public uint Y { get; set; }
}

public sealed class DestructExplosionState
{
    public bool IsAvailable { get; set; } = true;
    public uint X { get; set; }
    public uint Y { get; set; }
    public uint Width { get; set; }
    public uint MaximumWidth { get; set; }
    public uint Fill { get; set; }
    public DestructExplosionType Type { get; set; }
}

public interface IDestructHelpHost
{
    bool NewKey { get; }
    void FadeBlack(int steps);
    void SaveAndClearScreen();
    void DrawText(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness);
    void Present();
    void FadePalette(int steps, int firstColor, int lastColor);
    void ServiceEvents(bool clear);
    void Delay(uint milliseconds);
    void RestoreScreen();
}

public sealed class DestructHelpScreen(IDestructHelpHost host)
{
    public void Run(IReadOnlyList<ReadOnlyMemory<byte>> help, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(help);
        if (help.Count < 25)
            throw new ArgumentException("Destruct help requires all 25 native text entries.", nameof(help));

        host.FadeBlack(15);
        host.SaveAndClearScreen();
        for (var section = 0; section < 2; section++)
        {
            var sectionY = section * 90;
            host.DrawText(100, 5 + sectionY, help[section * 12], 2, 4);
            host.DrawText(100, 15 + sectionY, help[section * 12 + 1], 2, 1);
            for (var item = 3; item <= 12; item++)
            {
                var x = ((item - 1) % 2) * 160 + 10;
                var y = 15 + ((item - 1) / 2) * 12 + sectionY;
                host.DrawText(x, y, help[section * 12 + item - 1], 1, 3);
            }
        }
        host.DrawText(30, 190, help[24], 3, 4);
        host.Present();
        host.FadePalette(15, 0, 255);

        do
        {
            cancellation.ThrowIfCancellationRequested();
            host.ServiceEvents(true);
            host.Delay(16);
        } while (!host.NewKey);

        host.FadeBlack(15);
        host.RestoreScreen();
        host.Present();
        host.FadePalette(15, 0, 255);
    }
}

public interface IDestructPauseHost
{
    bool NewKey { get; }
    void SetVolume(ushort music, ushort effects);
    void SaveScreen();
    void DrawCenteredText(int y, ReadOnlyMemory<byte> text, byte hue, int brightness);
    void Present();
    void ServiceEvents(bool clear);
    void Delay(uint milliseconds);
    void RestoreScreen();
}

public interface IDestructMenuHost
{
    bool NewKey { get; }
    bool EscapePressed { get; }
    bool ReturnPressed { get; }
    bool UpPressed { get; }
    bool DownPressed { get; }
    void DrawCenteredText(int y, ReadOnlyMemory<byte> text, byte hue, int brightness);
    void DrawModeText(int mode, int y, bool selected);
    void DrawCustomModeText(int y, bool selected);
    void Show();
    void FadePalette(int steps, int firstColor, int lastColor);
    void FadeBlack(int steps);
    void ClearNewKey();
    void ServiceEvents(bool clear);
    void Delay(uint milliseconds);
}

public interface IDestructTickHost
{
    bool ToggleLeftCpuPressed { get; }
    bool ToggleRightCpuPressed { get; }
    bool PausePressed { get; }
    bool HelpPressed { get; }
    bool EscapePressed { get; }
    bool BackspacePressed { get; }
    void SetDelay(uint ticks);
    void CheckTemporaryScreen();
    void RunGravity();
    void AnimateUnits();
    void DrawWalls();
    void RunExplosions();
    void RunShots();
    void RunAi();
    void DrawCrosshairs();
    void DrawHud();
    void Present();
    void FadePalette(int steps, int firstColor, int lastColor);
    void GetInput();
    void ProcessInput();
    void PlayQueuedSounds();
    void ToggleLeftCpu();
    void ToggleRightCpu();
    void Pause();
    void ShowHelp();
    void ClearLastKey();
    void WaitDelay();
    void ClearEscape();
    void ClearBackspace();
}

public interface IDestructGameHost
{
    void ClearScreen(IndexedSurface screen);
    void Present();
    void LoadSpriteSheet();
    void ReleaseSpriteSheet();
    void FadeBlack(int steps);
    void LoadIntroPicture(DestructGameSession session);
    void ShowIntro(DestructGameSession session);
    DestructMode? SelectMode(DestructGameSession session);
    void PrepareMap(DestructGameSession session);
    DestructTickState RunTick(DestructGameSession session);
}

public sealed class DestructPauseScreen(IDestructPauseHost host)
{
    public void Run(
        ushort musicVolume,
        ushort effectsVolume,
        ReadOnlyMemory<byte> pausedText,
        CancellationToken cancellation = default)
    {
        host.SetVolume((ushort)(musicVolume / 2), effectsVolume);
        var screenSaved = false;
        try
        {
            host.SaveScreen();
            screenSaved = true;
            host.DrawCenteredText(90, pausedText, 12, 5);
            host.Present();
            do
            {
                cancellation.ThrowIfCancellationRequested();
                host.ServiceEvents(true);
                host.Delay(16);
            } while (!host.NewKey);
        }
        finally
        {
            if (screenSaved)
            {
                host.RestoreScreen();
                host.Present();
            }
            host.SetVolume(musicVolume, effectsVolume);
        }
    }
}

public static class DestructUnits
{
    private const float GroundY = 199;
    private const float CeilingY = 24;
    private static readonly byte[,] GraphicBase =
    {
        { 1, 6, 11, 58, 63, 68, 96, 153 },
        { 20, 25, 30, 77, 82, 87, 115, 172 }
    };
    private static readonly bool[] SystemAngle = [true, true, true, false, false, true, false, false];
    private static readonly bool[] SystemAnimation = [false, false, false, true, false, false, false, true];
    private static readonly DestructShot[] CpuWeapon =
    [
        DestructShot.Small, DestructShot.Micro, DestructShot.Demo, DestructShot.Invalid,
        DestructShot.Magnet, DestructShot.MiniLaser, DestructShot.Micro, DestructShot.Mini
    ];
    private static readonly DestructShot[] CpuWallWeapon =
    [
        DestructShot.Demo, DestructShot.SmallNuke, DestructShot.Demo, DestructShot.Invalid,
        DestructShot.Magnet, DestructShot.MegaLaser, DestructShot.Micro, DestructShot.Mini
    ];
    private static readonly uint[] ExplosionSize =
        [4, 20, 30, 14, 22, 16, 40, 60, 10, 30, 0, 5, 10, 3, 15, 7, 0];
    private static readonly uint[] ExplosionFill =
        [2, 5, 10, 15, 20, 15, 25, 30, 40, 80, 0, 30, 30, 4, 30, 5, 0];
    private static readonly DestructExplosionType[] ShotExplosionType =
        [DestructExplosionType.Normal, DestructExplosionType.Normal, DestructExplosionType.Normal,
         DestructExplosionType.Normal, DestructExplosionType.Normal, DestructExplosionType.Normal,
         DestructExplosionType.Normal, DestructExplosionType.Normal, DestructExplosionType.Dirt,
         DestructExplosionType.Dirt, DestructExplosionType.Magnet, DestructExplosionType.Normal,
         DestructExplosionType.Normal, DestructExplosionType.Normal, DestructExplosionType.Normal,
         DestructExplosionType.Normal, DestructExplosionType.None];
    private static readonly DestructShot[] DefaultWeapon =
    [
        DestructShot.Small, DestructShot.Micro, DestructShot.SmallDirt, DestructShot.Invalid,
        DestructShot.Magnet, DestructShot.MiniLaser, DestructShot.Micro, DestructShot.Mini
    ];
    private static readonly int[] BaseDamage = [200, 120, 400, 300, 80, 150, 600, 40];
    private static readonly byte[] ShotSound =
        [8, 2, 1, 7, 7, 9, 22, 22, 5, 13, 10, 15, 15, 26, 14, 7, 7];
    private static readonly bool[] ShotDemolishesWalls =
        [false, false, false, false, false, true, true, true, false, false, false, false, true, false, true, false, true];
    private static readonly bool[] ShotBounces =
        [false, false, false, false, false, false, false, false, false, false, false, true, true, true, true, false, true];
    private static readonly int[] ShotTrailType =
        [0, 0, 0, 1, 1, 1, 2, 2, 0, 0, 0, 1, 2, 1, 2, 1, 0];
    private static readonly int[] ShotColor =
        [16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 16, 10, 10, 10, 10, 16, 0];
    private static readonly uint[] ShotDelay =
        [10, 30, 80, 20, 60, 100, 140, 200, 20, 60, 5, 15, 50, 5, 80, 16, 0];
    private static readonly string[] KeyOptionNames =
        ["left", "right", "up", "down", "change", "fire", "previous weapon", "next weapon"];
    private static readonly string[] PlayerNames = ["left", "right"];
    private static readonly string[] UnitOptionNames =
        ["tank", "nuke", "dirt", "satellite", "magnet", "laser", "jumper", "heli"];

    private static readonly uint[,] ModeScore =
    {
        { 1, 0, 0, 5, 0, 1 },
        { 1, 0, 5, 0, 1, 1 }
    };

    private const uint ClearedPlatformSound = 30;
    private const byte NoSound = 0;
    private const uint Tracer = 1u << (int)DestructShot.Tracer;
    private const uint Small = 1u << (int)DestructShot.Small;
    private const uint Large = 1u << (int)DestructShot.Large;
    private const uint Micro = 1u << (int)DestructShot.Micro;
    private const uint Super = 1u << (int)DestructShot.Super;
    private const uint Demo = 1u << (int)DestructShot.Demo;
    private const uint SmallNuke = 1u << (int)DestructShot.SmallNuke;
    private const uint LargeNuke = 1u << (int)DestructShot.LargeNuke;
    private const uint SmallDirt = 1u << (int)DestructShot.SmallDirt;
    private const uint LargeDirt = 1u << (int)DestructShot.LargeDirt;
    private const uint Magnet = 1u << (int)DestructShot.Magnet;
    private const uint MiniLaser = 1u << (int)DestructShot.MiniLaser;
    private const uint MegaLaser = 1u << (int)DestructShot.MegaLaser;
    private const uint LaserTracer = 1u << (int)DestructShot.LaserTracer;
    private const uint MegaBlast = 1u << (int)DestructShot.MegaBlast;
    private const uint Mini = 1u << (int)DestructShot.Mini;
    private static readonly uint[] WeaponSystems =
    [
        Tracer | Small | Large | Demo,
        Micro | Super | Demo | SmallNuke | LargeNuke,
        Demo | SmallDirt | LargeDirt,
        0,
        Magnet,
        MiniLaser | MegaLaser,
        Tracer | Micro | MegaBlast,
        Tracer | Demo | Mini
    ];

    public static void CycleWeaponUp(DestructUnitState unit, bool enableTracerLaser = false) =>
        CycleWeapon(unit, 1, enableTracerLaser);

    public static void CycleWeaponDown(DestructUnitState unit, bool enableTracerLaser = false) =>
        CycleWeapon(unit, -1, enableTracerLaser);

    public static bool CheckEndgame(DestructMatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);
        if ((uint)match.Mode >= 6)
            throw new ArgumentOutOfRangeException(nameof(match), "The Destruct mode must be valid.");

        if (match.Players[0].UnitsRemaining == 0)
        {
            match.Players[1].Score = unchecked(match.Players[1].Score + ModeScore[0, (int)match.Mode]);
            match.SoundQueue[7] = ClearedPlatformSound;
            return true;
        }
        if (match.Players[1].UnitsRemaining == 0)
        {
            match.Players[0].Score = unchecked(match.Players[0].Score + ModeScore[1, (int)match.Mode]);
            match.SoundQueue[7] = ClearedPlatformSound;
            return true;
        }
        return false;
    }

    public static void ShowIntro(
        IndexedSurface screen,
        IndexedSurface background,
        ReadOnlyMemory<byte> title,
        ReadOnlyMemory<byte> promptOne,
        ReadOnlyMemory<byte> promptTwo,
        IDestructMenuHost host,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(host);
        if (screen.Pitch != background.Pitch || screen.Height != background.Height ||
            screen.Pixels.Length != background.Pixels.Length)
            throw new ArgumentException("Destruct intro surfaces must have matching dimensions and pitch.");

        screen.Pixels.Span.CopyTo(background.Pixels.Span);
        host.DrawCenteredText(90, title, 12, 5);
        host.DrawCenteredText(180, promptOne, 15, 2);
        host.DrawCenteredText(190, promptTwo, 15, 2);
        host.Show();
        host.FadePalette(15, 0, 255);
        host.ClearNewKey();
        while (!host.NewKey)
        {
            cancellation.ThrowIfCancellationRequested();
            host.ServiceEvents(false);
            host.Delay(16);
        }

        host.FadeBlack(15);
        background.Pixels.Span.CopyTo(screen.Pixels.Span);
        host.Show();
    }

    public static void DrawModeSelectMenu(
        IDestructMenuHost host,
        DestructMode mode,
        bool allowCustom,
        int modeCount = 5)
    {
        ArgumentNullException.ThrowIfNull(host);
        if (modeCount != 5 || (uint)mode > (uint)(allowCustom ? DestructMode.Custom : DestructMode.Outgunned))
            throw new ArgumentOutOfRangeException(nameof(mode));
        for (var index = 0; index < modeCount; index++)
            host.DrawModeText(index, 82 + index * 12, index == (int)mode);
        if (allowCustom)
            host.DrawCustomModeText(82 + modeCount * 12, mode == DestructMode.Custom);
    }

    public static DestructMode? RunModeSelect(
        IndexedSurface screen,
        IndexedSurface background,
        IDestructMenuHost host,
        bool allowCustom,
        CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(host);
        if (screen.Pitch != background.Pitch || screen.Height != background.Height ||
            screen.Pixels.Length != background.Pixels.Length)
            throw new ArgumentException("Destruct menu surfaces must have matching dimensions and pitch.");

        background.Pixels.Span.CopyTo(screen.Pixels.Span);
        var mode = DestructMode.FiveCardWar;
        DrawModeSelectMenu(host, mode, allowCustom);
        host.Show();
        host.FadePalette(15, 0, 255);

        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            DrawModeSelectMenu(host, mode, allowCustom);
            host.Show();
            host.ClearNewKey();
            while (!host.NewKey)
            {
                cancellation.ThrowIfCancellationRequested();
                host.ServiceEvents(false);
                host.Delay(16);
            }

            if (host.EscapePressed)
            {
                mode = (DestructMode)(-1);
                break;
            }
            if (host.ReturnPressed)
                break;
            if (host.UpPressed)
                mode = mode == DestructMode.FiveCardWar
                    ? allowCustom ? DestructMode.Custom : DestructMode.Outgunned
                    : (DestructMode)((int)mode - 1);
            if (host.DownPressed)
                mode = mode >= DestructMode.Outgunned
                    ? allowCustom && mode == DestructMode.Outgunned ? DestructMode.Custom : DestructMode.FiveCardWar
                    : (DestructMode)((int)mode + 1);
        }

        host.FadeBlack(15);
        background.Pixels.Span.CopyTo(screen.Pixels.Span);
        host.Show();
        return (int)mode < 0 ? null : mode;
    }

    public static void RunMain(
        DestructMatchState match,
        DestructConfigState configuration,
        Action loadIntroPicture,
        Action showIntro,
        Action resetPlayers,
        Func<DestructMode?> selectMode,
        Action prepareMap,
        Func<DestructTickState> runTick,
        Action fadeBlack)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(loadIntroPicture);
        ArgumentNullException.ThrowIfNull(showIntro);
        ArgumentNullException.ThrowIfNull(resetPlayers);
        ArgumentNullException.ThrowIfNull(selectMode);
        ArgumentNullException.ThrowIfNull(prepareMap);
        ArgumentNullException.ThrowIfNull(runTick);
        ArgumentNullException.ThrowIfNull(fadeBlack);

        loadIntroPicture();
        showIntro();
        resetPlayers();
        match.Players[0].IsCpu = configuration.IsCpu[0];
        match.Players[1].IsCpu = configuration.IsCpu[1];
        while (true)
        {
            var mode = selectMode();
            if (mode is null)
                return;
            match.Mode = mode.Value;
            DestructTickState tickState;
            do
            {
                prepareMap();
                do
                {
                    tickState = runTick();
                } while (tickState == DestructTickState.Continue);
                fadeBlack();
            } while (tickState == DestructTickState.Reload);
        }
    }

    public static void RunGame(
        Configuration textConfiguration,
        IKeyboardNames keyboard,
        int[,,] defaultKeyConfig,
        IReadOnlyList<DestructUnitRoster> rosters,
        IndexedSurface videoScreen,
        IndexedSurface temporaryScreen,
        IDestructGameHost host)
    {
        ArgumentNullException.ThrowIfNull(textConfiguration);
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(defaultKeyConfig);
        ArgumentNullException.ThrowIfNull(rosters);
        ArgumentNullException.ThrowIfNull(videoScreen);
        ArgumentNullException.ThrowIfNull(temporaryScreen);
        ArgumentNullException.ThrowIfNull(host);

        host.ClearScreen(videoScreen);
        host.Present();
        var config = LoadConfig(textConfiguration, keyboard, defaultKeyConfig, rosters);
        var maximumInstallations = 0;
        for (var rosterIndex = 0; rosterIndex < 10; rosterIndex++)
        {
            var roster = rosters[rosterIndex];
            ArgumentNullException.ThrowIfNull(roster);
            maximumInstallations = Math.Max(
                maximumInstallations, checked((int)roster.InstallationCount));
        }

        var shots = CreatePool<DestructShotState>(config.MaximumShots);
        var explosions = CreatePool<DestructExplosionState>(config.MaximumExplosions);
        var walls = CreatePool<DestructWallState>(config.MaximumWalls);
        var match = new DestructMatchState { MaximumExplosions = config.MaximumExplosions };
        foreach (var explosion in explosions)
            match.Explosions.Add(explosion);
        foreach (var player in match.Players)
        for (var index = 0; index < maximumInstallations; index++)
            player.Units.Add(new DestructUnitState());

        var session = new DestructGameSession(
            config, match, shots, explosions, walls, maximumInstallations, videoScreen, temporaryScreen);
        var spritesLoaded = false;
        try
        {
            host.LoadSpriteSheet();
            spritesLoaded = true;
            host.FadeBlack(1);
            RunMain(
                match, config,
                () => host.LoadIntroPicture(session),
                () => host.ShowIntro(session),
                () => ResetPlayers(match, CopyKeyConfiguration(defaultKeyConfig)),
                () => host.SelectMode(session),
                () => PrepareMap(session),
                () => host.RunTick(session),
                () => host.FadeBlack(25));
        }
        finally
        {
            if (spritesLoaded)
                host.ReleaseSpriteSheet();
        }

        void PrepareMap(DestructGameSession game)
        {
            game.Match.FirstTick = true;
            host.LoadIntroPicture(game);
            ResetUnits(
                game.Match.Players[0].Units,
                game.Match.Players[1].Units,
                game.MaximumInstallations);
            foreach (var shot in game.Shots)
                shot.IsAvailable = true;
            foreach (var explosion in game.Explosions)
                explosion.IsAvailable = true;
            host.PrepareMap(game);
            ResetAi(game.Match, (game.Match.MapFlags & DestructMapFlags.Walls) != 0);
        }
    }

    private static T[] CreatePool<T>(uint count) where T : new()
    {
        var length = checked((int)count);
        var pool = new T[length];
        for (var index = 0; index < length; index++)
            pool[index] = new T();
        return pool;
    }

    private static int[,,] CopyKeyConfiguration(int[,,] source)
    {
        var result = new int[2, 8, 4];
        Array.Copy(source, result, source.Length);
        return result;
    }

    public static DestructTickState RunTick(
        DestructMatchState match,
        int maximumInstallations,
        IDestructTickHost host)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(host);
        host.SetDelay(1);
        Array.Clear(match.SoundQueue);
        host.CheckTemporaryScreen();
        ResetActions(match);
        CycleDeadUnits(match, maximumInstallations);
        host.RunGravity();
        host.AnimateUnits();
        host.DrawWalls();
        host.RunExplosions();
        host.RunShots();
        host.RunAi();
        host.DrawCrosshairs();
        host.DrawHud();
        host.Present();

        if (match.FirstTick)
        {
            host.FadePalette(25, 0, 255);
            match.FirstTick = false;
            match.EndDelay = 0;
        }

        host.GetInput();
        host.ProcessInput();
        if (match.EndDelay > 0)
        {
            match.EndDelay--;
            if (match.EndDelay == 0)
                return DestructTickState.Reload;
        }
        else if (CheckEndgame(match))
        {
            match.EndDelay = 80;
        }

        host.PlayQueuedSounds();
        if (host.ToggleLeftCpuPressed)
            host.ToggleLeftCpu();
        if (host.ToggleRightCpuPressed)
            host.ToggleRightCpu();
        if (host.PausePressed)
        {
            host.Pause();
            host.ClearLastKey();
        }
        if (host.HelpPressed)
        {
            host.ShowHelp();
            host.ClearLastKey();
        }

        host.WaitDelay();
        if (host.EscapePressed)
        {
            host.ClearEscape();
            return DestructTickState.ReturnToModeSelect;
        }
        if (host.BackspacePressed)
        {
            host.ClearBackspace();
            return DestructTickState.Reload;
        }
        return DestructTickState.Continue;
    }

    public static DestructConfigState LoadConfig(
        Configuration configuration,
        IKeyboardNames keyboard,
        int[,,] defaultKeyConfig,
        IReadOnlyList<DestructUnitRoster> rosters)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(keyboard);
        ArgumentNullException.ThrowIfNull(defaultKeyConfig);
        ArgumentNullException.ThrowIfNull(rosters);
        if (defaultKeyConfig.GetLength(0) != 2 || defaultKeyConfig.GetLength(1) != 8 ||
            defaultKeyConfig.GetLength(2) != 4 || rosters.Count < 10)
            throw new ArgumentException("Destruct configuration requires two players, 8-by-4 keys, and ten rosters.");

        var result = new DestructConfigState();
        var section = configuration.FindOrAddSection("destruct");
        result.AlwaysAlias = section.GetOrSetBoolean("antialias craters", false, ConfigBoolStyle.NoYes);
        result.TracerLaser = section.GetOrSetBoolean("tracer laser", false, ConfigBoolStyle.OffOn);
        result.MaximumShots = unchecked((uint)section.GetOrSetInt32("max shots", 40));
        result.MaximumExplosions = unchecked((uint)section.GetOrSetInt32("max explosions", 40));
        result.MinimumWalls = unchecked((uint)section.GetOrSetInt32("min walls", 20));
        result.MaximumWalls = unchecked((uint)section.GetOrSetInt32("max walls", 20));
        result.IsCpu[0] = section.GetOrSetBoolean("left ai", true, ConfigBoolStyle.NoYes);
        result.JumperStraight[0] = section.GetOrSetBoolean("left jumper fires straight", true, ConfigBoolStyle.NoYes);
        result.IsCpu[1] = section.GetOrSetBoolean("right ai", false, ConfigBoolStyle.NoYes);
        result.JumperStraight[1] = section.GetOrSetBoolean("right jumper fires straight", false, ConfigBoolStyle.NoYes);

        for (var playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            section = configuration.FindSection("destruct keyboard", PlayerNames[playerIndex]) ??
                configuration.AddSection("destruct keyboard", PlayerNames[playerIndex]);
            for (var actionIndex = 0; actionIndex < 8; actionIndex++)
            {
                var option = section.GetOrSetOption(KeyOptionNames[actionIndex], null);
                for (var valueIndex = 0; valueIndex < option.Values.Count;)
                {
                    var scancode = keyboard.FromName(option.Values[valueIndex]);
                    if (scancode != 0 && valueIndex < defaultKeyConfig.GetLength(2))
                    {
                        defaultKeyConfig[playerIndex, actionIndex, valueIndex] = scancode;
                        valueIndex++;
                    }
                    else
                    {
                        option.RemoveValue(valueIndex);
                    }
                }

                if (option.Count > 0)
                {
                    for (var keyIndex = option.Count; keyIndex < defaultKeyConfig.GetLength(2); keyIndex++)
                        defaultKeyConfig[playerIndex, actionIndex, keyIndex] = 0;
                }
                else
                {
                    for (var keyIndex = 0; keyIndex < defaultKeyConfig.GetLength(2); keyIndex++)
                    {
                        var scancode = defaultKeyConfig[playerIndex, actionIndex, keyIndex];
                        if (scancode != 0)
                            option.AddValue(keyboard.Name(scancode));
                    }
                }
            }
        }

        section = configuration.FindSection("destruct custom") ??
            configuration.AddSection("destruct custom");
        result.AllowCustom = section.GetOrSetBoolean("enable", false, ConfigBoolStyle.NoYes);
        for (var playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            var roster = rosters[8 + playerIndex];
            var countKey = $"{PlayerNames[playerIndex]} num units";
            roster.InstallationCount = unchecked((byte)section.GetOrSetInt32(
                countKey, unchecked((int)roster.InstallationCount)));
            var option = section.GetOrSetOption($"{PlayerNames[playerIndex]} unit", null);
            var parsedUnits = 0;
            for (var valueIndex = 0; valueIndex < option.Values.Count;)
            {
                var unitType = Array.IndexOf(UnitOptionNames, option.Values[valueIndex]);
                if (unitType >= 0 && parsedUnits < roster.TypeChoices.Length)
                {
                    roster.TypeChoices[parsedUnits++] = (DestructUnit)unitType;
                    valueIndex++;
                }
                else
                {
                    option.RemoveValue(valueIndex);
                }
            }

            if (option.Count > 0)
            {
                for (var unitIndex = option.Count; unitIndex < roster.TypeChoices.Length; unitIndex++)
                {
                    roster.TypeChoices[unitIndex] = DestructUnit.Tank;
                    option.AddValue(UnitOptionNames[(int)DestructUnit.Tank]);
                }
            }
            else
            {
                for (var unitIndex = 0; unitIndex < roster.TypeChoices.Length; unitIndex++)
                    option.AddValue(UnitOptionNames[(int)roster.TypeChoices[unitIndex]]);
            }
        }

        return result;
    }

    public static void ResetActions(DestructMatchState match)
    {
        ArgumentNullException.ThrowIfNull(match);
        foreach (var player in match.Players)
            Array.Clear(player.Actions);
    }

    public static void ResetPlayers(DestructMatchState match, int[,,] defaultKeyConfig)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(defaultKeyConfig);
        if (defaultKeyConfig.GetLength(0) != 2 ||
            defaultKeyConfig.GetLength(1) != 8 ||
            defaultKeyConfig.GetLength(2) != 4)
            throw new ArgumentException("Default Destruct keys must be a 2 by 8 by 4 matrix.", nameof(defaultKeyConfig));

        for (var playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            var player = match.Players[playerIndex];
            player.IsCpu = false;
            player.UnitSelected = 0;
            player.ShotDelay = 0;
            player.Score = 0;
            player.Ai.Angle = 0;
            player.Ai.Power = 0;
            player.Ai.Fire = 0;
            player.Ai.NoDown = 0;
            for (var key = 0; key < 8; key++)
            for (var option = 0; option < 4; option++)
                player.KeyConfig[key, option] = defaultKeyConfig[playerIndex, key, option];
        }
    }

    public static void ResetAi(DestructMatchState match, bool wallsPresent)
    {
        ArgumentNullException.ThrowIfNull(match);
        foreach (var player in match.Players)
        {
            if (!player.IsCpu)
                continue;

            foreach (var unit in player.Units)
            {
                if (unit.Health <= 0)
                    continue;
                var type = (int)unit.UnitType;
                if ((uint)type >= (uint)DestructUnitNames.UnitCount)
                    throw new ArgumentOutOfRangeException(nameof(match), "A Destruct unit has an invalid type.");
                unit.Angle = SystemAngle[type] || unit.UnitType == DestructUnit.Heli
                    ? (float)(Math.PI / 4)
                    : 0;
                unit.Power = unit.UnitType == DestructUnit.Laser ? 6 : 4;
                unit.ShotType = wallsPresent ? CpuWallWeapon[type] : CpuWeapon[type];
            }
        }
    }

    public static void ResetLevel(
        DestructMatchState match,
        IReadOnlyList<DestructAvailabilityState> shots,
        int maxShots,
        IReadOnlyList<DestructAvailabilityState> explosions,
        int maxExplosions,
        Action generateTerrain,
        bool wallsPresent)
    {
        ArgumentNullException.ThrowIfNull(generateTerrain);
        ResetWeapons(shots, maxShots, explosions, maxExplosions);
        generateTerrain();
        ResetAi(match, wallsPresent);
    }

    public static void PlayQueuedSounds(
        byte[] soundQueue,
        byte effectsVolume,
        SoundBank soundBank,
        Action<ReadOnlyMemory<short>, byte, byte> playSample)
    {
        ArgumentNullException.ThrowIfNull(soundQueue);
        ArgumentNullException.ThrowIfNull(soundBank);
        ArgumentNullException.ThrowIfNull(playSample);
        if (soundQueue.Length != 8)
            throw new ArgumentException("Destruct sound queue must have eight channels.", nameof(soundQueue));

        for (byte channel = 0; channel < soundQueue.Length; channel++)
        {
            var sample = soundQueue[channel];
            if (sample == NoSound)
                continue;

            var volume = channel == 7 ? effectsVolume : (byte)(effectsVolume / 2);
            soundBank.Play(sample, channel, volume, playSample);
            soundQueue[channel] = NoSound;
        }
    }

    public static void QueueExplosionSound(DestructMatchState match, uint sound)
    {
        ArgumentNullException.ThrowIfNull(match);
        match.ExplosionSoundChannel++;
        if (match.ExplosionSoundChannel > 5)
            match.ExplosionSoundChannel = 1;
        match.SoundQueue[match.ExplosionSoundChannel] = sound;
    }

    public static void MakeExplosion(
        DestructMatchState match,
        uint x,
        uint y,
        DestructShot shotType,
        Func<uint> nextRandom)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(nextRandom);
        if (match.MaximumExplosions > match.Explosions.Count)
            throw new ArgumentException("The Destruct explosion slots must match the configured maximum.", nameof(match));

        var index = 0;
        while (index < match.MaximumExplosions && !match.Explosions[(int)index].IsAvailable)
            index++;
        if (index == match.MaximumExplosions)
            return;

        var shotIndex = (int)shotType;
        if (shotType != DestructShot.Invalid && (uint)shotIndex >= ExplosionSize.Length)
            throw new ArgumentOutOfRangeException(nameof(shotType));

        var explosion = match.Explosions[(int)index];
        explosion.IsAvailable = false;
        explosion.X = x;
        explosion.Y = y;
        explosion.Width = 2;
        if (shotType == DestructShot.Invalid)
        {
            QueueExplosionSound(match, 4);
            explosion.MaximumWidth = nextRandom() % 40 + 10;
            explosion.Fill = nextRandom() % 60 + 20;
            explosion.Type = DestructExplosionType.Normal;
            return;
        }

        var maximumWidth = ExplosionSize[shotIndex];
        if (maximumWidth < 5)
            QueueExplosionSound(match, 3);
        else if (maximumWidth < 15)
            QueueExplosionSound(match, 4);
        else if (maximumWidth < 20)
            QueueExplosionSound(match, 12);
        else if (maximumWidth < 40)
            QueueExplosionSound(match, 11);
        else
        {
            QueueExplosionSound(match, 12);
            QueueExplosionSound(match, 11);
        }

        explosion.MaximumWidth = maximumWidth;
        explosion.Fill = ExplosionFill[shotIndex];
        explosion.Type = ShotExplosionType[shotIndex];
    }

    public static void DrawSuperPixel(IndexedSurface surface, uint centerX, uint centerY)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ReadOnlySpan<byte> pattern =
        [
            0, 0, 246, 0, 0,
            0, 247, 249, 247, 0,
            246, 249, 252, 249, 246,
            0, 247, 249, 247, 0,
            0, 0, 246, 0, 0
        ];
        ReadOnlySpan<byte> intensity =
        [
            0, 0, 1, 0, 0,
            0, 1, 2, 1, 0,
            1, 2, 4, 2, 1,
            0, 1, 2, 1, 0,
            0, 0, 1, 0, 0
        ];

        var pixels = surface.Pixels.Span;
        for (var y = 0; y < 5; y++)
        {
            var targetY = (long)centerY + y - 2;
            if (targetY < 0 || targetY >= surface.Height)
                continue;
            for (var x = 0; x < 5; x++)
            {
                var targetX = (long)centerX + x - 2;
                if (targetX < 0 || targetX >= surface.Pitch)
                    continue;
                var patternIndex = y * 5 + x;
                if (pattern[patternIndex] == 0)
                    continue;

                var pixelIndex = checked((int)(targetY * surface.Pitch + targetX));
                var current = pixels[pixelIndex];
                if (current < pattern[patternIndex])
                    pixels[pixelIndex] = pattern[patternIndex];
                else
                    pixels[pixelIndex] = (byte)Math.Min(byte.MaxValue, current + intensity[patternIndex]);
            }
        }
    }

    public static void DrawCoolPixel(IndexedSurface surface, int x, int y, byte color)
    {
        ArgumentNullException.ThrowIfNull(surface);
        var edgeColor = unchecked((byte)(color - 2));
        surface.Pixel(x, y, color);
        surface.Pixel(x - 1, y, edgeColor);
        surface.Pixel(x + 1, y, edgeColor);
        surface.Pixel(x, y - 1, edgeColor);
        surface.Pixel(x, y + 1, edgeColor);
    }

    public static void DrawWalls(
        IReadOnlyList<DestructWallState> walls,
        int maxWalls,
        Action<uint, uint, int> drawSprite)
    {
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(drawSprite);
        if (maxWalls < 0)
            throw new ArgumentOutOfRangeException(nameof(maxWalls));
        if (walls.Count < maxWalls)
            throw new ArgumentException("The Destruct wall list must have the configured capacity.", nameof(walls));

        for (var index = 0; index < maxWalls; index++)
        {
            var wall = walls[index];
            if (wall.Exists)
                drawSprite(wall.X, wall.Y, 42);
        }
    }

    public static void DrawCrosshairs(
        IndexedSurface surface,
        DestructMatchState match,
        int maxInstallations)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(match);
        if (maxInstallations <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstallations));

        for (var playerIndex = 0; playerIndex < match.Players.Length; playerIndex++)
        {
            var player = match.Players[playerIndex];
            if (player.Units.Count < maxInstallations || player.UnitSelected >= maxInstallations)
                throw new InvalidOperationException("Each Destruct player must have a valid selected installation.");

            var unit = player.Units[(int)player.UnitSelected];
            int x;
            int y;
            if (unit.UnitType == DestructUnit.Heli)
            {
                var move = unit.LastMove;
                x = (int)(unit.X + RoundNative(0.1f * move * move * move)) + 5;
                y = RoundNative(unit.Y) + 1;
            }
            else
            {
                var direction = playerIndex == 0 ? -1 : 1;
                var offset = (unit.Power * 8 + 7);
                x = RoundNative(unit.X + 6 - MathF.Cos(unit.Angle) * offset * direction);
                y = RoundNative(unit.Y - 7 - MathF.Sin(unit.Angle) * offset);
            }

            if (y > 9)
            {
                if (y > 11)
                {
                    if (y > 13)
                        surface.Pixel(x, y - 2, 3);
                    surface.Pixel(x + 3, y, 3);
                    surface.Pixel(x, y, 14);
                    surface.Pixel(x - 3, y, 3);
                }
                surface.Pixel(x, y + 2, 3);
            }
        }
    }

    public static void DrawHud(
        IndexedSurface surface,
        DestructMatchState match,
        IReadOnlyList<string> weaponNames,
        Action<int, int, int> drawSprite,
        Action<int, int, string, int, int> drawText)
    {
        ArgumentNullException.ThrowIfNull(surface);
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(weaponNames);
        ArgumentNullException.ThrowIfNull(drawSprite);
        ArgumentNullException.ThrowIfNull(drawText);
        if (surface.Width < 320 || surface.Height < 11)
            throw new ArgumentException("The Destruct HUD requires at least a 320 by 11 surface.", nameof(surface));
        if (weaponNames.Count < 17)
            throw new ArgumentException("The Destruct HUD requires all 17 weapon names.", nameof(weaponNames));

        for (var playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            var player = match.Players[playerIndex];
            if (player.UnitSelected >= player.Units.Count)
                throw new InvalidOperationException("Each Destruct player must have a valid selected installation.");
            var unit = player.Units[(int)player.UnitSelected];
            if ((uint)unit.ShotType >= 17)
                throw new InvalidOperationException("The selected Destruct unit must have a valid weapon for HUD display.");

            var startX = playerIndex == 0 ? 0 : 320 - 150;
            surface.FillRectangle(startX + 5, 3, startX + 14, 8, 241);
            surface.Rectangle(startX + 4, 2, startX + 15, 9, 242);
            surface.Rectangle(startX + 3, 1, startX + 16, 10, 240);
            surface.FillRectangle(startX + 18, 3, startX + 140, 8, 241);
            surface.Rectangle(startX + 17, 2, startX + 143, 9, 242);
            surface.Rectangle(startX + 16, 1, startX + 144, 10, 240);

            drawSprite(startX + 4, 0, 191 + (int)unit.ShotType);
            drawText(startX + 20, 3, weaponNames[(int)unit.ShotType], 15, 2);
            drawText(startX + 75, 3, string.Create(
                System.Globalization.CultureInfo.InvariantCulture, $"dmg~{unit.Health}~"), 15, 0);
            drawText(startX + 110, 3, string.Create(
                System.Globalization.CultureInfo.InvariantCulture, $"pts~{unchecked((int)player.Score)}~"), 15, 0);
        }
    }

    public static void DrawUnit(int playerIndex, DestructUnitState unit, Action<uint, int, int> drawSprite)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(drawSprite);
        if ((uint)playerIndex >= 2)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        if (unit.UnitType is < DestructUnit.Tank or > DestructUnit.Heli)
            throw new ArgumentOutOfRangeException(nameof(unit), "The unit must have a valid Destruct type.");

        var animationIndex = (uint)(GraphicBase[playerIndex, (int)unit.UnitType] + unit.AnimationFrame);
        if (unit.UnitType == DestructUnit.Heli)
        {
            if (unit.LastMove < -2)
                animationIndex += 5;
            else if (unit.LastMove > 2)
                animationIndex += 10;
        }
        else
        {
            animationIndex += (uint)MathF.Floor(unit.Angle * 9.99f / MathF.PI);
        }

        var y = RoundNative(unit.Y) - 13;
        drawSprite(unit.X, y, checked((int)animationIndex));
    }

    public static void LowerUnit(DestructUnitState unit, Func<uint, uint, bool> stabilityCheck)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(stabilityCheck);
        if (unit.Y >= GroundY || !stabilityCheck(unit.X, (uint)RoundNative(unit.Y)))
            return;

        if (unit.UnitType == DestructUnit.Heli)
        {
            unit.YMovement = 1.5f;
            unit.Y += 0.2f;
        }
        else
        {
            unit.Y += 1;
        }

        if (unit.Y > GroundY)
            unit.Y = GroundY;
    }

    public static bool IsStableAt(IndexedSurface surface, uint x, uint y)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if (x == 0 || (ulong)x + 10 >= (uint)surface.Pitch)
            throw new ArgumentOutOfRangeException(nameof(x), "The Destruct stability probe must fit within the surface.");
        if (y >= surface.Height)
            throw new ArgumentOutOfRangeException(nameof(y));

        var pixels = surface.Pixels.Span;
        var start = checked((int)((ulong)y * (uint)surface.Pitch + x - 1));
        var dirtPixels = 0;
        for (var index = 0; index < 12; index++)
        {
            if (pixels[start + index] == 25)
                dirtPixels++;
        }
        return dirtPixels < 10;
    }

    public static uint PlaceTerrain(IList<uint> world, uint passedX, uint width)
    {
        ArgumentNullException.ThrowIfNull(world);
        var end = (ulong)passedX + width;
        if (width == 0 || end > (ulong)world.Count)
            throw new ArgumentOutOfRangeException(nameof(width), "The terrain placement range must fit within the world map.");

        var newY = 0u;
        for (var index = (int)passedX; index < (int)end; index++)
            newY = Math.Max(newY, world[index]);
        for (var index = (int)passedX; index < (int)end; index++)
            world[index] = newY;
        return newY;
    }

    public static void GenerateUnits(
        IReadOnlyList<DestructPlayerState> players,
        IReadOnlyList<DestructUnitRoster> rosters,
        IList<uint> baseWorld,
        Func<uint> nextRandom)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(rosters);
        ArgumentNullException.ThrowIfNull(baseWorld);
        ArgumentNullException.ThrowIfNull(nextRandom);
        if (players.Count != 2 || rosters.Count != 2)
            throw new ArgumentException("Destruct unit generation requires exactly two players and rosters.");
        if (baseWorld.Count < 320)
            throw new ArgumentException("Destruct unit generation requires 320 terrain heights.", nameof(baseWorld));

        for (var playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            var roster = rosters[playerIndex] ??
                throw new ArgumentException("Destruct unit rosters cannot be null.", nameof(rosters));
            var player = players[playerIndex] ??
                throw new ArgumentException("Destruct players cannot be null.", nameof(players));
            if (roster.InstallationCount > 8)
                throw new ArgumentOutOfRangeException(nameof(rosters), "A Destruct roster cannot exceed eight units.");
            player.UnitsRemaining = 0;
            uint satellites = 0;

            for (var unitIndex = 0u; unitIndex < roster.InstallationCount; unitIndex++)
            {
                var x = playerIndex == 0
                    ? nextRandom() % 120 + 10
                    : 320 - (nextRandom() % 120 + 22);
                var y = PlaceTerrain(baseWorld, x - 1, 14);
                var unitType = roster.TypeChoices[nextRandom() % 10];
                var typeIndex = (int)unitType;
                if ((uint)typeIndex >= DestructUnitNames.UnitCount)
                    throw new ArgumentOutOfRangeException(nameof(rosters), "A roster contains an invalid Destruct unit type.");

                if (unitType == DestructUnit.Satellite)
                {
                    if (satellites == roster.InstallationCount)
                    {
                        unitType = DestructUnit.Tank;
                        typeIndex = (int)unitType;
                        player.UnitsRemaining++;
                    }
                    else
                    {
                        y = 30 + nextRandom() % 40;
                        satellites++;
                    }
                }
                else
                {
                    player.UnitsRemaining++;
                }

                while (player.Units.Count <= unitIndex)
                    player.Units.Add(new DestructUnitState());
                var unit = player.Units[(int)unitIndex];
                unit.X = x;
                unit.Y = y;
                unit.UnitType = unitType;
                unit.LastMove = 0;
                unit.YMovement = 0;
                unit.IsInAir = false;
                unit.Angle = 0;
                unit.Power = unitType == DestructUnit.Laser ? 6 : 3;
                unit.ShotType = DefaultWeapon[typeIndex];
                unit.Health = BaseDamage[typeIndex];
                unit.AnimationFrame = 0;
            }
        }
    }

    public static void GenerateWalls(
        IReadOnlyList<DestructPlayerState> players,
        IList<DestructWallState> walls,
        IList<uint> baseWorld,
        int maximumInstallations,
        int minimumWalls,
        int maximumWalls,
        bool wallsEnabled,
        Func<uint> nextCRandom,
        Func<uint> nextGameRandom)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(baseWorld);
        ArgumentNullException.ThrowIfNull(nextCRandom);
        ArgumentNullException.ThrowIfNull(nextGameRandom);
        if (players.Count != 2 || maximumInstallations < 0 ||
            walls.Count < maximumWalls || maximumWalls < 0 ||
            minimumWalls < 0 || minimumWalls > maximumWalls)
            throw new ArgumentException("Invalid Destruct wall configuration or state capacity.");
        if (baseWorld.Count < 320)
            throw new ArgumentException("Destruct wall generation requires 320 terrain heights.", nameof(baseWorld));

        if (!wallsEnabled)
        {
            for (var index = 0; index < maximumWalls; index++)
                walls[index].Exists = false;
            return;
        }

        foreach (var player in players)
        {
            if (player.Units.Count < maximumInstallations)
                throw new ArgumentException("Each player must have the configured installation capacity.", nameof(players));
        }

        var remainingWalls = (int)(nextCRandom() % (uint)(maximumWalls - minimumWalls + 1) + (uint)minimumWalls);
        do
        {
            var wallHeight = (int)(nextGameRandom() % 5 + 1);
            if (wallHeight > remainingWalls)
                wallHeight = remainingWalls;

            uint wallX;
            var tries = 0;
            bool isGood;
            do
            {
                isGood = true;
                wallX = nextGameRandom() % 300 + 10;
                for (var playerIndex = 0; playerIndex < 2 && isGood; playerIndex++)
                for (var unitIndex = 0; unitIndex < maximumInstallations; unitIndex++)
                {
                    var unitX = players[playerIndex].Units[unitIndex].X;
                    if (wallX > unchecked(unitX - 12) && wallX < unchecked(unitX + 13))
                    {
                        isGood = false;
                        break;
                    }
                }
                tries++;
            } while (!isGood && tries < 5);

            for (var index = 1; index <= wallHeight; index++)
            {
                var wall = walls[remainingWalls - index];
                wall.Exists = true;
                wall.X = wallX;
                wall.Y = PlaceTerrain(baseWorld, wallX, 12) - (uint)(14 * index);
            }
            remainingWalls -= wallHeight;
        } while (remainingWalls != 0);
    }

    public static void GenerateBaseTerrain(
        DestructMapFlags mapFlags,
        IList<uint> baseWorld,
        Func<float> nextRandomFloat)
    {
        ArgumentNullException.ThrowIfNull(baseWorld);
        ArgumentNullException.ThrowIfNull(nextRandomFloat);
        if (baseWorld.Count <= 318)
            throw new ArgumentException("The Destruct base map must contain at least 319 heights.", nameof(baseWorld));

        var sineWave = (float)(nextRandomFloat() * Math.PI / 50 + 0.01f);
        var sineWave2 = (float)(nextRandomFloat() * Math.PI / 50 + 0.01f);
        var cosineWave = (float)(nextRandomFloat() * Math.PI / 50 + 0.01f);
        var cosineWave2 = (float)(nextRandomFloat() * Math.PI / 50 + 0.01f);
        var heightMultiplier = 20;
        if ((mapFlags & DestructMapFlags.Fuzzy) != 0)
        {
            sineWave = (float)(Math.PI - nextRandomFloat() * 0.3f);
            sineWave2 = (float)(Math.PI - nextRandomFloat() * 0.3f);
        }
        if ((mapFlags & DestructMapFlags.Tall) != 0)
            heightMultiplier = 100;

        for (var x = 1; x <= 318; x++)
        {
            var height = MathF.Sin(sineWave * x) * heightMultiplier +
                MathF.Sin(sineWave2 * x) * 15 +
                MathF.Cos(cosineWave * x) * 10 +
                MathF.Sin(cosineWave2 * x) * 15;
            var newHeight = (int)MathF.Round(height, MidpointRounding.AwayFromZero) + 130;
            baseWorld[x] = (uint)Math.Clamp(newHeight, 40, 195);
        }
    }

    public static void GenerateTerrain(
        DestructMatchState match,
        IReadOnlyList<DestructUnitRoster> rosters,
        IList<uint> baseWorld,
        IList<DestructWallState> walls,
        IndexedSurface screen,
        IndexedSurface temporaryScreen,
        int maximumInstallations,
        int minimumWalls,
        int maximumWalls,
        IReadOnlyList<uint> goodSongs,
        Func<uint> nextGameRandom,
        Func<uint> nextCRandom,
        Func<float> nextRandomFloat,
        Action<uint> playSong,
        Action showScreen)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(rosters);
        ArgumentNullException.ThrowIfNull(baseWorld);
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(temporaryScreen);
        ArgumentNullException.ThrowIfNull(goodSongs);
        ArgumentNullException.ThrowIfNull(nextGameRandom);
        ArgumentNullException.ThrowIfNull(nextCRandom);
        ArgumentNullException.ThrowIfNull(nextRandomFloat);
        ArgumentNullException.ThrowIfNull(playSong);
        ArgumentNullException.ThrowIfNull(showScreen);
        if (goodSongs.Count < 14)
            throw new ArgumentException("Destruct terrain requires fourteen song choices.", nameof(goodSongs));
        if (screen.Pitch != temporaryScreen.Pitch || screen.Height != temporaryScreen.Height)
            throw new ArgumentException("Destruct play and temporary screens must have matching pitch and height.");

        var flags = DestructMapFlags.Normal;
        if (nextGameRandom() % 2 == 0)
            flags |= DestructMapFlags.Walls;
        if (nextGameRandom() % 4 == 0)
            flags |= DestructMapFlags.Holes;
        switch (nextGameRandom() % 4)
        {
            case 0: flags |= DestructMapFlags.Fuzzy; break;
            case 1: flags |= DestructMapFlags.Tall; break;
            case 2: flags |= DestructMapFlags.Rings; break;
        }
        match.MapFlags = flags;
        playSong(unchecked(goodSongs[(int)(nextGameRandom() % 14)] - 1));

        GenerateBaseTerrain(flags, baseWorld, nextRandomFloat);
        GenerateUnits(match.Players, rosters, baseWorld, nextGameRandom);
        GenerateWalls(match.Players, walls, baseWorld, maximumInstallations,
            minimumWalls, maximumWalls, (flags & DestructMapFlags.Walls) != 0,
            nextCRandom, nextGameRandom);
        DrawBaseTerrain(screen, baseWorld);
        if ((flags & DestructMapFlags.Rings) != 0)
            GenerateRings(screen, 25, nextGameRandom, nextRandomFloat);
        if ((flags & DestructMapFlags.Holes) != 0)
            GenerateRings(screen, 0, nextGameRandom, nextRandomFloat);
        AliasDirt(screen);
        showScreen();
        screen.Pixels.Span.CopyTo(temporaryScreen.Pixels.Span);
    }

    public static void DrawBaseTerrain(IndexedSurface screen, IList<uint> baseWorld)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(baseWorld);
        if (baseWorld.Count <= 318 || screen.Pitch <= 318 || screen.Height <= 199)
            throw new ArgumentException("Destruct terrain drawing requires 319 heights and a 319-by-200 surface.");

        for (var x = 1; x <= 318; x++)
        {
            if (baseWorld[x] > 199)
                throw new ArgumentOutOfRangeException(nameof(baseWorld), "Terrain heights must fit within the playfield.");
            screen.Rectangle(x, (int)baseWorld[x], x, 199, 25);
        }
    }

    public static void GenerateRings(
        IndexedSurface screen,
        byte pixel,
        Func<uint> nextRandom,
        Func<float> nextRandomFloat)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(nextRandom);
        ArgumentNullException.ThrowIfNull(nextRandomFloat);
        if (screen.Pitch < 320 || screen.Height < 200)
            throw new ArgumentException("Destruct rings require a 320-by-200 screen.", nameof(screen));

        var rings = nextRandom() % 6 + 1;
        var pixels = screen.Pixels.Span;
        for (var ring = 0u; ring < rings; ring++)
        {
            var centerX = (int)(nextRandom() % 320);
            var centerY = (int)(nextRandom() % 160 + 20);
            var size = nextRandom() % 40 + 10;
            var pointCount = size * size * 2;
            for (var point = 0u; point < pointCount; point++)
            {
                var angle = (float)(nextRandomFloat() * 2 * Math.PI);
                var radialY = nextRandomFloat() * 0.1f + 0.9f;
                var radialX = nextRandomFloat() * 0.1f + 0.9f;
                var y = centerY + (int)MathF.Round(MathF.Cos(angle) * radialY * size,
                    MidpointRounding.AwayFromZero);
                var x = centerX + (int)MathF.Round(MathF.Sin(angle) * radialX * size,
                    MidpointRounding.AwayFromZero);
                if (y > 12 && y < 200 && x > 0 && x < 319)
                    pixels[y * screen.Pitch + x] = pixel;
            }
        }
    }

    public static void TestExplosionCollision(
        IReadOnlyList<DestructPlayerState> players,
        int maximumInstallations,
        uint x,
        uint y,
        Action<int, DestructUnitState> destroyUnit)
    {
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(destroyUnit);
        if (players.Count != 2 || maximumInstallations < 0)
            throw new ArgumentException("Destruct collision testing requires two players and a valid installation count.");

        for (var playerIndex = 0; playerIndex < 2; playerIndex++)
        {
            var player = players[playerIndex] ??
                throw new ArgumentException("Destruct players cannot be null.", nameof(players));
            if (player.Units.Count < maximumInstallations)
                throw new ArgumentException("Each player must have the configured installation capacity.", nameof(players));
            for (var unitIndex = 0; unitIndex < maximumInstallations; unitIndex++)
            {
                var unit = player.Units[unitIndex];
                if (unit.Health <= 0 || x <= unit.X || (ulong)x >= (ulong)unit.X + 11 ||
                    y >= unit.Y || (double)y <= unit.Y - 11)
                    continue;

                unit.Health--;
                if (unit.Health <= 0)
                    destroyUnit(playerIndex, unit);
            }
        }
    }

    public static void DrawTrails(
        DestructShotState shot,
        uint count,
        uint decay,
        uint startColor,
        Action<uint, uint, byte> drawCoolPixel)
    {
        ArgumentNullException.ThrowIfNull(shot);
        ArgumentNullException.ThrowIfNull(drawCoolPixel);
        if (count > shot.TrailX.Length)
            throw new ArgumentOutOfRangeException(nameof(count));

        for (var index = (int)count - 1; index >= 0; index--)
        {
            if (shot.TrailColor[index] > 0 && shot.TrailY[index] > 12)
                drawCoolPixel(shot.TrailX[index], shot.TrailY[index], unchecked((byte)shot.TrailColor[index]));

            if (index == 0)
            {
                shot.TrailX[index] = unchecked((uint)MathF.Round(shot.X, MidpointRounding.AwayFromZero));
                shot.TrailY[index] = unchecked((uint)MathF.Round(shot.Y, MidpointRounding.AwayFromZero));
                shot.TrailColor[index] = startColor;
            }
            else
            {
                shot.TrailX[index] = shot.TrailX[index - 1];
                shot.TrailY[index] = shot.TrailY[index - 1];
                if (shot.TrailColor[index - 1] > 0)
                    shot.TrailColor[index] = unchecked(shot.TrailColor[index - 1] - decay);
            }
        }
    }

    public static void MakeShot(
        DestructMatchState match,
        int playerIndex,
        DestructUnitState unit,
        int direction,
        IList<DestructShotState> shots,
        int maximumShots,
        bool jumperStraight)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(shots);
        if ((uint)playerIndex >= 2)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        if (maximumShots < 0 || shots.Count < maximumShots)
            throw new ArgumentOutOfRangeException(nameof(maximumShots));

        var shotIndex = -1;
        for (var index = 0; index < maximumShots; index++)
        {
            if (shots[index].IsAvailable)
            {
                shotIndex = index;
                break;
            }
        }
        if (shotIndex < 0)
            return;

        var typeIndex = (int)unit.UnitType;
        var weaponIndex = (int)unit.ShotType;
        if ((uint)typeIndex >= DestructUnitNames.UnitCount)
            throw new ArgumentOutOfRangeException(nameof(unit), "Invalid Destruct unit type.");
        if ((uint)weaponIndex >= ShotSound.Length)
            throw new ArgumentOutOfRangeException(nameof(unit), "Invalid Destruct shot type.");
        if (unit.UnitType == DestructUnit.Heli && !unit.IsInAir)
            return;

        var shot = shots[shotIndex];
        match.SoundQueue[playerIndex] = ShotSound[weaponIndex];
        if (unit.UnitType == DestructUnit.Heli)
        {
            shot.X = unit.X + unit.LastMove * 2 + 5;
            shot.XMovement = 0.02f * unit.LastMove * unit.LastMove * unit.LastMove;
            if (match.Players[playerIndex].Actions[3] && unit.Y < 30)
            {
                shot.Y = unit.Y;
                shot.YMovement = 0.1f;
                if (shot.XMovement < 0)
                    shot.XMovement += 0.1f;
                else if (shot.XMovement > 0)
                    shot.XMovement -= 0.1f;
            }
            else
            {
                shot.Y = unit.Y + 1;
                shot.YMovement = 0.5f + unit.YMovement * 0.1f;
            }
        }
        else if (unit.UnitType == DestructUnit.Jumper && !jumperStraight)
        {
            shot.X = unit.X + 2;
            shot.XMovement = -MathF.Cos(unit.Angle) * unit.Power * direction;
            if (unit.IsInAir)
            {
                shot.YMovement = 1;
                shot.Y = unit.Y + 2;
            }
            else
            {
                shot.YMovement = -2;
                shot.Y = unit.Y - 12;
            }
        }
        else
        {
            shot.X = unit.X + 6 - MathF.Cos(unit.Angle) * 10 * direction;
            shot.Y = unit.Y - 7 - MathF.Sin(unit.Angle) * 10;
            shot.XMovement = -MathF.Cos(unit.Angle) * unit.Power * direction;
            shot.YMovement = -MathF.Sin(unit.Angle) * unit.Power;
        }

        shot.IsAvailable = false;
        shot.ShotType = unit.ShotType;
        Array.Clear(shot.TrailColor);
    }

    public static void RunTickShots(
        DestructMatchState match,
        IList<DestructShotState> shots,
        int maximumShots,
        IList<DestructWallState> walls,
        int maximumWalls,
        int maximumInstallations,
        IndexedSurface temporaryScreen,
        Func<float> nextRandomFloat,
        Action<int, int, DestructShot> makeExplosion,
        Action<uint, uint, byte> drawCoolPixel)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(shots);
        ArgumentNullException.ThrowIfNull(walls);
        ArgumentNullException.ThrowIfNull(temporaryScreen);
        ArgumentNullException.ThrowIfNull(nextRandomFloat);
        ArgumentNullException.ThrowIfNull(makeExplosion);
        ArgumentNullException.ThrowIfNull(drawCoolPixel);
        if (maximumShots < 0 || shots.Count < maximumShots ||
            maximumWalls < 0 || walls.Count < maximumWalls || maximumInstallations < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumShots));
        foreach (var player in match.Players)
        {
            if (player.Units.Count < maximumInstallations)
                throw new ArgumentException("Each player must have the configured installation capacity.", nameof(match));
        }

        for (var shotIndex = 0; shotIndex < maximumShots; shotIndex++)
        {
            var shot = shots[shotIndex];
            if (shot.IsAvailable)
                continue;
            var typeIndex = (int)shot.ShotType;
            if ((uint)typeIndex >= ShotSound.Length)
                throw new ArgumentOutOfRangeException(nameof(shots), "An active Destruct shot has an invalid type.");

            shot.X += shot.XMovement;
            shot.Y += shot.YMovement;
            if (ShotBounces[typeIndex])
            {
                if (shot.Y > 199 || shot.Y < 14)
                {
                    shot.Y -= shot.YMovement;
                    shot.YMovement = -shot.YMovement;
                }
                if (shot.X < 1 || shot.X > 318)
                {
                    shot.X -= shot.XMovement;
                    shot.XMovement = -shot.XMovement;
                }
            }
            else
            {
                shot.YMovement += 0.05f;
                if (shot.Y > 199)
                {
                    shot.Y -= shot.YMovement;
                    shot.YMovement = -shot.YMovement * 0.8f;
                    if (shot.XMovement == 0)
                        shot.XMovement += nextRandomFloat() - 0.5f;
                }
            }

            if (shot.X > 318 || shot.X < 1)
            {
                shot.IsAvailable = true;
                continue;
            }
            if (shot.Y <= 14)
                continue;

            var positionX = unchecked((uint)MathF.Round(shot.X, MidpointRounding.AwayFromZero));
            var positionY = unchecked((uint)MathF.Round(shot.Y, MidpointRounding.AwayFromZero));
            for (var playerIndex = 0; playerIndex < 2; playerIndex++)
            {
                for (var unitIndex = 0; unitIndex < maximumInstallations; unitIndex++)
                {
                    var unit = match.Players[playerIndex].Units[unitIndex];
                    if (unit.Health <= 0 || positionX <= unit.X || (double)positionX >= unit.X + 11 ||
                        positionY >= unit.Y || (double)positionY <= unit.Y - 13)
                        continue;
                    shot.IsAvailable = true;
                    makeExplosion((int)positionX, (int)positionY, shot.ShotType);
                }
            }

            var trailColor = (ShotColor[typeIndex] << 4) - 3;
            drawCoolPixel(positionX, positionY, unchecked((byte)trailColor));
            switch (ShotTrailType[typeIndex])
            {
                case 1:
                    DrawTrails(shot, 2, 4, unchecked((uint)(trailColor - 3)), drawCoolPixel);
                    break;
                case 2:
                    DrawTrails(shot, 4, 3, unchecked((uint)(trailColor - 1)), drawCoolPixel);
                    break;
            }

            for (var wallIndex = 0; wallIndex < maximumWalls; wallIndex++)
            {
                var wall = walls[wallIndex];
                if (!wall.Exists || positionX < wall.X || positionX > wall.X + 11 ||
                    positionY < wall.Y || positionY > wall.Y + 14)
                    continue;
                if (ShotDemolishesWalls[typeIndex])
                {
                    wall.Exists = false;
                    shot.IsAvailable = true;
                    makeExplosion((int)positionX, (int)positionY, shot.ShotType);
                    continue;
                }

                if (shot.X - shot.XMovement < wall.X || shot.X - shot.XMovement > wall.X + 11)
                    shot.XMovement = -shot.XMovement;
                if (shot.Y - shot.YMovement < wall.Y || shot.Y - shot.YMovement > wall.Y + 14)
                    shot.YMovement = shot.YMovement < 0 ? -shot.YMovement : -shot.YMovement * 0.8f;
                positionX = unchecked((uint)MathF.Round(shot.X, MidpointRounding.AwayFromZero));
                positionY = unchecked((uint)MathF.Round(shot.Y, MidpointRounding.AwayFromZero));
            }

            var pixelIndex = (long)positionX + (long)positionY * temporaryScreen.Pitch;
            if ((ulong)pixelIndex >= (ulong)temporaryScreen.Pixels.Length)
                throw new InvalidOperationException("A Destruct shot position is outside the temporary screen buffer.");
            if (temporaryScreen.Pixels.Span[(int)pixelIndex] != 25)
                continue;
            shot.IsAvailable = true;
            makeExplosion((int)positionX, (int)positionY, shot.ShotType);
        }
    }

    public static void RunTickAi(
        DestructMatchState match,
        int maximumInstallations,
        Func<uint> nextRandom)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(nextRandom);
        if (maximumInstallations < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumInstallations));
        foreach (var player in match.Players)
        {
            if (player.Units.Count < maximumInstallations)
                throw new ArgumentException("Each player must have the configured installation capacity.", nameof(match));
            if (player.IsCpu && (maximumInstallations == 0 || player.UnitSelected >= maximumInstallations))
                throw new InvalidOperationException("A CPU player must have a selected installation.");
        }

        for (var playerIndex = 0; playerIndex < match.Players.Length; playerIndex++)
        {
            var player = match.Players[playerIndex];
            if (!player.IsCpu)
                continue;

            var target = match.Players[playerIndex == 0 ? 1 : 0];
            var currentUnit = player.Units[(int)player.UnitSelected];
            if (player.Ai.NoDown > 0)
                player.Ai.NoDown--;

            if (nextRandom() % 100 > 80)
                player.Ai.Angle = Math.Clamp(player.Ai.Angle + (int)(nextRandom() % 3) - 1, -1, 1);
            if (nextRandom() % 100 > 90 &&
                ((player.Ai.Angle > 0 && currentUnit.Angle > Math.PI / 2 - Math.PI / 9) ||
                 (player.Ai.Angle < 0 && currentUnit.Angle < Math.PI / 8)))
                player.Ai.Angle = 0;

            if (nextRandom() % 100 > 93)
                player.Ai.Power = Math.Clamp(player.Ai.Power + (int)(nextRandom() % 3) - 1, -1, 1);
            if (nextRandom() % 100 > 90)
            {
                if ((player.Ai.Power > 0 && currentUnit.Power > 4) ||
                    (player.Ai.Power < 0 && currentUnit.Power < 3))
                    player.Ai.Power = 0;
                else if (currentUnit.Power < 2)
                    player.Ai.Power = 1;
            }

            for (var unitIndex = 0; unitIndex < maximumInstallations; unitIndex++)
            {
                var unit = player.Units[unitIndex];
                if (!IsValid(unit) || unit.UnitType != DestructUnit.Heli)
                    continue;
                player.UnitSelected = (uint)unitIndex;
                break;
            }

            if (currentUnit.UnitType == DestructUnit.Heli)
            {
                if (!currentUnit.IsInAir)
                    player.Ai.Power = 1;
                if (currentUnit.X == 0)
                    throw new InvalidOperationException("A CPU helicopter must have a nonzero X coordinate.");
                if (nextRandom() % currentUnit.X > 100)
                    player.Ai.Power = 1;
                if (nextRandom() % 240 > currentUnit.X)
                    player.Actions[1] = true;
                else if (nextRandom() % 20 + 300 < currentUnit.X)
                    player.Actions[0] = true;
                else if (nextRandom() % 30 == 1)
                    player.Ai.Angle = (int)(nextRandom() % 3) - 1;

                if (currentUnit.X > 295 && currentUnit.LastMove > 1)
                {
                    player.Actions[0] = true;
                    player.Actions[1] = false;
                }

                if (currentUnit.LastMove > 3 || (currentUnit.X > 160 && currentUnit.LastMove > -3))
                {
                    var roundedY = MathF.Round(currentUnit.Y, MidpointRounding.AwayFromZero);
                    if (roundedY <= 0)
                        throw new InvalidOperationException("A CPU helicopter must have a positive Y coordinate.");
                    if (nextRandom() % (uint)roundedY < 150 && currentUnit.YMovement < 0.01f &&
                        (currentUnit.X < 160 || currentUnit.LastMove < 2))
                        player.Actions[5] = true;
                    var speed = 5 - Math.Abs(currentUnit.LastMove);
                    player.Ai.NoDown = (uint)(speed * speed + 3);
                    player.Ai.Power = 1;
                }
                else
                {
                    player.Actions[5] = false;
                }

                for (var unitIndex = 0; unitIndex < maximumInstallations; unitIndex++)
                {
                    var targetUnit = target.Units[unitIndex];
                    if (Math.Abs((int)targetUnit.X - (int)currentUnit.X) >= 8)
                        continue;
                    if (targetUnit.UnitType == DestructUnit.Satellite)
                        player.Actions[5] = false;
                    else
                    {
                        player.Actions[0] = false;
                        player.Actions[1] = false;
                        if (currentUnit.LastMove < -1)
                            currentUnit.LastMove++;
                        else if (currentUnit.LastMove > 1)
                            currentUnit.LastMove--;
                    }
                }
            }
            else
            {
                player.Actions[5] = true;
            }

            if (nextRandom() % 200 > 198)
            {
                player.Actions[4] = true;
                player.Ai.Angle = 0;
                player.Ai.Power = 0;
                player.Ai.Fire = 0;
            }
            if (nextRandom() % 100 > 98 || currentUnit.ShotType == DestructShot.Tracer)
                player.Actions[7] = true;
            if (player.Ai.Angle > 0)
                player.Actions[0] = true;
            if (player.Ai.Angle < 0)
                player.Actions[1] = true;
            if (player.Ai.Power > 0)
                player.Actions[2] = true;
            if (player.Ai.Power < 0 && player.Ai.NoDown == 0)
                player.Actions[3] = true;
            if (player.Ai.Fire > 0)
                player.Actions[5] = true;
            if (currentUnit.YMovement < -0.1f && currentUnit.UnitType == DestructUnit.Heli)
                player.Actions[5] = false;
            if (currentUnit.UnitType == DestructUnit.Laser || currentUnit.IsInAir)
                player.Ai.Power = 0;
        }
    }

    public static void RunTickGetInput(
        DestructMatchState match,
        bool[] activeKeys,
        Action<bool> serviceEvents)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(activeKeys);
        ArgumentNullException.ThrowIfNull(serviceEvents);
        serviceEvents(true);

        foreach (var player in match.Players)
        for (var actionIndex = 0; actionIndex < player.KeyConfig.GetLength(0); actionIndex++)
        for (var keySlot = 0; keySlot < player.KeyConfig.GetLength(1); keySlot++)
        {
            var key = player.KeyConfig[actionIndex, keySlot];
            if (key == 0)
                break;
            if ((uint)key >= activeKeys.Length || !activeKeys[key])
                continue;

            player.Actions[actionIndex] = true;
            if (actionIndex is 4 or 6 or 7)
                activeKeys[key] = false;
            break;
        }
    }

    public static void ProcessInput(
        DestructMatchState match,
        int maximumInstallations,
        IList<DestructShotState> shots,
        int maximumShots,
        IReadOnlyList<bool> jumperStraight,
        Func<uint, uint, bool> stabilityCheck,
        Action<int, DestructUnitState> runMagnet,
        bool enableTracerLaser = false)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(shots);
        ArgumentNullException.ThrowIfNull(jumperStraight);
        ArgumentNullException.ThrowIfNull(stabilityCheck);
        ArgumentNullException.ThrowIfNull(runMagnet);
        if (maximumInstallations < 0 || maximumShots < 0 || shots.Count < maximumShots ||
            jumperStraight.Count < match.Players.Length)
            throw new ArgumentOutOfRangeException(nameof(maximumInstallations));

        for (var playerIndex = 0; playerIndex < match.Players.Length; playerIndex++)
        {
            var player = match.Players[playerIndex];
            if (player.UnitsRemaining == 0)
                continue;
            if (player.Units.Count < maximumInstallations || maximumInstallations == 0 ||
                player.UnitSelected >= maximumInstallations)
                throw new InvalidOperationException("A living Destruct player must have a valid selected installation.");

            var direction = playerIndex == 0 ? -1 : 1;
            var unit = player.Units[(int)player.UnitSelected];
            var unitTypeIndex = (int)unit.UnitType;
            if ((uint)unitTypeIndex >= SystemAngle.Length)
                throw new ArgumentOutOfRangeException(nameof(match), "A selected Destruct unit has an invalid type.");

            if (SystemAngle[unitTypeIndex])
            {
                if (player.Actions[0])
                {
                    if (playerIndex == 0)
                        unit.RaiseAngle();
                    else
                        unit.LowerAngle();
                }
                if (player.Actions[1])
                {
                    if (playerIndex == 0)
                        unit.LowerAngle();
                    else
                        unit.RaiseAngle();
                }
            }
            else if (unit.UnitType == DestructUnit.Heli)
            {
                if (player.Actions[0] && unit.X > 5 &&
                    stabilityCheck(unit.X - 5, unchecked((uint)MathF.Round(unit.Y, MidpointRounding.AwayFromZero))))
                {
                    if (unit.LastMove > -5)
                        unit.LastMove--;
                    unit.X--;
                    if (stabilityCheck(unit.X,
                        unchecked((uint)MathF.Round(unit.Y, MidpointRounding.AwayFromZero))))
                        unit.IsInAir = true;
                }
                if (player.Actions[1] && unit.X < 305 &&
                    stabilityCheck(unit.X + 5, unchecked((uint)MathF.Round(unit.Y, MidpointRounding.AwayFromZero))))
                {
                    if (unit.LastMove < 5)
                        unit.LastMove++;
                    unit.X++;
                    if (stabilityCheck(unit.X,
                        unchecked((uint)MathF.Round(unit.Y, MidpointRounding.AwayFromZero))))
                        unit.IsInAir = true;
                }
            }

            if (unit.UnitType != DestructUnit.Laser)
            {
                if (player.Actions[2])
                {
                    if (unit.UnitType == DestructUnit.Heli)
                    {
                        unit.IsInAir = true;
                        unit.YMovement -= 0.1f;
                    }
                    else if (unit.UnitType == DestructUnit.Jumper && !unit.IsInAir)
                    {
                        unit.YMovement = -3;
                        unit.IsInAir = true;
                    }
                    else
                        unit.RaisePower();
                }
                if (player.Actions[3])
                {
                    if (unit.UnitType == DestructUnit.Heli && unit.IsInAir)
                        unit.YMovement += 0.1f;
                    else
                        unit.LowerPower();
                }
            }

            if (player.Actions[6])
                CycleWeaponUp(unit, enableTracerLaser);
            if (player.Actions[7])
                CycleWeaponDown(unit, enableTracerLaser);
            if (player.Actions[4])
            {
                player.UnitSelected++;
                if (player.UnitSelected >= maximumInstallations)
                    player.UnitSelected = 0;
            }

            if (player.ShotDelay > 0)
                player.ShotDelay--;
            if (!player.Actions[5] || player.ShotDelay != 0)
                continue;

            var shotTypeIndex = (int)unit.ShotType;
            if ((uint)shotTypeIndex >= ShotDelay.Length)
                throw new ArgumentOutOfRangeException(nameof(match), "The selected Destruct unit has an invalid weapon.");
            player.ShotDelay = ShotDelay[shotTypeIndex];
            switch (ShotExplosionType[shotTypeIndex])
            {
                case DestructExplosionType.None:
                    break;
                case DestructExplosionType.Magnet:
                    runMagnet(playerIndex, unit);
                    break;
                case DestructExplosionType.Dirt:
                case DestructExplosionType.Normal:
                    MakeShot(match, playerIndex, unit, direction,
                        shots, maximumShots, jumperStraight[playerIndex]);
                    break;
                default:
                    throw new InvalidOperationException("Unknown Destruct weapon effect type.");
            }
        }
    }

    public static void RunMagnet(
        DestructMatchState match,
        int playerIndex,
        DestructUnitState magnet,
        IList<DestructShotState> shots,
        int maximumShots,
        int maximumInstallations)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(magnet);
        ArgumentNullException.ThrowIfNull(shots);
        if ((uint)playerIndex >= match.Players.Length)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        if (maximumShots < 0 || shots.Count < maximumShots || maximumInstallations < 0)
            throw new ArgumentOutOfRangeException(nameof(maximumShots));

        var enemyIndex = playerIndex == 0 ? 1 : 0;
        var direction = playerIndex == 0 ? -1 : 1;
        for (var shotIndex = 0; shotIndex < maximumShots; shotIndex++)
        {
            var shot = shots[shotIndex];
            if (!shot.IsAvailable &&
                ((playerIndex == 0 && shot.X > magnet.X) ||
                 (playerIndex == 1 && shot.X < magnet.X)))
                shot.XMovement += magnet.Power * 0.1f * -direction;
        }

        var enemy = match.Players[enemyIndex];
        if (enemy.Units.Count < maximumInstallations)
            throw new ArgumentException("The opposing player must have the configured installation capacity.", nameof(match));
        for (var unitIndex = 0; unitIndex < maximumInstallations; unitIndex++)
        {
            var unit = enemy.Units[unitIndex];
            if (!IsValid(unit) || unit.UnitType != DestructUnit.Heli || !unit.IsInAir)
                continue;
            if ((enemyIndex == 1 && (ulong)unit.X + 11 < 318) ||
                (enemyIndex == 0 && unit.X > 1))
                unit.X = unchecked(unit.X - 2u * (uint)direction);
        }
        magnet.AnimationFrame = 1;
    }

    public static void RunTickExplosions(
        DestructMatchState match,
        int maximumInstallations,
        IndexedSurface temporaryScreen,
        Func<float> nextRandomFloat,
        Action<int, DestructUnitState> destroyUnit)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(temporaryScreen);
        ArgumentNullException.ThrowIfNull(nextRandomFloat);
        ArgumentNullException.ThrowIfNull(destroyUnit);
        if (maximumInstallations < 0 || match.MaximumExplosions > match.Explosions.Count)
            throw new ArgumentOutOfRangeException(nameof(maximumInstallations));

        for (var explosionIndex = 0; explosionIndex < match.MaximumExplosions; explosionIndex++)
        {
            var explosion = match.Explosions[(int)explosionIndex];
            if (explosion.IsAvailable)
                continue;

            for (var flare = 0u; flare < explosion.Fill; flare++)
            {
                var angle = (float)(nextRandomFloat() * (2 * Math.PI));
                var yOffset = (int)MathF.Round(
                    MathF.Cos(angle) * nextRandomFloat() * explosion.Width,
                    MidpointRounding.AwayFromZero);
                var xOffset = (int)MathF.Round(
                    MathF.Sin(angle) * nextRandomFloat() * explosion.Width,
                    MidpointRounding.AwayFromZero);
                var x = unchecked((int)(explosion.X + (uint)xOffset));
                var y = unchecked((int)(explosion.Y + (uint)yOffset));
                while (x < 0)
                    x += 320;
                while (x > 320)
                    x -= 320;
                if (y >= 200 || y <= 15)
                    continue;

                switch (explosion.Type)
                {
                    case DestructExplosionType.Dirt:
                        var pixelIndex = (long)y * temporaryScreen.Pitch + x;
                        if (pixelIndex >= 0 && pixelIndex < temporaryScreen.Pixels.Length)
                            temporaryScreen.Pixels.Span[(int)pixelIndex] = 25;
                        break;
                    case DestructExplosionType.Normal:
                        DrawSuperPixel(temporaryScreen, (uint)x, (uint)y);
                        TestExplosionCollision(match.Players, maximumInstallations, (uint)x, (uint)y, destroyUnit);
                        break;
                    default:
                        throw new InvalidOperationException($"Unsupported Destruct explosion type: {explosion.Type}.");
                }
            }

            explosion.Width++;
            if (explosion.Width == explosion.MaximumWidth)
                explosion.IsAvailable = true;
        }
    }

    public static byte AliasDirtPixel(IndexedSurface surface, int x, int y)
    {
        ArgumentNullException.ThrowIfNull(surface);
        if ((uint)x >= surface.Pitch || (uint)y >= surface.Height)
            throw new ArgumentOutOfRangeException(nameof(x));

        var pixels = surface.Pixels.Span;
        var index = y * surface.Pitch + x;
        var color = 0;
        if (y > 0 && pixels[index - surface.Pitch] == 25)
            color += 1;
        if (y < surface.Height - 1 && pixels[index + surface.Pitch] == 25)
            color += 3;
        if (x > 0 && pixels[index - 1] == 25)
            color += 2;
        if (x < surface.Pitch - 1 && pixels[index + 1] == 25)
            color += 2;
        return color == 0 ? (byte)0 : (byte)(color + 16);
    }

    public static void AliasDirt(IndexedSurface surface)
    {
        ArgumentNullException.ThrowIfNull(surface);
        for (var y = 12; y < surface.Height; y++)
        for (var x = 0; x < surface.Pitch; x++)
        {
            var index = y * surface.Pitch + x;
            if (surface.Pixels.Span[index] == 0)
                surface.Pixels.Span[index] = AliasDirtPixel(surface, x, y);
        }
    }

    public static void CheckTemporaryScreen(
        IndexedSurface screen,
        IndexedSurface temporaryScreen,
        bool alwaysAlias)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(temporaryScreen);
        if (screen.Pitch != temporaryScreen.Pitch || screen.Height != temporaryScreen.Height)
            throw new ArgumentException("Destruct screens must have matching pitches and heights.");

        var screenPixels = screen.Pixels.Span;
        var temporaryPixels = temporaryScreen.Pixels.Span;
        for (var y = 12; y < screen.Height; y++)
        for (var x = 0; x < screen.Pitch; x++)
        {
            var index = y * screen.Pitch + x;
            var pixel = temporaryPixels[index];
            if (pixel >= 241)
                pixel = pixel == 241 ? (byte)0 : (byte)(pixel - 1);
            if (alwaysAlias && pixel == 0)
            {
                temporaryPixels[index] = pixel;
                pixel = AliasDirtPixel(temporaryScreen, x, y);
            }
            temporaryPixels[index] = pixel;
            screenPixels[index] = pixel;
        }
    }

    public static void FlyUnit(DestructUnitState unit, Func<uint, uint, bool> stabilityCheck)
    {
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(stabilityCheck);
        if (unit.Y + unit.YMovement > GroundY)
        {
            unit.Y = GroundY;
            unit.YMovement = 0;
            unit.IsInAir = false;
            return;
        }

        unit.Y += unit.YMovement;
        if (unit.Y < CeilingY)
        {
            unit.YMovement = 0;
            unit.Y = CeilingY;
        }

        unit.YMovement += unit.UnitType == DestructUnit.Heli ? 0.0001f : 0.03f;
        if (!stabilityCheck(unit.X, (uint)RoundNative(unit.Y)))
        {
            unit.YMovement = 0;
            unit.IsInAir = false;
        }
    }

    public static void RunGravity(
        DestructMatchState match,
        int maxInstallations,
        Func<uint, uint, bool> stabilityCheck,
        Action<uint, int, int> drawSprite)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(stabilityCheck);
        ArgumentNullException.ThrowIfNull(drawSprite);
        if (maxInstallations < 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstallations));

        for (var playerIndex = 0; playerIndex < match.Players.Length; playerIndex++)
        {
            var player = match.Players[playerIndex];
            if (player.Units.Count < maxInstallations)
                throw new ArgumentException("Each Destruct player must have the configured installation capacity.", nameof(match));

            for (var unitIndex = 0; unitIndex < maxInstallations; unitIndex++)
            {
                var unit = player.Units[unitIndex];
                if (unit.Health <= 0)
                    continue;
                switch (unit.UnitType)
                {
                    case DestructUnit.Satellite:
                        continue;
                    case DestructUnit.Heli:
                    case DestructUnit.Jumper:
                        if (unit.IsInAir)
                        {
                            FlyUnit(unit, stabilityCheck);
                            break;
                        }
                        LowerUnit(unit, stabilityCheck);
                        break;
                    default:
                        if (unit.UnitType is < DestructUnit.Tank or > DestructUnit.Heli)
                            throw new ArgumentOutOfRangeException(nameof(match), "A Destruct unit has an invalid type.");
                        LowerUnit(unit, stabilityCheck);
                        break;
                }

                DrawUnit(playerIndex, unit, drawSprite);
            }
        }
    }

    private static int RoundNative(float value) =>
        checked((int)MathF.Round(value, MidpointRounding.AwayFromZero));

    public static bool IsValid(DestructUnitState unit)
    {
        ArgumentNullException.ThrowIfNull(unit);
        return unit.Health > 0;
    }

    public static void ResetUnits(
        IList<DestructUnitState> left,
        IList<DestructUnitState> right,
        int maxInstallations)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        if (maxInstallations < 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstallations));
        if (left.Count < maxInstallations || right.Count < maxInstallations)
            throw new ArgumentException("Each Destruct player must have the configured installation capacity.");

        for (var index = 0; index < maxInstallations; index++)
        {
            left[index].Health = 0;
            right[index].Health = 0;
        }
    }

    public static void CycleDeadUnits(DestructMatchState match, int maxInstallations)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (maxInstallations < 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstallations));

        foreach (var player in match.Players)
        {
            if (player.UnitsRemaining == 0)
                continue;
            if (maxInstallations == 0 || player.Units.Count < maxInstallations ||
                player.UnitSelected >= maxInstallations)
                throw new InvalidOperationException("A living Destruct player must have a selected installation.");

            for (var checkedUnits = 0; checkedUnits < maxInstallations; checkedUnits++)
            {
                var unit = player.Units[(int)player.UnitSelected];
                if (unit.Health > 0 && unit.ShotType != DestructShot.Invalid)
                    break;

                player.UnitSelected++;
                if (player.UnitSelected >= maxInstallations)
                    player.UnitSelected = 0;

                if (checkedUnits == maxInstallations - 1)
                    throw new InvalidOperationException("A living Destruct player has no selectable installation.");
            }
        }
    }

    public static void AnimateUnits(DestructMatchState match, int maxInstallations)
    {
        ArgumentNullException.ThrowIfNull(match);
        if (maxInstallations < 0)
            throw new ArgumentOutOfRangeException(nameof(maxInstallations));

        foreach (var player in match.Players)
        {
            if (player.Units.Count < maxInstallations)
                throw new ArgumentException("Each Destruct player must have the configured installation capacity.", nameof(match));

            for (var index = 0; index < maxInstallations; index++)
            {
                var unit = player.Units[index];
                if (unit.Health <= 0)
                    continue;
                var type = (int)unit.UnitType;
                if ((uint)type >= (uint)DestructUnitNames.UnitCount)
                    throw new ArgumentOutOfRangeException(nameof(match), "A Destruct unit has an invalid type.");
                if (!SystemAnimation[type] && unit.AnimationFrame == 0)
                    continue;

                unit.AnimationFrame = unchecked(unit.AnimationFrame + 1);
                if (unit.AnimationFrame > 3)
                    unit.AnimationFrame = 0;
            }
        }
    }

    public static void DestroyUnit(
        DestructMatchState match,
        int playerIndex,
        DestructUnitState unit,
        Action<uint, int, DestructShot> makeExplosion)
    {
        ArgumentNullException.ThrowIfNull(match);
        ArgumentNullException.ThrowIfNull(unit);
        ArgumentNullException.ThrowIfNull(makeExplosion);
        if ((uint)playerIndex >= 2)
            throw new ArgumentOutOfRangeException(nameof(playerIndex));
        if (unit.UnitType is < DestructUnit.Tank or > DestructUnit.Heli)
            throw new ArgumentOutOfRangeException(nameof(unit), "The unit must have a valid Destruct type.");

        var explosionShot = unit.UnitType == DestructUnit.Heli ? DestructShot.Small : DestructShot.Invalid;
        var explosionY = (int)MathF.Round(unit.Y, MidpointRounding.AwayFromZero) - 5;
        makeExplosion(unchecked(unit.X + 5), explosionY, explosionShot);

        if (unit.UnitType == DestructUnit.Satellite)
            return;

        var opponent = playerIndex == 0 ? 1 : 0;
        match.Players[playerIndex].UnitsRemaining = unchecked(match.Players[playerIndex].UnitsRemaining - 1);
        match.Players[opponent].Score = unchecked(match.Players[opponent].Score + 1);
    }

    public static void ResetWeapons(
        IReadOnlyList<DestructAvailabilityState> shots,
        int maxShots,
        IReadOnlyList<DestructAvailabilityState> explosions,
        int maxExplosions)
    {
        ResetAvailability(shots, maxShots, nameof(shots));
        ResetAvailability(explosions, maxExplosions, nameof(explosions));
    }

    private static void ResetAvailability(
        IReadOnlyList<DestructAvailabilityState> resources,
        int configuredCount,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(resources, parameterName);
        if (configuredCount < 0)
            throw new ArgumentOutOfRangeException(parameterName);
        if (resources.Count < configuredCount)
            throw new ArgumentException("Destruct resources must have the configured capacity.", parameterName);

        for (var index = 0; index < configuredCount; index++)
            resources[index].IsAvailable = true;
    }

    private static void CycleWeapon(DestructUnitState unit, int direction, bool enableTracerLaser)
    {
        ArgumentNullException.ThrowIfNull(unit);
        if (unit.UnitType is < DestructUnit.Tank or > DestructUnit.Heli)
            throw new ArgumentOutOfRangeException(nameof(unit), "The unit must have a valid Destruct type.");
        if ((uint)unit.ShotType > (uint)DestructShot.Bomb)
            throw new ArgumentOutOfRangeException(nameof(unit), "The selected shot must be valid.");

        var available = WeaponSystems[(int)unit.UnitType];
        if (unit.UnitType == DestructUnit.Laser && enableTracerLaser)
            available |= LaserTracer;
        var originalShot = unit.ShotType;
        for (var count = 0; count < 17; count++)
        {
            var next = ((int)unit.ShotType + direction + 17) % 17;
            unit.ShotType = (DestructShot)next;
            if ((available & (1u << next)) != 0)
                return;
        }

        unit.ShotType = originalShot;
        throw new InvalidOperationException("The Destruct unit has no selectable weapons.");
    }
}
