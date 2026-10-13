/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2022 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Text;

namespace OpenTyrian.Core;

public enum SetupMenuId { None, Setup, Graphics, Sound, Gameplay }
public enum SetupItemId { None, Done, Graphics, Sound, Jukebox, Destruct, Gameplay, ShopContents, Display, Scaler, ScalingMode, MusicVolume, SoundVolume }
public enum SetupAction { None, Previous, Next, Back, Activate, SetVolume, AdjustMusicVolume, AdjustSoundVolume }
public readonly record struct SetupMenuInput(SetupAction Action, int HoveredIndex = -1, int Value = 0);
public sealed class SetupMenuState
{
    public SetupMenuId[] Parents { get; } = new SetupMenuId[5];
    public int[] Selected { get; } = new int[5];
    public SetupMenuId Menu { get; set; } = SetupMenuId.Setup;
    public SetupItemId Picker { get; set; }
    public int PickerSelected { get; set; }
    public bool Restart { get; set; } = true;
}

public interface ISetupMenuHost
{
    int FullscreenDisplay { get; }
    int DisplayCount { get; }
    int Scaler { get; }
    ScalingMode ScalingMode { get; set; }
    int ShopContents { get; set; }
    bool MusicDisabled { get; set; }
    bool SamplesDisabled { get; set; }
    void EnsureCursor();
    void LoadBackground();
    void RestoreBackground();
    void Label(int x, int y, string text, Font font, FontAlignment alignment, sbyte brightness);
    void Gauge(int y, byte color, int amount);
    void Rectangle(int x1, int y1, int x2, int y2, byte color);
    void Fill(int x, int y, int width, int height, byte color);
    void Description(string text);
    void FadeIn();
    void FadeBlack();
    void PresentCursor();
    void PushJoysticks();
    void Sample(byte number);
    void Volume(ushort music, ushort effects);
    void RestartSong();
    void ReinitializeFullscreen(int display);
    bool InitializeScaler(int scaler);
    void SaveConfiguration();
    void Jukebox();
    void Destruct();
}

public sealed class SetupMenu(InputState input, DosConfiguration config, ISetupMenuHost host)
{
    private sealed record Item(SetupItemId Id, string Name, string Description);
    private sealed record Menu(string Header, Item[] Items);
    private static readonly Menu[] Menus =
    [
        new("", []),
        new("Setup", [
            new(SetupItemId.Graphics, "Graphics...", "Change the graphics settings."),
            new(SetupItemId.Sound, "Sound...", "Change the sound settings."),
            new(SetupItemId.Jukebox, "Jukebox", "Listen to the music of Tyrian."),
            new(SetupItemId.Gameplay, "Gameplay...", "Change gameplay settings."),
            new(SetupItemId.Done, "Done", "Return to the main menu.")]),
        new("Graphics", [
            new(SetupItemId.Display, "Display:", "Change the display mode."),
            new(SetupItemId.Scaler, "Scaler:", "Change the pixel art scaling algorithm."),
            new(SetupItemId.ScalingMode, "Scaling Mode:", "Change the scaling mode."),
            new(SetupItemId.Done, "Done", "Return to the previous menu.")]),
        new("Sound", [
            new(SetupItemId.MusicVolume, "Music Volume", "Change volume with the left/right arrow keys."),
            new(SetupItemId.SoundVolume, "Sound Volume", "Change volume with the left/right arrow keys."),
            new(SetupItemId.Done, "Done", "Return to the previous menu.")]),
        new("Gameplay", [
            new(SetupItemId.ShopContents, "Shop Contents:", "Change how the Upgrade shop is filled."),
            new(SetupItemId.Done, "Done", "Return to the previous menu.")])
    ];
    private static readonly string[] ScalingNames = ["Center", "Integer", "Fit 8:5", "Fit 4:3"];

    public int PickerCount(SetupItemId item) => item switch
    {
        SetupItemId.ShopContents => 3,
        SetupItemId.Display => checked(1 + host.DisplayCount),
        SetupItemId.Scaler => VideoScaler.Scalers.Count,
        SetupItemId.ScalingMode => ScalingNames.Length,
        _ => throw new ArgumentOutOfRangeException(nameof(item), "Setup item has no picker.")
    };
    public string PickerItem(SetupItemId item, int index) => item switch
    {
        SetupItemId.ShopContents => index == 1 ? "Mixed" : index == 2 ? "Random" : "Mission",
        SetupItemId.Display => index == 0 ? "Window" : $"Display {index.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
        SetupItemId.Scaler => VideoScaler.Scalers[index].Name,
        SetupItemId.ScalingMode => ScalingNames[index],
        _ => throw new ArgumentOutOfRangeException(nameof(item), "Setup item has no picker.")
    };

    public int Draw(SetupMenuState state)
    {
        host.RestoreBackground();
        var menu = Menus[(int)state.Menu]; var selected = state.Selected[(int)state.Menu]; var yPicker = 0;
        host.Label(160, 4, menu.Header, Font.Large, FontAlignment.Centered, -3);
        for (var i = 0; i < menu.Items.Length; i++)
        {
            var item = menu.Items[i]; var y = 37 + 21 * i;
            if (i == selected) yPicker = y;
            var brightness = (sbyte)(-3 + (i == selected ? 2 : 0) + (state.Picker != SetupItemId.None && i != selected ? -4 : 0));
            host.Label(45, y, item.Name, Font.Normal, FontAlignment.Left, brightness);
            var value = item.Id switch
            {
                SetupItemId.Display => PickerItem(item.Id, host.FullscreenDisplay + 1),
                SetupItemId.Scaler => PickerItem(item.Id, host.Scaler),
                SetupItemId.ScalingMode => PickerItem(item.Id, (int)host.ScalingMode),
                SetupItemId.ShopContents => PickerItem(item.Id, host.ShopContents),
                _ => null
            };
            if (value != null) host.Label(180, y, value, Font.Normal, FontAlignment.Left, brightness);
            if (item.Id is SetupItemId.MusicVolume or SetupItemId.SoundVolume)
            {
                var music = item.Id == SetupItemId.MusicVolume;
                var disabled = music ? host.MusicDisabled : host.SamplesDisabled;
                host.Gauge(y, (byte)(disabled ? 170 : 174), ((music ? config.MusicVolume : config.EffectsVolume) + 4) / 8);
                host.Rectangle(178, y - 2, 276, y + 11, 242);
            }

        }
        host.Description(menu.Items[selected].Description);
        if (state.Picker != SetupItemId.None)
        {
            var item = menu.Items[selected].Id; var count = PickerCount(item); var height = 15 * count - 2;
            yPicker = Math.Min(yPicker, 190 - (height + 7));
            host.Rectangle(175, yPicker - 3, 279, yPicker + height + 2, 248);
            host.Rectangle(176, yPicker - 4, 278, yPicker + height + 3, 250);
            host.Rectangle(177, yPicker - 5, 277, yPicker + height + 4, 248);
            host.Fill(178, yPicker - 2, 99, height + 4, 224);
            for (var i = 0; i < count; i++)
                host.Label(180, yPicker + 15 * i, PickerItem(item, i), Font.Normal, FontAlignment.Left, (sbyte)(i == state.PickerSelected ? -1 : -3));
        }
        return yPicker;
    }

    public bool WaitInput(int oldFullscreen, CancellationToken cancellation = default)
    {
        bool moved;
        do
        {
            cancellation.ThrowIfCancellationRequested(); input.Delay(16);
            var x = unchecked((ushort)input.MouseX); var y = unchecked((ushort)input.MouseY);
            host.PushJoysticks(); input.ServiceEvents(false);
            moved = input.MouseX != x || input.MouseY != y;
        } while (!input.NewKey && !input.NewMouse && !moved && host.FullscreenDisplay == oldFullscreen);
        return moved;
    }

    public SetupMenuInput CollectMenuInput(SetupMenuState state, bool moved)
    {
        var items = Menus[(int)state.Menu].Items; var action = SetupAction.None; var hovered = -1; var value = 0;
        if ((moved || input.NewMouse) && input.MouseX is >= 45 and < 275)
        {
            for (var i = 0; i < items.Length; i++)
            {
                var y = 37 + 21 * i;
                if (input.MouseY < y || input.MouseY >= y + 13) continue;
                hovered = i;
                if (input.NewMouse && input.LastMouseButton == 1 && input.LastMouseY >= y && input.LastMouseY < y + 13)
                {
                    if (input.LastMouseX is >= 45 and < 180) action = SetupAction.Activate;
                    else if (input.LastMouseX is >= 180 and < 275)
                    {
                        switch (items[i].Id)
                        {
                            case SetupItemId.ShopContents or SetupItemId.Display or SetupItemId.Scaler or SetupItemId.ScalingMode:
                                action = SetupAction.Activate; break;
                            case SetupItemId.MusicVolume:
                                action = SetupAction.SetVolume; value = (input.LastMouseX - 180) * 255 / 94; break;
                            case SetupItemId.SoundVolume:
                                action = SetupAction.SetVolume; value = -((input.LastMouseX - 180) * 255 / 94 + 1); break;
                        }
                    }
                }
                break;
            }
        }
        if (input.NewMouse) { if (input.LastMouseButton == 3) action = SetupAction.Back; }
        else if (input.NewKey)
        {
            var item = items[hovered < 0 ? state.Selected[(int)state.Menu] : hovered].Id;
            action = input.LastScancode switch
            {
                82 => SetupAction.Previous,
                81 => SetupAction.Next,
                44 or 40 or 88 => SetupAction.Activate,
                41 => SetupAction.Back,
                80 or 79 when item == SetupItemId.MusicVolume => SetupAction.AdjustMusicVolume,
                80 or 79 when item == SetupItemId.SoundVolume => SetupAction.AdjustSoundVolume,
                _ => SetupAction.None
            };
            if (action is SetupAction.AdjustMusicVolume or SetupAction.AdjustSoundVolume) value = input.LastScancode == 80 ? -8 : 8;
        }
        return new(action, hovered, value);
    }

    public SetupMenuInput CollectPickerInput(SetupItemId item, int yPicker, bool moved)
    {
        var action = SetupAction.None; var hovered = -1;
        if ((moved || input.NewMouse) && input.MouseX is >= 180 and < 275)
        {
            for (var i = 0; i < PickerCount(item); i++)
            {
                var y = yPicker + 15 * i;
                if (input.MouseY < y || input.MouseY >= y + 13) continue;
                hovered = i;
                // The native picker tests click Y against the X limit; retain its observable bounds.
                if (input.NewMouse && input.LastMouseButton == 1 && input.LastMouseX >= 180 && input.LastMouseY < 315 &&
                    input.LastMouseY >= y && input.LastMouseY < y + 13) action = SetupAction.Activate;
            }
        }
        if (input.NewMouse) { if (input.LastMouseButton == 3) action = SetupAction.Back; }
        else if (input.NewKey)
            action = input.LastScancode switch { 82 => SetupAction.Previous, 81 => SetupAction.Next, 44 or 40 or 88 => SetupAction.Activate, 41 => SetupAction.Back, _ => SetupAction.None };
        return new(action, hovered);
    }

    public void ApplyPicker(SetupMenuState state, SetupItemId item, SetupMenuInput action)
    {
        var count = PickerCount(item);
        if (action.HoveredIndex >= 0 && state.PickerSelected != action.HoveredIndex)
        { host.Sample(28); state.PickerSelected = action.HoveredIndex; }
        switch (action.Action)
        {
            case SetupAction.Previous: host.Sample(28); state.PickerSelected = state.PickerSelected == 0 ? count - 1 : state.PickerSelected - 1; break;
            case SetupAction.Next: host.Sample(28); state.PickerSelected = state.PickerSelected == count - 1 ? 0 : state.PickerSelected + 1; break;
            case SetupAction.Back: host.Sample(16); state.Picker = SetupItemId.None; break;
            case SetupAction.Activate:
                host.Sample(24);
                if (item == SetupItemId.Display && state.PickerSelected - 1 != host.FullscreenDisplay) host.ReinitializeFullscreen(state.PickerSelected - 1);
                else if (item == SetupItemId.Scaler && state.PickerSelected != host.Scaler)
                {
                    var old = host.Scaler;
                    if (!host.InitializeScaler(state.PickerSelected) && !host.InitializeScaler(old))
                        throw new InvalidOperationException("Unable to initialize the selected scaler or restore the previous scaler.");
                }
                else if (item == SetupItemId.ScalingMode) host.ScalingMode = (ScalingMode)state.PickerSelected;
                else if (item == SetupItemId.ShopContents) { host.ShopContents = state.PickerSelected; host.SaveConfiguration(); }
                state.Picker = SetupItemId.None;
                break;
        }
    }

    public void Activate(SetupMenuState state, SetupItemId item)
    {
        switch (item)
        {
            case SetupItemId.Done: host.Sample(8); state.Menu = state.Parents[(int)state.Menu]; break;
            case SetupItemId.Graphics or SetupItemId.Sound or SetupItemId.Gameplay:
                host.Sample(8);
                var target = item == SetupItemId.Graphics ? SetupMenuId.Graphics : item == SetupItemId.Sound ? SetupMenuId.Sound : SetupMenuId.Gameplay;
                state.Parents[(int)target] = state.Menu; state.Menu = target; state.Selected[(int)target] = 0; break;
            case SetupItemId.Jukebox: host.Sample(8); host.FadeBlack(); host.Jukebox(); state.Restart = true; break;
            case SetupItemId.Destruct: host.Sample(8); host.FadeBlack(); host.Destruct(); state.Restart = true; break;
            case SetupItemId.Display or SetupItemId.Scaler or SetupItemId.ScalingMode:
                host.Sample(24); state.Picker = item;
                state.PickerSelected = item == SetupItemId.Display ? host.FullscreenDisplay + 1 : item == SetupItemId.Scaler ? host.Scaler : (int)host.ScalingMode; break;
            case SetupItemId.ShopContents:
                host.Sample(24); state.Picker = item; state.PickerSelected = host.ShopContents is >= 0 and < 3 ? host.ShopContents : 0; break;
            case SetupItemId.MusicVolume:
                host.Sample(24); host.MusicDisabled = !host.MusicDisabled; if (!host.MusicDisabled) host.RestartSong(); break;
            case SetupItemId.SoundVolume: host.SamplesDisabled = !host.SamplesDisabled; host.Sample(24); break;
        }
    }

    public void ApplyMenu(SetupMenuState state, SetupMenuInput action)
    {
        var index = (int)state.Menu; var items = Menus[index].Items;
        if (action.HoveredIndex >= 0 && state.Selected[index] != action.HoveredIndex) { host.Sample(28); state.Selected[index] = action.HoveredIndex; }
        switch (action.Action)
        {
            case SetupAction.Previous: host.Sample(28); state.Selected[index] = state.Selected[index] == 0 ? items.Length - 1 : state.Selected[index] - 1; break;
            case SetupAction.Next: host.Sample(28); state.Selected[index] = state.Selected[index] == items.Length - 1 ? 0 : state.Selected[index] + 1; break;
            case SetupAction.Back: host.Sample(16); state.Menu = state.Parents[index]; break;
            case SetupAction.Activate: Activate(state, items[state.Selected[index]].Id); break;
            case SetupAction.SetVolume:
                if (action.Value >= 0) { host.Sample(28); config.MusicVolume = (ushort)Math.Clamp(action.Value, 0, 255); }
                else { config.EffectsVolume = (ushort)Math.Clamp(-(long)action.Value - 1, 0, 255); host.Sample(28); }
                host.Volume(config.MusicVolume, config.EffectsVolume); break;
            case SetupAction.AdjustMusicVolume: host.Sample(28); ChangeVolume(action.Value, 0); break;
            case SetupAction.AdjustSoundVolume: ChangeVolume(0, action.Value); host.Sample(28); break;
        }
        if (state.Menu == SetupMenuId.None) host.FadeBlack();
    }

    private void ChangeVolume(int musicChange, int effectsChange)
    {
        var music = config.MusicVolume; var effects = config.EffectsVolume;
        DelayTimer.ChangeVolume(ref music, musicChange, ref effects, effectsChange, () => host.Sample(23),
            (_, _) => { config.MusicVolume = music; config.EffectsVolume = effects; host.Volume(music, effects); });
    }

    public void Run(CancellationToken cancellation = default)
    {
        host.EnsureCursor(); var state = new SetupMenuState();
        while (true)
        {
            cancellation.ThrowIfCancellationRequested();
            if (state.Restart) host.LoadBackground();
            var y = Draw(state);
            if (state.Restart) { host.FadeIn(); state.Restart = false; }
            input.ServiceEvents(true); host.PresentCursor();
            var moved = WaitInput(host.FullscreenDisplay, cancellation);
            if (state.Picker == SetupItemId.None)
            {
                ApplyMenu(state, CollectMenuInput(state, moved));
                if (state.Menu == SetupMenuId.None) return;
            }
            else ApplyPicker(state, Menus[(int)state.Menu].Items[state.Selected[(int)state.Menu]].Id,
                CollectPickerInput(Menus[(int)state.Menu].Items[state.Selected[(int)state.Menu]].Id, y, moved));
        }
    }
}

public sealed class SetupMenuHost(SdlVideoDevice video, IndexedSurface background, FontRenderer font,
    SpriteSheet sprites, MouseCursor cursor, ReadOnlyMemory<PaletteColor> colors, DelayTimer timer,
    AudioMixer mixer, GameConfiguration config, Action loadCursor, Action loadPicture, Action pushJoysticks,
    Action<byte> sample, Action restartSong, Action saveConfiguration, Action jukebox, Action destruct) : ISetupMenuHost
{
    public int FullscreenDisplay => video.FullscreenDisplay;
    public int DisplayCount
    {
        get
        {
            var count = SdlNative.GetNumVideoDisplays();
            return count >= 0 ? count : throw SdlNative.Error("Failed to enumerate SDL displays for the setup menu");
        }
    }
    public int Scaler => Enumerable.Range(0, VideoScaler.Scalers.Count).First(i => VideoScaler.Scalers[i].Name == video.ScalerName);
    public ScalingMode ScalingMode { get => video.Layout.Mode; set { video.Layout.Mode = value; config.Layout.Mode = value; } }
    public int ShopContents { get => config.ShopContent; set => config.ShopContent = value; }
    public bool MusicDisabled { get => mixer.MusicDisabled; set => mixer.MusicDisabled = value; }
    public bool SamplesDisabled { get => mixer.SamplesDisabled; set => mixer.SamplesDisabled = value; }
    public void EnsureCursor() { if (sprites.Size == 0) loadCursor(); }
    public void LoadBackground() { loadPicture(); background.FillRectangleSize(0, 192, 320, 8, 0); }
    public void RestoreBackground()
    {
        var primary = video.Primary;
        if (primary.Width != background.Width || primary.Height != background.Height || primary.Pitch != background.Pitch)
            throw new InvalidDataException("Setup menu requires identical saved and primary surface geometry.");
        background.Pixels.Span.CopyTo(primary.Pixels.Span);
    }
    public void Label(int x, int y, string text, Font selected, FontAlignment alignment, sbyte brightness) =>
        font.DrawShadow(video.Primary, x, y, Encoding.Latin1.GetBytes(text), selected, alignment, 15, brightness, false, 2);
    public void Gauge(int y, byte color, int amount) =>
        video.Primary.ShadowedGauge(180, checked((ushort)y), 1, color, checked((ushort)amount), 2, 10);
    public void Rectangle(int x1, int y1, int x2, int y2, byte color) => video.Primary.Rectangle(x1, y1, x2, y2, color);
    public void Fill(int x, int y, int width, int height, byte color) => video.Primary.FillRectangleSize(x, y, width, height, color);
    public void Description(string text) => font.ShadeText(video.Primary, 45, 190, Encoding.Latin1.GetBytes(text), 15, 4, TextShade.Partial);
    public void FadeIn()
    {
        cursor.Cursor = 0;
        video.Palette.FadePalette(colors.Span, 10, 0, 255, () => timer.SetDelay(1), video.Present, timer.Wait);
    }
    public void FadeBlack() => video.Palette.FadeBlack(10, () => timer.SetDelay(1), video.Present, timer.Wait);
    public void PresentCursor() { cursor.Start(); video.Present(); cursor.Replace(); }
    public void PushJoysticks() => pushJoysticks();
    public void Sample(byte number) => sample(number);
    public void Volume(ushort music, ushort effects) => mixer.SetVolume(unchecked((byte)music), unchecked((byte)effects));
    public void RestartSong() => restartSong();
    public void ReinitializeFullscreen(int display) { video.ReinitializeFullscreen(display); config.FullscreenDisplay = video.FullscreenDisplay; }
    public bool InitializeScaler(int scaler)
    {
        var name = VideoScaler.Scalers[scaler].Name;
        if (!video.SetScalerByName(name)) return false;
        config.Scaler.SetByName(name);
        return true;
    }
    public void SaveConfiguration() => saveConfiguration();
    public void Jukebox() => jukebox();
    public void Destruct() => destruct();
}
