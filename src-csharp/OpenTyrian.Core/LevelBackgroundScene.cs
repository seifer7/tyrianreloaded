/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class LevelBackgroundSceneState
{
    public bool ForceEvents { get; set; }
    public ushort CurrentLocation { get; set; }
    public ushort BackMove { get; set; }
    public byte Map1YDelay { get; set; }
    public byte Map1YDelayMax { get; set; }
    public ushort BackPosition { get; set; }
    public ushort MapY { get; set; }
    public int MapYPositionOffset { get; set; }
    public byte AstralDuration { get; set; }
    public bool StarfieldActive { get; set; }
    public int StarfieldSpeed { get; set; }
    public byte ProcessorType { get; set; }
    public bool[] Smoothies { get; } = new bool[9];
    public byte[] SmoothieData { get; } = new byte[9];
    public byte Background2Over { get; set; }
    public bool Background2 { get; set; }
    public byte Background3Over { get; set; }
    public bool Wild { get; set; }
    public bool Background2NotTransparent { get; set; }
    public ushort EnemyOnScreen { get; set; }
    public ushort LastEnemyOnScreen { get; set; }
    public ushort MapXOffset { get; set; }
    public ushort MapX2Offset { get; set; }
    public ushort OldMapX3Offset { get; set; }
    public ushort MapX3Offset { get; set; }
    public ushort Background3Move { get; set; }
    public ushort TemporaryMapXOffset { get; set; }
    public ushort TemporaryBackgroundMove { get; set; }
    public byte StopBackgroundNumber { get; set; }
    public bool SuperWild { get; set; }
    public ushort Neat { get; set; }
    public bool EnemiesActive { get; set; }
    public ushort LevelEnemyFrequency { get; set; }
    public ushort LevelEnemyMaximum { get; set; }
    public ushort[] LevelEnemies { get; } = new ushort[40];
    public byte EnemySound { get; set; }
    public int LastSpawnedEnemyIndex { get; set; }
    public bool SkyEnemyOverAll { get; set; }
    public bool Background3X1 { get; set; }
    public bool TopEnemyOver { get; set; }
    public int CurrentScreen { get; set; } = 1;
}

public interface ILevelBackgroundSceneHost
{
    void ClearScreen(int screen);
    void DrawBackground1(int screen);
    void DrawBackground2(int screen);
    void DrawBackground2Blend(int screen);
    void DrawBackground3(int screen);
    void DrawStarfield(int screen, int speed);
    void IcedBlur(int destination, int source);
    void Lava(int destination, int source);
    void Water(int destination, int source);
    void Blur(int destination, int source);
    ushort DrawEnemy(int priority, int screen, ushort mapOffset, ushort backgroundMove, ushort enemiesOnScreen);
    uint NextRandomUInt32();
    int SpawnEnemy(ushort enemyType);
    void DarkenBackground(int screen, ushort amount);
}

public sealed class LevelBackgroundScene(LevelBackgroundSceneState state, ILevelBackgroundSceneHost host)
{
    public void Draw()
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.ForceEvents && state.BackMove == 0)
            state.CurrentLocation = unchecked((ushort)(state.CurrentLocation + 1));

        if (state.Map1YDelayMax > 1 && state.BackMove < 2)
            state.BackMove = state.Map1YDelay == 1 ? (ushort)1 : (ushort)0;

        if (state.AstralDuration == 0)
            host.DrawBackground1(state.CurrentScreen);
        else
            host.ClearScreen(state.CurrentScreen);

        state.Map1YDelay = unchecked((byte)(state.Map1YDelay - 1));
        if (state.Map1YDelay == 0)
        {
            state.Map1YDelay = state.Map1YDelayMax;
            state.CurrentLocation = unchecked((ushort)(state.CurrentLocation + state.BackMove));
            state.BackPosition = unchecked((ushort)(state.BackPosition + state.BackMove));
            if (state.BackPosition > 27)
            {
                state.BackPosition -= 28;
                state.MapY = unchecked((ushort)(state.MapY - 1));
                state.MapYPositionOffset -= 14;
            }
        }

        if (state.StarfieldActive || state.AstralDuration > 0)
            host.DrawStarfield(state.CurrentScreen, state.StarfieldSpeed);

        if (state.ProcessorType > 1 && state.Smoothies[4])
        {
            host.IcedBlur(0, state.CurrentScreen);
            state.CurrentScreen = 0;
        }

        if (state.Background2Over == 3)
        {
            host.DrawBackground2(state.CurrentScreen);
            state.Background2 = true;
        }

        if (state.Background2Over == 0 &&
            !(state.Smoothies[1] && state.ProcessorType < 4) &&
            !(state.Smoothies[0] && state.ProcessorType == 3))
        {
            DrawBackground2(state.CurrentScreen);
        }

        if (state.Smoothies[0] && state.ProcessorType > 2 && state.SmoothieData[0] == 0)
        {
            host.Lava(0, state.CurrentScreen);
            state.CurrentScreen = 0;
        }
        if (state.Smoothies[1] && state.ProcessorType > 2)
        {
            host.Water(0, state.CurrentScreen);
            state.CurrentScreen = 0;
        }

        state.LastEnemyOnScreen = state.EnemyOnScreen;
        state.TemporaryMapXOffset = state.MapXOffset;
        state.TemporaryBackgroundMove = state.BackMove;
        state.EnemyOnScreen = host.DrawEnemy(50, state.CurrentScreen, state.TemporaryMapXOffset,
            state.TemporaryBackgroundMove, state.EnemyOnScreen);
        state.EnemyOnScreen = host.DrawEnemy(100, state.CurrentScreen, state.TemporaryMapXOffset,
            state.TemporaryBackgroundMove, state.EnemyOnScreen);

        if (state.EnemyOnScreen == 0 || state.EnemyOnScreen == state.LastEnemyOnScreen)
        {
            if (state.StopBackgroundNumber == 1)
                state.StopBackgroundNumber = 9;
        }

        if (state.Smoothies[0] && state.ProcessorType > 2 && state.SmoothieData[0] > 0)
        {
            host.Lava(0, state.CurrentScreen);
            state.CurrentScreen = 0;
        }

        if (state.SuperWild)
        {
            state.Neat = unchecked((ushort)(state.Neat + 3));
            host.DarkenBackground(state.CurrentScreen, state.Neat);
        }

        if (!(state.Smoothies[1] && state.ProcessorType < 4) &&
            !(state.Smoothies[0] && state.ProcessorType == 3) &&
            state.Background2Over == 1)
        {
            DrawBackground2(state.CurrentScreen);
        }

        if (state.SuperWild)
        {
            state.Neat = unchecked((ushort)(state.Neat + 1));
            host.DarkenBackground(state.CurrentScreen, state.Neat);
        }

        if (state.Background3Over == 2)
            host.DrawBackground3(state.CurrentScreen);

        if (state.EnemiesActive && host.NextRandomUInt32() % 100 > state.LevelEnemyFrequency)
        {
            if (state.LevelEnemyMaximum == 0)
                throw new InvalidDataException("Active level enemy list has no entries.");
            var enemyType = state.LevelEnemies[host.NextRandomUInt32() % state.LevelEnemyMaximum];
            if (enemyType == 2)
                state.EnemySound = 7;
            state.LastSpawnedEnemyIndex = host.SpawnEnemy(enemyType);
        }

        if (state.ProcessorType > 1 && state.Smoothies[2])
        {
            host.IcedBlur(0, state.CurrentScreen);
            state.CurrentScreen = 0;
        }
        if (state.ProcessorType > 1 && state.Smoothies[3])
        {
            host.Blur(0, state.CurrentScreen);
            state.CurrentScreen = 0;
        }

        if (!state.SkyEnemyOverAll)
        {
            state.LastEnemyOnScreen = state.EnemyOnScreen;
            state.TemporaryMapXOffset = state.MapX2Offset;
            state.TemporaryBackgroundMove = 0;
            state.EnemyOnScreen = host.DrawEnemy(25, state.CurrentScreen, state.TemporaryMapXOffset,
                state.TemporaryBackgroundMove, state.EnemyOnScreen);
            if (state.EnemyOnScreen == state.LastEnemyOnScreen && state.StopBackgroundNumber == 2)
                state.StopBackgroundNumber = 9;
        }

        if (state.Background3Over == 0)
            host.DrawBackground3(state.CurrentScreen);

        if (!state.TopEnemyOver)
        {
            state.TemporaryMapXOffset = state.Background3X1 ? state.MapXOffset : state.OldMapX3Offset;
            state.TemporaryBackgroundMove = state.Background3Move;
            state.EnemyOnScreen = host.DrawEnemy(75, state.CurrentScreen, state.TemporaryMapXOffset,
                state.TemporaryBackgroundMove, state.EnemyOnScreen);
        }
    }

    private void DrawBackground2(int screen)
    {
        if (state.Wild && !state.Background2NotTransparent)
            host.DrawBackground2Blend(screen);
        else
            host.DrawBackground2(screen);
    }
}
