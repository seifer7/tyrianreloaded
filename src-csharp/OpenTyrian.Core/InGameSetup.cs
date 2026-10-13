/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class InGameSetupAction
{
    public bool Activate { get; set; }
    public bool Decrease { get; set; }
    public bool Increase { get; set; }
    public bool Close { get; set; }
    public bool VolumeChanged { get; set; }
    public int VolumeItem { get; set; }
    public int VolumeValue { get; set; }
}

public readonly record struct SetupInput(bool NewKey, bool NewMouse, int Scancode, int MouseX, int MouseY,
    int Button, int ClickX, int ClickY);

public interface IInGameSetupHost
{
    void EnsureCursor();
    void Shade(int left, int top, int right, int bottom);
    void Capture();
    void Restore();
    void ResetCursor();
    void Label(int x, int y, ReadOnlyMemory<byte> text, int brightness);
    void Gauge(int x, int y, int color, int amount);
    void Help(ReadOnlyMemory<byte> text);
    void ServiceEvents(bool clear);
    void MouseStart();
    void Present();
    void MouseReplace();
    void Delay(uint milliseconds);
    void PushJoysticks();
    void KeepAlive();
    SetupInput Input { get; }
    bool MusicDisabled { get; set; }
    bool SamplesDisabled { get; set; }
    void Sample(byte number);
    void Volume(ushort music, ushort effects);
    void InitializeProcessor();
    void SetSpeed();
    void Halt();
}

public sealed class InGameSetup(GameRuntimeSettings settings, DosConfiguration config, HelpTextData text, IInGameSetupHost host)
{
    private static readonly int[] HelpIndexes = [14, 14, 27, 28, 25, 26];
    public bool ConstantPlay { get; set; }
    public bool NetworkGame { get; set; }
    public bool HaltGame { get; set; }
    public bool PlayerEndLevel { get; set; }
    public int Selected { get; set; }

    public bool Run(CancellationToken cancellation = default)
    {
        var result = false; var restart = true; var done = false;
        Selected = 0; host.EnsureCursor();
        while (!done)
        {
            cancellation.ThrowIfCancellationRequested();
            Draw(ref restart);
            var moved = Wait(cancellation);
            var action = new InGameSetupAction();
            ReadMouse(moved, action); ReadKeyboard(action);
            Apply(action, ref done, ref result);
        }
        return result;
    }

    public void Draw(ref bool restart)
    {
        ValidateSelected();
        if (settings.ProcessorType is < 1 or > 6 || settings.GameSpeed is < 1 or > 5)
            throw new InvalidDataException("In-game setup requires valid detail and speed settings.");
        if (restart)
        {
            host.Shade(3, 13, 217, 137); host.Shade(5, 15, 215, 135);
            host.Shade(3, 143, 257, 157); host.Shade(5, 145, 255, 155);
            host.Capture(); host.ResetCursor(); restart = false;
        }
        host.Restore();
        for (var i = 0; i < 6; i++)
        {
            var y = 20 + i * 20;
            var brightness = -4 + (i == Selected ? 2 : 0);
            host.Label(10, y, text.InGameText[i], brightness);
            switch (i)
            {
                case 0: host.Gauge(120, y, host.MusicDisabled ? 12 : 16, (config.MusicVolume + 6) / 12); break;
                case 1: host.Gauge(120, y, host.SamplesDisabled ? 12 : 16, (config.EffectsVolume + 6) / 12); break;
                case 2: host.Label(120, y, text.DetailLevels[settings.ProcessorType - 1], brightness); break;
                case 3: host.Label(120, y, text.GameSpeeds[settings.GameSpeed - 1], brightness); break;
            }
        }
        host.Help(text.MainMenuHelp[HelpIndexes[Selected]]);
    }

    public bool Wait(CancellationToken cancellation = default)
    {
        host.ServiceEvents(true); host.MouseStart(); host.Present(); host.MouseReplace();
        bool moved;
        do
        {
            cancellation.ThrowIfCancellationRequested();
            host.Delay(16);
            var x = unchecked((ushort)host.Input.MouseX); var y = unchecked((ushort)host.Input.MouseY);
            host.PushJoysticks(); host.ServiceEvents(false); host.KeepAlive();
            moved = host.Input.MouseX != x || host.Input.MouseY != y;
        } while (!host.Input.NewKey && !host.Input.NewMouse && !moved);
        return moved;
    }

    public void ReadMouse(bool moved, InGameSetupAction action)
    {
        var input = host.Input;
        if (!input.NewMouse && !moved || input.MouseX < 10 || input.MouseX >= 210) return;
        for (var i = 0; i < 6; i++)
        {
            var y = 20 + i * 20;
            if (input.MouseY < y || input.MouseY >= y + 13) continue;
            if (Selected != i) { host.Sample(28); Selected = i; }
            if (!input.NewMouse || input.Button != 1 || input.ClickX < 10 || input.ClickX >= 210 ||
                input.ClickY < y || input.ClickY >= y + 13) break;
            if (input.ClickX < 120) action.Activate = true;
            else
            {
                switch (i)
                {
                    case 0 or 1:
                        action.VolumeChanged = true; action.VolumeItem = i;
                        action.VolumeValue = Math.Clamp((input.ClickX - 120) * 255 / 82, 0, 255);
                        break;
                    case 2 or 3: action.Increase = true; break;
                }
            }
            break;
        }
    }

    public void ReadKeyboard(InGameSetupAction action)
    {
        var input = host.Input;
        if (input.NewMouse)
        {
            if (input.Button == 3) { host.Sample(16); action.Close = true; }
            return;
        }
        if (!input.NewKey) return;
        switch (input.Scancode)
        {
            case 82: host.Sample(28); Selected = Selected == 0 ? 5 : Selected - 1; break;
            case 81: host.Sample(28); Selected = Selected == 5 ? 0 : Selected + 1; break;
            case 80: action.Decrease = true; break;
            case 79: action.Increase = true; break;
            case 44 or 40 or 88: action.Activate = true; break;
            case 41: host.Sample(16); action.Close = true; break;
            case 26:
                if (Selected == 2) { settings.ProcessorType = 6; host.InitializeProcessor(); }
                break;
        }
    }

    public void Adjust(int direction)
    {
        switch (Selected)
        {
            case 0: host.Sample(28); ChangeVolume(direction * 12, 0); break;
            case 1: ChangeVolume(0, direction * 12); host.Sample(28); break;
            case 2:
                host.Sample(28);
                settings.ProcessorType = (byte)(direction < 0 ? settings.ProcessorType > 1 ? settings.ProcessorType - 1 : 4
                    : settings.ProcessorType < 4 ? settings.ProcessorType + 1 : 1);
                host.InitializeProcessor(); host.SetSpeed(); break;
            case 3:
                host.Sample(28);
                settings.GameSpeed = (byte)(direction < 0 ? settings.GameSpeed > 1 ? settings.GameSpeed - 1 : 5
                    : settings.GameSpeed < 5 ? settings.GameSpeed + 1 : 1);
                host.InitializeProcessor(); host.SetSpeed(); break;
        }
    }

    private void ChangeVolume(int musicChange, int effectsChange)
    {
        var music = config.MusicVolume; var effects = config.EffectsVolume;
        DelayTimer.ChangeVolume(ref music, musicChange, ref effects, effectsChange, () => host.Sample(23),
            (_, _) => { config.MusicVolume = music; config.EffectsVolume = effects; host.Volume(music, effects); });
    }

    public void Apply(InGameSetupAction action, ref bool done, ref bool result)
    {
        ValidateSelected();
        if (action.VolumeChanged && action.VolumeItem == 0)
        {
            host.Sample(28); config.MusicVolume = unchecked((ushort)action.VolumeValue);
            host.Volume(config.MusicVolume, config.EffectsVolume);
        }
        else if (action.VolumeChanged && action.VolumeItem == 1)
        {
            config.EffectsVolume = unchecked((ushort)action.VolumeValue);
            host.Volume(config.MusicVolume, config.EffectsVolume); host.Sample(28);
        }
        if (action.Activate)
        {
            switch (Selected)
            {
                case 0: host.Sample(8); host.MusicDisabled = !host.MusicDisabled; break;
                case 1: host.SamplesDisabled = !host.SamplesDisabled; host.Sample(8); break;
                case 4: host.Sample(8); done = true; break;
                case 5:
                    host.Sample(8);
                    if (ConstantPlay) host.Halt();
                    if (NetworkGame) HaltGame = PlayerEndLevel = true;
                    result = done = true; break;
            }
        }
        else if (action.Decrease) Adjust(-1);
        else if (action.Increase) Adjust(1);
        if (action.Close) done = true;
    }

    private void ValidateSelected()
    {
        if (Selected is < 0 or > 5) throw new InvalidOperationException("In-game setup selection must be between zero and five.");
    }
}

public sealed class InGameSetupHost(IndexedSurface primary, IndexedSurface saved, FontRenderer font,
    InputState input, MouseCursor cursor, SpriteSheet cursorSheet, AudioMixer mixer, GameRuntimeSettings settings,
    DosConfiguration config, DelayTimer timer, Action loadCursor, Action present, Action pushJoysticks,
    Action keepAlive, Action<byte> sample, Action halt) : IInGameSetupHost
{
    public void EnsureCursor() { if (cursorSheet.Size == 0) loadCursor(); }
    public void Shade(int left, int top, int right, int bottom) => primary.ShadeRectangle(left, top, right, bottom);
    public void Capture() => Copy(primary, saved);
    public void Restore() => Copy(saved, primary);
    private static void Copy(IndexedSurface from, IndexedSurface to)
    {
        if (from.Width != to.Width || from.Height != to.Height || from.Pitch != to.Pitch)
            throw new InvalidOperationException("Setup backup surfaces must have identical native geometry.");
        from.Pixels.Span.CopyTo(to.Pixels.Span);
    }
    public void ResetCursor() => cursor.Cursor = 0;
    public void Label(int x, int y, ReadOnlyMemory<byte> text, int brightness)
        => font.DrawShadow(primary, x, y, text.Span, Font.Normal, FontAlignment.Left, 15, unchecked((sbyte)brightness), false, 2);
    public void Gauge(int x, int y, int color, int amount)
        => primary.ShadowedGauge(checked((ushort)x), checked((ushort)y), 1, checked((ushort)color), checked((ushort)amount), 3, 13);
    public void Help(ReadOnlyMemory<byte> text) => font.OutTextAdjust(primary, 10, 147, text.Span, 14, 6, Font.Small, true);
    public void ServiceEvents(bool clear) => input.ServiceEvents(clear);
    public void MouseStart() => cursor.Start();
    public void Present() => present();
    public void MouseReplace() => cursor.Replace();
    public void Delay(uint milliseconds) => input.Delay(milliseconds);
    public void PushJoysticks() => pushJoysticks();
    public void KeepAlive() => keepAlive();
    public SetupInput Input => new(input.NewKey, input.NewMouse, input.LastScancode, input.MouseX, input.MouseY,
        input.LastMouseButton, input.LastMouseX, input.LastMouseY);
    public bool MusicDisabled { get => mixer.MusicDisabled; set => mixer.MusicDisabled = value; }
    public bool SamplesDisabled { get => mixer.SamplesDisabled; set => mixer.SamplesDisabled = value; }
    public void Sample(byte number) => sample(number);
    public void Volume(ushort music, ushort effects) => mixer.SetVolume(unchecked((byte)music), unchecked((byte)effects));
    public void InitializeProcessor()
    {
        settings.InitializeProcessor();
        config.ProcessorType = settings.ProcessorType; config.GameSpeed = settings.GameSpeed;
    }
    public void SetSpeed() => settings.SetGameSpeed(timer);
    public void Halt() => halt();
}
