using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class GameRuntimePlatformTests
{
    internal static void Run()
    {
        if (Environment.GetEnvironmentVariable("SDL_VIDEODRIVER") != "dummy")
            throw new InvalidOperationException("Runtime platform checks require SDL_VIDEODRIVER=dummy.");
        if (Environment.GetEnvironmentVariable("SDL_AUDIODRIVER") != "dummy")
            throw new InvalidOperationException("Runtime platform checks require SDL_AUDIODRIVER=dummy.");
        var root = Path.Combine(Path.GetTempPath(), $"opentyrian-runtime-platform-{Guid.NewGuid():N}");
        var working = Path.Combine(root, "working");
        var assets = Path.Combine(root, "assets");
        var incompleteAssets = Path.Combine(root, "incomplete-assets");
        var user = Path.Combine(root, "user");
        Directory.CreateDirectory(working);
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(incompleteAssets);
        Directory.CreateDirectory(user);
        var dos = new DosConfiguration();
        dos.Defaults();
        dos.InputDevices[0] = 2;
        dos.InputDevices[1] = 1;
        using (var stream = File.Create(Path.Combine(user, "tyrian.cfg"))) dos.Write(stream);
        try
        {
            File.WriteAllBytes(Path.Combine(assets, "tyrian1.lvl"), BuildRuntimeLevelArchive());
            File.WriteAllBytes(Path.Combine(assets, "shapesa.dat"), LevelMapTests.BuildShapes(2));
            var paletteData = new byte[23 * 256 * 3];
            paletteData[(7 * 256 + 42) * 3] = 20;
            paletteData[(7 * 256 + 42) * 3 + 1] = 30;
            paletteData[(7 * 256 + 42) * 3 + 2] = 40;
            File.WriteAllBytes(Path.Combine(assets, "palette.dat"), paletteData);
            File.WriteAllBytes(Path.Combine(assets, "tyrian.pic"), BuildPictureArchive());
            File.WriteAllBytes(Path.Combine(assets, "estsc.shp"), BuildCreditsPictures());
            File.WriteAllBytes(Path.Combine(assets, "newsh1.shp"), BuildCursorArchive());
            File.WriteAllBytes(Path.Combine(assets, "tyrian.shp"), GameRuntimeAssetsTests.Shapes());
            File.WriteAllBytes(Path.Combine(assets, "tyrian.hdt"), GameRuntimeAssetsTests.HelpArchive());
            File.WriteAllBytes(Path.Combine(assets, "music.mus"), GameRuntimeAssetsTests.MusicArchive());
            File.WriteAllBytes(Path.Combine(assets, "tyrian.snd"), SoundBankTests.BuildBank(29, false, 0));
            File.WriteAllBytes(Path.Combine(assets, "voices.snd"), SoundBankTests.BuildBank(9, true, 1));
            GameRuntimeAssetsTests.WriteItemBundles(working);
            File.WriteAllText(Path.Combine(user, "opentyrian.cfg"),
                "section \"video\"\n\titem \"fullscreen\" \"-1\"\n" +
                "\titem \"scaler\" \"None\"\n\titem \"scaling_mode\" \"Center\"\n" +
                "section \"keyboard\"\n\titem \"up\" \"A\"\n");

            var failedStartup = new GameStartupState();
            GameStartupArguments.Parse(failedStartup,
                ["OpenTyrian", "--data", incompleteAssets, "--no-sound"], TextWriter.Null, TextWriter.Null);
            try
            {
                GameRuntimePlatform.Start(failedStartup, working, user, TextWriter.Null, TextWriter.Null,
                    () => { }, () => false, () => { }, () => 1u).Dispose();
                throw new InvalidOperationException("Incomplete runtime assets unexpectedly started.");
            }
            catch (IOException) { }

            var startup = new GameStartupState();
            var parsed = GameStartupArguments.Parse(startup,
                ["OpenTyrian", "--data", assets, "--no-sound", "--record", "--constant", "--loot", "--death"],
                TextWriter.Null, TextWriter.Null);
            Check(parsed.ExitCode is null, "Runtime platform test options did not parse.");
            using (var output = new StringWriter())
            using (var diagnostics = new StringWriter())
            using (var platform = GameRuntimePlatform.Start(startup, working, user, output, diagnostics,
                () => throw new InvalidOperationException("Unexpected quit callback."),
                () => false, () => throw new InvalidOperationException("Unexpected network callback."),
                () => 1u))
            {
                Check(platform.Video.ScalerName == "None" &&
                    platform.Configuration.Layout.Mode == ScalingMode.Center &&
                    platform.Video.Layout.Mode == ScalingMode.Center &&
                    platform.Configuration.GetKey(KeySetting.Up) == 4 &&
                    platform.Resources.AudioSession is null &&
                    platform.Resources.Assets.Episodes.Available[0],
                    $"Runtime platform composition differs (scaler={platform.Video.ScalerName}, " +
                    $"config layout={platform.Configuration.Layout.Mode}, video layout={platform.Video.Layout.Mode}, " +
                    $"audio={platform.Resources.AudioSession is not null}, " +
                    $"episode={platform.Resources.Assets.Episodes.Available[0]}).");
                var players = new[] { new PlayerState(0), new PlayerState(1) };
                var effects = new SceneEffects(new byte[8]);
                var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
                var items = platform.Resources.Assets.Items;
                var game = new LegacyGameState();
                platform.LoadPicture(2, platform.Video.Primary);
                var activePicturePalette = platform.Video.Palette.Active.ToArray();
                Check(platform.Video.Primary.Pixels.Span[..320].ToArray().All(pixel => pixel == 42) &&
                    platform.Video.Primary.Pixels.Span[199 * platform.Video.Primary.Pitch + 319] == 42 &&
                    platform.PictureColors.Span[42] == new PaletteColor(81, 121, 162) &&
                    platform.Video.Palette.Active.Span[42] == new PaletteColor(81, 121, 162),
                    "Runtime picture loading did not compose the indexed frame and selected palette.");
                var cursor = platform.CreateMouseCursor();
                cursor.Start();
                cursor.Replace();
                var titleHost = platform.CreateTitleScreenHost(platform.Video.Secondary, "test"u8.ToArray(),
                    () => false, () => false, () => { }, () => { }, () => { }, () => { }, () => { },
                    _ => false);
                var selection = new SelectionMenuState();
                var selectionHost = platform.CreateSelectionMenuPresentation(platform.Video.Secondary,
                    () => { });
                var menus = platform.CreateSelectionMenus(selectionHost, selection);
                var newGameHost = platform.CreateNewGameHost(game, menus, selection);
                Check(newGameHost.InitializeEpisode(1) == 1 &&
                    platform.Resources.Assets.Episodes.Number == 1,
                    "Runtime new-game host did not route episode selection through its owned archive.");
                var composedTitle = platform.CreateTitleScreen(game, menus, selection, platform.Video.Secondary,
                    "test"u8.ToArray(), episode => episode, () => { }, () => { }, () => { }, () => { }, () => { });
                var managedScreenTitle = platform.CreateTitleScreen(game, menus, selection,
                    platform.Video.Secondary, "test"u8.ToArray(), episode => episode,
                    () => { }, () => { }, () => { }, () => { });
                Check(titleHost.Width == 320 && newGameHost is not null,
                    "Runtime title and new-game hosts did not compose indexed video, fonts, palettes, cursor, timer, audio, and menu callbacks.");
                Check(composedTitle is not null && managedScreenTitle is not null,
                    "Runtime title screen did not bind new-game, saved-game, arcade, and SuperTyrian child flows.");
                var introHost = platform.CreateIntroLogoHost();
                introHost.LoadPicture(10);
                introHost.Present();
                var creditsHost = platform.CreateCreditsHost(() => { });
                creditsHost.LoadPictures();
                Check(creditsHost.PictureCount == 1,
                    "Runtime credits host did not load the credits picture archive.");
                creditsHost.FreePictures();
                Check(platform.CreateCredits("Episode"u8.ToArray(), () => { }) is not null,
                    "Runtime credits flow did not bind item data, episode text, and SDL services.");
                var episodePalette = Enumerable.Repeat(new PaletteColor(8, 16, 24),
                    PaletteSystem.ColorCount).ToArray();
                var keepAliveCalls = 0;
                var transitionHost = platform.CreateEpisodeTransitionHost(episodePalette,
                    () => { }, () => { }, () => keepAliveCalls++);
                transitionHost.Clear();
                transitionHost.SelectPalette();
                transitionHost.FadeIn(1);
                transitionHost.KeepAlive();
                Check(keepAliveCalls == 1 &&
                    platform.Video.Palette.Active.Span.SequenceEqual(episodePalette) &&
                    platform.Video.Primary.Pixels.Span[..platform.Video.Primary.Pitch]
                        .ToArray().All(pixel => pixel == 0),
                    "Runtime episode-transition host did not bind the selected episode palette, SDL surface, and keep-alive callback.");
                var completionCalls = 0;
                var completionHost = platform.CreateEpisodeCompletionHost(
                    () => completionCalls++, () => completionCalls++, () => completionCalls++);
                completionHost.Clear();
                completionHost.LoadPicture(2);
                completionHost.FadePalette(1, 0, 255);
                completionHost.DisplayText();
                completionHost.NextEpisode();
                completionHost.ReadTextSync();
                Check(completionCalls == 3 &&
                    platform.Video.Primary.Pixels.Span[..platform.Video.Primary.Pitch]
                        .ToArray().All(pixel => pixel == 42) &&
                    platform.Video.Palette.Active.Span[42] == new PaletteColor(81, 121, 162),
                    "Runtime episode-completion host did not bind SDL picture/palette/fade services and progression callbacks.");
                var completionState = new EpisodeCompletionState();
                Check(platform.CreateEpisodeCompletion(completionState, "Score"u8.ToArray(),
                    ["Player 1"u8.ToArray(), "Player 2"u8.ToArray()],
                    new EpisodeSuperArcadeData([], [], "Engage"u8.ToArray(), "Pilot"u8.ToArray(), []),
                    () => { }, () => { }, () => { }) is not null,
                    "Runtime episode-completion flow did not bind the state, progression tables, and concrete host.");
                Check(platform.InitializeEpisode(game, 1) == 1 && game.Episode == 1 &&
                    platform.Resources.Assets.Episodes.Number == 1,
                    "Runtime episode initialization did not update the shared episode archive and game state.");
                platform.InitializePlayerData(game, "Start"u8);
                game.SecretHint = 2;
                game.SuperTyrian = true;
                game.SuperArcadeMode = 3;
                game.Players[0].Cash = 123;
                game.Players[1].Cash = 456;
                var composedCompletionState = platform.CreateEpisodeCompletionState(game, 579);
                Check(composedCompletionState.SecretHint == 2 && composedCompletionState.SuperTyrian &&
                    composedCompletionState.SuperArcadeMode == 3 && composedCompletionState.TotalScore == 579 &&
                    composedCompletionState.Cash.SequenceEqual(new ulong[] { 123, 456 }) &&
                    composedCompletionState.ConstantPlay && !composedCompletionState.NetworkGame,
                    "Runtime episode-completion state did not bridge launch flags and player score/cash data.");
                Check(platform.CreateEpisodeCompletionState(game).TotalScore ==
                    new PlayerProgression(game, items).TotalScore(game.Players[0]),
                    "Runtime episode-completion state did not calculate single-player score from managed item values.");
                game.SuperTyrian = false;
                game.SuperArcadeMode = 9;
                var superArcade = new EpisodeSuperArcadeData([], [], "Engage"u8.ToArray(),
                    "Pilot"u8.ToArray(), []);
                using var automaticCompletionEpisode = BuildEpisodeCompletionText();
                Check(!platform.RunEpisodeCompletion(game, automaticCompletionEpisode,
                        "Score"u8.ToArray(), ["Player 1"u8.ToArray(), "Player 2"u8.ToArray()],
                        superArcade, () => { }, () => platform.Resources.Assets.Episodes.FindNext(), () => { }) &&
                    platform.Startup.JumpSection,
                    "Runtime episode-completion runner did not update its jump-back state after episode progression.");
                var completionGame = new LegacyGameState { MainLevel = 5, SuperArcadeMode = 9 };
                var completionRunState = new EpisodeCompletionState
                {
                    SecretHint = 1,
                    ConstantPlay = true,
                    JumpBackToEpisode1 = true,
                    SuperArcadeMode = 9,
                    MainLevel = 5
                };
                var completionRunCalls = 0;
                using var completionEpisode = BuildEpisodeCompletionText();
                Check(!platform.RunEpisodeCompletion(completionGame, completionRunState, completionEpisode,
                        "Score"u8.ToArray(), ["Player 1"u8.ToArray(), "Player 2"u8.ToArray()],
                        superArcade, () => completionRunCalls++, () => completionRunCalls++,
                        () => completionRunCalls++) &&
                    completionGame.MainLevel == 5 && completionGame.SuperArcadeMode == 9 &&
                    platform.Startup.JumpSection && completionRunCalls == 1,
                    "Runtime episode completion did not publish its section transition into game/startup state.");
                var failedCompletionState = new EpisodeCompletionState
                {
                    SecretHint = 1,
                    ConstantPlay = true,
                    NetworkGame = true,
                    JumpBackToEpisode1 = true,
                    SuperArcadeMode = 9,
                    MainLevel = 6
                };
                using var failedCompletionEpisode = BuildEpisodeCompletionText();
                try
                {
                    platform.RunEpisodeCompletion(completionGame, failedCompletionState, failedCompletionEpisode,
                        "Score"u8.ToArray(), ["Player 1"u8.ToArray(), "Player 2"u8.ToArray()],
                        superArcade, () => { }, () => { },
                        () => throw new InvalidOperationException("synthetic completion sync failure"));
                    throw new InvalidOperationException("Runtime completion swallowed a network synchronization failure.");
                }
                catch (InvalidOperationException exception) when
                    (exception.Message == "synthetic completion sync failure") { }
                Check(platform.Startup.JumpSection && completionGame.MainLevel == 6,
                    "Runtime episode completion failed to publish mutated game state when synchronization failed.");
                var episodeTransition = platform.CreateEpisodeTransition(game,
                    [Array.Empty<byte>()], "Continue"u8.ToArray(),
                    platform.Resources.Assets.Palettes[0], "Episode"u8.ToArray(), () => { }, () => { });
                Check(!episodeTransition.NetworkGame && episodeTransition.ConstantPlay,
                    "Runtime episode transition did not bind launch mode and the composed credits flow.");
                Check(platform.CreateEpisodeTransition(game, "Continue"u8.ToArray(),
                        platform.Resources.Assets.Palettes[0], "Episode"u8.ToArray(), () => { }, () => { })
                        is not null,
                    "Runtime episode transition did not bind loaded episode-name data.");
                var highScoreHost = platform.CreateHighScoreScreenHost();
                highScoreHost.EnsureCursor();
                highScoreHost.LoadBackground();
                var helpHost = platform.CreateHelpSystemHost();
                helpHost.EnsureCursor();
                helpHost.LoadBackground();
                Check(platform.Video.Secondary.Pixels.Span[199 * platform.Video.Secondary.Pitch + 319] == 42,
                    "Runtime high-score and help hosts did not load their shared title background.");
                var setupHost = platform.CreateSetupMenuHost(platform.Video.Secondary, () => { }, () => { });
                setupHost.EnsureCursor();
                setupHost.LoadBackground();
                setupHost.RestoreBackground();
                setupHost.MusicDisabled = true;
                setupHost.SamplesDisabled = true;
                Check(setupHost.MusicDisabled && setupHost.SamplesDisabled &&
                    setupHost.Scaler == 0 && setupHost.ScalingMode == ScalingMode.Center,
                    "Runtime setup host did not bind configuration, SDL video, cursor, and silent-audio settings.");
                titleHost.LoadPicture();
                titleHost.Present();
                Check(platform.Video.ReadPixels().Length == 320 * 200,
                    "Runtime title host did not present the loaded indexed background through SDL.");
                platform.LoadPicture(3, platform.Video.Secondary, storePalette: false);
                Check(platform.Video.Secondary.Pixels.Span[199 * platform.Video.Secondary.Pitch + 319] == 42 &&
                    platform.PictureColors.Span[42] == default &&
                    platform.Video.Palette.Active.Span.SequenceEqual(activePicturePalette),
                    "Runtime picture loading changed palette state when palette storage was disabled.");
                var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
                var shots = new PlayerShots(items.Shots, items.Weapons, players, new NoopShotEffects());
                var movement = new PlayerMovement(players, combat, level, shots, items);
                platform.InitializePlayerData(game, "Start"u8);
                game.Episode = game.InitialEpisode = 1;
                game.Difficulty = 2;
                game.Players[0].Cash = 12345;
                game.SaveLevel = 4;
                game.LastLevelName = "Runtime"u8.ToArray();
                Directory.CreateDirectory(Path.Combine(user, "saves"));
                VersionedSaveGame.SaveFile(user, "Runtime", game, items);
                var performSave = true;
                var loadScreenHost = platform.CreateLoadScreenHost(game, () => performSave = false,
                    episode => episode);
                Check(loadScreenHost.BuildSaves().Count == 1 &&
                    loadScreenHost.BuildSaves()[0].Name == "Runtime",
                    "Runtime load-screen host did not discover compatible saved games.");
                game.GameLoaded = false;
                loadScreenHost.LoadSave("Runtime");
                Check(!performSave && loadScreenHost.GameLoaded && game.GameJustLoaded &&
                    game.MainLevel == 4 && game.Players[0].Cash == 12345,
                    "Runtime load-screen host did not clear pending saves and restore selected game state.");
                var playerInput = platform.CreatePlayerInput(game, movement, combat, level);
                var newGame = platform.CreateNewGame(game, new NoopNewGameHost());
                var mainLoop = platform.CreateLevelMainLoopState(game);
                var gameplayGame = new LegacyGameState
                {
                    Difficulty = 4,
                    InitialDifficulty = 3,
                    SuperTyrian = true,
                    OnePlayerAction = true,
                    TwoPlayerMode = true,
                    SuperArcadeMode = 3
                };
                platform.Startup.PlayDemo = true;
                var gameplay = platform.CreateLevelGameplay(gameplayGame, platform.Video.Primary,
                    new NoopShotEffects());
                platform.Startup.PlayDemo = false;
                var gameplayMap = platform.LoadLevelMap(gameplay, 1);
                Check(game.InputDevices.SequenceEqual(new byte[] { 2, 1 }) &&
                    playerInput.RecordDemo && playerInput.ConstantPlay && !playerInput.NetworkGame &&
                    newGame.RichMode && newGame.ConstantDie &&
                    mainLoop.RecordDemo && !mainLoop.PlayDemo && !mainLoop.NetworkGame &&
                    !mainLoop.TwoPlayerMode && mainLoop.MainLevel == game.MainLevel &&
                    ReferenceEquals(playerInput.Requests, platform.GameplayRequests) &&
                    playerInput.LocalPlayer == 1,
                    "Runtime platform did not compose new-game, level-loop, and gameplay input from typed launch options.");
                Check(ReferenceEquals(gameplay.Map, gameplayMap) &&
                    ReferenceEquals(gameplay.Backgrounds.First.Tiles, gameplayMap.Maps[0]) &&
                    ReferenceEquals(gameplay.Backgrounds.Second.Tiles, gameplayMap.Maps[1]) &&
                    ReferenceEquals(gameplay.Backgrounds.Third.Tiles, gameplayMap.Maps[2]) &&
                    ReferenceEquals(gameplay.Events.Events, gameplayMap.Header.Events) &&
                    gameplay.Scene.LevelEnemyMaximum == gameplayMap.Header.Enemies.Length &&
                    gameplay.Scene.LevelEnemies.Take(gameplayMap.Header.Enemies.Length)
                        .SequenceEqual(gameplayMap.Header.Enemies),
                    "Runtime gameplay did not load the selected episode map into its shared background, event, and enemy state.");
                Check(ReferenceEquals(gameplay.Game, gameplayGame) &&
                    ReferenceEquals(gameplay.Players, gameplayGame.Players) &&
                    ReferenceEquals(gameplay.Enemies.Enemies, gameplay.Shots.Enemies) &&
                    ReferenceEquals(gameplay.Effects.SoundQueue, gameplay.Shots.SoundQueue) &&
                    ReferenceEquals(gameplay.Frame.Input, gameplay.Input) &&
                    gameplay.Frame.Episode == gameplayGame.Episode &&
                    gameplay.Frame.Devices.SequenceEqual(gameplayGame.InputDevices) &&
                    gameplay.Combat.TwoPlayers && !gameplay.Combat.Galaga &&
                    gameplay.Combat.Cheating == platform.Startup.YouAreCheating &&
                    gameplay.Enemies.Difficulty == gameplayGame.Difficulty &&
                    gameplay.Enemies.SuperArcadeMode == gameplayGame.SuperArcadeMode &&
                    gameplay.Enemies.TwoPlayers &&
                    gameplay.Events.SuperTyrian && gameplay.Events.OnePlayerAction &&
                    gameplay.Events.InitialDifficulty == gameplayGame.InitialDifficulty &&
                    gameplay.Input.PlayDemo &&
                    gameplay.SpecialWeapons.ArcadeMode == gameplayGame.SuperArcadeMode &&
                    gameplay.Shots.BackgroundTwo == platform.Settings.Runtime.Background2 &&
                    ReferenceEquals(gameplay.Input.Requests, platform.GameplayRequests),
                    "Runtime level gameplay composition split shared state or failed to apply game and launch modes.");
                gameplay.Frame.MapXOffset = 99;
                gameplay.Frame.MapX3Offset = 99;
                foreach (var player in gameplayGame.Players)
                    player.XVelocity = player.YVelocity = 4;
                gameplay.InitializeMapAndShipState();
                Check(gameplay.Backgrounds.First.MapY == 292 &&
                    gameplay.Backgrounds.Second.MapY == 592 &&
                    gameplay.Backgrounds.Third.MapY == 592 &&
                    gameplay.Frame.MapXOffset == 0 && gameplay.Frame.MapX3Offset == 0 &&
                    gameplay.Backgrounds.SecondDelay == 1 && gameplay.Events.FirstDelay == 1 &&
                    gameplay.Shots.Power == 0 &&
                    gameplayGame.Players.All(player => player.InvulnerableTicks == 100 &&
                        player.XVelocity == 0 && player.YVelocity == 0),
                    "Runtime gameplay did not initialize the shared map, player-ship, and frame state.");
                gameplay.Input.InputState.NewKey = gameplay.Input.InputState.NewMouse = true;
                gameplay.Pickups.BonusLevel = true;
                gameplay.Events.ReadyToEndLevel = true;
                gameplay.Events.FirstGameOver = false;
                gameplay.Events.EnemiesActive = false;
                gameplay.Events.QuitRequested = true;
                gameplay.Frame.Buttons[0] = true;
                gameplay.Enemies.ContinualDamage = true;
                gameplay.Enemies.GlobalFlags[0] = true;
                gameplay.Enemies.BossBars[0].LinkNumber = 3;
                gameplay.InitializeFlags();
                Check(!gameplay.Input.InputState.NewKey && !gameplay.Input.InputState.NewMouse &&
                    !gameplay.Pickups.BonusLevel && !gameplay.Events.ReadyToEndLevel &&
                    gameplay.Events.FirstGameOver && gameplay.Events.EnemiesActive &&
                    !gameplay.Events.QuitRequested && !gameplay.Frame.Buttons[0] &&
                    !gameplay.Enemies.ContinualDamage && gameplay.Enemies.GlobalFlags[0] &&
                    gameplay.Enemies.BossBars[0].LinkNumber == 0 &&
                    gameplay.Combat.LevelEnd == byte.MaxValue &&
                    gameplay.Movement.LevelEndWarp == -4 &&
                    gameplay.Backgrounds.LevelFilter == -99 &&
                    gameplay.Backgrounds.Brightness == -14,
                    $"Runtime gameplay did not reset level flags/input: input={gameplay.Input.InputState.NewKey}/{gameplay.Input.InputState.NewMouse}, " +
                    $"pickup={gameplay.Pickups.BonusLevel}, event={gameplay.Events.ReadyToEndLevel}/{gameplay.Events.FirstGameOver}/" +
                    $"{gameplay.Events.EnemiesActive}/{gameplay.Events.QuitRequested}, button={gameplay.Frame.Buttons[0]}, " +
                    $"enemy={gameplay.Enemies.ContinualDamage}/{gameplay.Enemies.GlobalFlags[0]}/" +
                    $"{gameplay.Enemies.BossBars[0].LinkNumber}, combat={gameplay.Combat.LevelEnd}/{gameplay.Movement.LevelEndWarp}, " +
                    $"filter={gameplay.Backgrounds.LevelFilter}/{gameplay.Backgrounds.Brightness}.");
                gameplay.Shots.Availability[0] = 1;
                gameplay.Enemies.Enemies[0].Availability = 0;
                gameplay.Requests.YourInGameMenuRequest = true;
                var gameplayButtons = new[] { true, true, true, true };
                var optionDraws = 0;
                gameplay.InitializeCombatState(gameplayButtons, () => optionDraws++);
                Check(gameplay.Shots.Availability.All(value => value == 0) &&
                    !gameplay.Requests.YourInGameMenuRequest &&
                    gameplayButtons.All(value => !value) && optionDraws == 1 &&
                    gameplay.Enemies.Enemies[0].Availability == 0 &&
                    gameplay.Weapons.ChargeWait == 5 && gameplay.Enemies.GalagaLife == 10000 &&
                    gameplay.Backgrounds.First.WrapAt == 14 &&
                    gameplay.Backgrounds.Third.WrapAt == 15,
                    "Runtime gameplay did not run native-order combat-state initialization against its shared owners.");
                var loopGame = new LegacyGameState { MainLevel = 1 };
                platform.RunLevelMainLoop(loopGame, new RuntimeLoopHost());
                Check(loopGame.MainLevel == 0 && loopGame.TwoPlayerMode && loopGame.EndLevel &&
                    loopGame.ReallyEndLevel && loopGame.PlayerEndLevel && loopGame.ExtraGame &&
                    loopGame.DoNotSaveBackup && platform.Startup.PlayDemo,
                    "Runtime level loop did not publish mutated native-compatible game state.");
                var failedLoopGame = new LegacyGameState { MainLevel = 1 };
                try
                {
                    platform.RunLevelMainLoop(failedLoopGame, new RuntimeLoopHost(true));
                    throw new InvalidOperationException("Level-loop host failure was swallowed.");
                }
                catch (IOException error) when (error.Message == "scripted level-map failure")
                {
                    Check(failedLoopGame.MainLevel == 0 && failedLoopGame.EndLevel &&
                        platform.Startup.PlayDemo,
                        "Runtime level loop did not publish mutated state on host failure.");
                }
                platform.SaveGame(game, 1, "Runtime test"u8);
                var loaded = new LegacyGameState();
                platform.LoadGame(loaded, 1, episode => episode);
                Check(loaded.Episode == 1 && loaded.MainLevel == 4 && loaded.Difficulty == 2 &&
                    loaded.Players[0].Cash == 12345 && loaded.InputDevices.SequenceEqual(new byte[] { 2, 1 }),
                    "Runtime platform did not persist and reload a native legacy game save.");
                platform.SaveConfiguration();
                Check(File.Exists(Path.Combine(user, "tyrian.cfg")) &&
                    File.Exists(Path.Combine(user, "tyrian.sav")) &&
                    File.Exists(Path.Combine(user, "opentyrian.cfg")),
                    "Runtime platform did not persist DOS, save, and text configuration.");
            }

            var networkStartup = new GameStartupState();
            var localPort = GetAvailableUdpPort();
            parsed = GameStartupArguments.Parse(networkStartup,
                ["OpenTyrian", "--data", assets, "--no-sound", "--net=127.0.0.1",
                "--net-port", localPort.ToString(), "--net-player-number", "2"],
                TextWriter.Null, TextWriter.Null);
            Check(parsed.ExitCode is null, "Runtime platform network options did not parse.");
            using (var networkPlatform = GameRuntimePlatform.Start(networkStartup, working, user,
                TextWriter.Null, TextWriter.Null, () => { }, () => false, () => { }, () => 1u))
            {
                var network = networkPlatform.Network
                    ?? throw new InvalidOperationException("Runtime platform did not own an SDL2_net session.");
                Check(networkPlatform.Startup.NetworkGame &&
                    network.DelayFrames == networkStartup.Options.NetworkDelay,
                    "Runtime platform did not own a configured SDL2_net gameplay session.");
                var players = new[] { new PlayerState(0), new PlayerState(1) };
                var effects = new SceneEffects(new byte[8]);
                var items = networkPlatform.Resources.Assets.Items;
                var combat = new PlayerCombat(players, effects, new(320, 200, 320, TextWriter.Null));
                var level = new LevelEvents(items, new(items, TextWriter.Null), players, new(), effects);
                var shots = new PlayerShots(items.Shots, items.Weapons, players, new NoopShotEffects());
                var movement = new PlayerMovement(players, combat, level, shots, items);
                var game = new LegacyGameState { TwoPlayerMode = true, ExtraGame = true, MainLevel = 9 };
                var mainLoop = networkPlatform.CreateLevelMainLoopState(game);
                var playerInput = networkPlatform.CreatePlayerInput(game, movement, combat, level);
                var gameplay = networkPlatform.CreateLevelGameplay(game, networkPlatform.Video.Primary,
                    new NoopShotEffects());
                gameplay.Enemies.Enemies[0].Availability = 0;
                var networkButtons = new[] { true, true, true, true };
                gameplay.InitializeCombatState(networkButtons, () => { });
                var selection = new SelectionMenuState { TwoPlayerMode = true };
                var menuPresentation = networkPlatform.CreateSelectionMenuPresentation(
                    networkPlatform.Video.Secondary, () => { });
                var menus = networkPlatform.CreateSelectionMenus(menuPresentation, selection);
                var networkHost = networkPlatform.CreateNetworkStartHost(game, menus, selection,
                    networkPlatform.Video.Secondary);
                Check(networkHost.InitializeEpisode(1) == 1 &&
                    networkPlatform.Resources.Assets.Episodes.Number == 1,
                    "Runtime network host did not route episode selection through its owned archive.");
                networkHost.LoadPicture();
                networkHost.Capture();
                networkHost.Restore();
                Check(ReferenceEquals(playerInput.Requests, network.Requests) &&
                    playerInput.LocalPlayer == 2 && mainLoop.NetworkGame &&
                    mainLoop.TwoPlayerMode && mainLoop.ExtraGame && mainLoop.MainLevel == 9 &&
                    game.TwoPlayerMode && gameplay.Input.NetworkGame &&
                    gameplay.Input.LocalPlayer == 2 && gameplay.Enemies.TwoPlayers &&
                    gameplay.Enemies.Enemies.All(enemy => enemy.Availability == 1) &&
                    networkButtons.All(value => !value) &&
                    ReferenceEquals(gameplay.Input.Requests, network.Requests) &&
                    networkHost is not null,
                    "Network runtime did not compose the shared request queue, player identity, level-loop state, and screen services.");
            }

            var audioStartup = new GameStartupState();
            parsed = GameStartupArguments.Parse(audioStartup, ["OpenTyrian", "--data", assets],
                TextWriter.Null, TextWriter.Null);
            Check(parsed.ExitCode is null, "Runtime platform audio options did not parse.");
            using (var audioPlatform = GameRuntimePlatform.Start(audioStartup, working, user, TextWriter.Null,
                TextWriter.Null, () => { }, () => false, () => { }, () => 1u))
            {
                var audio = audioPlatform.Resources.AudioSession
                    ?? throw new InvalidOperationException("Runtime platform did not initialize configured SDL audio.");
                Check(!audioStartup.AudioDisabled && audio.Sounds.Samples.Count == 38,
                    "Runtime platform did not compose loaded sound data with the SDL audio device.");
                audio.PlaySample(5);
                var timeout = Stopwatch.StartNew();
                while (audio.CompletedCallbacks == 0 && timeout.ElapsedMilliseconds < 5000)
                {
                    audio.ThrowIfFaulted();
                    Thread.Sleep(5);
                }
                Check(audio.CompletedCallbacks > 0, "Runtime platform SDL audio callback did not progress.");
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static ushort GetAvailableUdpPort()
    {
        for (var attempt = 0; attempt < 128; attempt++)
        {
            var port = Random.Shared.Next(40000, 49152);
            try
            {
                using var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, port));
                return checked((ushort)((IPEndPoint)socket.Client.LocalEndPoint!).Port);
            }
            catch (SocketException) { }
        }
        throw new IOException("Could not reserve a free UDP port accepted by the native-compatible CLI.");
    }

    private static byte[] BuildPictureArchive()
    {
        const int headerSize = 2 + PictureLoader.PictureCount * sizeof(int);
        var image = new byte[12 * 200];
        for (var row = 0; row < 200; row++)
        {
            var offset = row * 12;
            for (var run = 0; run < 5; run++)
            {
                image[offset++] = 0xFF;
                image[offset++] = 42;
            }

            image[offset++] = 0xC5;
            image[offset] = 42;
        }

        var archive = new byte[headerSize + image.Length * PictureLoader.PictureCount];
        System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(archive, PictureLoader.PictureCount);
        for (var index = 0; index < PictureLoader.PictureCount; index++)
        {
            var offset = headerSize + index * image.Length;
            System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(archive.AsSpan(2 + index * 4), offset);
            image.CopyTo(archive, offset);
        }
        return archive;
    }

    private static byte[] BuildRuntimeLevelArchive()
    {
        using var archive = new MemoryStream();
        BinaryFile.WriteUInt16(archive, 1);
        BinaryFile.WriteInt32(archive, 6);
        archive.Write(LevelMapTests.BuildLevel(2, 'A'));
        return archive.ToArray();
    }

    private static byte[] BuildCreditsPictures()
    {
        using var stream = new MemoryStream();
        BinaryFile.WriteUInt16(stream, 1);
        stream.WriteByte(1);
        BinaryFile.WriteUInt16(stream, 1);
        BinaryFile.WriteUInt16(stream, 1);
        BinaryFile.WriteUInt16(stream, 1);
        stream.WriteByte(7);
        return stream.ToArray();
    }

    private static MemoryStream BuildEpisodeCompletionText()
    {
        var stream = new MemoryStream();
        for (var section = 0; section < 12; section++)
        {
            WriteEncryptedString(stream, "Hint"u8);
            WriteEncryptedString(stream, "#"u8);
        }
        stream.Position = 0;
        return stream;
    }

    private static void WriteEncryptedString(Stream stream, ReadOnlySpan<byte> text)
    {
        stream.WriteByte(checked((byte)text.Length));
        Span<byte> encrypted = stackalloc byte[255];
        ReadOnlySpan<byte> key = [204, 129, 63, 255, 71, 19, 25, 62, 1, 99];
        byte previous = 0;
        for (var index = 0; index < text.Length; index++)
        {
            encrypted[index] = (byte)(text[index] ^ key[index % key.Length] ^ previous);
            previous = encrypted[index];
        }
        stream.Write(encrypted[..text.Length]);
    }

    private static byte[] BuildCursorArchive()
    {
        const int dataOffset = 294 * sizeof(ushort);
        var archive = new byte[dataOffset + 1];
        for (var index = 0; index < 294; index++)
            System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(
                archive.AsSpan(index * sizeof(ushort)), dataOffset);
        archive[dataOffset] = 0x0F;
        return archive;
    }

    private sealed class NoopShotEffects : IPlayerShotEffects
    {
        public void Draw(int sheet, int x, int y, int frame, bool darken) { }
        public (ushort Width, ushort Height) DrawSpecial(int x, int y, int frame) => default;
        public void Explosion(int x, int y, byte type) { }
        public void SuperPixels(int x, int y, ushort count, byte width, byte color) { }
    }

    private sealed class NoopNewGameHost : INewGameHost
    {
        public bool SelectGameplay() => false;
        public bool SelectEpisode() => false;
        public bool SelectDifficulty() => false;
        public void LoadPicture() { }
        public void Clear() { }
        public void Title(int y, ReadOnlyMemory<byte> text, Font font) { }
        public void Ship(ushort graphic) { }
        public void Text(int y, ReadOnlyMemory<byte> text) { }
        public void Present() { }
        public void FadeIn(int steps) { }
        public void FadeBlack() { }
        public void WaitNoInput(CancellationToken cancellation) { }
        public void WaitInput(CancellationToken cancellation) { }
        public void PlaySong(uint song) { }
        public void Sample(byte sample) { }
        public byte InitializeEpisode(byte episode) => episode;
    }

    private sealed class RuntimeLoopHost(bool failAfterMap = false) : ILevelMainLoopHost
    {
        public bool PrepareNextLevel() => false;
        public void SetVolume() { }
        public void LoadMap(LevelMainLoopState state)
        {
            state.MainLevel = 0;
            state.PlayDemo = true;
            state.TwoPlayerMode = true;
            state.EndLevel = true;
            state.ReallyEndLevel = true;
            state.PlayerEndLevel = true;
            state.ExtraGame = true;
            state.DoNotSaveBackup = true;
            if (failAfterMap) throw new IOException("scripted level-map failure");
        }
        public void SetMouseRelative(bool enabled) { }
        public void FadeSong() { }
        public void InitializePlayersAndDisplay() { }
        public void InitializeMapAndShipState() { }
        public void InitializeFlags() { }
        public void InitializeStatusDisplay() { }
        public void ClearKeyboard() { }
        public void ClearSpecialRequests() { }
        public void SeedRandom(uint seed) { }
        public void InitializeStarfield() { }
        public void SetNewGameSpeed() { }
        public void SaveGame(int slot, string description) { }
        public void RecordLevelDemo() { }
        public void InitializeCombatState() { }
        public bool RunLevelFrame(uint[] oldWeaponBars, ref int lastEnemyOnScreen, byte[] buffer) => false;
    }
}
