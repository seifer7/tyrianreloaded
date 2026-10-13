/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public interface ICreditsHost
{
    void LoadPictures();
    int PictureCount { get; }
    (ushort Width, ushort Height) PictureSize(int index);
    Stream OpenText();
    void SetDelay(int ticks);
    void SetPictureDelay(int ticks);
    uint PictureDelay { get; }
    void PlaySong(uint song);
    void FadeSong();
    void Clear();
    void Present();
    void FadeIn(int steps);
    void Picture(int index, int x, int y, sbyte brightness);
    void Ship(int x, int y, uint graphic);
    int TextWidth(ReadOnlyMemory<byte> text);
    void Text(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness);
    void Fill(int left, int top, int right, int bottom);
    void KeepAlive();
    void Wait();
    bool AnyButton();
    void FadeBlack(int steps);
    void FreePictures();
}

public sealed class Credits(ItemDatabase items, ReadOnlyMemory<byte> episodeText, ICreditsHost host)
{
    public const int LineCount = 131;
    public const int LineLength = 65;

    public void Run(CancellationToken cancellation = default)
    {
        try
        {
            host.LoadPictures();
            if (host.PictureCount is < 1 or > SpriteTable.Capacity)
                throw new InvalidDataException("Credits require a nonempty picture table.");
            host.SetPictureDelay(1000);
            host.PlaySong(8);
            var lines = new byte[LineCount][];
            using (var stream = host.OpenText())
            {
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = new byte[LineLength + 1];
                    EncryptedText.ReadPascalString(stream, line);
                    lines[i] = line[..Array.IndexOf(line, (byte)0)];
                }
            }
            host.Clear(); host.Present(); host.FadeIn(2);
            byte picture = 0, fade = 0, ship = 0;
            sbyte fadeChange = 1, velocity = 0, acceleration = 0;
            short shipX = 0, shipWait = 0;
            const int maximumTicks = LineCount * 20 * 3;
            for (var ticks = 0; ticks < maximumTicks; ticks++)
            {
                cancellation.ThrowIfCancellationRequested();
                host.SetDelay(1); host.Clear();
                var size = host.PictureSize(picture);
                host.Picture(picture, 319 - size.Width, 100 - size.Height / 2, unchecked((sbyte)(fade - 15)));
                fade = unchecked((byte)(fade + fadeChange));
                if (fade == 0 && fadeChange == -1)
                {
                    fadeChange = 1;
                    picture++;
                    if (picture >= host.PictureCount) picture = 0;
                }
                if (fade == 15) fadeChange = 0;
                if (host.PictureDelay == 0) { fadeChange = -1; host.SetPictureDelay(900); }
                if (ticks % 200 == 0)
                {
                    ship = (byte)(MersenneTwister.NextUInt32() % 11 + 1);
                    shipWait = (short)(MersenneTwister.NextUInt32() % 80 + 10);
                    if (MersenneTwister.NextUInt32() % 2 == 1)
                    { shipX = 1; velocity = 0; acceleration = 1; }
                    else { shipX = 900; velocity = 0; acceleration = -1; }
                }
                shipWait--;
                if (shipWait == 0)
                {
                    if (shipX is 1 or 900) velocity = 0;
                    acceleration = unchecked((sbyte)-acceleration);
                    shipWait = (short)(MersenneTwister.NextUInt32() % 40 + 15);
                }
                velocity = unchecked((sbyte)(velocity + acceleration));
                shipX = unchecked((short)(shipX + velocity));
                if (shipX < 1) { shipX = 1; shipWait = 1; }
                if (shipX > 900) { shipX = 900; shipWait = 1; }
                if (450 + velocity * velocity > 900)
                {
                    if (acceleration < 0 && velocity < 0 || acceleration > 0 && velocity > 0) shipWait = 1;
                }
                uint graphic = items.Ships[ship].ShipGraphic;
                if (velocity < -10) graphic = unchecked(graphic - (velocity < -20 ? 4u : 2u));
                else if (velocity > 10) graphic += velocity > 20 ? 4u : 2u;
                host.Ship(shipX / 40, 184 - ticks % 200, graphic);
                var bottom = ticks / 3 / 20;
                var y = 20 - ticks / 3 % 20;
                for (var line = bottom - 10; line < bottom; line++, y += 20)
                {
                    if (line < 0 || line >= LineCount || lines[line].Length == 0 ||
                        lines[line].AsSpan().SequenceEqual("."u8)) continue;
                    var color = unchecked((byte)(lines[line][0] - 65));
                    var text = lines[line].AsMemory(1);
                    var x = 110 - host.TextWidth(text) / 2;
                    host.Text(x + Math.Abs(y / 18 % 4 - 2) - 1, y - 1, text, color, -8);
                    host.Text(x, y, text, color, -2);
                }
                host.Fill(0, 0, 319, 10); host.Fill(0, 190, 319, 199);
                if (picture == host.PictureCount - 1) host.Text(5, 180, episodeText, 2, -2);
                if (bottom == LineCount - 8) host.FadeSong();
                // Native holds this last frame indefinitely until a button is pressed.
                if (ticks == maximumTicks - 1) { ticks--; host.PlaySong(9); }
                host.KeepAlive(); host.Present(); host.Wait();
                if (host.AnyButton()) break;
            }
            host.FadeBlack(10);
        }
        finally { host.FreePictures(); }
    }
}

public sealed class CreditsHost(DataFiles files, IndexedSurface surface, SpriteTable pictures, SpriteSheet ships,
    FontRenderer font, PaletteSystem palette, ReadOnlyMemory<PaletteColor> colors, InputState input,
    DelayTimer timer, Action<uint> playSong, Action fadeSong, Action present, Action keepAlive) : ICreditsHost
{
    public void LoadPictures()
    {
        using var stream = files.OpenRequired(files.Directory, "estsc.shp");
        pictures.Load(stream);
    }
    public int PictureCount => pictures.Count;
    public (ushort Width, ushort Height) PictureSize(int index) => (pictures.Get(index).Width, pictures.Get(index).Height);
    public Stream OpenText() => files.OpenRequired(files.Directory, "tyrian.cdt");
    public void SetDelay(int ticks) => timer.SetDelay(ticks);
    public void SetPictureDelay(int ticks) => timer.SetDelay2(ticks);
    public uint PictureDelay => timer.GetDelayTicks2();
    public void PlaySong(uint song) => playSong(song);
    public void FadeSong() => fadeSong();
    public void Clear() => surface.Pixels.Span.Clear();
    public void Present() => present();
    public void FadeIn(int steps)
    {
        if (colors.Length != 256) throw new InvalidDataException("Credits require a 256-color palette.");
        palette.FadePalette(colors.Span, steps, 0, 255, () => timer.SetDelay(1), present, timer.Wait);
    }
    public void Picture(int index, int x, int y, sbyte brightness) => pictures.Blit(surface, x, y, index, SpriteEffect.Hue, 0, brightness);
    public void Ship(int x, int y, uint graphic) => ships.Blit2x2(surface, x, y, checked((int)graphic));
    public int TextWidth(ReadOnlyMemory<byte> text) => font.TextWidth(text.Span, Font.Normal);
    public void Text(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness)
        => font.OutTextAdjust(surface, x, y, text.Span, color, brightness, Font.Normal, false);
    public void Fill(int left, int top, int right, int bottom) => surface.FillRectangle(left, top, right, bottom, 0);
    public void KeepAlive() => keepAlive();
    public void Wait() => timer.Wait();
    public bool AnyButton() => input.AnyButton();
    public void FadeBlack(int steps) => palette.FadeBlack(steps, () => timer.SetDelay(1), present, timer.Wait);
    public void FreePictures() => pictures.Clear();
}
