using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class DestructUnitTests
{
    public static void Run()
    {
        var destructText = new Configuration();
        var generalOptions = destructText.AddSection("destruct");
        generalOptions.SetOption("antialias craters", "yes");
        generalOptions.SetOption("tracer laser", "on");
        generalOptions.SetOption("max shots", "-1");
        generalOptions.SetOption("max explosions", "22");
        generalOptions.SetOption("min walls", "3");
        generalOptions.SetOption("max walls", "7");
        generalOptions.SetOption("left ai", "no");
        generalOptions.SetOption("left jumper fires straight", "no");
        generalOptions.SetOption("right ai", "yes");
        generalOptions.SetOption("right jumper fires straight", "yes");
        var leftKeys = destructText.AddSection("destruct keyboard", "left");
        leftKeys.GetOrSetOption("left", null).AddValue("key9").AddValue("invalid");
        leftKeys.GetOrSetOption("change", null).AddValue("key11").AddValue("key12");
        var customOptions = destructText.AddSection("destruct custom");
        customOptions.SetOption("enable", "yes");
        customOptions.SetOption("left num units", "258");
        customOptions.GetOrSetOption("left unit", null)
            .AddValue("heli").AddValue("invalid").AddValue("dirt").AddValue("nuke");
        var defaultsForConfig = new int[2, 8, 4];
        defaultsForConfig[0, 0, 0] = 1;
        defaultsForConfig[0, 1, 0] = 2;
        defaultsForConfig[0, 1, 1] = 3;
        defaultsForConfig[0, 4, 0] = 4;
        defaultsForConfig[1, 0, 0] = 5;
        var configRosters = Enumerable.Range(0, 10).Select(_ =>
        {
            var roster = new DestructUnitRoster { InstallationCount = 5 };
            Array.Fill(roster.TypeChoices, DestructUnit.Tank);
            return roster;
        }).ToArray();
        var loadedDestructConfig = DestructUnits.LoadConfig(
            destructText, new DestructKeyboardNames(), defaultsForConfig, configRosters);
        Check(loadedDestructConfig.AlwaysAlias && loadedDestructConfig.TracerLaser &&
            loadedDestructConfig.MaximumShots == uint.MaxValue && loadedDestructConfig.MaximumExplosions == 22 &&
            loadedDestructConfig.MinimumWalls == 3 && loadedDestructConfig.MaximumWalls == 7 &&
            !loadedDestructConfig.IsCpu[0] && loadedDestructConfig.IsCpu[1] &&
            !loadedDestructConfig.JumperStraight[0] && loadedDestructConfig.JumperStraight[1] &&
            loadedDestructConfig.AllowCustom,
            "Destruct configuration did not load its gameplay options with native integer wrapping.");
        Check(defaultsForConfig[0, 0, 0] == 9 && defaultsForConfig[0, 0, 1] == 0 &&
            defaultsForConfig[0, 4, 0] == 11 && defaultsForConfig[0, 4, 1] == 12 &&
            defaultsForConfig[1, 0, 0] == 5 &&
            leftKeys.GetOption("left")!.Values.SequenceEqual(["key9"]) &&
            leftKeys.GetOption("change")!.Values.SequenceEqual(["key11", "key12"]),
            "Destruct keyboard loading did not filter invalid names, replace defaults, or serialize fallback keys.");
        Check(configRosters[8].InstallationCount == 2 &&
            configRosters[8].TypeChoices[0] == DestructUnit.Heli &&
            configRosters[8].TypeChoices[1] == DestructUnit.Dirt &&
            configRosters[8].TypeChoices[2] == DestructUnit.Nuke &&
            configRosters[8].TypeChoices.Skip(3).All(unitType => unitType == DestructUnit.Tank) &&
            customOptions.GetOption("left unit")!.Values.SequenceEqual(
                ["heli", "dirt", "nuke", "tank", "tank", "tank", "tank", "tank", "tank", "tank"]) &&
            customOptions.GetOption("right unit")!.Values.SequenceEqual(
                Enumerable.Repeat("tank", 10)),
            "Destruct custom unit configuration did not filter, pad, narrow, or write defaults.");
        var menuScreen = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var menuBackground = new IndexedSurface(320, 200, 320, TextWriter.Null);
        menuScreen.Pixels.Span.Fill(1);
        menuBackground.Pixels.Span.Fill(2);
        var menuHost = new DestructMenuHost([DestructMenuKey.Down, DestructMenuKey.Down, DestructMenuKey.Enter]);
        var selectedMode = DestructUnits.RunModeSelect(
            menuScreen, menuBackground, menuHost, allowCustom: false);
        Check(selectedMode == DestructMode.HeliAssault &&
            menuHost.ModeDraws.Contains((0, 82, true)) && menuHost.ModeDraws.Contains((2, 106, true)) &&
            !menuHost.CustomDrawn && menuHost.PaletteFades == 1 && menuHost.BlackFades == 1 &&
            menuHost.DelayCalls == 3 && menuScreen.Pixels.Span[0] == 2,
            "Destruct mode selection did not preserve menu layout, navigation, fade, delay, or screen restoration.");
        var customMenuHost = new DestructMenuHost([DestructMenuKey.Up, DestructMenuKey.Enter]);
        Check(DestructUnits.RunModeSelect(menuScreen, menuBackground, customMenuHost, allowCustom: true) ==
            DestructMode.Custom && customMenuHost.ModeDraws.Any(draw => draw.Mode == 4 && draw.Y == 130) &&
            customMenuHost.CustomDraws.Any(draw => draw.Y == 142 && draw.Selected),
            "Destruct custom mode navigation or custom-row drawing differs.");
        var quitMenuHost = new DestructMenuHost([DestructMenuKey.Escape]);
        Check(DestructUnits.RunModeSelect(menuScreen, menuBackground, quitMenuHost, allowCustom: false) is null,
            "Destruct mode menu did not return no selection on Escape.");
        var introHost = new DestructMenuHost([DestructMenuKey.Enter]);
        menuScreen.Pixels.Span.Fill(7);
        DestructUnits.ShowIntro(menuScreen, menuBackground, new byte[] { 1 }, new byte[] { 2 }, new byte[] { 3 }, introHost);
        Check(menuScreen.Pixels.Span[0] == 7 && introHost.CenteredDraws.SequenceEqual(
                [(90, (byte)12, 5), (180, (byte)15, 2), (190, (byte)15, 2)]) &&
            introHost.PaletteFades == 1 && introHost.BlackFades == 1 && introHost.DelayCalls == 1,
            "Destruct intro did not draw, wait, fade, and restore the background in native order.");
        var mainMatch = new DestructMatchState();
        var mainConfig = new DestructConfigState();
        mainConfig.IsCpu[0] = false;
        mainConfig.IsCpu[1] = true;
        var mainOrder = new List<string>();
        var modes = new Queue<DestructMode?>([DestructMode.Custom, null]);
        var tickStates = new Queue<DestructTickState>(
            [DestructTickState.Continue, DestructTickState.Reload,
             DestructTickState.Continue, DestructTickState.ReturnToModeSelect]);
        DestructUnits.RunMain(mainMatch, mainConfig,
            () => mainOrder.Add("picture"), () => mainOrder.Add("intro"),
            () => mainOrder.Add("players"), () => modes.Dequeue(),
            () => mainOrder.Add("map"), () =>
            {
                mainOrder.Add("tick");
                return tickStates.Dequeue();
            }, () => mainOrder.Add("fade"));
        Check(mainMatch.Mode == DestructMode.Custom && !mainMatch.Players[0].IsCpu &&
            mainMatch.Players[1].IsCpu && mainOrder.SequenceEqual(
                ["picture", "intro", "players", "map", "tick", "tick", "fade",
                 "map", "tick", "tick", "fade"]) && modes.Count == 0,
            "Destruct main loop did not preserve introduction, reload, mode-select return, or CPU initialization order.");
        var gameConfiguration = new Configuration();
        var gameOptions = gameConfiguration.AddSection("destruct");
        gameOptions.SetOption("max shots", "2");
        gameOptions.SetOption("max explosions", "3");
        gameOptions.SetOption("min walls", "2");
        gameOptions.SetOption("max walls", "4");
        var gameRosters = Enumerable.Range(0, 11).Select(index =>
            new DestructUnitRoster { InstallationCount = (uint)(index == 3 ? 8 : index == 10 ? 40 : 0) }).ToArray();
        var gameDefaults = new int[2, 8, 4];
        var gameHost = new DestructGameHost();
        DestructUnits.RunGame(gameConfiguration, new DestructKeyboardNames(), gameDefaults, gameRosters,
            menuScreen, menuBackground, gameHost);
        Check(gameHost.Session is { MaximumInstallations: 8 } session &&
            session.Shots.Count == 2 && session.Shots.All(shot => shot.IsAvailable) &&
            session.Explosions.Count == 3 && session.Explosions.All(explosion => explosion.IsAvailable) &&
            session.Walls.Count == 4 && session.Match.Players.All(player => player.Units.Count == 8) &&
            session.Match.Players[0].Units[0].ShotType == DestructShot.Demo &&
            session.Match.Players[0].Units[0].Angle == (float)(Math.PI / 4) &&
            gameHost.Calls.SequenceEqual(
                ["clear", "present", "sprites", "fade1", "intro-picture", "intro",
                 "mode", "intro-picture", "map", "tick", "fade25", "mode", "release"]),
            "Destruct game startup did not allocate, initialize, run, and release mode resources in native order.");
        var tickLoopMatch = new DestructMatchState();
        tickLoopMatch.Players[0].UnitsRemaining = tickLoopMatch.Players[1].UnitsRemaining = 1;
        tickLoopMatch.Players[0].Units.Add(new DestructUnitState { Health = 1 });
        tickLoopMatch.Players[1].Units.Add(new DestructUnitState { Health = 1 });
        tickLoopMatch.Players[0].Actions[0] = true;
        var tickHost = new DestructTickHost();
        Check(DestructUnits.RunTick(tickLoopMatch, 1, tickHost) == DestructTickState.Continue &&
            !tickLoopMatch.Players[0].Actions[0] && !tickLoopMatch.FirstTick &&
            tickHost.Calls.SequenceEqual(
                ["delay", "screen", "gravity", "animate", "walls", "explosions", "shots", "ai",
                 "crosshairs", "hud", "present", "fade25", "input", "process", "sounds", "wait"]),
            "Destruct tick did not preserve first-frame draw, gameplay update, and input/sound ordering.");
        tickHost.Calls.Clear();
        tickLoopMatch.EndDelay = 1;
        Check(DestructUnits.RunTick(tickLoopMatch, 1, tickHost) == DestructTickState.Reload &&
            tickHost.Calls.Contains("process") && !tickHost.Calls.Contains("sounds") &&
            !tickHost.Calls.Contains("wait"),
            "Destruct tick end-delay completion did not return before sound and post-tick processing.");
        tickHost.Calls.Clear();
        tickHost.EscapePressed = true;
        tickHost.ToggleLeftCpuPressed = true;
        tickHost.ToggleRightCpuPressed = true;
        tickHost.PausePressed = true;
        tickHost.HelpPressed = true;
        Check(DestructUnits.RunTick(tickLoopMatch, 1, tickHost) == DestructTickState.ReturnToModeSelect &&
            tickHost.Calls.IndexOf("toggle-left") < tickHost.Calls.IndexOf("pause") &&
            tickHost.Calls.IndexOf("pause") < tickHost.Calls.IndexOf("help") &&
            tickHost.Calls.IndexOf("help") < tickHost.Calls.IndexOf("wait") &&
            tickHost.Calls.Contains("clear-escape"),
            "Destruct tick did not handle CPU toggles, overlays, and Escape in native order.");

        foreach (var health in new[] { int.MinValue, -1, 0 })
            Check(!DestructUnits.IsValid(new DestructUnitState { Health = health }),
                $"Destruct unit with health {health} was treated as alive.");
        foreach (var health in new[] { 1, int.MaxValue })
            Check(DestructUnits.IsValid(new DestructUnitState { Health = health }),
                $"Destruct unit with health {health} was treated as dead.");

        var controls = new DestructUnitState { Angle = 1.56f, Power = 4.98f };
        controls.RaiseAngle();
        controls.RaisePower();
        Check(controls.Angle == (float)(Math.PI / 2 - 0.01) && controls.Power == 5,
            "Destruct angle/power increase exceeded its native maximum.");
        controls.Angle = 0.005f;
        controls.Power = 1.02f;
        controls.LowerAngle();
        controls.LowerPower();
        Check(controls.Angle == 0 && controls.Power == 1,
            "Destruct angle/power decrease exceeded its native minimum.");
        controls.Angle = 0.5f;
        controls.Power = 2;
        controls.RaiseAngle();
        controls.RaisePower();
        Check(controls.Angle == 0.51f && controls.Power == 2.05f,
            "Destruct angle/power controls did not apply the native increments.");

        var tank = new DestructUnitState { UnitType = DestructUnit.Tank, ShotType = DestructShot.Demo };
        DestructUnits.CycleWeaponUp(tank);
        Check(tank.ShotType == DestructShot.Tracer, "Destruct weapon-up did not wrap to the first available shot.");
        DestructUnits.CycleWeaponDown(tank);
        Check(tank.ShotType == DestructShot.Demo, "Destruct weapon-down did not wrap to the last available shot.");
        var laser = new DestructUnitState { UnitType = DestructUnit.Laser, ShotType = DestructShot.MegaLaser };
        DestructUnits.CycleWeaponUp(laser);
        Check(laser.ShotType == DestructShot.MiniLaser, "Destruct weapon-up did not skip unavailable shot types.");
        laser.ShotType = DestructShot.MegaLaser;
        DestructUnits.CycleWeaponUp(laser, enableTracerLaser: true);
        Check(laser.ShotType == DestructShot.LaserTracer, "Configured Destruct tracer laser was not selectable.");
        DestructUnits.CycleWeaponDown(laser, enableTracerLaser: true);
        Check(laser.ShotType == DestructShot.MegaLaser, "Destruct weapon-down did not wrap past the optional tracer laser.");
        Throws<InvalidOperationException>(() => DestructUnits.CycleWeaponUp(
            new DestructUnitState { UnitType = DestructUnit.Satellite }));
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.CycleWeaponUp(
            new DestructUnitState { UnitType = DestructUnit.None }));

        var match = new DestructMatchState { Mode = DestructMode.HeliDefense };
        match.Players[0].UnitsRemaining = 0;
        match.Players[1].UnitsRemaining = 1;
        match.Players[1].Score = uint.MaxValue;
        Check(DestructUnits.CheckEndgame(match) && match.Players[1].Score == 4 &&
            match.SoundQueue[7] == 30,
            "Left-player elimination did not award the native mode score, wrap uint, and queue its sound.");
        match.Players[0].UnitsRemaining = 1;
        match.Players[1].UnitsRemaining = 0;
        match.Players[0].Score = 10;
        match.Mode = DestructMode.HeliAssault;
        match.SoundQueue[7] = 0;
        Check(DestructUnits.CheckEndgame(match) && match.Players[0].Score == 15 &&
            match.SoundQueue[7] == 30,
            "Right-player elimination did not award the native mode score and queue its sound.");
        match.Players[1].UnitsRemaining = 1;
        match.SoundQueue[7] = 9;
        Check(!DestructUnits.CheckEndgame(match) && match.SoundQueue[7] == 9,
            "A live match changed its queued sound or reported endgame.");
        match.Players[0].UnitsRemaining = 0;
        match.Players[1].UnitsRemaining = 0;
        match.Players[1].Score = 0;
        match.Mode = DestructMode.HeliDefense;
        Check(DestructUnits.CheckEndgame(match) && match.Players[1].Score == 5,
            "Simultaneous elimination did not preserve the native left-player check priority.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.CheckEndgame(
            new DestructMatchState { Mode = (DestructMode)6 }));

        var explosionMatch = new DestructMatchState { MaximumExplosions = 2 };
        explosionMatch.Explosions.Add(new DestructExplosionState());
        explosionMatch.Explosions.Add(new DestructExplosionState());
        var randomValues = new Queue<uint>([80, 123]);
        DestructUnits.MakeExplosion(explosionMatch, 31, 42, DestructShot.Tracer, () =>
        {
            throw new InvalidOperationException("A configured explosion unexpectedly consumed randomness.");
        });
        var tracerExplosion = explosionMatch.Explosions[0];
        Check(!tracerExplosion.IsAvailable && tracerExplosion.X == 31 && tracerExplosion.Y == 42 &&
            tracerExplosion.Width == 2 && tracerExplosion.MaximumWidth == 4 &&
            tracerExplosion.Fill == 2 && tracerExplosion.Type == DestructExplosionType.Normal &&
            explosionMatch.SoundQueue[1] == 3 && explosionMatch.ExplosionSoundChannel == 1,
            "Configured Destruct explosion parameters or small-explosion sound routing differ.");
        DestructUnits.MakeExplosion(explosionMatch, 50, 60, DestructShot.LargeNuke, () =>
        {
            throw new InvalidOperationException("A configured explosion unexpectedly consumed randomness.");
        });
        var nukeExplosion = explosionMatch.Explosions[1];
        Check(nukeExplosion.MaximumWidth == 60 && nukeExplosion.Fill == 30 &&
            explosionMatch.SoundQueue[2] == 12 && explosionMatch.SoundQueue[3] == 11 &&
            explosionMatch.ExplosionSoundChannel == 3,
            "Large Destruct explosions did not queue both sounds in native order.");
        DestructUnits.MakeExplosion(explosionMatch, 0, 0, DestructShot.Invalid, () =>
        {
            throw new InvalidOperationException("A full Destruct explosion pool consumed randomness.");
        });
        Check(explosionMatch.ExplosionSoundChannel == 3,
            "A full Destruct explosion pool changed sound state.");
        nukeExplosion.IsAvailable = true;
        DestructUnits.MakeExplosion(explosionMatch, 7, 8, DestructShot.Invalid, randomValues.Dequeue);
        Check(!nukeExplosion.IsAvailable && nukeExplosion.X == 7 && nukeExplosion.Y == 8 &&
            nukeExplosion.MaximumWidth == 10 && nukeExplosion.Fill == 23 &&
            nukeExplosion.Type == DestructExplosionType.Normal &&
            explosionMatch.SoundQueue[4] == 4 && explosionMatch.ExplosionSoundChannel == 4 &&
            randomValues.Count == 0,
            "Random Destruct explosion dimensions, sound, or RNG ordering differ.");
        DestructUnits.QueueExplosionSound(explosionMatch, 6);
        Check(explosionMatch.ExplosionSoundChannel == 5 && explosionMatch.SoundQueue[5] == 6,
            "Destruct explosion sound channel did not advance through channel five.");
        DestructUnits.QueueExplosionSound(explosionMatch, 7);
        Check(explosionMatch.ExplosionSoundChannel == 1 && explosionMatch.SoundQueue[1] == 7,
            "Destruct explosion sound channel did not wrap to channel one.");

        match.Players[0].Actions[0] = true;
        match.Players[0].Actions[7] = true;
        match.Players[1].Actions[3] = true;
        DestructUnits.ResetActions(match);
        Check(match.Players.All(player => player.Actions.All(action => !action)),
            "Destruct action reset did not clear all eight action flags for both players.");

        var defaults = new int[2, 8, 4];
        for (var player = 0; player < 2; player++)
        for (var key = 0; key < 8; key++)
        for (var option = 0; option < 4; option++)
            defaults[player, key, option] = player * 100 + key * 4 + option;
        foreach (var player in match.Players)
        {
            player.IsCpu = true;
            player.UnitSelected = 5;
            player.ShotDelay = 6;
            player.Score = 7;
            player.UnitsRemaining = 8;
            player.Ai.Angle = 1;
            player.Ai.Power = 2;
            player.Ai.Fire = 3;
            player.Ai.NoDown = 4;
            player.Actions[0] = true;
        }
        DestructUnits.ResetPlayers(match, defaults);
        Check(match.Players.All(player => !player.IsCpu && player.UnitSelected == 0 &&
            player.ShotDelay == 0 && player.Score == 0 && player.Ai.Angle == 0 &&
            player.Ai.Power == 0 && player.Ai.Fire == 0 && player.Ai.NoDown == 0 &&
            player.UnitsRemaining == 8 && player.Actions[0]),
            "Destruct player reset changed unreset state or retained reset state.");
        Check(match.Players[0].KeyConfig[7, 3] == defaults[0, 7, 3] &&
            match.Players[1].KeyConfig[7, 3] == defaults[1, 7, 3],
            "Destruct player reset did not copy per-player default key configurations.");
        Throws<ArgumentException>(() => DestructUnits.ResetPlayers(match, new int[2, 8, 3]));

        var computer = match.Players[0];
        computer.IsCpu = true;
        var liveTank = new DestructUnitState { UnitType = DestructUnit.Tank, Health = 1 };
        var deadLaser = new DestructUnitState
        {
            UnitType = DestructUnit.Laser, Health = 0, Angle = 0.2f, Power = 3,
            ShotType = DestructShot.Tracer
        };
        var liveSatellite = new DestructUnitState { UnitType = DestructUnit.Satellite, Health = 1 };
        var liveLaser = new DestructUnitState { UnitType = DestructUnit.Laser, Health = 1 };
        computer.Units.Add(liveTank);
        computer.Units.Add(deadLaser);
        computer.Units.Add(liveSatellite);
        computer.Units.Add(liveLaser);
        DestructUnits.ResetAi(match, wallsPresent: false);
        Check(liveTank.Angle == (float)(Math.PI / 4) && liveTank.Power == 4 &&
            liveTank.ShotType == DestructShot.Small,
            "Destruct AI reset did not initialize a live tank.");
        Check(deadLaser.Angle == 0.2f && deadLaser.Power == 3 &&
            deadLaser.ShotType == DestructShot.Tracer,
            "Destruct AI reset changed a dead unit.");
        Check(liveSatellite.Angle == 0 && liveSatellite.Power == 4 &&
            liveSatellite.ShotType == DestructShot.Invalid,
            "Destruct AI reset did not preserve the satellite's native invalid default weapon.");
        Check(liveLaser.Angle == (float)(Math.PI / 4) && liveLaser.Power == 6 &&
            liveLaser.ShotType == DestructShot.MiniLaser,
            "Destruct AI reset did not initialize a live laser.");
        DestructUnits.ResetAi(match, wallsPresent: true);
        Check(liveTank.ShotType == DestructShot.Demo &&
            liveLaser.ShotType == DestructShot.MegaLaser,
            "Destruct AI reset did not choose wall-map CPU weapons.");
        var humanUnit = new DestructUnitState { UnitType = DestructUnit.Tank, Health = 1 };
        match.Players[1].Units.Add(humanUnit);
        DestructUnits.ResetAi(match, wallsPresent: false);
        Check(humanUnit.Angle == 0 && humanUnit.Power == 0 && humanUnit.ShotType == DestructShot.Tracer,
            "Destruct AI reset changed a human player's unit.");

        computer.Units[0].Angle = 0;
        computer.Units[0].Power = 0;
        computer.Units[0].ShotType = DestructShot.Tracer;
        var levelShots = CreateAvailability(2);
        var levelExplosions = CreateAvailability(2);
        var resetOrder = new List<string>();
        DestructUnits.ResetLevel(match, levelShots, 2, levelExplosions, 2,
            () =>
            {
                Check(levelShots.All(shot => shot.IsAvailable) &&
                    levelExplosions.All(explosion => explosion.IsAvailable),
                    "Destruct terrain generation ran before weapon resources were reset.");
                Check(computer.Units[0].Angle == 0 && computer.Units[0].Power == 0,
                    "Destruct AI was reset before terrain generation.");
                resetOrder.Add("terrain");
            }, wallsPresent: false);
        Check(resetOrder.SequenceEqual(["terrain"]) &&
            levelShots.All(shot => shot.IsAvailable) &&
            levelExplosions.All(explosion => explosion.IsAvailable) &&
            computer.Units[0].Angle == (float)(Math.PI / 4),
            "Destruct level reset did not reset resources, generate terrain, then initialize AI.");

        var selection = new DestructMatchState();
        foreach (var player in selection.Players)
        {
            player.UnitsRemaining = 2;
            player.Units.Add(new DestructUnitState { Health = 0, ShotType = DestructShot.Small });
            player.Units.Add(new DestructUnitState { Health = 1, ShotType = DestructShot.Invalid });
            player.Units.Add(new DestructUnitState { Health = 1, ShotType = DestructShot.Small });
            player.Units.Add(new DestructUnitState { Health = 1, ShotType = DestructShot.Demo });
        }
        selection.Players[0].UnitSelected = 0;
        selection.Players[1].UnitSelected = 3;
        DestructUnits.CycleDeadUnits(selection, 4);
        Check(selection.Players[0].UnitSelected == 2 && selection.Players[1].UnitSelected == 3,
            "Destruct unit cycling did not skip dead/unarmed units or retain a selectable unit.");
        selection.Players[0].UnitSelected = 3;
        selection.Players[0].Units[3].Health = 0;
        DestructUnits.CycleDeadUnits(selection, 4);
        Check(selection.Players[0].UnitSelected == 2,
            "Destruct unit cycling did not wrap over dead units to a valid selection.");
        selection.Players[1].UnitsRemaining = 0;
        selection.Players[1].UnitSelected = 99;
        DestructUnits.CycleDeadUnits(selection, 4);
        Check(selection.Players[1].UnitSelected == 99,
            "Destruct unit cycling modified a player with no remaining units.");
        selection.Players[0].Units[2].Health = 0;
        selection.Players[0].Units[3].Health = 0;
        Throws<InvalidOperationException>(() => DestructUnits.CycleDeadUnits(selection, 4));
        Throws<InvalidOperationException>(() => DestructUnits.CycleDeadUnits(selection, 5));

        var animation = new DestructMatchState();
        foreach (var player in animation.Players)
        {
            player.Units.Add(new DestructUnitState { Health = 1, UnitType = DestructUnit.Tank });
            player.Units.Add(new DestructUnitState { Health = 1, UnitType = DestructUnit.Satellite });
            player.Units.Add(new DestructUnitState { Health = 1, UnitType = DestructUnit.Heli, AnimationFrame = 3 });
            player.Units.Add(new DestructUnitState { Health = 0, UnitType = DestructUnit.Heli, AnimationFrame = 2 });
        }
        DestructUnits.AnimateUnits(animation, 4);
        Check(animation.Players.All(player =>
            player.Units[0].AnimationFrame == 0 &&
            player.Units[1].AnimationFrame == 1 &&
            player.Units[2].AnimationFrame == 0 &&
            player.Units[3].AnimationFrame == 2),
            "Destruct animation did not retain static units, advance animated units, wrap, and skip dead units.");
        animation.Players[0].Units[0].AnimationFrame = 2;
        DestructUnits.AnimateUnits(animation, 1);
        Check(animation.Players[0].Units[0].AnimationFrame == 3 &&
            animation.Players[0].Units[1].AnimationFrame == 1,
            "Destruct animation did not advance an already-active frame within the configured limit.");
        Throws<ArgumentException>(() => DestructUnits.AnimateUnits(animation, 5));
        Throws<ArgumentOutOfRangeException>(() =>
        {
            animation.Players[0].Units[0].UnitType = DestructUnit.None;
            DestructUnits.AnimateUnits(animation, 1);
        });

        var destruction = new DestructMatchState();
        destruction.Players[0].UnitsRemaining = 0;
        destruction.Players[1].Score = uint.MaxValue;
        var heli = new DestructUnitState
        {
            UnitType = DestructUnit.Heli, X = uint.MaxValue - 2, Y = 10.5f
        };
        var explosionCalls = new List<(uint X, int Y, DestructShot Shot, uint Remaining, uint Score)>();
        DestructUnits.DestroyUnit(destruction, 0, heli, (x, y, shot) =>
            explosionCalls.Add((x, y, shot, destruction.Players[0].UnitsRemaining, destruction.Players[1].Score)));
        Check(explosionCalls.SequenceEqual(new[]
        {
            (X: 2u, Y: 6, Shot: DestructShot.Small, Remaining: 0u, Score: uint.MaxValue)
        }) && destruction.Players[0].UnitsRemaining == uint.MaxValue &&
            destruction.Players[1].Score == 0,
            "Destruct heli destruction did not preserve explosion order, rounding, or native uint arithmetic.");
        destruction.Players[0].UnitsRemaining = 1;
        destruction.Players[1].Score = 4;
        var satellite = new DestructUnitState { UnitType = DestructUnit.Satellite, X = 20, Y = 9.4f };
        DestructUnits.DestroyUnit(destruction, 0, satellite,
            (x, y, shot) => Check(x == 25 && y == 4 && shot == DestructShot.Invalid,
                "Destruct satellite explosion parameters did not match native behavior."));
        Check(destruction.Players[0].UnitsRemaining == 1 && destruction.Players[1].Score == 4,
            "Destruct satellite destruction changed unit counts or scores.");
        Throws<InvalidOperationException>(() => DestructUnits.DestroyUnit(
            destruction, 0, heli, (_, _, _) => throw new InvalidOperationException()));
        Check(destruction.Players[0].UnitsRemaining == 1 && destruction.Players[1].Score == 4,
            "Destruct destruction updated scores before a failed explosion callback.");

        var bank = new SoundBank();
        bank.Load(
            new MemoryStream(SoundBankTests.BuildBank(29, false, 0)),
            new MemoryStream(SoundBankTests.BuildBank(9, true, 0)),
            new TestSampleConverter());
        var soundQueue = new byte[] { 2, 0, 3, 0, 4, 0, 5, 6 };
        var played = new List<(byte Channel, byte Volume, short Sample)>();
        DestructUnits.PlayQueuedSounds(soundQueue, 5, bank,
            (data, channel, volume) => played.Add((channel, volume, data.Span[0])));
        Check(played.SequenceEqual(new[]
        {
            (Channel: (byte)0, Volume: (byte)2, Sample: bank.Samples[1][0]),
            (Channel: (byte)2, Volume: (byte)2, Sample: bank.Samples[2][0]),
            (Channel: (byte)4, Volume: (byte)2, Sample: bank.Samples[3][0]),
            (Channel: (byte)6, Volume: (byte)2, Sample: bank.Samples[4][0]),
            (Channel: (byte)7, Volume: (byte)5, Sample: bank.Samples[5][0])
        }) && soundQueue.All(sample => sample == 0),
            "Destruct sound dispatch did not preserve channel order, volume, and queue clearing.");
        var failedQueue = new byte[] { 1, 0, 255, 0, 0, 0, 0, 0 };
        Throws<InvalidDataException>(() => DestructUnits.PlayQueuedSounds(
            failedQueue, 4, bank, (_, _, _) => { }));
        Check(failedQueue[0] == 0 && failedQueue[2] == 255,
            "Destruct sound dispatch changed a failing queue entry or failed to clear prior playback.");
        Throws<ArgumentException>(() => DestructUnits.PlayQueuedSounds(
            new byte[7], 4, bank, (_, _, _) => { }));

        var pixels = new IndexedSurface(5, 5, 7, TextWriter.Null);
        DestructUnits.DrawCoolPixel(pixels, 2, 2, 1);
        Check(pixels.Pixels.Span[2 * pixels.Pitch + 2] == 1 &&
            pixels.Pixels.Span[2 * pixels.Pitch + 1] == 255 &&
            pixels.Pixels.Span[2 * pixels.Pitch + 3] == 255 &&
            pixels.Pixels.Span[pixels.Pitch + 2] == 255 &&
            pixels.Pixels.Span[3 * pixels.Pitch + 2] == 255,
            "Destruct cool-pixel effect did not draw its center and wrapping-color edges.");
        DestructUnits.DrawCoolPixel(pixels, 0, 0, 4);
        Check(pixels.Pixels.Span[0] == 4 && pixels.Pixels.Span[1] == 2 &&
            pixels.Pixels.Span[pixels.Pitch] == 2,
            "Destruct cool-pixel effect did not clip safely at screen edges.");

        var walls = new[]
        {
            new DestructWallState { Exists = true, X = 10, Y = 20 },
            new DestructWallState { Exists = false, X = 30, Y = 40 },
            new DestructWallState { Exists = true, X = 50, Y = 60 }
        };
        var drawnWalls = new List<(uint X, uint Y, int Sprite)>();
        DestructUnits.DrawWalls(walls, 2, (x, y, sprite) => drawnWalls.Add((x, y, sprite)));
        Check(drawnWalls.SequenceEqual(new[] { (10u, 20u, 42), }),
            "Destruct wall drawing did not preserve wall order, sprite index, and configured limit.");
        Throws<ArgumentException>(() => DestructUnits.DrawWalls(walls, 4, (_, _, _) => { }));
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.DrawWalls(walls, -1, (_, _, _) => { }));

        var crosshairMatch = new DestructMatchState();
        foreach (var player in crosshairMatch.Players)
            player.Units.Add(new DestructUnitState { UnitType = DestructUnit.Tank, X = 100, Y = 50, Power = 1 });
        var crosshairs = new IndexedSurface(320, 200, 324, TextWriter.Null);
        DestructUnits.DrawCrosshairs(crosshairs, crosshairMatch, 1);
        Check(crosshairs.Pixels.Span[41 * crosshairs.Pitch + 121] == 3 &&
            crosshairs.Pixels.Span[43 * crosshairs.Pitch + 121] == 14 &&
            crosshairs.Pixels.Span[43 * crosshairs.Pitch + 124] == 3 &&
            crosshairs.Pixels.Span[43 * crosshairs.Pitch + 118] == 3 &&
            crosshairs.Pixels.Span[45 * crosshairs.Pitch + 121] == 3 &&
            crosshairs.Pixels.Span[43 * crosshairs.Pitch + 91] == 14,
            "Destruct crosshair positions, player directions, or native pixel colors differ.");
        crosshairs.Pixels.Span.Clear();
        crosshairMatch.Players[0].Units[0].Y = 19;
        DestructUnits.DrawCrosshairs(crosshairs, crosshairMatch, 1);
        Check(crosshairs.Pixels.Span[14 * crosshairs.Pitch + 121] == 3 &&
            crosshairs.Pixels.Span[12 * crosshairs.Pitch + 121] == 14 &&
            crosshairs.Pixels.Span[10 * crosshairs.Pitch + 121] == 0,
            "Destruct crosshair did not clip center/top pixels at the HUD boundary.");
        crosshairs.Pixels.Span.Clear();
        crosshairMatch.Players[0].Units[0].UnitType = DestructUnit.Heli;
        crosshairMatch.Players[0].Units[0].LastMove = -3;
        crosshairMatch.Players[0].Units[0].Y = 10.5f;
        DestructUnits.DrawCrosshairs(crosshairs, crosshairMatch, 1);
        Check(crosshairs.Pixels.Span[14 * crosshairs.Pitch + 102] == 3 &&
            crosshairs.Pixels.Span[12 * crosshairs.Pitch + 102] == 14 &&
            crosshairs.Pixels.Span[10 * crosshairs.Pitch + 102] == 0,
            "Destruct helicopter crosshair did not account for flight direction and HUD clipping.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.DrawCrosshairs(crosshairs, crosshairMatch, 0));

        var hudMatch = new DestructMatchState();
        hudMatch.Players[0].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Tank, ShotType = DestructShot.Small, Health = 123 });
        hudMatch.Players[1].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Heli, ShotType = DestructShot.Mini, Health = -7 });
        hudMatch.Players[0].Score = uint.MaxValue;
        hudMatch.Players[1].Score = 42;
        var hudSurface = new IndexedSurface(320, 12, 328, TextWriter.Null);
        var weaponNames = Enumerable.Range(0, 17).Select(index => $"weapon{index}").ToArray();
        var hudActions = new List<string>();
        DestructUnits.DrawHud(hudSurface, hudMatch, weaponNames,
            (x, y, sprite) => hudActions.Add($"sprite:{x}:{y}:{sprite}"),
            (x, y, text, bank, brightness) =>
                hudActions.Add($"text:{x}:{y}:{text}:{bank}:{brightness}"));
        Check(hudActions.SequenceEqual(new[]
        {
            "sprite:4:0:192", "text:20:3:weapon1:15:2", "text:75:3:dmg~123~:15:0",
            "text:110:3:pts~-1~:15:0", "sprite:174:0:206", "text:190:3:weapon15:15:2",
            "text:245:3:dmg~-7~:15:0", "text:280:3:pts~42~:15:0"
        }), "Destruct HUD sprite/text positions, templates, colors, or signed score formatting differ.");
        Check(hudSurface.Pixels.Span[1 * hudSurface.Pitch + 3] == 240 &&
            hudSurface.Pixels.Span[2 * hudSurface.Pitch + 4] == 242 &&
            hudSurface.Pixels.Span[3 * hudSurface.Pitch + 5] == 241 &&
            hudSurface.Pixels.Span[1 * hudSurface.Pitch + 16] == 240 &&
            hudSurface.Pixels.Span[2 * hudSurface.Pitch + 17] == 242 &&
            hudSurface.Pixels.Span[3 * hudSurface.Pitch + 18] == 241 &&
            hudSurface.Pixels.Span[1 * hudSurface.Pitch + 173] == 240 &&
            hudSurface.Pixels.Span[2 * hudSurface.Pitch + 174] == 242 &&
            hudSurface.Pixels.Span[3 * hudSurface.Pitch + 175] == 241,
            "Destruct HUD panel fill/border pixels do not match native rectangles.");
        Throws<ArgumentException>(() => DestructUnits.DrawHud(hudSurface, hudMatch,
            weaponNames[..16], (_, _, _) => { }, (_, _, _, _, _) => { }));
        hudMatch.Players[0].Units[0].ShotType = DestructShot.Invalid;
        Throws<InvalidOperationException>(() => DestructUnits.DrawHud(hudSurface, hudMatch,
            weaponNames, (_, _, _) => { }, (_, _, _, _, _) => { }));

        var drawnUnits = new List<(uint X, int Y, int Sprite)>();
        DestructUnits.DrawUnit(0, new DestructUnitState
            { UnitType = DestructUnit.Tank, X = 123, Y = 40.5f, AnimationFrame = 2, Angle = 1 },
            (x, y, sprite) => drawnUnits.Add((x, y, sprite)));
        DestructUnits.DrawUnit(1, new DestructUnitState
            { UnitType = DestructUnit.Heli, X = 250, Y = 41.5f, AnimationFrame = 3, LastMove = -3 },
            (x, y, sprite) => drawnUnits.Add((x, y, sprite)));
        DestructUnits.DrawUnit(0, new DestructUnitState
            { UnitType = DestructUnit.Heli, X = 20, Y = 44, AnimationFrame = 1, LastMove = 3 },
            (x, y, sprite) => drawnUnits.Add((x, y, sprite)));
        Check(drawnUnits.SequenceEqual(new[]
        {
            (123u, 28, 6), (250u, 29, 180), (20u, 31, 164)
        }), "Destruct unit sprite indices, heli direction banks, or rounded draw positions differ.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.DrawUnit(
            2, new DestructUnitState(), (_, _, _) => { }));

        var terrain = new IndexedSurface(20, 4, 24, TextWriter.Null);
        var terrainRow = terrain.Pixels.Span.Slice(2 * terrain.Pitch + 4, 12);
        terrainRow[..9].Fill(25);
        Check(DestructUnits.IsStableAt(terrain, 5, 2),
            "Destruct stability rejected fewer than ten dirt pixels.");
        terrainRow[9] = 25;
        Check(!DestructUnits.IsStableAt(terrain, 5, 2),
            "Destruct stability did not reject exactly ten dirt pixels.");
        terrainRow[10..].Fill(25);
        Check(!DestructUnits.IsStableAt(terrain, 5, 2),
            "Destruct stability accepted more than ten dirt pixels.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.IsStableAt(terrain, 0, 2));
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.IsStableAt(terrain, 15, 2));
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.IsStableAt(terrain, uint.MaxValue, 2));
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.IsStableAt(terrain, 5, 4));

        var terrainHeight = new List<uint> { 80, 120, 90, 110, 75 };
        Check(DestructUnits.PlaceTerrain(terrainHeight, 1, 3) == 120 &&
            terrainHeight.SequenceEqual(new uint[] { 80, 120, 120, 120, 75 }),
            "Destruct terrain placement did not level the selected segment to its maximum height.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.PlaceTerrain(terrainHeight, 0, 0));
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.PlaceTerrain(terrainHeight, uint.MaxValue, 2));
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.PlaceTerrain(terrainHeight, 4, 2));

        var generatedPlayers = new[] { new DestructPlayerState(), new DestructPlayerState() };
        generatedPlayers[0].Units.Add(new DestructUnitState { Health = -1 });
        generatedPlayers[0].Units.Add(new DestructUnitState { Health = -2 });
        generatedPlayers[0].Units.Add(new DestructUnitState { Health = 77 });
        generatedPlayers[1].Units.Add(new DestructUnitState { Health = -3 });
        var generatedRosters = new[] { new DestructUnitRoster { InstallationCount = 2 },
            new DestructUnitRoster { InstallationCount = 1 } };
        generatedRosters[0].TypeChoices[3] = DestructUnit.Satellite;
        generatedRosters[0].TypeChoices[4] = DestructUnit.Laser;
        generatedRosters[1].TypeChoices[5] = DestructUnit.Dirt;
        var generatedWorld = Enumerable.Range(0, 320).Select(index => (uint)(100 + index % 30)).ToArray();
        var unitRandom = new Queue<uint>([5, 3, 9, 10, 4, 2, 5]);
        DestructUnits.GenerateUnits(generatedPlayers, generatedRosters, generatedWorld, unitRandom.Dequeue);
        Check(unitRandom.Count == 0 && generatedPlayers[0].UnitsRemaining == 1 &&
            generatedPlayers[0].Units[0].X == 15 && generatedPlayers[0].Units[0].Y == 39 &&
            generatedPlayers[0].Units[0].UnitType == DestructUnit.Satellite &&
            generatedPlayers[0].Units[0].ShotType == DestructShot.Invalid &&
            generatedPlayers[0].Units[1].X == 20 && generatedPlayers[0].Units[1].UnitType == DestructUnit.Laser &&
            generatedPlayers[0].Units[1].Power == 6 && generatedPlayers[0].Units[1].Health == 150 &&
            generatedPlayers[0].Units[1].ShotType == DestructShot.MiniLaser &&
            generatedPlayers[0].Units[2].Health == 77 &&
            generatedPlayers[1].UnitsRemaining == 1 && generatedPlayers[1].Units[0].X == 296 &&
            generatedPlayers[1].Units[0].UnitType == DestructUnit.Dirt &&
            generatedPlayers[1].Units[0].Health == 400 && generatedPlayers[1].Units[0].ShotType == DestructShot.SmallDirt &&
            generatedPlayers[1].Units[0].YMovement == 0 && !generatedPlayers[1].Units[0].IsInAir,
            "Destruct unit generation changed random ordering, satellite treatment, player positioning, or defaults.");
        Throws<ArgumentException>(() => DestructUnits.GenerateUnits(generatedPlayers[..1],
            generatedRosters, generatedWorld, () => 0));

        var wallPlayers = new[] { new DestructPlayerState(), new DestructPlayerState() };
        wallPlayers[0].Units.Add(new DestructUnitState { X = 10 });
        wallPlayers[1].Units.Add(new DestructUnitState { X = 300 });
        var wallRecords = Enumerable.Range(0, 5).Select(_ => new DestructWallState()).ToArray();
        wallRecords[4].Exists = true;
        var wallWorld = Enumerable.Repeat(150u, 320).ToArray();
        var cRandom = new Queue<uint>([2]);
        var gameRandom = new Queue<uint>([1, 50, 4, 99]);
        DestructUnits.GenerateWalls(wallPlayers, wallRecords, wallWorld, 1, 2, 4, true,
            cRandom.Dequeue, gameRandom.Dequeue);
        Check(cRandom.Count == 0 && gameRandom.Count == 0 &&
            wallRecords.Take(4).Select(wall => (wall.Exists, wall.X, wall.Y)).SequenceEqual(new[]
            {
                (true, 109u, 122u), (true, 109u, 136u),
                (true, 60u, 122u), (true, 60u, 136u)
            }) && wallRecords[4].Exists,
            "Destruct wall generation changed the C RNG count, game RNG order, wall positions, or stacked heights.");
        var blockedWalls = new[] { new DestructWallState() };
        var blockedRandom = new Queue<uint>([0, 0, 38, 38, 38, 38, 38]);
        wallPlayers[0].Units[0].X = 50;
        DestructUnits.GenerateWalls(wallPlayers, blockedWalls, wallWorld, 1, 1, 1, true,
            blockedRandom.Dequeue, blockedRandom.Dequeue);
        Check(blockedWalls[0].Exists && blockedWalls[0].X == 48 && blockedRandom.Count == 0,
            "Destruct wall placement did not stop retrying after five obstructed candidates.");
        var clearedWalls = new[] { new DestructWallState { Exists = true },
            new DestructWallState { Exists = true } };
        DestructUnits.GenerateWalls(wallPlayers, clearedWalls, wallWorld, 0, 0, 1, false,
            () => throw new InvalidOperationException("Disabled walls consumed C randomness."),
            () => throw new InvalidOperationException("Disabled walls consumed game randomness."));
        Check(!clearedWalls[0].Exists && clearedWalls[1].Exists,
            "Disabled Destruct walls did not clear only the configured wall slots.");

        var baseWorld = Enumerable.Repeat(999u, 320).ToArray();
        var terrainRandomCalls = 0;
        DestructUnits.GenerateBaseTerrain(DestructMapFlags.Normal, baseWorld, () =>
        {
            terrainRandomCalls++;
            return 0;
        });
        Check(terrainRandomCalls == 4 && baseWorld[0] == 999 &&
            baseWorld[1] == 140 && baseWorld[318] is >= 40 and <= 195 && baseWorld[319] == 999,
            "Destruct base terrain generation changed native RNG usage, height bounds, or untouched edge entries.");
        var fuzzyTallWorld = new uint[319];
        terrainRandomCalls = 0;
        DestructUnits.GenerateBaseTerrain(DestructMapFlags.Fuzzy | DestructMapFlags.Tall,
            fuzzyTallWorld, () =>
            {
                terrainRandomCalls++;
                return 0;
            });
        Check(terrainRandomCalls == 6 && fuzzyTallWorld[1] == 140 &&
            fuzzyTallWorld.Skip(1).All(height => height is >= 40 and <= 195),
            "Fuzzy/tall Destruct terrain did not consume native random values or clamp its heights.");
        Throws<ArgumentException>(() => DestructUnits.GenerateBaseTerrain(
            DestructMapFlags.Normal, new uint[318], () => 0));
        var terrainScreen = new IndexedSurface(320, 200, 324, TextWriter.Null);
        DestructUnits.DrawBaseTerrain(terrainScreen, baseWorld);
        Check(terrainScreen.Pixels.Span[(int)baseWorld[1] * terrainScreen.Pitch + 1] == 25 &&
            terrainScreen.Pixels.Span[199 * terrainScreen.Pitch + 318] == 25 &&
            terrainScreen.Pixels.Span[0] == 0,
            "Destruct terrain renderer did not draw brown columns over the native world range.");
        Throws<ArgumentException>(() => DestructUnits.DrawBaseTerrain(
            new IndexedSurface(318, 200, 318, TextWriter.Null), baseWorld));
        var ringScreen = new IndexedSurface(320, 200, 324, TextWriter.Null);
        var ringRandom = new Queue<uint>([0, 10, 0, 0]);
        var ringRandomCalls = 0;
        var ringFloatCalls = 0;
        DestructUnits.GenerateRings(ringScreen, 25, () =>
        {
            ringRandomCalls++;
            return ringRandom.Dequeue();
        }, () =>
        {
            ringFloatCalls++;
            return 0;
        });
        Check(ringRandomCalls == 4 && ringFloatCalls == 600 && ringRandom.Count == 0 &&
            ringScreen.Pixels.Span[29 * ringScreen.Pitch + 10] == 25 &&
            ringScreen.Pixels.ToArray().Count(pixel => pixel == 25) == 1,
            "Destruct ring generation changed its random-call order, point count, or bounded pixel output.");
        var terrainMatch = new DestructMatchState();
        var terrainScreenAll = new IndexedSurface(320, 200, 324, TextWriter.Null);
        var terrainTemporary = new IndexedSurface(320, 200, 324, TextWriter.Null);
        var orchestrationRandom = new Queue<uint>([0, 0, 0, 0, 0, 0, 0, 10, 0, 0]);
        var orchestrationFloats = 0;
        var terrainActions = new List<string>();
        DestructUnits.GenerateTerrain(terrainMatch,
            [new DestructUnitRoster(), new DestructUnitRoster()],
            new uint[320], [new DestructWallState()], terrainScreenAll, terrainTemporary,
            0, 1, 1, Enumerable.Range(10, 14).Select(value => (uint)value).ToArray(),
            orchestrationRandom.Dequeue, () => 0, () =>
            {
                orchestrationFloats++;
                return 0;
            }, song => terrainActions.Add($"song:{song}"), () => terrainActions.Add("show"));
        Check(terrainMatch.MapFlags == (DestructMapFlags.Walls | DestructMapFlags.Holes | DestructMapFlags.Fuzzy) &&
            terrainActions.SequenceEqual(new[] { "song:9", "show" }) &&
            orchestrationRandom.Count == 0 && orchestrationFloats == 606 &&
            terrainScreenAll.Pixels.Span.SequenceEqual(terrainTemporary.Pixels.Span) &&
            terrainScreenAll.Pixels.Span[29 * terrainScreenAll.Pitch + 10] == 0,
            $"Destruct terrain orchestration differs: flags={terrainMatch.MapFlags}, actions={string.Join(",", terrainActions)}, " +
            $"random={orchestrationRandom.Count}, floats={orchestrationFloats}, screensEqual=" +
            $"{terrainScreenAll.Pixels.Span.SequenceEqual(terrainTemporary.Pixels.Span)}, pixel=" +
            $"{terrainScreenAll.Pixels.Span[29 * terrainScreenAll.Pitch + 10]}.");

        var collisionPlayers = new[] { new DestructPlayerState(), new DestructPlayerState() };
        var collisionUnit = new DestructUnitState { X = 100, Y = 60, Health = 2 };
        collisionPlayers[0].Units.Add(collisionUnit);
        collisionPlayers[1].Units.Add(new DestructUnitState { X = 110, Y = 70, Health = 0 });
        var destroyedUnits = new List<(int Player, DestructUnitState Unit)>();
        DestructUnits.TestExplosionCollision(collisionPlayers, 1, 100, 50,
            (player, unit) => destroyedUnits.Add((player, unit)));
        DestructUnits.TestExplosionCollision(collisionPlayers, 1, 101, 49,
            (player, unit) => destroyedUnits.Add((player, unit)));
        Check(collisionUnit.Health == 2 && destroyedUnits.Count == 0,
            "Destruct explosion collision did not enforce strict unit bounds.");
        DestructUnits.TestExplosionCollision(collisionPlayers, 1, 101, 50,
            (player, unit) => destroyedUnits.Add((player, unit)));
        DestructUnits.TestExplosionCollision(collisionPlayers, 1, 110, 59,
            (player, unit) => destroyedUnits.Add((player, unit)));
        Check(collisionUnit.Health == 0 && destroyedUnits.Count == 1 &&
            destroyedUnits[0] == (0, collisionUnit),
            "Destruct explosion collision did not damage and destroy only the intersected live unit.");

        var tickMatch = new DestructMatchState { MaximumExplosions = 2 };
        tickMatch.Players[0].Units.Add(new DestructUnitState { X = 318, Y = 60, Health = 1 });
        tickMatch.Players[1].Units.Add(new DestructUnitState());
        tickMatch.Explosions.Add(new DestructExplosionState
        {
            IsAvailable = false, X = 1, Y = 50, Width = 2, MaximumWidth = 3,
            Fill = 1, Type = DestructExplosionType.Normal
        });
        tickMatch.Explosions.Add(new DestructExplosionState
        {
            IsAvailable = false, X = 10, Y = 20, Width = 2, MaximumWidth = 3,
            Fill = 1, Type = DestructExplosionType.Dirt
        });
        var explosionSurface = new IndexedSurface(320, 200, 324, TextWriter.Null);
        var explosionRandom = new Queue<float>([0.75f, 0, 1, 0, 0.5f, 0.5f]);
        var explosionHits = new List<(int Player, uint X, float Y)>();
        DestructUnits.RunTickExplosions(tickMatch, 1, explosionSurface,
            explosionRandom.Dequeue,
            (player, unit) => explosionHits.Add((player, unit.X, unit.Y)));
        Check(explosionRandom.Count == 0 && tickMatch.Explosions.All(explosion => explosion.IsAvailable) &&
            explosionHits.SequenceEqual(new[] { (0, 318u, 60f) }) &&
            explosionSurface.Pixels.Span[50 * explosionSurface.Pitch + 319] == 252 &&
            explosionSurface.Pixels.Span[21 * explosionSurface.Pitch + 10] == 25,
            "Destruct explosion tick changed random spread, horizontal wrap, collision, dirt placement, or lifecycle.");

        var trailShot = new DestructShotState { X = 12.5f, Y = 20.5f };
        for (var index = 0; index < 4; index++)
        {
            trailShot.TrailX[index] = (uint)(10 + index);
            trailShot.TrailY[index] = (uint)(20 + index);
            trailShot.TrailColor[index] = (uint)(10 - index * 2);
        }
        var trailDraws = new List<(uint X, uint Y, byte Color)>();
        DestructUnits.DrawTrails(trailShot, 4, 3, 15,
            (x, y, color) => trailDraws.Add((x, y, color)));
        Check(trailDraws.SequenceEqual(new[]
            {
                (13u, 23u, (byte)4), (12u, 22u, (byte)6),
                (11u, 21u, (byte)8), (10u, 20u, (byte)10)
            }) &&
            trailShot.TrailX.SequenceEqual(new uint[] { 13, 10, 11, 12 }) &&
            trailShot.TrailY.SequenceEqual(new uint[] { 21, 20, 21, 22 }) &&
            trailShot.TrailColor.SequenceEqual(new uint[] { 15, 7, 5, 3 }),
            "Destruct shot trails changed reverse draw order, decay, copying, or rounded head position.");
        trailShot.TrailY[0] = 12;
        trailShot.TrailColor[0] = 0;
        var shortTrailDraws = new List<(uint X, uint Y, byte Color)>();
        DestructUnits.DrawTrails(trailShot, 1, 2, 9,
            (x, y, color) => shortTrailDraws.Add((x, y, color)));
        Check(shortTrailDraws.Count == 0 && trailShot.TrailY[0] == 21 &&
            trailShot.TrailColor[0] == 9,
            "Destruct shot trails drew a hidden/zero-color trail or failed to update its head.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.DrawTrails(
            trailShot, 5, 1, 1, (_, _, _) => { }));

        var firingMatch = new DestructMatchState();
        var projectile = new DestructShotState();
        projectile.TrailColor.AsSpan().Fill(7);
        var projectiles = new[] { projectile };
        var helicopter = new DestructUnitState
        {
            UnitType = DestructUnit.Heli, ShotType = DestructShot.Small,
            X = 100, Y = 25, YMovement = 2, LastMove = -1, IsInAir = false
        };
        DestructUnits.MakeShot(firingMatch, 0, helicopter, -1, projectiles, 1, jumperStraight: true);
        Check(projectile.IsAvailable && firingMatch.SoundQueue[0] == 0 &&
            projectile.TrailColor.All(color => color == 7),
            "Grounded Destruct helicopters fired or changed shot state.");
        helicopter.IsInAir = true;
        firingMatch.Players[0].Actions[3] = true;
        DestructUnits.MakeShot(firingMatch, 0, helicopter, -1, projectiles, 1, jumperStraight: true);
        Check(!projectile.IsAvailable && projectile.ShotType == DestructShot.Small &&
            projectile.X == 103 && projectile.Y == 25 &&
            MathF.Abs(projectile.XMovement - 0.08f) < 0.00001f && projectile.YMovement == 0.1f &&
            firingMatch.SoundQueue[0] == 2 && projectile.TrailColor.All(color => color == 0),
            "Destruct helicopter firing did not preserve upward-flight positioning and velocity.");
        projectile.IsAvailable = true;
        var jumper = new DestructUnitState
        {
            UnitType = DestructUnit.Jumper, ShotType = DestructShot.LargeDirt,
            X = 40, Y = 90, Angle = 0.5f, Power = 3, IsInAir = false
        };
        DestructUnits.MakeShot(firingMatch, 1, jumper, 1, projectiles, 1, jumperStraight: false);
        Check(projectile.X == 42 && projectile.Y == 78 && projectile.XMovement == -MathF.Cos(0.5f) * 3 &&
            projectile.YMovement == -2 && projectile.ShotType == DestructShot.LargeDirt &&
            firingMatch.SoundQueue[1] == 13,
            "Destruct non-straight jumper firing did not preserve grounded projectile physics.");
        var fullProjectiles = new[] { new DestructShotState { IsAvailable = false } };
        firingMatch.SoundQueue[0] = 99;
        DestructUnits.MakeShot(firingMatch, 0, jumper, -1, fullProjectiles, 1, jumperStraight: false);
        Check(firingMatch.SoundQueue[0] == 99,
            "A full Destruct projectile pool played a shot sound.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.MakeShot(
            firingMatch, 0, jumper, -1, fullProjectiles, 2, jumperStraight: false));

        var shotMatch = new DestructMatchState();
        var shotScreen = new IndexedSurface(320, 200, 320, TextWriter.Null);
        var shotWalls = new List<DestructWallState> { new() };
        var shotEffects = new List<(uint X, uint Y, byte Color)>();
        var shotExplosions = new List<(int X, int Y, DestructShot Type)>();
        var tickingShot = new DestructShotState
        {
            IsAvailable = false, ShotType = DestructShot.Tracer,
            X = 100, Y = 50, XMovement = 1
        };
        DestructUnits.RunTickShots(shotMatch, [tickingShot], 1, shotWalls, 1, 0,
            shotScreen, () => throw new InvalidOperationException("A non-bouncing shot unexpectedly consumed randomness."),
            (x, y, type) => shotExplosions.Add((x, y, type)),
            (x, y, color) => shotEffects.Add((x, y, color)));
        Check(tickingShot.X == 101, "Destruct shot did not advance horizontally.");
        Check(tickingShot.Y == 50 &&
            MathF.Abs(tickingShot.YMovement - 0.05f) < 0.0001f,
            "Destruct shot did not apply gravity.");
        Check(shotEffects.Count == 1 && shotEffects[0] == (101u, 50u, (byte)253),
            "Destruct shot did not draw its base trail pixel.");
        Check(shotExplosions.Count == 0, "A non-colliding Destruct shot exploded.");
        tickingShot.X = 318;
        tickingShot.XMovement = 2;
        shotEffects.Clear();
        DestructUnits.RunTickShots(shotMatch, [tickingShot], 1, shotWalls, 1, 0, shotScreen,
            () => throw new InvalidOperationException("An out-of-bounds shot unexpectedly consumed randomness."),
            (_, _, _) => throw new InvalidOperationException("An out-of-bounds shot exploded."),
            (_, _, _) => shotEffects.Add((0, 0, 0)));
        Check(tickingShot.IsAvailable && shotEffects.Count == 0,
            "A non-bouncing Destruct shot survived leaving the horizontal map bounds.");

        var bouncingShot = new DestructShotState
        {
            IsAvailable = false, ShotType = DestructShot.MiniLaser,
            X = 318, Y = 30, XMovement = 1
        };
        DestructUnits.RunTickShots(shotMatch, [bouncingShot], 1, shotWalls, 1, 0, shotScreen,
            () => throw new InvalidOperationException("A map-bouncing shot unexpectedly consumed randomness."),
            (_, _, _) => { }, (_, _, _) => { });
        Check(!bouncingShot.IsAvailable && bouncingShot.X == 318 && bouncingShot.XMovement == -1,
            $"Map bounce differs: available={bouncingShot.IsAvailable}, x={bouncingShot.X}, dx={bouncingShot.XMovement}.");
        var floorShot = new DestructShotState
        {
            IsAvailable = false, ShotType = DestructShot.Tracer,
            X = 100, Y = 199.2f, YMovement = 0.5f
        };
        var floorRandomCalls = 0;
        DestructUnits.RunTickShots(shotMatch, [floorShot], 1, shotWalls, 1, 0, shotScreen,
            () => { floorRandomCalls++; return 0.75f; }, (_, _, _) => { }, (_, _, _) => { });
        Check(floorShot.Y < 200 && MathF.Abs(floorShot.YMovement + 0.44f) < 0.00001f &&
            floorShot.XMovement == 0.25f && floorRandomCalls == 1,
            "A floor-bouncing Destruct shot did not damp vertical velocity and randomize a stationary path.");

        var collidingShot = new DestructShotState
        {
            IsAvailable = false, ShotType = DestructShot.Small,
            X = 105, Y = 60
        };
        shotMatch.Players[0].Units.Add(new DestructUnitState { Health = 1, X = 100, Y = 65 });
        shotMatch.Players[1].Units.Add(new DestructUnitState());
        shotExplosions.Clear();
        DestructUnits.RunTickShots(shotMatch, [collidingShot], 1, shotWalls, 1, 1, shotScreen,
            () => 0.5f,
            (x, y, type) => shotExplosions.Add((x, y, type)), (_, _, _) => { });
        Check(collidingShot.IsAvailable && shotExplosions.SequenceEqual([(105, 60, DestructShot.Small)]),
            "A Destruct projectile did not detect an installation collision and explode.");
        var demolishingShot = new DestructShotState
        {
            IsAvailable = false, ShotType = DestructShot.Demo, X = 100, Y = 50
        };
        shotWalls[0].Exists = true;
        shotWalls[0].X = 95;
        shotWalls[0].Y = 45;
        shotExplosions.Clear();
        DestructUnits.RunTickShots(shotMatch, [demolishingShot], 1, shotWalls, 1, 1, shotScreen,
            () => 0.5f,
            (x, y, type) => shotExplosions.Add((x, y, type)), (_, _, _) => { });
        Check(demolishingShot.IsAvailable && !shotWalls[0].Exists &&
            shotExplosions.SequenceEqual([(100, 50, DestructShot.Demo)]),
            "A demolishing Destruct shot did not destroy a wall and explode.");
        var dirtShot = new DestructShotState
        {
            IsAvailable = false, ShotType = DestructShot.Small, X = 80, Y = 70
        };
        shotScreen.Pixels.Span[70 * shotScreen.Pitch + 80] = 25;
        shotExplosions.Clear();
        DestructUnits.RunTickShots(shotMatch, [dirtShot], 1, shotWalls, 1, 1, shotScreen,
            () => 0.5f,
            (x, y, type) => shotExplosions.Add((x, y, type)), (_, _, _) => { });
        Check(dirtShot.IsAvailable && shotExplosions.SequenceEqual([(80, 70, DestructShot.Small)]),
            "A Destruct projectile did not explode on a dirt pixel.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.RunTickShots(
            shotMatch, [new DestructShotState { IsAvailable = false, ShotType = (DestructShot)(-1) }],
            1, shotWalls, 1, 1, shotScreen, () => 0.5f, (_, _, _) => { }, (_, _, _) => { }));

        var aiMatch = new DestructMatchState();
        var cpu = aiMatch.Players[0];
        cpu.IsCpu = true;
        cpu.Ai.NoDown = 1;
        cpu.Units.Add(new DestructUnitState
        {
            Health = 1, UnitType = DestructUnit.Tank, ShotType = DestructShot.Tracer
        });
        aiMatch.Players[1].Units.Add(new DestructUnitState { Health = 1, UnitType = DestructUnit.Tank });
        var aiRandomCalls = 0;
        DestructUnits.RunTickAi(aiMatch, 1, () => { aiRandomCalls++; return 0; });
        Check(cpu.Ai.NoDown == 0 && cpu.Actions[5] && cpu.Actions[7] && aiRandomCalls == 6 &&
            aiMatch.Players[1].Actions.All(action => !action),
            "Destruct AI did not preserve the basic tank firing, tracer selection, no-down countdown, or RNG sequence.");
        var helicopterAi = new DestructMatchState();
        var helicopterCpu = helicopterAi.Players[0];
        helicopterCpu.IsCpu = true;
        helicopterCpu.Units.Add(new DestructUnitState
        {
            Health = 1, UnitType = DestructUnit.Heli, ShotType = DestructShot.Small,
            X = 200, Y = 100, IsInAir = true
        });
        helicopterAi.Players[1].Units.Add(new DestructUnitState
        {
            Health = 1, UnitType = DestructUnit.Satellite, X = 200
        });
        var helicopterRandomCalls = 0;
        DestructUnits.RunTickAi(helicopterAi, 1,
            () => { helicopterRandomCalls++; return 0; });
        Check(helicopterCpu.Actions[2] && !helicopterCpu.Actions[5] &&
            helicopterCpu.Ai.Power == 0 && helicopterCpu.Ai.NoDown == 28 &&
            helicopterRandomCalls == 11,
            "Destruct helicopter AI did not retain movement cooldown, satellite hovering, and input suppression.");
        var heliPreference = new DestructMatchState();
        var heliCpu = heliPreference.Players[0];
        heliCpu.IsCpu = true;
        heliCpu.Units.Add(new DestructUnitState { Health = 1, UnitType = DestructUnit.Tank });
        heliCpu.Units.Add(new DestructUnitState { Health = 1, UnitType = DestructUnit.Heli });
        heliPreference.Players[1].Units.Add(new DestructUnitState());
        heliPreference.Players[1].Units.Add(new DestructUnitState());
        DestructUnits.RunTickAi(heliPreference, 2, () => 0);
        Check(heliCpu.UnitSelected == 1,
            "Destruct CPU did not prioritize its first living helicopter.");
        var inputMatch = new DestructMatchState();
        inputMatch.Players[0].KeyConfig[0, 0] = 3;
        inputMatch.Players[0].KeyConfig[0, 1] = 4;
        inputMatch.Players[0].KeyConfig[1, 0] = 0;
        inputMatch.Players[0].KeyConfig[1, 1] = 6;
        inputMatch.Players[0].KeyConfig[4, 0] = 5;
        inputMatch.Players[0].KeyConfig[6, 0] = 6;
        inputMatch.Players[0].KeyConfig[7, 0] = 7;
        inputMatch.Players[1].KeyConfig[2, 0] = 8;
        var activeKeys = new bool[16];
        activeKeys[3] = activeKeys[5] = activeKeys[6] = activeKeys[7] = activeKeys[8] = true;
        var eventServiceCalls = 0;
        DestructUnits.RunTickGetInput(inputMatch, activeKeys, clear =>
        {
            Check(clear, "Destruct input polling did not clear pending SDL events.");
            eventServiceCalls++;
        });
        Check(eventServiceCalls == 1 && inputMatch.Players[0].Actions[0] &&
            !inputMatch.Players[0].Actions[1] && inputMatch.Players[0].Actions[4] &&
            inputMatch.Players[0].Actions[6] && inputMatch.Players[0].Actions[7] &&
            inputMatch.Players[1].Actions[2] && activeKeys[3] && activeKeys[8] &&
            !activeKeys[5] && !activeKeys[6] && !activeKeys[7],
            "Destruct key polling did not preserve unknown-key breaks, key latches, and toggled-action consumption.");
        var processMatch = new DestructMatchState();
        var processPlayer = processMatch.Players[0];
        processPlayer.UnitsRemaining = 2;
        processPlayer.Units.Add(new DestructUnitState
        {
            Health = 100, UnitType = DestructUnit.Tank, ShotType = DestructShot.Small,
            Angle = 0.5f, Power = 2
        });
        processPlayer.Units.Add(new DestructUnitState { Health = 100, UnitType = DestructUnit.Tank });
        processMatch.Players[1].UnitsRemaining = 1;
        processMatch.Players[1].Units.Add(new DestructUnitState { Health = 100, UnitType = DestructUnit.Tank });
        processMatch.Players[1].Units.Add(new DestructUnitState());
        processPlayer.Actions[0] = processPlayer.Actions[2] = processPlayer.Actions[4] =
            processPlayer.Actions[5] = processPlayer.Actions[6] = true;
        var processShots = new[] { new DestructShotState(), new DestructShotState() };
        var magnetCalls = 0;
        DestructUnits.ProcessInput(processMatch, 2, processShots, 2, [false, false],
            (_, _) => true, (_, _) => magnetCalls++);
        var firedProjectile = processShots[0];
        Check(processPlayer.UnitSelected == 1 && processPlayer.Units[0].Angle == 0.51f &&
            processPlayer.Units[0].Power == 2.05f && processPlayer.Units[0].ShotType == DestructShot.Large &&
            processPlayer.ShotDelay == 80 && !firedProjectile.IsAvailable &&
            firedProjectile.ShotType == DestructShot.Large && processMatch.SoundQueue[0] == 1 &&
            magnetCalls == 0,
            "Destruct input processing did not preserve controls, switch order, firing delay, and projectile creation.");
        var processHelicopter = new DestructMatchState();
        processHelicopter.Players[0].UnitsRemaining = 1;
        processHelicopter.Players[0].Units.Add(new DestructUnitState
        {
            Health = 1, UnitType = DestructUnit.Heli, X = 10, Y = 80
        });
        processHelicopter.Players[1].Units.Add(new DestructUnitState());
        processHelicopter.Players[0].Actions[0] = processHelicopter.Players[0].Actions[2] =
            processHelicopter.Players[0].Actions[3] = true;
        var stabilityQueries = new List<uint>();
        DestructUnits.ProcessInput(processHelicopter, 1, [], 0, [false, false],
            (x, _) => { stabilityQueries.Add(x); return true; }, (_, _) => { });
        var movedHelicopter = processHelicopter.Players[0].Units[0];
        Check(movedHelicopter.X == 9 && movedHelicopter.LastMove == -1 &&
            movedHelicopter.IsInAir && movedHelicopter.YMovement == 0 &&
            stabilityQueries.SequenceEqual([5u, 9u]),
            "Destruct helicopter input did not verify adjacent and landing stability before movement.");
        var magnetMatch = new DestructMatchState();
        var magnetUnit = new DestructUnitState
        {
            UnitType = DestructUnit.Magnet, X = 100, Power = 2
        };
        var magnetShots = new[]
        {
            new DestructShotState { IsAvailable = false, X = 120 },
            new DestructShotState { IsAvailable = false, X = 80 },
            new DestructShotState { IsAvailable = true, X = 130 }
        };
        var pushedHeli = new DestructUnitState
        {
            Health = 1, UnitType = DestructUnit.Heli, X = 50, IsInAir = true
        };
        var groundedHeli = new DestructUnitState
        {
            Health = 1, UnitType = DestructUnit.Heli, X = 60, IsInAir = false
        };
        magnetMatch.Players[1].Units.Add(pushedHeli);
        magnetMatch.Players[1].Units.Add(groundedHeli);
        DestructUnits.RunMagnet(magnetMatch, 0, magnetUnit, magnetShots, 3, 2);
        Check(MathF.Abs(magnetShots[0].XMovement - 0.2f) < 0.00001f &&
            magnetShots[1].XMovement == 0 && magnetShots[2].XMovement == 0 &&
            pushedHeli.X == 52 && groundedHeli.X == 60 && magnetUnit.AnimationFrame == 1,
            "Destruct magnet did not attract only forward shots and airborne enemy helicopters.");

        var aliasSurface = new IndexedSurface(20, 16, 24, TextWriter.Null);
        var aliasPixels = aliasSurface.Pixels.Span;
        aliasPixels[13 * aliasSurface.Pitch + 5] = 25;
        Check(DestructUnits.AliasDirtPixel(aliasSurface, 5, 14) == 17,
            "Destruct dirt alias did not map the upper neighbor to its native shade.");
        aliasPixels[15 * aliasSurface.Pitch + 5] = 25;
        aliasPixels[14 * aliasSurface.Pitch + 4] = 25;
        aliasPixels[14 * aliasSurface.Pitch + 6] = 25;
        Check(DestructUnits.AliasDirtPixel(aliasSurface, 5, 14) == 24,
            "Destruct dirt alias did not combine neighboring dirt into the expected shade.");
        aliasPixels[14 * aliasSurface.Pitch + 5] = 25;
        DestructUnits.AliasDirt(aliasSurface);
        Check(aliasPixels[14 * aliasSurface.Pitch + 5] == 25,
            "Destruct dirt alias overwrote terrain instead of black pixels.");

        var display = new IndexedSurface(20, 16, 24, TextWriter.Null);
        var temporary = new IndexedSurface(20, 16, 24, TextWriter.Null);
        display.Pixels.Span.Fill(99);
        temporary.Pixels.Span.Fill(241);
        temporary.Pixels.Span[12 * temporary.Pitch] = 242;
        DestructUnits.CheckTemporaryScreen(display, temporary, alwaysAlias: false);
        Check(display.Pixels.Span[11 * display.Pitch] == 99 &&
            display.Pixels.Span[12 * display.Pitch] == 241 &&
            display.Pixels.Span[12 * display.Pitch + 1] == 0,
            $"Destruct temporary-screen check produced top={display.Pixels.Span[11 * display.Pitch]}, " +
            $"first={display.Pixels.Span[12 * display.Pitch]}, second={display.Pixels.Span[12 * display.Pitch + 1]}.");
        temporary.Pixels.Span.Fill(0);
        temporary.Pixels.Span[12 * temporary.Pitch + 4] = 25;
        DestructUnits.CheckTemporaryScreen(display, temporary, alwaysAlias: true);
        Check(display.Pixels.Span[12 * display.Pitch + 5] == 18,
            "Destruct temporary-screen check did not alias dirt adjacent to terrain.");
        Throws<ArgumentException>(() => DestructUnits.CheckTemporaryScreen(
            display, new IndexedSurface(20, 15, 24, TextWriter.Null), alwaysAlias: false));

        var helpHost = new TestDestructHelpHost();
        var helpLines = Enumerable.Range(0, 25)
            .Select(index => (ReadOnlyMemory<byte>)new byte[] { (byte)index }).ToArray();
        new DestructHelpScreen(helpHost).Run(helpLines);
        Check(helpHost.DrawnText.Count == 25 &&
            helpHost.DrawnText[0] == (100, 5, (byte)2, 4, "\0") &&
            helpHost.DrawnText[1] == (100, 15, (byte)2, 1, "\u0001") &&
            helpHost.DrawnText[2] == (10, 27, (byte)1, 3, "\u0002") &&
            helpHost.DrawnText[23] == (170, 165, (byte)1, 3, "\u0017") &&
            helpHost.DrawnText[24] == (30, 190, (byte)3, 4, "\u0018") &&
            helpHost.Delays.SequenceEqual(new uint[] { 16, 16 }) &&
            helpHost.Calls.SequenceEqual(new[]
            {
                "fade-black", "save-clear", "present", "fade-palette", "service", "delay",
                "service", "delay", "fade-black", "restore", "present", "fade-palette"
            }),
            "Destruct help screen did not preserve native text layout, event polling, and restore order.");
        Throws<ArgumentException>(() => new DestructHelpScreen(helpHost).Run(helpLines[..24]));

        var pauseHost = new TestDestructPauseHost();
        new DestructPauseScreen(pauseHost).Run(255, 91, helpLines[22]);
        Check(pauseHost.Volumes.SequenceEqual(new[] { ((ushort)127, (ushort)91), ((ushort)255, (ushort)91) }) &&
            pauseHost.DrawnText == (90, "\u0016", (byte)12, 5) &&
            pauseHost.Calls.SequenceEqual(new[]
            {
                "volume", "save", "draw", "present", "service", "delay",
                "restore", "present", "volume"
            }),
            "Destruct pause screen did not halve/restore audio, render, wait, and restore state in native order.");

        var superPixelSurface = new IndexedSurface(20, 16, 24, TextWriter.Null);
        DestructUnits.DrawSuperPixel(superPixelSurface, 0, 2);
        Check(superPixelSurface.Pixels.Span[0] == 246 &&
            superPixelSurface.Pixels.Span[2 * superPixelSurface.Pitch] == 252,
            "Destruct super-pixel effect did not clip correctly at the upper-left edge.");
        superPixelSurface.Pixels.Span.Fill(250);
        DestructUnits.DrawSuperPixel(superPixelSurface, 10, 8);
        Check(superPixelSurface.Pixels.Span[6 * superPixelSurface.Pitch + 10] == 251 &&
            superPixelSurface.Pixels.Span[8 * superPixelSurface.Pitch + 10] == 252,
            "Destruct super-pixel effect did not apply its center value and saturated intensity.");
        superPixelSurface.Pixels.Span[8 * superPixelSurface.Pitch + 10] = 255;
        DestructUnits.DrawSuperPixel(superPixelSurface, 10, 8);
        Check(superPixelSurface.Pixels.Span[8 * superPixelSurface.Pitch + 10] == 255,
            "Destruct super-pixel effect overflowed a saturated pixel.");

        var fallQueries = new List<(uint X, uint Y)>();
        var fallingTank = new DestructUnitState
            { UnitType = DestructUnit.Tank, X = 23, Y = 40.5f };
        DestructUnits.LowerUnit(fallingTank, (x, y) =>
        {
            fallQueries.Add((x, y));
            return true;
        });
        Check(fallingTank.Y == 41.5f && fallingTank.YMovement == 0 &&
            fallQueries.SequenceEqual(new[] { (23u, 41u) }),
            "Destruct regular installation lowering did not use native rounded terrain coordinates.");
        var loweringHeli = new DestructUnitState { UnitType = DestructUnit.Heli, Y = 198.9f };
        DestructUnits.LowerUnit(loweringHeli, (_, _) => true);
        Check(loweringHeli.Y == 199 && loweringHeli.YMovement == 1.5f,
            "Destruct helicopter lowering did not apply its slower step and velocity.");
        var grounded = new DestructUnitState { UnitType = DestructUnit.Tank, Y = 199 };
        DestructUnits.LowerUnit(grounded, (_, _) => throw new InvalidOperationException());
        Check(grounded.Y == 199, "Destruct lowering queried stability for a unit already at ground level.");

        var flying = new DestructUnitState
            { UnitType = DestructUnit.Tank, X = 9, Y = 100, YMovement = 2, IsInAir = true };
        DestructUnits.FlyUnit(flying, (x, y) =>
        {
            fallQueries.Add((x, y));
            return true;
        });
        Check(flying.Y == 102 && MathF.Abs(flying.YMovement - 2.03f) < 0.00001f &&
            flying.IsInAir && fallQueries[^1] == (9u, 102u),
            "Destruct airborne movement or normal gravity increment differs from native.");
        var flyingHeli = new DestructUnitState
            { UnitType = DestructUnit.Heli, Y = 100, YMovement = 1, IsInAir = true };
        DestructUnits.FlyUnit(flyingHeli, (_, _) => true);
        Check(MathF.Abs(flyingHeli.YMovement - 1.0001f) < 0.00001f && flyingHeli.IsInAir,
            "Destruct airborne helicopter acceleration differs from native.");
        var ceiling = new DestructUnitState
            { UnitType = DestructUnit.Jumper, Y = 24.5f, YMovement = -2, IsInAir = true };
        DestructUnits.FlyUnit(ceiling, (_, y) => y == 24);
        Check(ceiling.Y == 24 && ceiling.YMovement == 0.03f && ceiling.IsInAir,
            "Destruct flight did not clamp at the ceiling before applying gravity.");
        var landed = new DestructUnitState
            { UnitType = DestructUnit.Tank, Y = 198, YMovement = 2, IsInAir = true };
        DestructUnits.FlyUnit(landed, (_, _) => throw new InvalidOperationException());
        Check(landed.Y == 199 && landed.YMovement == 0 && !landed.IsInAir,
            "Destruct flight did not land immediately when the next step crosses ground.");
        var stable = new DestructUnitState
            { UnitType = DestructUnit.Tank, Y = 80, YMovement = 1, IsInAir = true };
        DestructUnits.FlyUnit(stable, (_, _) => false);
        Check(stable.Y == 81 && stable.YMovement == 0 && !stable.IsInAir,
            "Destruct flight did not stop on stable terrain.");

        var gravityMatch = new DestructMatchState();
        gravityMatch.Players[0].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Tank, Health = 1, X = 30, Y = 40 });
        gravityMatch.Players[0].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Satellite, Health = 1, X = 40, Y = 50 });
        gravityMatch.Players[0].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Heli, Health = 1, X = 50, Y = 60, IsInAir = true, YMovement = 1 });
        gravityMatch.Players[0].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Tank, Health = 0, X = 60, Y = 70 });
        gravityMatch.Players[1].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Jumper, Health = 1, X = 70, Y = 80, IsInAir = true });
        gravityMatch.Players[1].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Tank, Health = 1, X = 80, Y = 90 });
        gravityMatch.Players[1].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Tank, Health = 0 });
        gravityMatch.Players[1].Units.Add(new DestructUnitState
            { UnitType = DestructUnit.Tank, Health = 0 });
        var gravityDraws = new List<(uint X, int Y, int Sprite)>();
        var gravityChecks = 0;
        DestructUnits.RunGravity(gravityMatch, 4,
            (_, _) => { gravityChecks++; return true; },
            (x, y, sprite) => gravityDraws.Add((x, y, sprite)));
        Check(gravityChecks == 4 && gravityDraws.Select(draw => draw.X)
            .SequenceEqual(new uint[] { 30, 50, 70, 80 }) &&
            gravityMatch.Players[0].Units[0].Y == 41 &&
            gravityMatch.Players[0].Units[2].Y == 61 &&
            gravityMatch.Players[1].Units[0].Y == 80 &&
            MathF.Abs(gravityMatch.Players[1].Units[0].YMovement - 0.03f) < 0.00001f,
            "Destruct gravity dispatcher did not preserve unit filtering, physics, and draw order.");

        var left = CreateUnits(4, 3);
        var right = CreateUnits(4, 7);
        DestructUnits.ResetUnits(left, right, 3);
        for (var index = 0; index < 3; index++)
            Check(left[index].Health == 0 && right[index].Health == 0,
                "Destruct reset did not clear each configured installation's health.");
        Check(left[3].Health == 3 && right[3].Health == 7,
            "Destruct reset changed a slot outside max_installations.");

        DestructUnits.ResetUnits([], [], 0);
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.ResetUnits(left, right, -1));
        Throws<ArgumentException>(() => DestructUnits.ResetUnits(left, right[..2], 3));
        Throws<ArgumentNullException>(() => DestructUnits.IsValid(null!));
        Throws<ArgumentNullException>(() => DestructUnits.ResetUnits(null!, right, 0));

        var shots = CreateAvailability(4);
        var explosions = CreateAvailability(3);
        DestructUnits.ResetWeapons(shots, 2, explosions, 1);
        Check(shots[0].IsAvailable && shots[1].IsAvailable && explosions[0].IsAvailable,
            "Destruct weapon reset did not enable configured resources.");
        Check(!shots[2].IsAvailable && !shots[3].IsAvailable &&
            !explosions[1].IsAvailable && !explosions[2].IsAvailable,
            "Destruct weapon reset changed resources outside configured capacities.");
        Throws<ArgumentOutOfRangeException>(() => DestructUnits.ResetWeapons(shots, 2, explosions, -1));
        Throws<ArgumentException>(() => DestructUnits.ResetWeapons(shots[..1], 2, explosions, 1));
    }

    private static DestructUnitState[] CreateUnits(int count, int health) =>
        Enumerable.Range(0, count).Select(_ => new DestructUnitState { Health = health }).ToArray();

    private static DestructAvailabilityState[] CreateAvailability(int count) =>
        Enumerable.Range(0, count).Select(_ => new DestructAvailabilityState()).ToArray();

    private sealed class TestDestructHelpHost : IDestructHelpHost
    {
        public List<string> Calls { get; } = [];
        public List<(int X, int Y, byte Hue, int Brightness, string Text)> DrawnText { get; } = [];
        public List<uint> Delays { get; } = [];
        private int _serviceCalls;
        public bool NewKey => _serviceCalls >= 2;
        public void FadeBlack(int steps) => Calls.Add("fade-black");
        public void SaveAndClearScreen() => Calls.Add("save-clear");
        public void DrawText(int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness) =>
            DrawnText.Add((x, y, hue, brightness, System.Text.Encoding.Latin1.GetString(text.Span)));
        public void Present() => Calls.Add("present");
        public void FadePalette(int steps, int firstColor, int lastColor) => Calls.Add("fade-palette");
        public void ServiceEvents(bool clear) { Calls.Add("service"); _serviceCalls++; }
        public void Delay(uint milliseconds) { Calls.Add("delay"); Delays.Add(milliseconds); }
        public void RestoreScreen() => Calls.Add("restore");
    }

    private sealed class TestDestructPauseHost : IDestructPauseHost
    {
        public List<string> Calls { get; } = [];
        public List<(ushort Music, ushort Effects)> Volumes { get; } = [];
        public (int Y, string Text, byte Hue, int Brightness) DrawnText { get; private set; }
        private int _serviceCalls;
        public bool NewKey => _serviceCalls >= 1;
        public void SetVolume(ushort music, ushort effects)
        {
            Calls.Add("volume");
            Volumes.Add((music, effects));
        }
        public void SaveScreen() => Calls.Add("save");
        public void DrawCenteredText(int y, ReadOnlyMemory<byte> text, byte hue, int brightness)
        {
            Calls.Add("draw");
            DrawnText = (y, System.Text.Encoding.Latin1.GetString(text.Span), hue, brightness);
        }
        public void Present() => Calls.Add("present");
        public void ServiceEvents(bool clear) { Calls.Add("service"); _serviceCalls++; }
        public void Delay(uint milliseconds) => Calls.Add("delay");
        public void RestoreScreen() => Calls.Add("restore");
    }

    private sealed class TestSampleConverter : IAudioSampleConverter
    {
        public short[] Convert(ReadOnlySpan<sbyte> samples) => samples.ToArray()
            .Select(sample => (short)sample).ToArray();
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }

    private enum DestructMenuKey { Down, Up, Enter, Escape }

    private sealed class DestructMenuHost(IEnumerable<DestructMenuKey> keys) : IDestructMenuHost
    {
        private readonly Queue<DestructMenuKey> _keys = new(keys);
        public bool NewKey { get; private set; }
        public bool EscapePressed { get; private set; }
        public bool ReturnPressed { get; private set; }
        public bool UpPressed { get; private set; }
        public bool DownPressed { get; private set; }
        public int PaletteFades { get; private set; }
        public int BlackFades { get; private set; }
        public int DelayCalls { get; private set; }
        public bool CustomDrawn { get; private set; }
        public List<(int Mode, int Y, bool Selected)> ModeDraws { get; } = [];
        public List<(int Y, bool Selected)> CustomDraws { get; } = [];
        public List<(int Y, byte Hue, int Brightness)> CenteredDraws { get; } = [];
        public void DrawCenteredText(int y, ReadOnlyMemory<byte> text, byte hue, int brightness) =>
            CenteredDraws.Add((y, hue, brightness));
        public void DrawModeText(int mode, int y, bool selected) => ModeDraws.Add((mode, y, selected));
        public void DrawCustomModeText(int y, bool selected)
        {
            CustomDrawn = true;
            CustomDraws.Add((y, selected));
        }
        public void Show() { }
        public void FadePalette(int steps, int firstColor, int lastColor) => PaletteFades++;
        public void FadeBlack(int steps) => BlackFades++;
        public void ClearNewKey()
        {
            NewKey = false;
            EscapePressed = ReturnPressed = UpPressed = DownPressed = false;
        }
        public void ServiceEvents(bool clear)
        {
            if (_keys.Count == 0)
                throw new InvalidOperationException("Destruct menu requested more input than the test provided.");
            NewKey = true;
            switch (_keys.Dequeue())
            {
                case DestructMenuKey.Down: DownPressed = true; break;
                case DestructMenuKey.Up: UpPressed = true; break;
                case DestructMenuKey.Enter: ReturnPressed = true; break;
                case DestructMenuKey.Escape: EscapePressed = true; break;
            }
        }
        public void Delay(uint milliseconds) => DelayCalls++;
    }

    private sealed class DestructTickHost : IDestructTickHost
    {
        public bool ToggleLeftCpuPressed { get; set; }
        public bool ToggleRightCpuPressed { get; set; }
        public bool PausePressed { get; set; }
        public bool HelpPressed { get; set; }
        public bool EscapePressed { get; set; }
        public bool BackspacePressed { get; set; }
        public List<string> Calls { get; } = [];
        public void SetDelay(uint ticks) => Calls.Add("delay");
        public void CheckTemporaryScreen() => Calls.Add("screen");
        public void RunGravity() => Calls.Add("gravity");
        public void AnimateUnits() => Calls.Add("animate");
        public void DrawWalls() => Calls.Add("walls");
        public void RunExplosions() => Calls.Add("explosions");
        public void RunShots() => Calls.Add("shots");
        public void RunAi() => Calls.Add("ai");
        public void DrawCrosshairs() => Calls.Add("crosshairs");
        public void DrawHud() => Calls.Add("hud");
        public void Present() => Calls.Add("present");
        public void FadePalette(int steps, int firstColor, int lastColor) => Calls.Add($"fade{steps}");
        public void GetInput() => Calls.Add("input");
        public void ProcessInput() => Calls.Add("process");
        public void PlayQueuedSounds() => Calls.Add("sounds");
        public void ToggleLeftCpu() => Calls.Add("toggle-left");
        public void ToggleRightCpu() => Calls.Add("toggle-right");
        public void Pause() => Calls.Add("pause");
        public void ShowHelp() => Calls.Add("help");
        public void ClearLastKey() => Calls.Add("clear-last-key");
        public void WaitDelay() => Calls.Add("wait");
        public void ClearEscape() => Calls.Add("clear-escape");
        public void ClearBackspace() => Calls.Add("clear-backspace");
    }

    private sealed class DestructGameHost : IDestructGameHost
    {
        private int _modeCalls;
        public List<string> Calls { get; } = [];
        public DestructGameSession? Session { get; private set; }
        public void ClearScreen(IndexedSurface screen) => Calls.Add("clear");
        public void Present() => Calls.Add("present");
        public void LoadSpriteSheet() => Calls.Add("sprites");
        public void ReleaseSpriteSheet() => Calls.Add("release");
        public void FadeBlack(int steps) => Calls.Add($"fade{steps}");
        public void LoadIntroPicture(DestructGameSession session)
        {
            Session = session;
            Calls.Add("intro-picture");
        }
        public void ShowIntro(DestructGameSession session) => Calls.Add("intro");
        public DestructMode? SelectMode(DestructGameSession session)
        {
            Calls.Add("mode");
            return _modeCalls++ == 0 ? DestructMode.Traditional : null;
        }
        public void PrepareMap(DestructGameSession session)
        {
            Calls.Add("map");
            session.Match.MapFlags = DestructMapFlags.Walls;
            session.Match.Players[0].Units[0].Health = 1;
            session.Match.Players[0].Units[0].UnitType = DestructUnit.Tank;
        }
        public DestructTickState RunTick(DestructGameSession session)
        {
            Calls.Add("tick");
            Check(session.Match.Players[0].Units[0].ShotType == DestructShot.Demo,
                "Destruct CPU setup ran before its map generation completed.");
            return DestructTickState.ReturnToModeSelect;
        }
    }

    private sealed class DestructKeyboardNames : IKeyboardNames
    {
        public int FromName(string name) =>
            name.StartsWith("key", StringComparison.Ordinal) &&
            int.TryParse(name.AsSpan(3), out var scancode) ? scancode : 0;

        public string Name(int scancode) => $"key{scancode}";
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
