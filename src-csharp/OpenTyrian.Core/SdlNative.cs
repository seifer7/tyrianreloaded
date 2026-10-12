/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Reflection;
using System.Runtime.InteropServices;

namespace OpenTyrian.Core;

internal static class SdlNative
{
    internal const string Library = "OpenTyrian.SDL2";

    static SdlNative() => NativeLibrary.SetDllImportResolver(typeof(SdlNative).Assembly, Resolve);
    // SDL2_net shares this assembly's single import resolver.
    internal static void RegisterLibraries() { }

    private static nint Resolve(string name, Assembly assembly, DllImportSearchPath? searchPath)
    {
        var network = name == "OpenTyrian.SDL2_net";
        if (name != Library && !network) return 0;
        var variable = network ? "SDL2_NET_LIBRARY_PATH" : "SDL2_LIBRARY_PATH";
        var configured = Environment.GetEnvironmentVariable(variable);
        if (!string.IsNullOrEmpty(configured))
        {
            if (!Path.IsPathFullyQualified(configured))
                throw new InvalidOperationException($"{variable} must identify an absolute SDL library filename.");
            return NativeLibrary.Load(configured);
        }
        var filename = network
            ? OperatingSystem.IsWindows() ? "SDL2_net.dll" : OperatingSystem.IsMacOS() ? "libSDL2_net.dylib" : "libSDL2_net-2.0.so.0"
            : OperatingSystem.IsWindows() ? "SDL2.dll" : OperatingSystem.IsMacOS() ? "libSDL2.dylib" : "libSDL2-2.0.so.0";
        return NativeLibrary.Load(filename, assembly, searchPath);
    }

    internal static IOException Error(string operation) =>
        new($"{operation}: {Marshal.PtrToStringUTF8(GetError())}");

    [DllImport(Library, EntryPoint = "SDL_GetError", CallingConvention = CallingConvention.Cdecl)]
    private static extern nint GetError();

    [DllImport(Library, EntryPoint = "SDL_BuildAudioCVT", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int BuildAudioConverter(nint converter, ushort sourceFormat, byte sourceChannels,
        int sourceRate, ushort destinationFormat, byte destinationChannels, int destinationRate);

    [DllImport(Library, EntryPoint = "SDL_ConvertAudio", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int ConvertAudio(nint converter);

    [StructLayout(LayoutKind.Sequential)]
    internal struct AudioSpec
    {
        internal int Frequency;
        internal ushort Format;
        internal byte Channels, Silence;
        internal ushort Samples, Padding;
        internal uint Size;
        internal nint Callback, UserData;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate void AudioCallback(nint userData, nint stream, int length);

    [DllImport(Library, EntryPoint = "SDL_InitSubSystem", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int InitSubSystem(uint flags);

    [DllImport(Library, EntryPoint = "SDL_QuitSubSystem", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void QuitSubSystem(uint flags);

    [DllImport(Library, EntryPoint = "SDL_OpenAudioDevice", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint OpenAudioDevice(nint name, int capture, ref AudioSpec desired, out AudioSpec obtained, int allowedChanges);

    [DllImport(Library, EntryPoint = "SDL_PauseAudioDevice", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void PauseAudioDevice(uint device, int pause);

    [DllImport(Library, EntryPoint = "SDL_CloseAudioDevice", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void CloseAudioDevice(uint device);

    [StructLayout(LayoutKind.Sequential)]
    internal struct Rectangle
    {
        internal int X, Y, Width, Height;
        internal Rectangle(OutputRectangle rectangle)
        {
            X = rectangle.X; Y = rectangle.Y; Width = rectangle.Width; Height = rectangle.Height;
        }
    }

    [DllImport(Library, EntryPoint = "SDL_CreateWindow", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint CreateWindow([MarshalAs(UnmanagedType.LPUTF8Str)] string title,
        int x, int y, int width, int height, uint flags);
    [DllImport(Library, EntryPoint = "SDL_DestroyWindow", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DestroyWindow(nint window);
    [DllImport(Library, EntryPoint = "SDL_ShowWindow", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void ShowWindow(nint window);
    [DllImport(Library, EntryPoint = "SDL_GetWindowSize", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void GetWindowSize(nint window, out int width, out int height);
    [DllImport(Library, EntryPoint = "SDL_SetWindowSize", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetWindowSize(nint window, int width, int height);
    [DllImport(Library, EntryPoint = "SDL_SetWindowPosition", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void SetWindowPosition(nint window, int x, int y);
    [DllImport(Library, EntryPoint = "SDL_GetWindowDisplayIndex", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetWindowDisplayIndex(nint window);
    [DllImport(Library, EntryPoint = "SDL_GetNumVideoDisplays", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetNumVideoDisplays();
    [DllImport(Library, EntryPoint = "SDL_GetDisplayBounds", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetDisplayBounds(int display, out Rectangle bounds);
    [DllImport(Library, EntryPoint = "SDL_SetWindowFullscreen", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetWindowFullscreen(nint window, uint flags);
    [DllImport(Library, EntryPoint = "SDL_CreateRenderer", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint CreateRenderer(nint window, int index, uint flags);
    [DllImport(Library, EntryPoint = "SDL_DestroyRenderer", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DestroyRenderer(nint renderer);
    [DllImport(Library, EntryPoint = "SDL_CreateTexture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint CreateTexture(nint renderer, uint format, int access, int width, int height);
    [DllImport(Library, EntryPoint = "SDL_DestroyTexture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void DestroyTexture(nint texture);
    [DllImport(Library, EntryPoint = "SDL_UpdateTexture", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int UpdateTexture(nint texture, nint rectangle, nint pixels, int pitch);
    [DllImport(Library, EntryPoint = "SDL_SetRenderDrawColor", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetRenderDrawColor(nint renderer, byte red, byte green, byte blue, byte alpha);
    [DllImport(Library, EntryPoint = "SDL_RenderClear", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int RenderClear(nint renderer);
    [DllImport(Library, EntryPoint = "SDL_RenderCopy", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int RenderCopy(nint renderer, nint texture, nint source, ref Rectangle destination);
    [DllImport(Library, EntryPoint = "SDL_RenderPresent", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void RenderPresent(nint renderer);
    [DllImport(Library, EntryPoint = "SDL_RenderReadPixels", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int RenderReadPixels(nint renderer, nint rectangle, uint format, nint pixels, int pitch);

    [DllImport(Library, EntryPoint = "SDL_PollEvent", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int PollEvent(nint input);
    [DllImport(Library, EntryPoint = "SDL_PushEvent", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int PushEvent(nint input);
    [DllImport(Library, EntryPoint = "SDL_GetTicks", CallingConvention = CallingConvention.Cdecl)]
    internal static extern uint GetTicks();
    [DllImport(Library, EntryPoint = "SDL_Delay", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Delay(uint milliseconds);
    [DllImport(Library, EntryPoint = "SDL_ShowCursor", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int ShowCursor(int visible);
    [DllImport(Library, EntryPoint = "SDL_SetRelativeMouseMode", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetRelativeMouseMode(int enabled);
    [DllImport(Library, EntryPoint = "SDL_SetHint", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);
    [DllImport(Library, EntryPoint = "SDL_free", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void Free(nint memory);
    [DllImport(Library, EntryPoint = "SDL_GetModState", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetModState();
    [DllImport(Library, EntryPoint = "SDL_GetScancodeFromName", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int GetScancodeFromName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);
    [DllImport(Library, EntryPoint = "SDL_GetScancodeName", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint GetScancodeName(int scancode);

    [DllImport(Library, EntryPoint = "SDL_NumJoysticks", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int NumJoysticks();
    [DllImport(Library, EntryPoint = "SDL_JoystickEventState", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int JoystickEventState(int state);
    [DllImport(Library, EntryPoint = "SDL_JoystickOpen", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint JoystickOpen(int index);
    [DllImport(Library, EntryPoint = "SDL_JoystickClose", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void JoystickClose(nint joystick);
    [DllImport(Library, EntryPoint = "SDL_JoystickName", CallingConvention = CallingConvention.Cdecl)]
    internal static extern nint JoystickName(nint joystick);
    [DllImport(Library, EntryPoint = "SDL_JoystickNumAxes", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int JoystickNumAxes(nint joystick);
    [DllImport(Library, EntryPoint = "SDL_JoystickNumButtons", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int JoystickNumButtons(nint joystick);
    [DllImport(Library, EntryPoint = "SDL_JoystickNumHats", CallingConvention = CallingConvention.Cdecl)]
    internal static extern int JoystickNumHats(nint joystick);
    [DllImport(Library, EntryPoint = "SDL_JoystickGetAxis", CallingConvention = CallingConvention.Cdecl)]
    internal static extern short JoystickGetAxis(nint joystick, int index);
    [DllImport(Library, EntryPoint = "SDL_JoystickGetButton", CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte JoystickGetButton(nint joystick, int index);
    [DllImport(Library, EntryPoint = "SDL_JoystickGetHat", CallingConvention = CallingConvention.Cdecl)]
    internal static extern byte JoystickGetHat(nint joystick, int index);
    [DllImport(Library, EntryPoint = "SDL_JoystickUpdate", CallingConvention = CallingConvention.Cdecl)]
    internal static extern void JoystickUpdate();
}
