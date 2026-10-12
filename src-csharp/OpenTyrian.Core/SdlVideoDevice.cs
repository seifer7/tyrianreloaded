/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

public sealed class SdlVideoDevice : IDisposable
{
    private const uint VideoSubsystem = 0x20, Rgb888 = 0x16161804;
    private readonly TextWriter _warnings;
    private readonly VideoScaler _scaler = new();
    private nint _window, _renderer, _texture;
    private uint[] _scaled = [];
    private bool _initialized, _disposed;
    public IndexedSurface Primary { get; }
    public IndexedSurface Secondary { get; }
    public IndexedSurface GameScreen { get; }
    public PaletteSystem Palette { get; } = new(c => ((uint)c.Red << 16) | ((uint)c.Green << 8) | c.Blue);
    public VideoLayout Layout { get; } = new();
    public int FullscreenDisplay { get; private set; }
    public string ScalerName => VideoScaler.Scalers[_scaler.SelectedIndex].Name;

    public SdlVideoDevice(TextWriter warnings, int fullscreenDisplay = 0, string scaler = "None")
    {
        ArgumentNullException.ThrowIfNull(warnings);
        _warnings = warnings;
        SelectScaler(scaler);
        Primary = new(320, 200, 320, warnings);
        Secondary = new(320, 200, 320, warnings);
        GameScreen = new(320, 200, 320, warnings);
        if (SdlNative.InitSubSystem(VideoSubsystem) != 0) throw SdlNative.Error("Failed to initialize SDL video");
        _initialized = true;
        try
        {
            _window = SdlNative.CreateWindow("OpenTyrian", 0x2FFF0000, 0x2FFF0000, 320, 200, 0x20 | 8);
            if (_window == 0) throw SdlNative.Error("Failed to create SDL window");
            ReinitializeFullscreen(fullscreenDisplay);
            _renderer = SdlNative.CreateRenderer(_window, -1, 0);
            if (_renderer == 0) throw SdlNative.Error("Failed to create SDL renderer");
            CreateTexture();
            if (FullscreenDisplay == -1) ResizeToScaler();
            SdlNative.ShowWindow(_window);
            ClearRenderer();
            SdlNative.RenderPresent(_renderer);
        }
        catch
        {
            Release();
            throw;
        }
    }

    public (int Width, int Height) WindowSize
    {
        get { EnsureOpen(); SdlNative.GetWindowSize(_window, out var width, out var height); return (width, height); }
    }

    public int DisplayIndex
    {
        get
        {
            EnsureOpen();
            var index = SdlNative.GetWindowDisplayIndex(_window);
            return index >= 0 ? index : throw SdlNative.Error("Failed to locate SDL window display");
        }
    }

    public void ReinitializeFullscreen(int display)
    {
        EnsureOpen();
        var count = SdlNative.GetNumVideoDisplays();
        if (count <= 0) throw SdlNative.Error("Failed to enumerate SDL video displays");
        if (display < -1) throw new ArgumentOutOfRangeException(nameof(display));
        if (display >= count) display = 0;
        if (SdlNative.SetWindowFullscreen(_window, 0) != 0) throw SdlNative.Error("Failed to leave SDL fullscreen");
        ResizeToScaler(false);
        CenterInDisplay(display == -1 ? DisplayIndex : display);
        if (display != -1 && SdlNative.SetWindowFullscreen(_window, 0x1001) != 0)
        {
            _warnings.WriteLine(SdlNative.Error("Failed to enter SDL fullscreen; returning to windowed mode").Message);
            ReinitializeFullscreen(-1);
            return;
        }
        FullscreenDisplay = display;
    }

    public void ToggleFullscreen() => ReinitializeFullscreen(FullscreenDisplay == -1 ? DisplayIndex : -1);

    public void SetWindowSize(int width, int height)
    {
        EnsureOpen();
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        SdlNative.SetWindowSize(_window, width, height);
    }

    public void WindowResized()
    {
        var (width, height) = WindowSize;
        var factor = VideoScaler.Scalers[_scaler.SelectedIndex].Factor;
        if (width < 320 * factor || height < 200 * factor)
            SdlNative.SetWindowSize(_window, Math.Max(width, 320 * factor), Math.Max(height, 200 * factor));
    }

    public bool SetScalerByName(string name)
    {
        EnsureOpen();
        if (!VideoScaler.Scalers.Any(s => s.Name == name)) return false;
        var previous = ScalerName;
        SelectScaler(name);
        try { CreateTexture(); }
        catch { _scaler.SetByName(previous); throw; }
        if (FullscreenDisplay == -1) ResizeToScaler();
        return true;
    }

    private void SelectScaler(string name)
    {
        if (!VideoScaler.Scalers.Any(s => s.Name == name)) throw new ArgumentException("Unknown video scaler.", nameof(name));
        _scaler.SetByName(name);
    }

    private void CreateTexture()
    {
        var factor = VideoScaler.Scalers[_scaler.SelectedIndex].Factor;
        var pixels = new uint[checked(320 * 200 * factor * factor)];
        var texture = SdlNative.CreateTexture(_renderer, Rgb888, 1, 320 * factor, 200 * factor);
        if (texture == 0) throw SdlNative.Error("Failed to create SDL scaler texture");
        if (_texture != 0) SdlNative.DestroyTexture(_texture);
        _texture = texture;
        _scaled = pixels;
    }

    private void ResizeToScaler(bool center = true)
    {
        var factor = VideoScaler.Scalers[_scaler.SelectedIndex].Factor;
        SdlNative.SetWindowSize(_window, 320 * factor, 200 * factor);
        if (center) CenterInDisplay(DisplayIndex);
    }

    private void CenterInDisplay(int display)
    {
        if (SdlNative.GetDisplayBounds(display, out var bounds) != 0) throw SdlNative.Error("Failed to read SDL display bounds");
        var (width, height) = WindowSize;
        SdlNative.SetWindowPosition(_window, bounds.X + (bounds.Width - width) / 2, bounds.Y + (bounds.Height - height) / 2);
    }

    public static void Clear(IndexedSurface screen) => screen.Pixels.Span.Clear();

    public void Present() => Present(Primary);

    public void Present(IndexedSurface screen)
    {
        EnsureOpen();
        if (screen.Width != 320 || screen.Height != 200) throw new ArgumentException("Game video requires a 320x200 indexed surface.", nameof(screen));
        var factor = VideoScaler.Scalers[_scaler.SelectedIndex].Factor;
        var algorithm = _scaler.SelectedIndex switch { 2 => ScaleAlgorithm.Scale2x, 5 => ScaleAlgorithm.Scale3x, _ => ScaleAlgorithm.Nearest };
        if (ScalerName.StartsWith("hq", StringComparison.Ordinal))
            HqVideoScaler.Scale(screen, _scaled, 320 * factor, Palette.Rgb.Span, Palette.Yuv.Span, factor);
        else
            VideoScaler.Scale32(screen, _scaled, 320 * factor, Palette.Rgb.Span, algorithm, factor);
        var pinned = GCHandle.Alloc(_scaled, GCHandleType.Pinned);
        try
        {
            if (SdlNative.UpdateTexture(_texture, 0, pinned.AddrOfPinnedObject(), 320 * factor * sizeof(uint)) != 0)
                throw SdlNative.Error("Failed to upload SDL scaler pixels");
        }
        finally { pinned.Free(); }
        var (width, height) = WindowSize;
        var output = Layout.Calculate(width, height, 320 * factor, 200 * factor);
        var rectangle = new SdlNative.Rectangle(output);
        ClearRenderer();
        if (SdlNative.RenderCopy(_renderer, _texture, 0, ref rectangle) != 0) throw SdlNative.Error("Failed to render SDL texture");
        SdlNative.RenderPresent(_renderer);
        Layout.Output = output;
    }

    public uint[] ReadPixels()
    {
        var (width, height) = WindowSize;
        var pixels = new uint[checked(width * height)];
        var pinned = GCHandle.Alloc(pixels, GCHandleType.Pinned);
        try
        {
            if (SdlNative.RenderReadPixels(_renderer, 0, Rgb888, pinned.AddrOfPinnedObject(), checked(width * sizeof(uint))) != 0)
                throw SdlNative.Error("Failed to read SDL framebuffer");
        }
        finally { pinned.Free(); }
        return pixels;
    }

    private void ClearRenderer()
    {
        if (SdlNative.SetRenderDrawColor(_renderer, 0, 0, 0, 255) != 0 || SdlNative.RenderClear(_renderer) != 0)
            throw SdlNative.Error("Failed to clear SDL renderer");
    }

    private void EnsureOpen() => ObjectDisposedException.ThrowIf(_disposed, this);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Release();
        GC.SuppressFinalize(this);
    }

    private void Release()
    {
        if (_texture != 0) { SdlNative.DestroyTexture(_texture); _texture = 0; }
        if (_renderer != 0) { SdlNative.DestroyRenderer(_renderer); _renderer = 0; }
        if (_window != 0) { SdlNative.DestroyWindow(_window); _window = 0; }
        if (_initialized) { SdlNative.QuitSubSystem(VideoSubsystem); _initialized = false; }
    }
}
