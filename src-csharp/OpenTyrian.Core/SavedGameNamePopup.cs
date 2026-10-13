using System.Text;

namespace OpenTyrian.Core;

public interface ISavedGameNamePopupHost
{
    void EnsureMouseSprite();
    void WaitForNoInput(CancellationToken cancellation);
    void Shade(int left, int top, int right, int bottom);
    void ServiceEvents(bool clearNew);
    HighScoreInput Input { get; }
    void DrawMessageBox();
    void DrawShadedText(int x, int y, ReadOnlyMemory<byte> text, byte color, int brightness, bool darken);
    void DrawName(int x, int y, ReadOnlyMemory<byte> text);
    int TextWidth(ReadOnlyMemory<byte> text, Font font);
    void DrawAdjustedText(int x, int y, ReadOnlyMemory<byte> text, byte color, int adjustment, Font font, bool centered);
    void Fill(int left, int top, int right, int bottom, byte color);
    void SetDelay(int ticks);
    void MouseStart();
    void Present();
    void MouseReplace();
    void PushJoysticks();
    void ServiceWait();
    void ReadMousePosition();
    bool Playing { get; }
    void PlaySong(uint song);
    void SaveGame(string name);
    void LoadGame(string name);
}

public static class SavedGameNamePopup
{
    public static void Run(bool saving, ReadOnlyMemory<byte> levelName,
        IReadOnlyList<byte[]> miscText, ISavedGameNamePopupHost host, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(miscText);
        ArgumentNullException.ThrowIfNull(host);
        if (miscText.Count < 11)
            throw new InvalidDataException("Save-name popup text is incomplete.");

        host.EnsureMouseSprite();
        host.WaitForNoInput(cancellation);
        var name = Enumerable.Repeat((byte)' ', 29).ToArray();
        var length = 0;
        var flash = (byte)138;
        var quit = false;
        var cancel = false;
        host.Shade(65, 55, 255, 155);

        do
        {
            cancellation.ThrowIfCancellationRequested();
            host.ServiceEvents(true);
            host.DrawMessageBox();
            host.DrawShadedText(60, 55, miscText[0], 11, 4, true);
            host.DrawShadedText(70, 70, levelName, 11, 4, true);

            do
            {
                cancellation.ThrowIfCancellationRequested();
                flash = flash == 138 ? (byte)130 : (byte)138;
                var current = name.AsMemory(0, length);
                host.DrawName(65, 89, current);
                var cursorX = unchecked((ushort)(65 + host.TextWidth(current, Font.Small)));
                host.Shade(cursorX + 2, 90, cursorX + 6, 95);
                host.Fill(cursorX + 1, 89, cursorX + 5, 94, flash);
                var acceptX = 54 + 45 - host.TextWidth(miscText[9], Font.Large) / 2;
                host.DrawAdjustedText(acceptX, 128, miscText[9], 15, -5, Font.Large, true);
                var cancelX = 149 + 45 - host.TextWidth(miscText[10], Font.Large) / 2;
                host.DrawAdjustedText(cancelX, 128, miscText[10], 15, -5, Font.Large, true);

                for (var i = 0; i < 14; i++)
                {
                    cancellation.ThrowIfCancellationRequested();
                    host.SetDelay(1);
                    host.MouseStart();
                    host.Present();
                    host.MouseReplace();
                    host.PushJoysticks();
                    host.ServiceWait();
                    if (host.Input.NewKey || host.Input.NewMouse)
                        break;
                }
                host.ReadMousePosition();
            } while (!host.Input.NewKey && !host.Input.NewMouse && !host.Input.NewText);

            if (!host.Playing)
                host.PlaySong(31);

            var input = host.Input;
            if (input.NewMouse)
            {
                if (input.MouseX is > 56 and < 142 && input.MouseY is > 123 and < 149)
                    quit = true;
                else if (input.MouseX is > 151 and < 237 && input.MouseY is > 123 and < 149)
                    quit = cancel = true;
            }
            else if (input.NewText)
            {
                foreach (var raw in input.Text.Span)
                {
                    if (raw == 0)
                        break;
                    var character = raw <= 127 ? (byte)char.ToUpperInvariant((char)raw) : (byte)0;
                    if ((character == ' ' || FontRenderer.SpriteIndex(character) != -1) && length < 28)
                        name[length++] = character;
                }
            }
            else if (input.NewKey)
            {
                switch (input.Scancode)
                {
                    case 42:
                    case 76:
                        if (length > 0)
                            name[--length] = (byte)' ';
                        break;
                    case 41:
                        quit = cancel = true;
                        break;
                    case 40:
                    case 88:
                        quit = true;
                        break;
                }
            }
        } while (!quit);

        if (cancel)
            return;

        while (length > 0 && (name[length - 1] == 0 || name[length - 1] == ' '))
            length--;
        if (length == 0)
            return;

        var savedName = Encoding.ASCII.GetString(name, 0, length);
        if (saving)
            host.SaveGame(savedName);
        else
            host.LoadGame(savedName);
    }
}
