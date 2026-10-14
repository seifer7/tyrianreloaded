/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class EpisodeMapLoadState
{
    public bool PlayDemo { get; set; }
    public bool GameLoaded { get; set; }
    public bool JumpSection { get; set; }
    public bool LoadLevelOk { get; set; }
    public bool GalagaMode { get; set; }
    public bool UseLastBank { get; set; }
    public bool ExtraGame { get; set; }
    public bool HaltGame { get; set; }
    public bool NetworkGame { get; set; }
    public bool EscapePressed { get; set; }
    public bool BonusLevel { get; set; }
    public bool NormalBonusLevel { get; set; }
    public bool GameJustLoaded { get; set; }
    public byte MainLevel { get; set; }
    public byte SaveLevel { get; set; }
    public byte NextLevel { get; set; }
    public byte LevelFileNumber { get; set; }
    public byte SongBuy
    {
        get => MusicMaster.SongBuy;
        set => MusicMaster.SongBuy = value;
    }
    public ushort CubeMaximum { get; set; }
    public ushort LastCubeMaximum { get; set; }
    public ushort MapOrigin { get; set; }
    public ushort MapPlanetCount { get; set; }
    public ushort[] CubeList { get; } = new ushort[4];
    public byte[] MapPlanets { get; } = new byte[5];
    public byte[] MapSections { get; } = new byte[5];
    public byte[] LevelName { get; } = new byte[11];
    public byte LevelSong { get; set; }
}

public interface IEpisodeMapLoaderHost
{
    Stream OpenEpisodeFile();
    void PlayAnimation(string filename, int firstFrame, int lastFrame);
    void ConfigureMode(EpisodeMapLoadState state, char command);
    void HandleLevelJump(EpisodeMapLoadState state, char command, ReadOnlyMemory<byte> section);
    void SaveGame(int slot, string description);
    void ShowItemScreen(Stream episode, ReadOnlyMemory<byte> section);
    bool HandlePresentation(char command, Stream episode, ReadOnlyMemory<byte> section,
        Memory<byte> buffer, Memory<byte> pictureBuffer);
    bool LoadNextDemo();
    void FadeBlack(int steps);
    void PlaySong(int song);
    void ReadNetworkText();
    void LoadSelectedLevelMap(EpisodeMapLoadState state);
}

public sealed class EpisodeMapLoader(EpisodeMapLoadState state, IEpisodeMapLoaderHost host,
    byte defaultSongBuy, sbyte initialDifficulty, sbyte normalDifficulty)
{
    public bool Run()
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        state.LastCubeMaximum = state.CubeMaximum;
        state.SongBuy = defaultSongBuy;
        while (true)
        {
            state.GalagaMode = false;
            state.UseLastBank = false;
            state.ExtraGame = false;
            state.HaltGame = false;
            state.GameLoaded = false;

            if (!state.PlayDemo)
            {
                var result = ReadEpisodeUntilSelected();
                if (result == EpisodeReadResult.ReturnToTitle)
                    return false;
                if (result == EpisodeReadResult.RestartGame)
                    continue;
            }
            else if (!host.LoadNextDemo())
            {
                return false;
            }
            else
            {
                state.LoadLevelOk = true;
            }

            if (!state.PlayDemo)
                host.FadeBlack(50);
            host.LoadSelectedLevelMap(state);
            return true;
        }
    }

    private EpisodeReadResult ReadEpisodeUntilSelected()
    {
        while (true)
        {
            var loadLevel = false;
            do
            {
                using var episode = host.OpenEpisodeFile();
                state.JumpSection = false;
                state.LoadLevelOk = false;
                SeekMainLevel(episode);
                state.EscapePressed = false;
                var section = new byte[256];
                var buffer = new byte[256];
                var pictureBuffer = new byte[320 * 200];
                while (!state.LoadLevelOk && !state.JumpSection)
                {
                    if (state.GameLoaded)
                    {
                        return state.MainLevel == 0
                            ? EpisodeReadResult.ReturnToTitle
                            : EpisodeReadResult.RestartGame;
                    }

                    Array.Clear(section);
                    EncryptedText.ReadPascalString(episode, section);
                    if (section[0] != (byte)']')
                        continue;

                    var command = (char)section[1];
                    switch (command)
                    {
                        case 'A':
                            host.PlayAnimation("tyrend.anm", 0, 7);
                            break;
                        case 'G':
                            ReadGalaxyMap(section);
                            break;
                        case '?':
                            ReadCubeList(section);
                            break;
                        case '!':
                            state.CubeMaximum = unchecked((ushort)Atoi(section, 4));
                            break;
                        case '+':
                            state.CubeMaximum = unchecked((ushort)(state.CubeMaximum + Atoi(section, 4)));
                            if (state.CubeMaximum > 4) state.CubeMaximum = 4;
                            break;
                        case 'g':
                        case 'x':
                        case 'e':
                            host.ConfigureMode(state, command);
                            break;
                        case 'J':
                        case '2':
                        case 'w':
                        case 't':
                        case 'l':
                        case 'H':
                            host.HandleLevelJump(state, command, section);
                            break;
                        case 's':
                            state.SaveLevel = state.MainLevel;
                            break;
                        case 'b':
                            host.SaveGame(11, "LAST LEVEL    ");
                            break;
                        case 'i':
                            state.SongBuy = unchecked((byte)(Atoi(section, 3) - 1));
                            break;
                        case 'I':
                            host.ShowItemScreen(episode, section);
                            break;
                        case 'L':
                            ReadLevelRecord(section);
                            loadLevel = true;
                            break;
                        case '@':
                            state.UseLastBank = !state.UseLastBank;
                            break;
                        case 'Q':
                        case 'P':
                        case 'U':
                        case 'V':
                        case 'R':
                        case 'C':
                        case 'B':
                        case 'F':
                        case 'W':
                            if (host.HandlePresentation(command, episode, section, buffer, pictureBuffer))
                                return EpisodeReadResult.ReturnToTitle;
                            break;
                        case 'h':
                            if (initialDifficulty > normalDifficulty)
                                EncryptedText.SkipPascalString(episode);
                            break;
                        case 'S':
                            if (state.NetworkGame) host.ReadNetworkText();
                            break;
                        case 'n':
                            state.EscapePressed = false;
                            break;
                        case 'M':
                            host.PlaySong(Atoi(section, 3) - 1);
                            break;
                    }
                }
            } while (!loadLevel);
            return EpisodeReadResult.LevelSelected;
        }
    }

    private enum EpisodeReadResult { LevelSelected, RestartGame, ReturnToTitle }

    private void SeekMainLevel(Stream episode)
    {
        var found = 0;
        while (found < state.MainLevel)
        {
            var section = new byte[256];
            EncryptedText.ReadPascalString(episode, section);
            if (section[0] == (byte)'*')
            {
                found++;
                section[0] = (byte)' ';
            }
        }
    }

    private void ReadGalaxyMap(ReadOnlySpan<byte> section)
    {
        state.MapOrigin = unchecked((ushort)Atoi(section, 4));
        state.MapPlanetCount = unchecked((ushort)Atoi(section, 7));
        if (state.MapPlanetCount > state.MapPlanets.Length)
            throw new InvalidDataException("Episode galaxy map exceeds its five-planet native table.");
        for (var index = 0; index < state.MapPlanetCount; index++)
        {
            state.MapPlanets[index] = unchecked((byte)Atoi(section, 1 + (index + 1) * 8));
            state.MapSections[index] = unchecked((byte)Atoi(section, 4 + (index + 1) * 8));
        }
    }

    private void ReadCubeList(ReadOnlySpan<byte> section)
    {
        var count = Atoi(section, 4);
        if (count > state.CubeList.Length)
            throw new InvalidDataException("Episode cube list exceeds its four-cube native table.");
        for (var index = 0; index < count; index++)
            state.CubeList[index] = unchecked((ushort)Atoi(section, 3 + (index + 1) * 4));
        if (state.CubeMaximum > count)
            state.CubeMaximum = unchecked((ushort)count);
    }

    private void ReadLevelRecord(ReadOnlySpan<byte> section)
    {
        state.NextLevel = unchecked((byte)Atoi(section, 9));
        var nameOffset = 13;
        var nameLength = 0;
        while (nameLength < 9 && nameOffset + nameLength < section.Length &&
               section[nameOffset + nameLength] != 0)
        {
            state.LevelName[nameLength] = section[nameOffset + nameLength];
            nameLength++;
        }
        state.LevelName[nameLength] = 0;
        state.LevelSong = unchecked((byte)Atoi(section, 22));
        if (state.NextLevel == 0)
            state.NextLevel = unchecked((byte)(state.MainLevel + 1));
        state.LevelFileNumber = unchecked((byte)Atoi(section, 25));
        state.LoadLevelOk = true;
        var sectionLength = section.IndexOf((byte)0);
        if (sectionLength < 0) sectionLength = section.Length;
        state.BonusLevel = sectionLength > 28 && section[28] == (byte)'$';
        state.NormalBonusLevel = sectionLength > 27 && section[27] == (byte)'$';
        state.GameJustLoaded = false;
    }

    private static int Atoi(ReadOnlySpan<byte> section, int offset)
        => offset >= section.Length ? 0 : EpisodeLevelJump.ParseAtoi(section[offset..]);
}
