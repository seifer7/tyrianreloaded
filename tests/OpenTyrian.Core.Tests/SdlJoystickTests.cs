using System.Runtime.InteropServices;

namespace OpenTyrian.Core.Tests;

internal static class SdlJoystickTests
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Initialize(uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Shutdown(uint flags);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Attach(int type, int axes, int buttons, int hats);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Detach(int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate nint Open(int index);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void Close(nint handle);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetAxis(nint handle, int index, short value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetButton(nint handle, int index, byte value);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetHint([MarshalAs(UnmanagedType.LPUTF8Str)] string name, [MarshalAs(UnmanagedType.LPUTF8Str)] string value);

    internal static void Run(SdlVideoDevice video)
    {
        var path = Environment.GetEnvironmentVariable("SDL2_LIBRARY_PATH") ??
            (OperatingSystem.IsWindows() ? "SDL2.dll" : OperatingSystem.IsMacOS() ? "libSDL2.dylib" : "libSDL2-2.0.so.0");
        var library = NativeLibrary.Load(path);
        T Export<T>(string name) where T : Delegate => Marshal.GetDelegateForFunctionPointer<T>(NativeLibrary.GetExport(library, name));
        var init = Export<Initialize>("SDL_InitSubSystem"); var shutdown = Export<Shutdown>("SDL_QuitSubSystem");
        var attach = Export<Attach>("SDL_JoystickAttachVirtual"); var detach = Export<Detach>("SDL_JoystickDetachVirtual");
        var open = Export<Open>("SDL_JoystickOpen"); var close = Export<Close>("SDL_JoystickClose");
        var axis = Export<SetAxis>("SDL_JoystickSetVirtualAxis"); var button = Export<SetButton>("SDL_JoystickSetVirtualButton");
        var hat = Export<SetButton>("SDL_JoystickSetVirtualHat"); var hint = Export<SetHint>("SDL_SetHint");
        var initialized = false;
        var index = -1;
        nint handle = 0;
        try
        {
            Check(init(0x200) == 0, "Cannot initialize SDL virtual joystick test.");
            initialized = true;
            Check(hint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS", "1") != 0, "Cannot enable virtual joystick polling without window focus.");
            index = attach(0, 2, 6, 1);
            Check(index >= 0, "Could not attach synthetic SDL joystick.");
            handle = open(index);
            Check(handle != 0, "Could not open synthetic SDL joystick.");
            var config = new Configuration();
            using var output = new StringWriter();
            SdlInputPlatform? platform = null;
            using var session = new SdlJoysticks(config, output, output, e => platform!.PostEvent(e), () => false,
                () => throw new InvalidOperationException("Unexpected network polling in joystick fixture."));
            var synthetic = session.States[index];
            Check(synthetic.Device != null && synthetic.Device.AxisCount == 2 && synthetic.Device.ButtonCount == 6 &&
                synthetic.Device.HatCount == 1 && output.ToString().Contains("joystick detected:", StringComparison.Ordinal),
                "Real SDL joystick enumeration/capabilities were not retained.");
            using (platform = new SdlInputPlatform(video, session, () => throw new InvalidOperationException("Unexpected joystick quit.")))
            {
                var state = new InputState(platform, video.Layout);
                state.FlushEvents();
                Check(axis(handle, 0, 32767) == 0 && axis(handle, 1, -32768) == 0 && button(handle, 0, 1) == 0 &&
                    hat(handle, 0, 8) == 0, "Synthetic SDL input could not be set.");
                session.PollJoysticks();
                Check(session.JoystickDown && synthetic.Direction[0] && synthetic.Direction[1] && synthetic.Direction[3] &&
                    synthetic.Action[0] && synthetic.Confirm && synthetic.AnalogDirection[0] == 32768,
                    "Real SDL axes/buttons/hats did not reach managed joystick state.");
                Check(axis(handle, 0, 0) == 0 && axis(handle, 1, 0) == 0 && button(handle, 0, 0) == 0 &&
                    hat(handle, 0, 0) == 0, "Could not neutralize synthetic SDL joystick.");
                session.PollJoysticks();
                Check(button(handle, 0, 1) == 0, "Could not trigger synthetic SDL confirm.");
                platform.PushJoysticksAsKeyboard(); state.ServiceEvents(true);
                Check(state.NewKey && state.LastScancode == 40 && state.KeyDown, "Joystick menu key did not reach actual SDL input queue.");
                state.ServiceEvents(false);
                Check(!state.KeyDown, "Joystick SDL key-release ordering changed.");
                synthetic.Analog = true; synthetic.Sensitivity = 7; synthetic.Threshold = 3;
            }
            var device = synthetic.Device!;
            var name = device.Name;
            session.Dispose(); session.Dispose();
            Check(config.FindSection("joystick", name)?.GetOption("sensitivity")?.Value == "7",
                "SDL joystick teardown did not persist assignments/config.");
            Throws<ObjectDisposedException>(() => device.Axis(0));
            Throws<ObjectDisposedException>(session.PollJoysticks);
            using var reopened = new SdlJoysticks(config, output, output, _ => { }, () => false, () => { });
            Check(reopened.States[index].Analog && reopened.States[index].Sensitivity == 7 &&
                reopened.States[index].Threshold == 3, "Reopened SDL joystick did not load managed config.");
            using var ignored = new SdlJoysticks(config, output, output, _ => throw new InvalidOperationException(),
                () => false, () => { }, ignore: true);
            ignored.PollJoysticks();
            Check(ignored.States.Count == 0 && !ignored.JoystickDown, "Ignored joystick host touched actual devices.");
        }
        finally
        {
            if (handle != 0) close(handle);
            if (index >= 0) Check(detach(index) == 0, "SDL virtual joystick cleanup failed.");
            if (initialized) shutdown(0x200);
            NativeLibrary.Free(library);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
