/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Runtime.InteropServices;
using System.Text;

namespace OpenTyrian.Core;

public interface IKeyboardNames
{
    int FromName(string name);
    string Name(int scancode);
}

public sealed class SdlKeyboardNames : IKeyboardNames
{
    public int FromName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return SdlNative.GetScancodeFromName(name);
    }
    public string Name(int scancode) => Marshal.PtrToStringUTF8(SdlNative.GetScancodeName(scancode))
        ?? throw new InvalidOperationException("SDL returned a null scancode name.");
}

public sealed class GameConfiguration(IKeyboardNames keyboard)
{
    private static readonly string[] KeyNames =
        ["up", "down", "left", "right", "fire", "change fire", "left sidekick", "right sidekick"];
    private static readonly int[] DefaultKeys = [82, 81, 80, 79, 44, 40, 224, 226];
    public Configuration Text { get; private set; } = new();
    public VideoScaler Scaler { get; } = new();
    public VideoLayout Layout { get; } = new();
    public int FullscreenDisplay { get; set; } = -1;
    public int ShopContent { get; set; }
    public int[] Keys { get; } = DefaultKeys.ToArray();

    public int GetKey(KeySetting setting) => Keys[KeyIndex(setting)];

    public void SetKey(KeySetting setting, int scancode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(scancode);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(scancode, 512);
        Keys[KeyIndex(setting)] = scancode;
    }

    private static int KeyIndex(KeySetting setting)
        => (uint)setting < KeyNames.Length ? (int)setting : throw new ArgumentOutOfRangeException(nameof(setting));

    private void Defaults()
    {
        FullscreenDisplay = -1;
        Scaler.SetByName("Scale2x");
        DefaultKeys.CopyTo(Keys, 0);
        ShopContent = 0;
    }

    public void Load(TextReader reader)
    {
        Defaults();
        Text = Configuration.Parse(reader);
        var section = Text.FindSection("video");
        if (section != null)
        {
            if (section.TryGetInt32("fullscreen", out var fullscreen)) FullscreenDisplay = fullscreen;
            if (section.TryGetString("scaler", out var scaler)) Scaler.SetByName(scaler!);
            if (section.TryGetString("scaling_mode", out var scaling)) Layout.SetModeByName(scaling!);
        }
        section = Text.FindSection("keyboard");
        if (section != null)
        {
            for (var i = 0; i < Keys.Length; i++)
            {
                if (!section.TryGetString(KeyNames[i], out var name)) continue;
                var scancode = keyboard.FromName(name!);
                if (scancode != 0) SetKey((KeySetting)i, scancode);
            }
        }
        section = Text.FindSection("gameplay");
        if (section != null && section.TryGetInt32("shop_content", out var content)) ShopContent = content;
    }

    public bool LoadFile(string directory, TextWriter diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Defaults();
        var path = Path.Combine(directory, "opentyrian.cfg");
        try
        {
            using var reader = new StreamReader(path, Encoding.UTF8);
            Load(reader);
            return true;
        }
        catch (FileNotFoundException) { diagnostics.WriteLine($"warning: configuration file not found: {path}"); return false; }
        catch (DirectoryNotFoundException) { diagnostics.WriteLine($"warning: configuration directory not found: {directory}"); return false; }
    }

    public void Save(TextWriter writer)
    {
        var section = Text.FindOrAddSection("video");
        section.SetInt32("fullscreen", FullscreenDisplay);
        section.SetOption("scaler", VideoScaler.Scalers[Scaler.SelectedIndex].Name);
        section.SetOption("scaling_mode", Layout.Mode switch
        {
            ScalingMode.Center => "Center",
            ScalingMode.Integer => "Integer",
            ScalingMode.Fit8By5 => "Fit 8:5",
            ScalingMode.Fit4By3 => "Fit 4:3",
            _ => throw new InvalidOperationException("Unknown video scaling mode.")
        });
        section = Text.FindOrAddSection("keyboard");
        for (var i = 0; i < Keys.Length; i++)
        {
            var name = keyboard.Name(GetKey((KeySetting)i));
            section.SetOption(KeyNames[i], name.Length == 0 ? null : name);
        }
        Text.FindOrAddSection("gameplay").SetInt32("shop_content", ShopContent);
        Text.Write(writer);
    }

    public void SaveFile(string directory)
    {
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        using var stream = new FileStream(Path.Combine(directory, "opentyrian.cfg"), FileMode.Create, FileAccess.Write);
        using (var writer = new StreamWriter(stream, new UTF8Encoding(false), leaveOpen: true))
        {
            Save(writer);
            writer.Flush();
        }
        stream.Flush(flushToDisk: true);
    }
}
