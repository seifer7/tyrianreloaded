/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface IWarningTextHost
{
    bool EscapePressed { get; }
    bool HaltGame { get; }
    bool WarningEnabled { get; set; }
    void Draw(ushort x, ushort y, ReadOnlyMemory<byte> text, CancellationToken cancellation);
    int Center(ReadOnlyMemory<byte> text);
    void Halt(int code);
    void UpdateWarning();
    void SetDelay(int frames);
    void KeepAlive();
    void Wait();
    bool AnyButton();
}

public interface IWarningTextPresenter
{
    void Run(IReadOnlyList<ReadOnlyMemory<byte>> lines, CancellationToken cancellation = default);
}

public sealed class WarningTextDisplay(TextGlow glow, HelpTextData text, IWarningTextHost host) : IWarningTextPresenter
{
    public void Run(IReadOnlyList<ReadOnlyMemory<byte>> lines, CancellationToken cancellation = default)
    {
        if (lines.Count > 10) throw new InvalidDataException("Warning text exceeds the native ten-line buffer.");
        foreach (var line in lines)
        {
            var end = line.Span.IndexOf((byte)0);
            if ((end < 0 ? line.Length : end) > 60)
                throw new InvalidDataException("Warning text exceeds the native 60-character line buffer.");
        }
        cancellation.ThrowIfCancellationRequested();
        ushort y = glow.WarningRed ? (ushort)2 : (ushort)55;
        foreach (var line in lines)
        {
            cancellation.ThrowIfCancellationRequested();
            if (host.EscapePressed) continue;
            host.Draw(10, y, line, cancellation);
            if (host.HaltGame)
            {
                host.Halt(5);
                throw new InvalidOperationException("Game halt unexpectedly returned.");
            }
            y += 10;
        }
        var wasAnimated = glow.FrameCount != 0;
        if (wasAnimated) glow.FrameCount = 6;
        glow.Font = Font.Small;
        var prompt = text.MiscText[4];
        host.Draw(unchecked((ushort)host.Center(prompt)), glow.WarningRed ? (ushort)118 : (ushort)184, prompt, cancellation);
        do
        {
            cancellation.ThrowIfCancellationRequested();
            if (host.WarningEnabled) host.UpdateWarning();
            host.SetDelay(1);
            host.KeepAlive();
            host.Wait();
        } while (!(host.AnyButton() || (glow.FrameCount == 0 && wasAnimated) || host.EscapePressed));
        host.WarningEnabled = false;
    }

    // The native synchronization function is entirely disabled with #if 0.
    public static void Synchronize() { }
}

public sealed class WarningTextHost(CharacterGlow characters, CharacterGlowHost characterHost,
    InputState input, DelayTimer timer, FontRenderer font, WarningBars warning,
    Func<bool> haltGame, Action<int> halt, Action keepAlive) : IWarningTextHost
{
    public bool EscapePressed => input.EscapePressed;
    public bool HaltGame => haltGame();
    public bool WarningEnabled { get => characterHost.WarningEnabled; set => characterHost.WarningEnabled = value; }
    public void Draw(ushort x, ushort y, ReadOnlyMemory<byte> text, CancellationToken cancellation)
        => characters.Draw(x, y, text, cancellation);
    public int Center(ReadOnlyMemory<byte> text) => font.Center(text.Span, Font.Small);
    public void Halt(int code) => halt(code);
    public void UpdateWarning() => warning.Update();
    public void SetDelay(int frames) => timer.SetDelay(frames);
    public void KeepAlive() => keepAlive();
    public void Wait() => timer.Wait();
    public bool AnyButton() => input.AnyButton();
}
