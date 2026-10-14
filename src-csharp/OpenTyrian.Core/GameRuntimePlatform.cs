/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameRuntimePlatform : IDisposable
{
    private bool _disposed;
    private readonly string _userDirectory;
    private readonly TextWriter _diagnostics;
    private readonly AudioMixer _silentMixer = CreateSilentMixer();
    public GameStartupState Startup { get; }
    public GameConfiguration Configuration { get; }
    public GameSettingsStore Settings { get; }
    public SdlVideoDevice Video { get; }
    public SdlJoysticks Joysticks { get; }
    public SdlInputPlatform InputPlatform { get; }
    public InputState Input { get; }
    public GameRuntimeResources Resources { get; }
    public NetworkSession? Network { get; }
    private readonly PaletteColor[] _pictureColors = new PaletteColor[PaletteSystem.ColorCount];
    private SpriteSheet? _cursorSprites;
    public ReadOnlyMemory<PaletteColor> PictureColors => _pictureColors;
    public NetworkRequests GameplayRequests => Network?.Requests ?? _localGameplayRequests;
    private readonly SdlNetTransport? _networkTransport;
    private readonly NetworkRequests _localGameplayRequests = new();

    private GameRuntimePlatform(GameStartupState startup, GameConfiguration configuration,
        GameSettingsStore settings, SdlVideoDevice video, SdlJoysticks joysticks,
        SdlInputPlatform inputPlatform, InputState input, GameRuntimeResources resources,
        string userDirectory, TextWriter diagnostics, SdlNetTransport? networkTransport, NetworkSession? network)
    {
        Startup = startup;
        Configuration = configuration;
        Settings = settings;
        Video = video;
        Joysticks = joysticks;
        InputPlatform = inputPlatform;
        Input = input;
        Resources = resources;
        _userDirectory = userDirectory;
        _diagnostics = diagnostics;
        _networkTransport = networkTransport;
        Network = network;
    }

    public static GameRuntimePlatform Start(GameStartupState startup, string workingDirectory,
        string userDirectory, TextWriter output, TextWriter diagnostics, Action quitRequested,
        Func<bool> isNetworkGame, Action checkNetwork, Func<uint>? nextRandom = null)
    {
        ArgumentNullException.ThrowIfNull(startup);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDirectory);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(diagnostics);
        ArgumentNullException.ThrowIfNull(quitRequested);
        ArgumentNullException.ThrowIfNull(isNetworkGame);
        ArgumentNullException.ThrowIfNull(checkNetwork);

        var video = new SdlVideoDevice(diagnostics, -1);
        GameRuntimeResources? resources = null;
        SdlNetTransport? networkTransport = null;
        NetworkSession? network = null;
        SdlJoysticks? joysticks = null;
        SdlInputPlatform? inputPlatform = null;
        try
        {
            var configuration = new GameConfiguration(new SdlKeyboardNames());
            configuration.LoadFile(userDirectory, diagnostics);
            video.SetScalerByName(VideoScaler.Scalers[configuration.Scaler.SelectedIndex].Name);
            video.ReinitializeFullscreen(configuration.FullscreenDisplay);
            video.Layout.Mode = configuration.Layout.Mode;
            joysticks = new SdlJoysticks(configuration.Text, output, diagnostics,
                input => (inputPlatform ?? throw new InvalidOperationException("SDL input is not initialized.")).PostEvent(input),
                () => startup.NetworkGame || isNetworkGame(),
                () =>
                {
                    if (startup.NetworkGame)
                        (network ?? throw new InvalidOperationException("Network session is not initialized.")).Check();
                    else
                        checkNetwork();
                },
                startup.Options.IgnoreJoystick);
            inputPlatform = new SdlInputPlatform(video, joysticks, quitRequested);
            var input = new InputState(inputPlatform, video.Layout);
            input.FlushEvents();
            input.Initialize();
            if (startup.NetworkGame)
            {
                var options = startup.Options;
                var host = options.OpponentHost
                    ?? throw new InvalidOperationException("Network startup requires a configured opponent host.");
                networkTransport = new SdlNetTransport(options.PlayerPort);
                networkTransport.Bind(host, options.OpponentPort);
                network = new NetworkSession(networkTransport, input, options.NetworkDelay, diagnostics);
            }
            resources = GameRuntimeResources.LoadForStartup(workingDirectory, startup,
                userDirectory, diagnostics, nextRandom);

            var settings = new GameSettingsStore(configuration, diagnostics);
            var loadedResources = resources;
            settings.Load(userDirectory, (music, samples) => loadedResources.AudioSession?.SetVolume(music, samples));
            return new(startup, configuration, settings, video, joysticks, inputPlatform, input,
                loadedResources, userDirectory, diagnostics, networkTransport, network);
        }
        catch
        {
            try { resources?.Dispose(); }
            finally
            {
                try { networkTransport?.Dispose(); }
                finally
                {
                    try { inputPlatform?.Dispose(); }
                    finally
                    {
                        try { joysticks?.Dispose(); }
                        finally { video.Dispose(); }
                    }
                }
            }
            throw;
        }
    }

    public void SaveConfiguration()
    {
        EnsureOpen();
        Settings.Save(_userDirectory);
    }

    public void InitializePlayerData(LegacyGameState game, ReadOnlySpan<byte> initialLevelName)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        new PlayerProgression(game, Resources.Assets.Items).Initialize(initialLevelName);
        Settings.Dos.InputDevices.CopyTo(game.InputDevices, 0);
    }

    public void LoadGame(LegacyGameState game, byte slot, Func<int, int> initializeEpisode)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        LegacySaveGame.Load(Settings.Saves, game, slot, initializeEpisode);
    }

    public void SaveGame(LegacyGameState game, byte slot, ReadOnlySpan<byte> name)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        LegacySaveGame.Save(Settings.Saves, game, slot, name, _ => Settings.Save(_userDirectory));
    }

    public void LoadPicture(byte number, IndexedSurface destination, bool storePalette = true)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(destination);
        using var stream = Resources.Assets.Files.OpenRequired(Resources.Assets.Files.Directory, "tyrian.pic");
        PictureLoader.LoadPicture(stream, number, destination, _pictureColors,
            Resources.Assets.Palettes, storePalette, Video.Palette);
    }

    public DelayTimer CreateDelayTimer()
    {
        EnsureOpen();
        return new(Input);
    }

    public byte InitializeEpisode(byte episode)
    {
        EnsureOpen();
        Resources.Assets.Episodes.Initialize(episode);
        return episode;
    }

    public byte InitializeEpisode(LegacyGameState game, byte episode)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        game.Episode = InitializeEpisode(episode);
        return game.Episode;
    }

    public IntroLogoHost CreateIntroLogoHost()
    {
        EnsureOpen();
        return new(Video.Primary, Video.Palette, _pictureColors, CreateDelayTimer(),
            number => LoadPicture(checked((byte)number), Video.Primary), Video.Present);
    }

    public IntroLogos CreateIntroLogos(TitleScreen title)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(title);
        return new(() => title.MoveLogoUp = true, CreateIntroLogoHost());
    }

    public CreditsHost CreateCreditsHost(Action keepAlive)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(keepAlive);
        var timer = CreateDelayTimer();
        return new(Resources.Assets.Files, Video.Primary, new SpriteTable(),
            Resources.Assets.Shapes.Sheets[1], new FontRenderer(Resources.Assets.Shapes),
            Video.Palette, Resources.Assets.Palettes[0], Input, timer,
            song => Resources.AudioSession?.PlaySong(song),
            () => Resources.AudioSession?.FadeSong(), Video.Present, keepAlive);
    }

    public Credits CreateCredits(ReadOnlyMemory<byte> episodeText, Action keepAlive)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(keepAlive);
        return new(Resources.Assets.Items, episodeText, CreateCreditsHost(keepAlive));
    }

    public EpisodeTransitionHost CreateEpisodeTransitionHost(ReadOnlyMemory<PaletteColor> episodePalette,
        Action highScores, Action credits, Action keepAlive)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(highScores);
        ArgumentNullException.ThrowIfNull(credits);
        ArgumentNullException.ThrowIfNull(keepAlive);
        return new(Video.Primary, new FontRenderer(Resources.Assets.Shapes), Video.Palette, episodePalette,
            Input, CreateDelayTimer(), highScores, credits,
            song => Resources.AudioSession?.PlaySong(song), Video.Present, keepAlive);
    }

    public EpisodeTransition CreateEpisodeTransition(LegacyGameState game,
        IReadOnlyList<byte[]> episodeNames, ReadOnlyMemory<byte> continueText,
        ReadOnlyMemory<PaletteColor> episodePalette, ReadOnlyMemory<byte> creditsText,
        Action highScores, Action keepAlive)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(episodeNames);
        ArgumentNullException.ThrowIfNull(highScores);
        ArgumentNullException.ThrowIfNull(keepAlive);
        var host = CreateEpisodeTransitionHost(episodePalette, highScores,
            () => CreateCredits(creditsText, keepAlive).Run(), keepAlive);
        return new(game, Resources.Assets.Episodes, Resources.Assets.Items, episodeNames, continueText, host)
        {
            NetworkGame = Startup.NetworkGame,
            ConstantPlay = Startup.Options.ConstantPlay
        };
    }

    public EpisodeTransition CreateEpisodeTransition(LegacyGameState game,
        ReadOnlyMemory<byte> continueText, ReadOnlyMemory<PaletteColor> episodePalette,
        ReadOnlyMemory<byte> creditsText, Action highScores, Action keepAlive)
    {
        EnsureOpen();
        return CreateEpisodeTransition(game, Resources.Assets.Help.EpisodeNames, continueText,
            episodePalette, creditsText, highScores, keepAlive);
    }

    public EpisodeCompletionHost CreateEpisodeCompletionHost(Action displayText, Action nextEpisode,
        Action readTextSync)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(displayText);
        ArgumentNullException.ThrowIfNull(nextEpisode);
        ArgumentNullException.ThrowIfNull(readTextSync);
        return new(Video.Primary, Resources.Assets.Shapes.Sheets[1],
            new FontRenderer(Resources.Assets.Shapes), Video.Palette, _pictureColors, Input,
            CreateDelayTimer(), displayText, nextEpisode,
            number => LoadPicture(checked((byte)number), Video.Primary), Video.Present, readTextSync);
    }

    public EpisodeCompletion CreateEpisodeCompletion(EpisodeCompletionState state,
        ReadOnlyMemory<byte> singlePlayerScoreLabel,
        IReadOnlyList<ReadOnlyMemory<byte>> playerCashLabels, EpisodeSuperArcadeData superArcade,
        Action displayText, Action nextEpisode, Action readTextSync)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(playerCashLabels);
        ArgumentNullException.ThrowIfNull(superArcade);
        return new(state, singlePlayerScoreLabel, playerCashLabels, superArcade,
            CreateEpisodeCompletionHost(displayText, nextEpisode, readTextSync));
    }

    public EpisodeCompletionState CreateEpisodeCompletionState(LegacyGameState game, ulong totalScore)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        var state = new EpisodeCompletionState
        {
            SecretHint = game.SecretHint,
            TwoPlayerMode = game.TwoPlayerMode,
            ConstantPlay = Startup.Options.ConstantPlay,
            NetworkGame = Startup.NetworkGame,
            JumpBackToEpisode1 = Resources.Assets.Episodes.JumpBackToFirst,
            SuperTyrian = game.SuperTyrian,
            InitialDifficulty = game.InitialDifficulty,
            SuperArcadeMode = game.SuperArcadeMode,
            MainLevel = game.MainLevel,
            TotalScore = totalScore
        };
        state.Cash[0] = game.Players[0].Cash;
        state.Cash[1] = game.Players[1].Cash;
        return state;
    }

    public EpisodeCompletionState CreateEpisodeCompletionState(LegacyGameState game)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        var totalScore = new PlayerProgression(game, Resources.Assets.Items).TotalScore(game.Players[0]);
        return CreateEpisodeCompletionState(game, totalScore);
    }

    public bool RunEpisodeCompletion(LegacyGameState game, EpisodeCompletionState state, Stream episode,
        ReadOnlyMemory<byte> singlePlayerScoreLabel,
        IReadOnlyList<ReadOnlyMemory<byte>> playerCashLabels, EpisodeSuperArcadeData superArcade,
        Action displayText, Action nextEpisode, Action readTextSync)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(episode);
        ArgumentNullException.ThrowIfNull(displayText);
        ArgumentNullException.ThrowIfNull(nextEpisode);
        ArgumentNullException.ThrowIfNull(readTextSync);
        try
        {
            void AdvanceEpisode()
            {
                nextEpisode();
                state.JumpBackToEpisode1 = Resources.Assets.Episodes.JumpBackToFirst;
            }

            return CreateEpisodeCompletion(state, singlePlayerScoreLabel, playerCashLabels, superArcade,
                displayText, AdvanceEpisode, readTextSync).Show(episode);
        }
        finally
        {
            game.SuperArcadeMode = state.SuperArcadeMode;
            game.MainLevel = state.MainLevel;
            Startup.JumpSection = state.JumpSection;
            Startup.TrentWin = state.TrentWin;
        }
    }

    public bool RunEpisodeCompletion(LegacyGameState game, Stream episode,
        ReadOnlyMemory<byte> singlePlayerScoreLabel,
        IReadOnlyList<ReadOnlyMemory<byte>> playerCashLabels, EpisodeSuperArcadeData superArcade,
        Action displayText, Action nextEpisode, Action readTextSync)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        var state = CreateEpisodeCompletionState(game);
        return RunEpisodeCompletion(game, state, episode, singlePlayerScoreLabel,
            playerCashLabels, superArcade, displayText, nextEpisode, readTextSync);
    }

    public MouseCursor CreateMouseCursor()
    {
        EnsureOpen();
        return new(Input, Video.Primary, LoadCursorSprites());
    }

    public TitleScreenHost CreateTitleScreenHost(IndexedSurface background, ReadOnlyMemory<byte> version,
        Func<bool> newGame, Func<bool> loadGame, Action highScores, Action instructions, Action setup,
        Action whoa, Action superTyrian, Func<int, bool> superArcade)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(newGame);
        ArgumentNullException.ThrowIfNull(loadGame);
        ArgumentNullException.ThrowIfNull(highScores);
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(whoa);
        ArgumentNullException.ThrowIfNull(superTyrian);
        ArgumentNullException.ThrowIfNull(superArcade);
        var cursorSprites = LoadCursorSprites();
        return new(Video.Primary, background, new FontRenderer(Resources.Assets.Shapes),
            Resources.Assets.Shapes.Tables[3], cursorSprites, new MouseCursor(Input, Video.Primary, cursorSprites),
            Video.Palette, _pictureColors, CreateDelayTimer(),
            version,
            () => { }, () => LoadPicture(2, Video.Primary, storePalette: false),
            song => Resources.AudioSession?.PlaySong(song), Video.Present, Joysticks.PushJoysticksAsKeyboard,
            sample => Resources.AudioSession?.PlaySample(sample), newGame, loadGame, highScores,
            instructions, setup, whoa, superTyrian, superArcade);
    }

    public void ConnectNetwork(CancellationToken cancellation = default)
    {
        EnsureOpen();
        if (!Startup.NetworkGame || Network is null)
            throw new InvalidOperationException("Network mode was not enabled for this runtime.");
        if (Startup.Options.PlayerNumber is < 1 or > 2)
            throw new InvalidOperationException("Network startup requires --net-player-number=1 or 2.");
        var name = System.Text.Encoding.UTF8.GetBytes(Startup.Options.PlayerName);
        Network.Connect(Resources.Assets.Episodes.Available, (ushort)Startup.Options.PlayerNumber,
            name, Joysticks.PushJoysticksAsKeyboard, cancellation);
    }

    public void RunNetworkStartScreen(LegacyGameState game, INetworkStartHost host,
        CancellationToken cancellation = default)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(host);
        if (!Startup.NetworkGame || Network is null)
            throw new InvalidOperationException("Network mode was not enabled for this runtime.");
        if (Startup.Options.PlayerNumber is < 1 or > 2)
            throw new InvalidOperationException("Network startup requires --net-player-number=1 or 2.");

        var screen = new NetworkStartScreen(game, Resources.Assets.Items, Resources.Assets.Help, Input,
            host, new NetworkStartChannel(Network), () => ConnectNetwork(cancellation));
        screen.Run((int)Startup.Options.PlayerNumber, cancellation);
    }

    public NetworkStartHost CreateNetworkStartHost(LegacyGameState game, SelectionMenus menus,
        SelectionMenuState selection, IndexedSurface background, Func<byte, byte> initializeEpisode)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        var newGameHost = CreateNewGameHost(game, menus, selection, episode =>
            initializeEpisode(checked((byte)episode)));
        var timer = CreateDelayTimer();
        return new(Video.Primary, background, new FontRenderer(Resources.Assets.Shapes),
            Video.Palette, _pictureColors, timer, () => LoadPicture(2, background, storePalette: false),
            Video.Present, () => ConnectNetwork(), newGameHost.SelectEpisode, newGameHost.SelectDifficulty,
            episode => initializeEpisode(checked((byte)episode)));
    }

    public NetworkStartHost CreateNetworkStartHost(LegacyGameState game, SelectionMenus menus,
        SelectionMenuState selection, IndexedSurface background)
        => CreateNetworkStartHost(game, menus, selection, background,
            episode => InitializeEpisode(checked((byte)episode)));

    public void RunNetworkStartScreen(LegacyGameState game, SelectionMenus menus,
        SelectionMenuState selection, IndexedSurface background, Func<byte, byte> initializeEpisode,
        CancellationToken cancellation = default)
    {
        EnsureOpen();
        RunNetworkStartScreen(game, CreateNetworkStartHost(game, menus, selection, background, initializeEpisode),
            cancellation);
    }

    public void UpdateNetworkGameState(LevelEvents level, PlayerCombat combat,
        ILevelNetworkServices services, ref ushort shipGraphic, CancellationToken cancellation = default)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(services);
        if (!Startup.NetworkGame) return;
        var network = Network
            ?? throw new InvalidOperationException("Network gameplay has no initialized network session.");
        level.UpdateNetworkState(true, network, combat, services, ref shipGraphic, cancellation);
    }

    public TitleScreen CreateTitleScreen(ITitleScreenHost host)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(host);
        return new(Resources.Assets.Help, Input, host);
    }

    public TitleScreen CreateTitleScreen(LegacyGameState game, SelectionMenus menus,
        SelectionMenuState selection, IndexedSurface background, ReadOnlyMemory<byte> version,
        Func<byte, byte> initializeEpisode, Action clearPerformSave, Action highScores,
        Action instructions, Action setup, Action whoa)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        ArgumentNullException.ThrowIfNull(clearPerformSave);
        ArgumentNullException.ThrowIfNull(highScores);
        ArgumentNullException.ThrowIfNull(instructions);
        ArgumentNullException.ThrowIfNull(setup);
        ArgumentNullException.ThrowIfNull(whoa);

        var newGame = CreateNewGame(game, CreateNewGameHost(game, menus, selection, initializeEpisode));
        var host = CreateTitleScreenHost(background, version, newGame.Run,
            () => CreateLoadScreen(CreateLoadScreenHost(game, clearPerformSave,
                episode => initializeEpisode(checked((byte)episode)))).Run(),
            highScores, instructions, setup, whoa, () => newGame.RunSuperTyrian(),
            mode => newGame.RunSuperArcade(mode));
        return CreateTitleScreen(host);
    }

    public TitleScreen CreateTitleScreen(LegacyGameState game, SelectionMenus menus,
        SelectionMenuState selection, IndexedSurface background, ReadOnlyMemory<byte> version,
        Func<byte, byte> initializeEpisode, Action clearPerformSave, Action jukebox, Action destruct, Action whoa)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        ArgumentNullException.ThrowIfNull(clearPerformSave);
        ArgumentNullException.ThrowIfNull(jukebox);
        ArgumentNullException.ThrowIfNull(destruct);
        ArgumentNullException.ThrowIfNull(whoa);
        return CreateTitleScreen(game, menus, selection, background, version, initializeEpisode,
            clearPerformSave, () => CreateHighScoreScreen(CreateHighScoreScreenHost()).Run(),
            () => CreateHelpSystem(CreateHelpSystemHost()).Run(),
            () => CreateSetupMenu(CreateSetupMenuHost(background, jukebox, destruct)).Run(), whoa);
    }

    public NewGame CreateNewGame(LegacyGameState game, INewGameHost host)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(host);
        return new(game, Resources.Assets.Items, Resources.Assets.Help, Input, host)
        {
            RichMode = Startup.Options.RichMode,
            ConstantDie = Startup.Options.ConstantDie
        };
    }

    public SelectionMenus CreateSelectionMenus(ISelectionMenuHost host, SelectionMenuState state)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(state);
        return new(Input, host, state);
    }

    public SelectionMenuPresentation CreateSelectionMenuPresentation(IndexedSurface background,
        Action keepAlive, Func<int, int> initializeEpisode)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(keepAlive);
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        var timer = CreateDelayTimer();
        return new(Video.Primary, background, new FontRenderer(Resources.Assets.Shapes),
            CreateMouseCursor(), () => { }, () => LoadPicture(2, background, storePalette: false),
            () => Video.Palette.FadePalette(_pictureColors, 10, 0, 255,
                () => timer.SetDelay(1), Video.Present, timer.Wait),
            Video.Present, sample => Resources.AudioSession?.PlaySample(sample),
            steps => Video.Palette.FadeBlack(steps, () => timer.SetDelay(1), Video.Present, timer.Wait),
            Joysticks.PushJoysticksAsKeyboard, keepAlive, () => InputPlatform.Modifiers, initializeEpisode);
    }

    public SelectionMenuPresentation CreateSelectionMenuPresentation(IndexedSurface background,
        Action keepAlive)
        => CreateSelectionMenuPresentation(background, keepAlive,
            episode => InitializeEpisode(checked((byte)episode)));

    public NewGameHost CreateNewGameHost(LegacyGameState game, SelectionMenus menus,
        SelectionMenuState selection, Func<byte, byte> initializeEpisode)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(menus);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        return new(game, menus, selection, Resources.Assets.Help, Resources.Assets.Episodes.Available,
            Video.Primary, new FontRenderer(Resources.Assets.Shapes), Resources.Assets.Shapes.Sheets[1],
            Video.Palette, _pictureColors, Input, CreateDelayTimer(),
            () => LoadPicture(2, Video.Primary), Video.Present,
            song => Resources.AudioSession?.PlaySong(song), sample => Resources.AudioSession?.PlaySample(sample),
            initializeEpisode);
    }

    public NewGameHost CreateNewGameHost(LegacyGameState game, SelectionMenus menus,
        SelectionMenuState selection)
        => CreateNewGameHost(game, menus, selection, episode => InitializeEpisode(episode));

    public SetupMenuHost CreateSetupMenuHost(IndexedSurface background, Action jukebox, Action destruct)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(background);
        ArgumentNullException.ThrowIfNull(jukebox);
        ArgumentNullException.ThrowIfNull(destruct);
        var font = new FontRenderer(Resources.Assets.Shapes);
        return new(Video, background, font, LoadCursorSprites(), CreateMouseCursor(),
            _pictureColors, CreateDelayTimer(), Resources.AudioSession?.Mixer ?? _silentMixer,
            Configuration, () => LoadCursorSprites(), () => LoadPicture(2, background, storePalette: false),
            Joysticks.PushJoysticksAsKeyboard,
            sample => Resources.AudioSession?.PlaySample(sample),
            () => Resources.AudioSession?.PlaySong(29), SaveConfiguration, jukebox, destruct);
    }

    public LoadScreenHost CreateLoadScreenHost(LegacyGameState game, Action clearPerformSave,
        Func<int, int> initializeEpisode)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(clearPerformSave);
        ArgumentNullException.ThrowIfNull(initializeEpisode);
        var catalog = new SavedGameCatalog(_userDirectory, _diagnostics);
        var timer = CreateDelayTimer();
        var font = new FontRenderer(Resources.Assets.Shapes);
        return new(Video.Primary, Video.Secondary, font, new HelpTextRenderer(font), LoadCursorSprites(),
            CreateMouseCursor(), Video.Palette, _pictureColors, timer, () => LoadCursorSprites(),
            () => LoadPicture(2, Video.Secondary), Video.Present, Joysticks.PushJoysticksAsKeyboard,
            sample => Resources.AudioSession?.PlaySample(sample),
            () => catalog.BuildList(game.TwoPlayerMode ? (byte)2 : (byte)1, _userDirectory),
            clearPerformSave,
            name => VersionedSaveGame.LoadFile(_userDirectory, name, game, Resources.Assets.Items,
                initializeEpisode, _diagnostics),
            () => game.GameLoaded);
    }

    public LoadScreen CreateLoadScreen(ILoadScreenHost host)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(host);
        return new(Resources.Assets.Help, Input, host);
    }

    public HighScoreScreen CreateHighScoreScreen(IHighScoreScreenHost host)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(host);
        return new(Settings.Saves, Resources.Assets.Help, Resources.Assets.Episodes.Available, Input, host);
    }

    public HighScoreScreenHost CreateHighScoreScreenHost()
    {
        EnsureOpen();
        var font = new FontRenderer(Resources.Assets.Shapes);
        var timer = CreateDelayTimer();
        return new(Video.Primary, Video.Secondary, font, new HelpTextRenderer(font), LoadCursorSprites(),
            CreateMouseCursor(), Video.Palette, _pictureColors, timer, () => LoadCursorSprites(),
            () => LoadPicture(2, Video.Secondary), Video.Present, Joysticks.PushJoysticksAsKeyboard,
            sample => Resources.AudioSession?.PlaySample(sample));
    }

    public HelpSystemHost CreateHelpSystemHost()
    {
        EnsureOpen();
        var font = new FontRenderer(Resources.Assets.Shapes);
        var timer = CreateDelayTimer();
        return new(Video.Primary, Video.Secondary, font, new HelpTextRenderer(font), Resources.Assets.Help,
            LoadCursorSprites(), CreateMouseCursor(), Video.Palette, _pictureColors, timer,
            () => LoadCursorSprites(), () => LoadPicture(2, Video.Secondary),
            song => Resources.AudioSession?.PlaySong(song), Video.Present,
            Joysticks.PushJoysticksAsKeyboard, sample => Resources.AudioSession?.PlaySample(sample));
    }

    public SetupMenu CreateSetupMenu(ISetupMenuHost host)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(host);
        return new(Input, Settings.Dos, host);
    }

    public HelpSystem CreateHelpSystem(IHelpSystemHost host)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(host);
        return new(Resources.Assets.Help, Input, host);
    }

    public LevelMainLoopState CreateLevelMainLoopState(LegacyGameState game)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        return new()
        {
            PlayDemo = Startup.PlayDemo,
            RecordDemo = Startup.RecordDemo,
            NetworkGame = Startup.NetworkGame,
            TwoPlayerMode = game.TwoPlayerMode,
            EndLevel = game.EndLevel,
            ReallyEndLevel = game.ReallyEndLevel,
            PlayerEndLevel = game.PlayerEndLevel,
            ExtraGame = game.ExtraGame,
            DoNotSaveBackup = game.DoNotSaveBackup,
            MainLevel = game.MainLevel
        };
    }

    public LevelGameplayRuntime CreateLevelGameplay(LegacyGameState game, IndexedSurface hud,
        IPlayerShotEffects shotEffects)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(hud);
        ArgumentNullException.ThrowIfNull(shotEffects);

        var items = Resources.Assets.Items;
        var effects = new SceneEffects(new byte[8]);
        var enemies = new EnemySystem(items, _diagnostics)
        {
            Difficulty = game.Difficulty,
            SuperArcadeMode = game.SuperArcadeMode,
            GalagaMode = game.GalagaMode,
            TwoPlayers = game.TwoPlayerMode
        };
        var combat = new PlayerCombat(game.Players, effects, hud)
        {
            TwoPlayers = game.TwoPlayerMode,
            Galaga = game.GalagaMode,
            Cheating = Startup.YouAreCheating
        };
        var backgrounds = new BackgroundRenderer();
        var events = new LevelEvents(items, enemies, game.Players, backgrounds, effects)
        {
            OnePlayerAction = game.OnePlayerAction,
            SuperTyrian = game.SuperTyrian,
            InitialDifficulty = game.InitialDifficulty
        };
        var shots = new PlayerShots(items.Shots, items.Weapons, game.Players, shotEffects,
            enemies.Enemies, effects.SoundQueue)
        {
            BackgroundTwo = Settings.Runtime.Background2
        };
        var movement = new PlayerMovement(game.Players, combat, events, shots, items);
        var weapons = new PlayerWeapons(shots, items, combat);
        var specialWeapons = new PlayerSpecialWeapons(items, shots, weapons, combat,
            enemies, effects, events, backgrounds)
        {
            ArcadeMode = game.SuperArcadeMode
        };
        var input = CreatePlayerInput(game, movement, combat, events);
        var graphics = new ushort[2];
        var shipInfo = new PlayerShipInfo(game.Players, items, Resources.Assets.Shapes.Sheets[0],
            Resources.Assets.EditedShips.Shapes, Resources.Assets.EditedShips.Records, graphics);
        var frame = new PlayerFrame(game.Players, combat, events, movement, input, weapons,
            specialWeapons, shots, items, effects, enemies)
        {
            Episode = game.Episode,
            Network = Network
        };
        game.InputDevices.CopyTo(frame.Devices, 0);
        var legacyText = Resources.Assets.Help.MiscText.Select(System.Text.Encoding.Latin1.GetString).ToArray();
        var legacySecondaryText = Resources.Assets.Help.MiscTextB.Select(System.Text.Encoding.Latin1.GetString).ToArray();
        var pickups = new PlayerPickups(game.Players, enemies, combat, shots, items, events, effects,
            legacyText, legacySecondaryText)
        {
            NetworkGame = input.NetworkGame,
            LocalPlayer = input.LocalPlayer,
            ArcadeMode = game.SuperArcadeMode
        };
        var warnings = new WarningBars(Video.Primary, CreateDelayTimer(), Video.Present,
            sample => Resources.AudioSession?.PlaySample(sample));
        var armorWarning = new ArmorWarning(game.Players, combat, warnings, effects,
            new ArmorWarningHost(enemies, items, Video.Primary, new FontRenderer(Resources.Assets.Shapes)));
        var flagsInitializer = new LevelFlagsInitializer(Input, combat, movement, events,
            backgrounds, effects, enemies, frame, pickups, warnings, armorWarning);
        return new(game, effects, enemies, combat, backgrounds, events, shots, movement, weapons,
            specialWeapons, input, shipInfo, frame, pickups, warnings, armorWarning, flagsInitializer);
    }

    public LevelMap LoadLevelMap(LevelGameplayRuntime gameplay, byte levelNumber)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(gameplay);
        return gameplay.LoadMap(Resources.Assets.Files, Resources.Assets.Episodes, levelNumber);
    }

    public void RunLevelMainLoop(LegacyGameState game, ILevelMainLoopHost host)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(host);
        var state = CreateLevelMainLoopState(game);
        try
        {
            LevelMainLoop.Run(state, host);
        }
        finally
        {
            Startup.PlayDemo = state.PlayDemo;
            game.TwoPlayerMode = state.TwoPlayerMode;
            game.EndLevel = state.EndLevel;
            game.ReallyEndLevel = state.ReallyEndLevel;
            game.PlayerEndLevel = state.PlayerEndLevel;
            game.ExtraGame = state.ExtraGame;
            game.DoNotSaveBackup = state.DoNotSaveBackup;
            game.MainLevel = state.MainLevel;
        }
    }

    public PlayerInput CreatePlayerInput(LegacyGameState game, PlayerMovement movement,
        PlayerCombat combat, LevelEvents level)
        => CreatePlayerInput(game, movement, combat, level, GameplayRequests);

    public PlayerInput CreatePlayerInput(LegacyGameState game, PlayerMovement movement,
        PlayerCombat combat, LevelEvents level, NetworkRequests requests)
    {
        EnsureOpen();
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(movement);
        ArgumentNullException.ThrowIfNull(combat);
        ArgumentNullException.ThrowIfNull(level);
        ArgumentNullException.ThrowIfNull(requests);

        return new(Input, Joysticks.Input, Joysticks.States, Configuration.Keys,
            movement, combat, level, Startup.NetworkGame
                ? Network?.Requests ?? throw new InvalidOperationException("Network gameplay has no initialized network session.")
                : requests)
        {
            NetworkGame = Startup.NetworkGame,
            LocalPlayer = Startup.NetworkGame
                ? checked((byte)Startup.Options.PlayerNumber)
                : (byte)1,
            RecordDemo = Startup.RecordDemo,
            PlayDemo = Startup.PlayDemo,
            ConstantPlay = Startup.Options.ConstantPlay
        };
    }

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static AudioMixer CreateSilentMixer()
    {
        var mixer = new AudioMixer(22050, new SilentMusicSynthesizer()) { AudioDisabled = true };
        return mixer;
    }

    private sealed class SilentMusicSynthesizer : IMusicSynthesizer
    {
        public void Update() { }
        public void Render(Span<short> samples) => samples.Clear();
    }

    private SpriteSheet LoadCursorSprites()
    {
        if (_cursorSprites is not null)
            return _cursorSprites;
        var sprites = new SpriteSheet();
        try
        {
            using var stream = Resources.Assets.Files.OpenRequired(Resources.Assets.Files.Directory, "newsh1.shp");
            sprites.Load(stream, checked((int)stream.Length));
            _cursorSprites = sprites;
            return sprites;
        }
        catch
        {
            sprites.Clear();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { Resources.Dispose(); }
        finally
        {
            _cursorSprites?.Clear();
            _cursorSprites = null;
            try { _networkTransport?.Dispose(); }
            finally
            {
                try { InputPlatform.Dispose(); }
                finally
                {
                    try { Joysticks.Dispose(); }
                    finally { Video.Dispose(); }
                }
            }
        }
        GC.SuppressFinalize(this);
    }
}
