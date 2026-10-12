/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GamePausePresentation(IndexedSurface primary, FontRenderer font,
    ReadOnlyMemory<byte> text, Action present)
{
    public void Draw()
    {
        font.DrawString(primary, 120, 90, text.Span, Font.Normal);
        present();
    }
}

public sealed class GamePause(InputState input, DelayTimer timer, Action draw, Action pushJoysticks,
    Action<byte, byte> setVolume)
{
    public void Run(byte musicVolume, byte effectsVolume, bool superPause, NetworkSession? network = null,
        CancellationToken cancellation = default)
    {
        input.SetRelative(false);
        try
        {
            if (!superPause) draw();
            setVolume((byte)(musicVolume / 2), effectsVolume);
            if (network != null)
            {
                network.Prepare(NetworkPacketType.GamePause);
                if (!network.Send(4)) throw new IOException("Network pause could not enter the acknowledged packet queue.");
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    input.ServiceEvents(false);
                    if (network.ControlPacket?.Type == NetworkPacketType.GamePause)
                    {
                        network.Update(); break;
                    }
                    network.Update(); network.Check(); input.Delay(16);
                }
            }
            input.WaitNoInput(false, false, true, cancellation);
            var done = false;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                timer.SetDelay(2);
                pushJoysticks();
                input.ServiceEvents(true);
                if ((input.NewKey && input.LastScancode is not (224 or 228 or 226 or 230)) ||
                    input.MousePosition().Button > 0)
                {
                    if (network != null)
                    {
                        network.Prepare(NetworkPacketType.Waiting);
                        if (!network.Send(4)) throw new IOException("Network resume could not enter the acknowledged packet queue.");
                    }
                    done = true;
                }
                if (network != null)
                {
                    network.Check();
                    if (network.ControlPacket?.Type == NetworkPacketType.Waiting)
                    {
                        network.Check(); done = true;
                    }
                }
                timer.Wait();
            } while (!done);
            if (network != null)
            {
                while (!network.IsSynchronized)
                {
                    cancellation.ThrowIfCancellationRequested();
                    input.ServiceEvents(false);
                    network.Check(); input.Delay(16);
                }
            }
        }
        finally
        {
            try { setVolume(musicVolume, effectsVolume); }
            finally { input.SetRelative(true); }
        }
    }
}
