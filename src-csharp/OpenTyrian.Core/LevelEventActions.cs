/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ILevelEventServices
{
    void LoadEnemySheet(SpriteSheet sheet, char file);
    void DrawTextWindow(int index);
    void PlaySong(int index);
    void SetVolume(ushort music, ushort effects);
}

public sealed partial class LevelEvents
{
    public short StarfieldSpeed { get; set; }
    public bool StopBackgrounds { get; set; }
    public byte StopBackgroundNumber { get; set; }
    public bool StarsActive { get; set; }
    public bool EnemiesActive { get; set; }
    public bool AllPlayersGone { get; set; }
    public bool ReallyEndLevel { get; set; }
    public bool ReadyToEndLevel { get; set; }
    public bool EndLevel { get; set; }
    public bool FirstGameOver { get; set; }
    public bool MusicFade
    {
        get => MusicMaster.MusicFade;
        set => MusicMaster.MusicFade = value;
    }
    public ushort EffectsVolume { get; set; }
    public ushort EnemyFrequency { get; set; }
    public byte SecondOver { get; set; }
    public bool FilterActive { get; set; }
    public byte DamageRate { get; set; }
    public bool SecondNotTransparent { get; set; }
    public bool ForceEvents { get; set; }
    public sbyte InitialDifficulty { get; set; }
    public ushort TimerCountdown { get; set; }
    public ushort TimerJump { get; set; }
    public bool RandomExplosions { get; set; }
    public bool SkyOverAll { get; set; }
    public bool ReturnActive { get; set; }
    public int SecondWrap { get; set; }
    public int SecondWrapTo { get; set; }
    private const string ShapeFiles = "2478ABCDEFGHIJKLMNOPQRSTU5#V0@3^59";
    private static ReadOnlySpan<byte> TextSamples => SoundMaster.WindowTextSamples;

    public void HandleEnvironmentEvent(ILevelEventServices services, PlayerCombat combat, TextWriter warnings)
    {
        var record = Current;
        switch (record.Type)
        {
            case 1: StarfieldSpeed = record.Data; break;
            case 2:
                FirstDelay = FirstDelayMax = backgrounds.SecondDelay = backgrounds.SecondDelayMax = 1;
                SetBackgroundMoves(record);
                effects.ExplosionMove = backgrounds.Second.Move > 0 ? backgrounds.Second.Move : backgrounds.First.Move;
                if (backgrounds.First.Move > 0) StopBackgroundNumber = 0;
                break;
            case 3:
                backgrounds.First.Move = backgrounds.Second.Move = backgrounds.Third.Move = 1;
                FirstDelay = FirstDelayMax = 3; backgrounds.SecondDelay = backgrounds.SecondDelayMax = 2;
                enemies.FirstBackgroundMove = 1; effects.BackgroundMove = 1;
                break;
            case 4:
                StopBackgrounds = true;
                if (record.Data is 0 or 1) StopBackgroundNumber = 1;
                else if (record.Data is 2 or 3) StopBackgroundNumber = (byte)record.Data;
                break;
            case 5:
                byte[] ids = [PositiveByte(record.Data), PositiveByte(record.Data2), PositiveByte(record.Data3), record.Data4];
                for (var i = 0; i < ids.Length; i++)
                {
                    if (enemies.SpriteIds[i] == ids[i]) continue;
                    if (ids[i] > 0)
                    {
                        if (ids[i] > ShapeFiles.Length) throw new InvalidDataException("Enemy shape bank exceeds the native shape-file table.");
                        services.LoadEnemySheet(enemies.SpriteSheets[i], ShapeFiles[ids[i] - 1]);
                    }
                    else enemies.SpriteSheets[i].Clear();
                    enemies.SpriteIds[i] = ids[i];
                }
                break;
            case 6: CreateEnemy(0, 25); break;
            case 7: CreateEnemy(0, 50); break;
            case 8: StarsActive = false; break;
            case 9: StarsActive = true; break;
            case 10: CreateEnemy(0, 75); break;
            case 11:
                if (AllPlayersGone || record.Data == 1) ReallyEndLevel = true;
                else if (!EndLevel) { ReadyToEndLevel = false; EndLevel = true; combat.LevelEnd = 40; }
                break;
            case 12:
                var offset = record.Data6 switch { 0 or 1 => 25, 2 => 0, 3 => 50, 4 => 75, _ => 0 };
                record.Data6 = 0;
                CreateEnemy(0, (ushort)offset);
                var second = CreateEnemy(1, (ushort)offset);
                if (second > 0) enemies.Enemies[second - 1].X = unchecked((short)(enemies.Enemies[second - 1].X + 24));
                var third = CreateEnemy(2, (ushort)offset);
                if (third > 0) enemies.Enemies[third - 1].Y = unchecked((short)(enemies.Enemies[third - 1].Y - 28));
                var fourth = CreateEnemy(3, (ushort)offset);
                if (fourth > 0)
                {
                    enemies.Enemies[fourth - 1].X = unchecked((short)(enemies.Enemies[fourth - 1].X + 24));
                    enemies.Enemies[fourth - 1].Y = unchecked((short)(enemies.Enemies[fourth - 1].Y - 28));
                }
                break;
            case 13: EnemiesActive = false; break;
            case 14: EnemiesActive = true; break;
            case 15: CreateEnemy(0, 0); break;
            case 16:
                if (record.Data > 9) warnings.Write("warning: event 16: bad event data\n");
                else if (record.Data < 1) throw new InvalidDataException("Text-window event index must be in 1..9.");
                else { services.DrawTextWindow(record.Data); effects.SoundQueue[3] = TextSamples[record.Data - 1]; }
                break;
            case 17: CreateEnemyAtY(0, 25, 190, true); break;
            case 18: CreateEnemyAtY(0, 0, 190, true); break;
            default: throw new InvalidDataException($"Event {record.Type} is outside the environment-event group.");
        }
    }

    private static byte PositiveByte(int value) => value > 0 ? unchecked((byte)value) : (byte)0;

    private void SetBackgroundMoves(LevelEvent record)
    {
        backgrounds.First.Move = unchecked((ushort)record.Data);
        backgrounds.Second.Move = unchecked((ushort)record.Data2);
        backgrounds.Third.Move = unchecked((ushort)record.Data3);
        enemies.FirstBackgroundMove = backgrounds.First.Move;
        effects.BackgroundMove = backgrounds.Second.Move;
    }

    public void HandleLevelEvent(ILevelEventServices services, PlayerCombat combat)
    {
        var record = Current;
        unchecked
        {
            switch (record.Type)
            {
                case 34:
                    if (FirstGameOver) { MusicFade = true; combat.TemporaryVolume = combat.MusicVolume; }
                    break;
                case 35:
                    if (FirstGameOver) { services.PlaySong(record.Data - 1); services.SetVolume(combat.MusicVolume, EffectsVolume); }
                    MusicFade = false;
                    break;
                case 36: ReadyToEndLevel = true; break;
                case 37: EnemyFrequency = (ushort)record.Data; break;
                case 38:
                    Location = (ushort)record.Data;
                    var newLocation = 1;
                    for (var i = 0; i < Events.Length; i++) if (Events[i].Time <= Location) newLocation = i;
                    EventLocation = (ushort)newLocation;
                    break;
                case 39:
                    foreach (var enemy in enemies.Enemies) if (enemy.LinkNumber == record.Data) enemy.LinkNumber = (byte)record.Data2;
                    break;
                case 40: enemies.ContinualDamage = true; break;
                case 41:
                    for (var i = 0; i < (record.Data == 0 ? 100 : 25); i++) enemies.Enemies[i].Availability = 1;
                    break;
                case 42: ThirdOver = 2; break;
                case 43: SecondOver = (byte)record.Data; break;
                case 44:
                    FilterActive = record.Data > 0; backgrounds.FilterFade = record.Data == 2;
                    backgrounds.LevelFilter = (sbyte)record.Data2; backgrounds.Brightness = record.Data3;
                    backgrounds.NewLevelFilter = (sbyte)record.Data4; backgrounds.BrightnessChange = record.Data5;
                    backgrounds.FilterFadeStart = record.Data6 == 0;
                    break;
                case 45:
                    if (!SuperTyrian)
                    {
                        ApplyRandomSpecialDrop();
                        if (enemies.TwoPlayers || OnePlayerAction) SetEnemyDeath();
                    }
                    break;
                case 46:
                    if (record.Data3 != 0) DamageRate = (byte)record.Data3;
                    if (record.Data2 == 0 || enemies.TwoPlayers || OnePlayerAction)
                    {
                        enemies.Difficulty = (sbyte)(enemies.Difficulty + record.Data);
                        enemies.Difficulty = (sbyte)Math.Clamp((int)enemies.Difficulty, 1, 10);
                    }
                    break;
                case 47:
                    foreach (var enemy in enemies.Enemies)
                        if (record.Data4 == 0 || enemy.LinkNumber == record.Data4) enemy.Armor = (byte)record.Data;
                    break;
                case 48: SecondNotTransparent = true; break;
                case 49 or 50 or 51 or 52:
                    var graphic = record.Data; var bank = record.Data3; var armor = record.Data6;
                    record.Data = 0; record.Data3 = record.Data6 = 0;
                    items.Enemies[0].Armor = (byte)armor; items.Enemies[0].EGraphic[0] = (ushort)graphic;
                    try
                    {
                        CreateEnemy(0, (ushort)(record.Type switch { 49 => 25, 50 => 0, 51 => 50, _ => 75 }), bank);
                    }
                    finally { record.Data = graphic; record.Data3 = bank; record.Data6 = armor; }
                    break;
                case 53: ForceEvents = record.Data != 99; break;
                case 54: Jump((ushort)record.Data); break;
                case 55:
                    ResolveEnemyLink();
                    foreach (var enemy in enemies.Enemies)
                    {
                        if (record.Data4 != 0 && enemy.LinkNumber != record.Data4) continue;
                        if (record.Data != -99) enemy.XAcceleration = (byte)record.Data;
                        if (record.Data2 != -99) enemy.YAcceleration = (byte)record.Data2;
                    }
                    break;
                case 56: CreateEnemyAtY(0, 75, 190, false); break;
                case 57: enemies.Enemy254Jump = (ushort)record.Data; break;
                default: throw new InvalidDataException($"Event {record.Type} is outside the level-event group.");
            }
        }
    }

    public void HandleMiscEvent(PlayerShots shots, PlayerCombat combat)
    {
        var record = Current;
        unchecked
        {
            switch (record.Type)
            {
                case 60:
                    foreach (var enemy in enemies.Enemies)
                    {
                        if (enemy.LinkNumber != record.Data4) continue;
                        enemy.Special = true; enemy.FlagNumber = (byte)record.Data; enemy.SetTo = record.Data2 == 1;
                    }
                    break;
                case 61:
                    if (record.Data is < 1 or > 10) throw new InvalidDataException("Event flag index must be in 1..10.");
                    if ((enemies.GlobalFlags[record.Data - 1] ? 1 : 0) == record.Data2) EventLocation += (ushort)record.Data3;
                    break;
                case 62: effects.SoundQueue[3] = (byte)record.Data; break;
                case 63:
                    if (!enemies.TwoPlayers && !OnePlayerAction) EventLocation += (ushort)record.Data;
                    break;
                case 64:
                    if (record.Data == 6 && enemies.TwoPlayers && enemies.Difficulty > 2) break;
                    if (record.Data is < 1 or > 9) throw new InvalidDataException("Smoothie event index must be in 1..9.");
                    backgrounds.Smoothies[record.Data - 1] = record.Data2 != 0;
                    backgrounds.SmoothieData[(record.Data == 5 ? 3 : record.Data) - 1] = (byte)record.Data3;
                    break;
                case 65: ThirdAtFirstX = record.Data == 0; break;
                case 66:
                    if (InitialDifficulty <= record.Data) EventLocation += (ushort)record.Data2;
                    break;
                case 67:
                    combat.LevelTimer = record.Data == 1; TimerCountdown = (ushort)(record.Data3 * 100); TimerJump = (ushort)record.Data2;
                    break;
                case 68: RandomExplosions = record.Data == 1; break;
                case 69:
                    foreach (var player in players) player.InvulnerableTicks = (uint)record.Data;
                    break;
                case 70:
                    if (record.Data2 == 0)
                    {
                        var found = false;
                        for (byte link = 1; link <= 19; link++) found = found || SearchEnemy(link, out _);
                        if (!found) Jump((ushort)record.Data);
                    }
                    else if (!SearchEnemy((byte)record.Data2, out _) &&
                        (record.Data3 == 0 || !SearchEnemy((byte)record.Data3, out _)) &&
                        (record.Data4 == 0 || !SearchEnemy(record.Data4, out _))) Jump((ushort)record.Data);
                    break;
                case 71:
                    if ((long)backgrounds.First.MapOffset * 2 <= (uint)record.Data2) Jump((ushort)record.Data);
                    break;
                case 72: ThirdAtFirstXB = record.Data == 1; break;
                case 73: SkyOverAll = record.Data == 1; break;
                case 74:
                    foreach (var enemy in enemies.Enemies)
                    {
                        if (record.Data4 != 0 && enemy.LinkNumber != record.Data4) continue;
                        if (record.Data5 != -99) enemy.XMinBounce = record.Data5;
                        if (record.Data6 != -99) enemy.YMinBounce = record.Data6;
                        if (record.Data != -99) enemy.XMaxBounce = record.Data;
                        if (record.Data2 != -99) enemy.YMaxBounce = record.Data2;
                    }
                    break;
                case 75: AssignRandomLink(record); break;
                case 76: ReturnActive = true; break;
                case 77:
                    backgrounds.First.MapOffset = record.Data / 2;
                    backgrounds.Second.MapOffset = (record.Data2 > 0 ? record.Data2 : record.Data) / 2;
                    break;
                case 78:
                    if (enemies.GalagaShotFrequency < 10) enemies.GalagaShotFrequency++;
                    break;
                case 79:
                    enemies.BossBars[0].LinkNumber = (byte)record.Data; enemies.BossBars[1].LinkNumber = (byte)record.Data2;
                    break;
                case 80:
                    if (enemies.TwoPlayers) EventLocation += (ushort)record.Data;
                    break;
                case 81:
                    SecondWrap = record.Data / 2;
                    SecondWrapTo = record.Data2 / 2;
                    backgrounds.Second.WrapAt = SecondWrap;
                    backgrounds.Second.WrapTo = SecondWrapTo;
                    break;
                case 82:
                    if (!items.TryFind(ItemType.Special, record.Data, out var special))
                        throw new InvalidDataException($"Event special weapon ID '{record.Data}' is missing.");
                    players[0].Items.Special = (byte)special;
                    shots.MultiPosition[8] = shots.MultiPosition[10] = shots.Repeat[8] = shots.Repeat[10] = 0;
                    break;
                default: throw new InvalidDataException($"Event {record.Type} is outside the miscellaneous-event group.");
            }
        }
    }

    private void AssignRandomLink(LevelEvent record)
    {
        if (record.Data3 is < 80 or > 89) throw new InvalidDataException("Random link slot must be in 80..89.");
        var exists = enemies.Enemies.Any(enemy => enemy.Availability == 0 && enemy.YSpeed == 0 &&
            enemy.LinkNumber >= record.Data && enemy.LinkNumber <= record.Data2);
        if (exists)
        {
            if (record.Data < 0 || record.Data2 > 255 || record.Data2 < record.Data)
                throw new InvalidDataException("Random enemy link range must be within 0..255.");
            // Native search selects the last matching active slot, not the first stationary one.
            var eligible = enemies.Enemies.Select(enemy => enemy.LinkNumber).Distinct()
                .Any(link => link >= record.Data && link <= record.Data2 &&
                    enemies.Enemies.LastOrDefault(enemy => enemy.Availability == 0 && enemy.LinkNumber == link)?.YSpeed == 0);
            if (!eligible) throw new InvalidDataException("Random link event would loop forever: last matching enemies are moving.");
            byte link;
            do { link = (byte)(MersenneTwister.NextUInt32() % (uint)(record.Data2 + 1 - record.Data) + record.Data); }
            while (!(SearchEnemy(link, out var enemyIndex) && enemies.Enemies[enemyIndex].YSpeed == 0));
            NewLinks[record.Data3 - 80] = link;
        }
        else
        {
            NewLinks[record.Data3 - 80] = 255;
            if (record.Data4 > 0)
            {
                var index = EventLocation - 1 + record.Data4;
                if (index >= Events.Length) throw new InvalidDataException("Random link skip is outside the event array.");
                Location = unchecked((ushort)(Events[index].Time - 1));
                EventLocation = unchecked((ushort)(EventLocation + record.Data4 - 1));
            }
        }
    }

    public void Dispatch(ILevelEventServices services, PlayerShots shots, PlayerCombat combat, TextWriter warnings)
    {
        if (!ReferenceEquals(shots.Enemies, enemies.Enemies) || !ReferenceEquals(shots.Players, players) ||
            !ReferenceEquals(shots.SoundQueue, effects.SoundQueue) ||
            !ReferenceEquals(combat.Players, players) || !ReferenceEquals(combat.Effects, effects))
            throw new ArgumentException("Level events require shared gameplay state.", nameof(shots));
        var type = Current.Type;
        if (type is >= 1 and <= 18) HandleEnvironmentEvent(services, combat, warnings);
        else if (type is >= 19 and <= 33) HandleEnemyEvent();
        else if (type is >= 34 and <= 57) HandleLevelEvent(services, combat);
        else if (type is >= 60 and <= 82) HandleMiscEvent(shots, combat);
        else warnings.Write($"warning: ignoring unknown event {type}\n");
        EventLocation = unchecked((ushort)(EventLocation + 1));
    }

    public void UpdateEndState(PlayerCombat combat, bool waitToEndLevel)
    {
        if (!ReferenceEquals(combat.Players, players) || !ReferenceEquals(combat.Effects, effects))
            throw new ArgumentException("Level completion requires shared gameplay state.", nameof(combat));
        if (StopBackgroundNumber == 9 && backgrounds.First.Move == 0 && !effects.EnemyStillExploding)
        {
            ResumeBackgrounds();
            StopBackgroundNumber = 0;
            StopBackgrounds = false;
            if (waitToEndLevel) { EndLevel = true; combat.LevelEnd = 40; }
            if (AllPlayersGone) ReallyEndLevel = true;
        }
        if (!EndLevel && enemies.EnemiesOnScreen == 0)
        {
            if (ReadyToEndLevel && !effects.EnemyStillExploding)
            {
                if (TimerCountdown > 0) combat.LevelTimer = false;
                ReadyToEndLevel = false;
                EndLevel = true;
                combat.LevelEnd = 40;
                if (AllPlayersGone) ReallyEndLevel = true;
            }
            if (StopBackgrounds) { StopBackgrounds = false; ResumeBackgrounds(); }
        }
    }

    private void ResumeBackgrounds()
    {
        backgrounds.First.Move = enemies.FirstBackgroundMove = 1;
        backgrounds.Second.Move = effects.BackgroundMove = effects.ExplosionMove = 2;
        backgrounds.Third.Move = 3;
    }

    public void PlaySounds(Action<byte, byte, byte> playSample)
    {
        if (!FirstGameOver) return;
        for (byte channel = 0; channel < effects.SoundQueue.Length; channel++)
        {
            var sample = effects.SoundQueue[channel];
            if (sample == 0) continue;
            if (sample > 38) throw new InvalidDataException($"Level sound sample {sample} is outside the native sound bank.");
            var volume = channel == 3 ? (byte)4 : sample == 15 ? (byte)1 : (byte)2;
            playSample(sample, channel, volume);
            effects.SoundQueue[channel] = 0;
        }
    }
}
