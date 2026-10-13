/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameSetupWaiting(IndexedSurface primary, FontRenderer font, Action present)
{
    public void Draw()
    {
        primary.ShadeRectangle(3, 60, 257, 80);
        primary.ShadeRectangle(5, 62, 255, 78);
        font.DrawString(primary, 10, 65, "Other player in options menu."u8, Font.Normal);
        present();
    }
}

public sealed class GameSetupSession(InputState input, InGameSetup setup, LevelEvents level,
    NetworkRequests requests, Action waiting, Action present)
{
    public bool QuitRequested { get => level.QuitRequested; set => level.QuitRequested = value; }

    public void Run(NetworkSession? network = null, CancellationToken cancellation = default)
    {
        input.SetRelative(false);
        setup.HaltGame = false;
        setup.NetworkGame = network is not null;
        try
        {
            if (network is not null)
            {
                Send(NetworkPacketType.GameMenu);
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    input.ServiceEvents(false);
                    if (network.ControlPacket?.Type == NetworkPacketType.GameMenu) { network.Update(); break; }
                    network.Update(); network.Check(); input.Delay(16);
                }
            }
            if (requests.YourInGameMenu)
            {
                if (setup.Run(cancellation)) { level.ReallyEndLevel = true; setup.PlayerEndLevel = true; }
                QuitRequested = false;
                input.KeysActive[41] = 0;
                if (network is not null) Send(setup.PlayerEndLevel ? NetworkPacketType.GameQuit : NetworkPacketType.Waiting);
            }
            if (network is not null)
            {
                if (!requests.YourInGameMenu)
                {
                    waiting();
                    while (true)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        input.ServiceEvents(false); present();
                        if (network.ControlPacket?.Type == NetworkPacketType.Waiting) { network.Check(); break; }
                        if (network.ControlPacket?.Type == NetworkPacketType.GameQuit)
                        {
                            level.ReallyEndLevel = true; setup.PlayerEndLevel = true;
                            network.Check(); break;
                        }
                        network.Update(); network.Check(); input.Delay(16);
                    }
                }
                while (!network.IsSynchronized)
                {
                    cancellation.ThrowIfCancellationRequested();
                    input.ServiceEvents(false); network.Check(); input.Delay(16);
                }
            }
        }
        finally
        {
            requests.YourInGameMenu = false;
            input.SetRelative(true);
        }

        void Send(NetworkPacketType type)
        {
            network!.Prepare(type);
            if (!network.Send(4)) throw new IOException($"Network setup could not queue {type}.");
        }
    }
}
