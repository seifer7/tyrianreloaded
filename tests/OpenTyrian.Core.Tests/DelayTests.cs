using OpenTyrian.Core;

namespace OpenTyrian.Core.Tests;

internal static class DelayTests
{
    public static void Run()
    {
        var platform = new Platform();
        var timer = new DelayTimer(platform);
        timer.SetDelay(2);
        timer.SetDelay2(3);
        Check(timer.GetDelayTicks() == 28 && timer.GetDelayTicks2() == 43, "PIT delay period differs.");
        timer.Wait();
        Check(platform.Ticks == 28 && timer.GetDelayTicks() == 0 && timer.GetDelayTicks2() == 15, "Delay targets are not independent.");
        timer.SetSpeed(0x4300);
        timer.SetDelay(3);
        timer.ServiceWait();
        Check(platform.Ticks == 71, "Service wait did not advance time.");
        Check(platform.Delays.Skip(1).SequenceEqual(new uint[] { 10, 10, 10, 10, 3 }) && platform.Events == 6,
            "Service wait polling interval or final event differs.");
        timer.SetDelay(10);
        platform.NewKey = true;
        timer.WaitOrInput();
        Check(!platform.NewKey && platform.Joysticks == 1 && timer.GetDelayTicks() > 0, "Input did not interrupt or clear the new-key flag.");
        platform.MouseDown = true;
        timer.WaitOrInput();
        Check(platform.Joysticks == 2 && platform.Ticks == 71, "Mouse input did not interrupt.");
        platform.MouseDown = false;
        timer.SetSpeed(0);
        timer.SetDelay(100);
        timer.WaitOrInput();
        Check(timer.GetDelayTicks() == 0, "Zero-speed timer differs.");
        platform.Ticks = 100;
        timer.SetSpeed(1);
        timer.SetDelay(-1);
        Check(timer.GetDelayTicks() == 0, "Past target delay differs.");
        platform.Ticks = 16_777_217;
        timer.SetSpeed(0);
        timer.SetDelay(0);
        Check(timer.GetDelayTicks() == 0, "Native float precision of target differs.");
        platform.Ticks = uint.MaxValue;
        try
        {
            timer.SetDelay(1);
            throw new InvalidOperationException("Undefined out-of-range float target conversion accepted.");
        }
        catch (ArgumentOutOfRangeException)
        {
        }

        ushort music = 250, sample = 2;
        var events = new List<string>();
        DelayTimer.ChangeVolume(ref music, 12, ref sample, -12, () => events.Add("clink"),
            (first, second) => events.Add($"volume:{first}:{second}"));
        Check(music == 255 && sample == 0 && events.SequenceEqual(new[] { "clink", "clink", "volume:255:0" }), "Volume bounds/callback ordering differs.");
        events.Clear();
        DelayTimer.ChangeVolume(ref music, -12, ref sample, 12, () => events.Add("clink"),
            (first, second) => events.Add($"volume:{first}:{second}"));
        Check(music == 243 && sample == 12 && events.SequenceEqual(new[] { "volume:243:12" }), "Unclipped volume change differs.");
    }

    private sealed class Platform : IDelayPlatform
    {
        public uint Ticks { get; set; }
        public bool NewKey { get; set; }
        public bool MouseDown { get; set; }
        public bool JoystickDown { get; set; }
        public List<uint> Delays { get; } = [];
        public int Events { get; private set; }
        public int Joysticks { get; private set; }
        public void Delay(uint milliseconds)
        {
            Delays.Add(milliseconds);
            Ticks = unchecked(Ticks + milliseconds);
        }

        public void ServiceEvents(bool clearNew) => Events++;
        public void PollJoysticks() => Joysticks++;
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
