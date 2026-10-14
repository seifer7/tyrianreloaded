/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the terms of the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelGameplayRuntime
{
    public LegacyGameState Game { get; }
    public IReadOnlyList<PlayerState> Players => Game.Players;
    public SceneEffects Effects { get; }
    public EnemySystem Enemies { get; }
    public PlayerCombat Combat { get; }
    public BackgroundRenderer Backgrounds { get; }
    public LevelBackgroundSceneState Scene { get; } = new();
    public LevelMap? Map { get; private set; }
    public LevelEvents Events { get; }
    public PlayerShots Shots { get; }
    public PlayerMovement Movement { get; }
    public PlayerWeapons Weapons { get; }
    public PlayerSpecialWeapons SpecialWeapons { get; }
    public PlayerInput Input { get; }
    public PlayerShipInfo ShipInfo { get; }
    public PlayerFrame Frame { get; }
    public PlayerPickups Pickups { get; }
    public WarningBars Warnings { get; }
    public ArmorWarning ArmorWarning { get; }
    private readonly LevelFlagsInitializer _flagsInitializer;
    public NetworkRequests Requests => Input.Requests;

    internal LevelGameplayRuntime(LegacyGameState game, SceneEffects effects, EnemySystem enemies, PlayerCombat combat,
        BackgroundRenderer backgrounds, LevelEvents events, PlayerShots shots, PlayerMovement movement,
        PlayerWeapons weapons, PlayerSpecialWeapons specialWeapons, PlayerInput input,
        PlayerShipInfo shipInfo, PlayerFrame frame, PlayerPickups pickups, WarningBars warnings,
        ArmorWarning armorWarning, LevelFlagsInitializer flagsInitializer)
    {
        Game = game;
        Effects = effects;
        Enemies = enemies;
        Combat = combat;
        Backgrounds = backgrounds;
        Events = events;
        Shots = shots;
        Movement = movement;
        Weapons = weapons;
        SpecialWeapons = specialWeapons;
        Input = input;
        ShipInfo = shipInfo;
        Frame = frame;
        Pickups = pickups;
        Warnings = warnings;
        ArmorWarning = armorWarning;
        _flagsInitializer = flagsInitializer;
    }

    public LevelMap LoadMap(DataFiles files, EpisodeState episode, byte levelNumber)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentNullException.ThrowIfNull(episode);
        var map = LevelMap.LoadSelected(files, episode, levelNumber);
        map.Apply(Backgrounds, Events, Scene);
        Map = map;
        return map;
    }

    public void InitializeCombatState(Span<bool> buttons, Action drawOptions)
    {
        ArgumentNullException.ThrowIfNull(drawOptions);
        new LevelCombatStateInitializer(Game, Combat, Movement, Weapons, SpecialWeapons,
            Shots, Enemies, Effects, Backgrounds, Events, Requests, drawOptions)
            .Initialize(Input.NetworkGame, buttons);
    }

    public void InitializeMapAndShipState()
    {
        new LevelMapShipInitializer(Backgrounds, Events, Game.Players, ShipInfo, Shots, Frame).Initialize();
    }

    public void InitializeFlags() => _flagsInitializer.Initialize();
}
