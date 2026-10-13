/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Text;

namespace OpenTyrian.Core;

public interface ISelectionMenuHost
{
    void EnsureCursorSprites();
    void LoadBackground();
    void RestoreBackground();
    int TextWidth(ReadOnlyMemory<byte> text);
    void DrawText(bool background, int x, int y, ReadOnlyMemory<byte> text, Font font,
        FontAlignment alignment, sbyte brightness);
    void FadeIn();
    void PresentWithCursor();
    void PlaySound(byte sample);
    void FadeBlack(int steps);
    void PushJoysticksAsKeyboard();
    void KeepAlive();
    ushort Modifiers { get; }
    int InitializeEpisode(int episode);
}

public static class MainMenuChoiceLayout
{
    private const byte DataCubeMenu = 7;
    private const byte LoadSaveMenu = 6;
    private const byte DataCubeSubmenu = 8;
    private const byte PlayNextLevelMenu = 3;

    public static short PartWay(short start, short finish, byte dots, byte distance)
    {
        var position = (finish - start) / (dots + 2) * (distance + 1) + start;
        return unchecked((short)position);
    }

    public static int GetChoiceY(byte menu, byte choice, byte playNextLevelChoice)
    {
        var y = 38 + (choice - 1) * 16;
        if (menu == 0 && choice == 7) y += 16;
        if (menu == 9)
        {
            if (choice > 3) y += 16;
            if (choice > 4) y += 16;
        }
        if (menu != PlayNextLevelMenu || choice != playNextLevelChoice) y -= 16;
        return y;
    }

    public static void DrawHeader(byte menu, byte dataCubeSelection, int performSave,
        IReadOnlyList<ReadOnlyMemory<byte>> cubeHeaders,
        IReadOnlyList<IReadOnlyList<ReadOnlyMemory<byte>>> menuLabels,
        Func<ReadOnlyMemory<byte>, int> textWidth,
        Action<int, int, ReadOnlyMemory<byte>, Font> drawText)
    {
        ArgumentNullException.ThrowIfNull(cubeHeaders);
        ArgumentNullException.ThrowIfNull(menuLabels);
        ArgumentNullException.ThrowIfNull(textWidth);
        ArgumentNullException.ThrowIfNull(drawText);

        ReadOnlyMemory<byte> title;
        if (menu == DataCubeSubmenu)
        {
            var cubeIndex = dataCubeSelection - 2;
            if ((uint)cubeIndex >= (uint)cubeHeaders.Count)
                throw new ArgumentOutOfRangeException(nameof(dataCubeSelection));
            title = cubeHeaders[cubeIndex];
        }
        else if (menu == DataCubeMenu)
        {
            title = GetMenuLabel(menuLabels, 1, 1);
        }
        else if (menu == LoadSaveMenu)
        {
            title = GetMenuLabel(menuLabels, 3, performSave + 1);
        }
        else
        {
            title = GetMenuLabel(menuLabels, menu + 1, 0);
        }

        var terminator = title.Span.IndexOf((byte)0);
        if (terminator >= 0) title = title[..terminator];
        drawText(234 - textWidth(title) / 2, 10, title, Font.Large);
    }

    public static void DrawChoices(byte menu, byte choiceCount, byte selectedChoice,
        byte playNextLevelChoice, IReadOnlyList<ReadOnlyMemory<byte>> labels,
        Action<int, int, ReadOnlyMemory<byte>, Font> drawText)
    {
        ArgumentNullException.ThrowIfNull(labels);
        ArgumentNullException.ThrowIfNull(drawText);
        if (labels.Count < choiceCount)
            throw new ArgumentException("Menu labels must include the header and every choice.", nameof(labels));

        for (var choice = 2; choice <= choiceCount; choice++)
        {
            var text = labels[choice - 1];
            if (choice == selectedChoice)
            {
                var source = text.Span;
                var terminator = source.IndexOf((byte)0);
                if (terminator >= 0) source = source[..terminator];
                var selected = new byte[source.Length + 2];
                selected[0] = (byte)'~';
                source.CopyTo(selected.AsSpan(1));
                text = selected;
            }

            drawText(166, GetChoiceY(menu, (byte)choice, playNextLevelChoice), text, Font.Small);
        }
    }

    private static ReadOnlyMemory<byte> GetMenuLabel(
        IReadOnlyList<IReadOnlyList<ReadOnlyMemory<byte>>> menuLabels, int row, int column)
    {
        if ((uint)row >= (uint)menuLabels.Count || (uint)column >= (uint)menuLabels[row].Count)
            throw new ArgumentOutOfRangeException(nameof(menuLabels), "The selected menu header does not exist.");
        return menuLabels[row][column];
    }
}

public static class MainMenuHelpLayout
{
    public static int GetHelpIndex(byte menu, byte selectedChoice, byte choiceCount,
        bool leftPower, bool rightPower, int cubeCount,
        IReadOnlyList<IReadOnlyList<byte>> menuHelp)
    {
        ArgumentNullException.ThrowIfNull(menuHelp);
        var selectionOffset = unchecked((byte)(selectedChoice - 2));
        int helpIndex;
        if (menu == 12)
        {
            ReadOnlySpan<byte> joystickHelp = [15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 15, 24, 11];
            if (selectionOffset >= joystickHelp.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedChoice));
            helpIndex = joystickHelp[selectionOffset];
        }
        else if (menu < 3 || menu == 9 || menu > 10)
        {
            if (menu >= menuHelp.Count || selectionOffset >= menuHelp[menu].Count)
                throw new ArgumentOutOfRangeException(nameof(menu));
            var oneBasedIndex = menuHelp[menu][selectionOffset];
            if (oneBasedIndex == 0)
                throw new ArgumentOutOfRangeException(nameof(menuHelp), "Menu help indices are one-based.");
            helpIndex = oneBasedIndex - 1;
        }
        else if (menu == 5 && selectedChoice == 10)
        {
            helpIndex = 24;
        }
        else if (leftPower || rightPower)
        {
            helpIndex = 23;
        }
        else if ((int)selectionOffset == choiceCount - 2 || (menu == 7 && cubeCount == 0))
        {
            helpIndex = 11;
        }
        else
        {
            helpIndex = 17 + menu - 3;
        }

        return helpIndex;
    }

    public static void Draw(byte menu, byte selectedChoice, byte choiceCount,
        bool leftPower, bool rightPower, int cubeCount,
        IReadOnlyList<IReadOnlyList<byte>> menuHelp,
        IReadOnlyList<ReadOnlyMemory<byte>> helpText,
        Action<int, int, ReadOnlyMemory<byte>, int, int, int> drawText)
    {
        ArgumentNullException.ThrowIfNull(helpText);
        ArgumentNullException.ThrowIfNull(drawText);
        var helpIndex = GetHelpIndex(menu, selectedChoice, choiceCount,
            leftPower, rightPower, cubeCount, menuHelp);
        if ((uint)helpIndex >= (uint)helpText.Count)
            throw new ArgumentOutOfRangeException(nameof(helpText), "The selected main-menu help text does not exist.");
        drawText(10, 187, helpText[helpIndex], 14, 1, 0);
    }
}

public static class MainMenuScore
{
    public static void Draw(byte menu, int cashLeft, Action<int, int, ReadOnlyMemory<byte>, int, int, int> drawText)
    {
        ArgumentNullException.ThrowIfNull(drawText);
        if (menu == 4)
            drawText(65, 173, System.Text.Encoding.ASCII.GetBytes(cashLeft.ToString(
                System.Globalization.CultureInfo.InvariantCulture)), 1, 6, 0);
    }
}

public sealed record UpgradeSubmenuState(byte ChoiceCount, byte SelectedChoice,
    ReadOnlyMemory<byte>[] Labels);

public static class ItemIconDrawing
{
    public static void Draw(byte itemType, ushort itemNumber, int x, int y,
        ItemDatabase items, SpriteSheet shopSprites, SpriteSheet shipSprites,
        PlayerShipInfo shipInfo, Action<SpriteSheet, int, int, int> drawLargeSprite)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(shopSprites);
        ArgumentNullException.ThrowIfNull(shipSprites);
        ArgumentNullException.ThrowIfNull(shipInfo);
        ArgumentNullException.ThrowIfNull(drawLargeSprite);
        if (itemNumber == 0)
            return;

        if (itemType == 1)
        {
            if (itemNumber > 90)
            {
                var sheet = shipSprites;
                var graphic = shipInfo.ExtraGraphic((ushort)(itemNumber - 90), ref sheet);
                drawLargeSprite(sheet, x, y, graphic);
            }
            else
            {
                drawLargeSprite(shipSprites, x, y, GetItem<ShipItem>(items.Ships, itemNumber).ShipGraphic);
            }
            return;
        }

        var type = itemType switch
        {
            2 => ItemType.WeaponFront,
            3 => ItemType.WeaponRear,
            4 => ItemType.Shield,
            5 => ItemType.Generator,
            6 => ItemType.SidekickLeft,
            7 => ItemType.SidekickRight,
            _ => (ItemType?)null
        };
        if (type is null)
            return;

        var graphicIndex = GetItem<NamedItem>(items.Items(type.Value), itemNumber).ItemGraphic;
        if (graphicIndex > 0)
            drawLargeSprite(shopSprites, x, y, graphicIndex);
    }

    private static T GetItem<T>(IReadOnlyList<GameItem> catalog, ushort itemNumber) where T : GameItem
    {
        if (itemNumber >= catalog.Count || catalog[itemNumber] is not T item)
            throw new InvalidDataException($"Menu item {itemNumber} is not a valid {typeof(T).Name}.");
        return item;
    }
}

public interface IShipIllustrationHost
{
    void DrawSprite(int x, int y, int shapeTable, int sprite);
    void DrawHueSprite(int x, int y, int shapeTable, int sprite, byte hue, byte brightness);
    void DrawItemIcon(byte itemType, byte itemNumber, int x, int y);
}

public static class ShipIllustrationDrawing
{
    public static void Draw(PlayerState player, ItemDatabase items, IShipIllustrationHost host)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(host);

        var shipIndex = (int)player.Items.Ship;
        if (shipIndex == 0 || shipIndex >= items.Ships.Count)
            throw new InvalidDataException($"Ship illustration has invalid ship index {shipIndex}.");
        var ship = items.Ships[shipIndex];
        if (ship.Id.Length == 0)
        {
            shipIndex = items.Index(ItemType.Ship, 1);
            if (shipIndex <= 0 || shipIndex >= items.Ships.Count)
                throw new InvalidDataException("The default ship definition is unavailable.");
            ship = items.Ships[shipIndex];
        }

        if (ship.BigShipGraphic == 0)
            throw new InvalidDataException("The ship illustration graphic must be one-based.");
        host.DrawSprite(ship.BigShipGraphX, ship.BigShipGraphY, 5, ship.BigShipGraphic - 1);

        var generatorIndex = player.Items.Generator;
        if (generatorIndex == 0 || generatorIndex >= items.Generators.Count)
            throw new InvalidDataException($"Ship illustration has invalid generator index {generatorIndex}.");
        var generator = items.Generators[generatorIndex];
        host.DrawSprite(generator.BigShipGraphX, generator.BigShipGraphY, 6, generator.BigShipGraphic);

        DrawWeapon(player.Items.Weapons[(int)WeaponPort.Front].Id, true);
        DrawWeapon(player.Items.Weapons[(int)WeaponPort.Rear].Id, false);
        host.DrawItemIcon(6, player.Items.Sidekicks[(int)SidekickPort.Left], 3, 84);
        host.DrawItemIcon(7, player.Items.Sidekicks[(int)SidekickPort.Right], 129, 84);

        var shieldIndex = player.Items.Shield;
        if (shieldIndex >= items.Shields.Count)
            throw new InvalidDataException($"Ship illustration has invalid shield index {shieldIndex}.");
        host.DrawHueSprite(28, 23, 5, 26, 15, unchecked((byte)(items.Shields[shieldIndex].Mpwr - 10)));
        return;

        void DrawWeapon(byte weaponIndex, bool front)
        {
            if (weaponIndex == 0)
                return;
            if (weaponIndex >= items.Weapons.Count)
                throw new InvalidDataException($"Ship illustration has invalid weapon index {weaponIndex}.");
            var weapon = items.Weapons[weaponIndex];
            host.DrawSprite(front ? weapon.BigShipGraphFrntX : weapon.BigShipGraphRearX,
                front ? weapon.BigShipGraphFrntY : weapon.BigShipGraphRearY, 6,
                weapon.BigShipGraphic);
        }
    }
}

public interface IShipSpecificationsDrawingHost
{
    int VerticalHeight { get; set; }
    void DrawLines(IndexedSurface screen, bool dark);
    void DrawHelpBox(IndexedSurface screen, int x, int y, ReadOnlyMemory<byte> text, int width);
    void DrawText(IndexedSurface screen, int x, int y, ReadOnlyMemory<byte> text, byte hue, int brightness);
    int Center(ReadOnlyMemory<byte> text, Font font);
}

public static class ShipSpecificationsDrawing
{
    public static void Draw(IndexedSurface screen, IndexedSurface temporaryScreen,
        IReadOnlyList<ShipItem> ships, byte selectedShip, byte previousShip,
        IReadOnlyList<ReadOnlyMemory<byte>> shipDescription, ReadOnlyMemory<byte> doneLabel,
        SpriteTable optionShapes, IShipSpecificationsDrawingHost host)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(temporaryScreen);
        ArgumentNullException.ThrowIfNull(ships);
        ArgumentNullException.ThrowIfNull(shipDescription);
        ArgumentNullException.ThrowIfNull(optionShapes);
        ArgumentNullException.ThrowIfNull(host);
        if (screen.Width != temporaryScreen.Width || screen.Height != temporaryScreen.Height ||
            screen.Pitch != temporaryScreen.Pitch || screen.Pixels.Length != temporaryScreen.Pixels.Length)
            throw new ArgumentException("Ship specifications require matching screen buffers.", nameof(temporaryScreen));

        var shipIndex = selectedShip > 0 ? selectedShip : previousShip;
        if (shipIndex == 0 || shipIndex >= ships.Count)
            throw new InvalidDataException($"Ship specification index {shipIndex} is unavailable.");
        if (shipDescription.Count < 2)
            throw new InvalidDataException("Ship specifications require two description paragraphs.");
        var ship = ships[shipIndex];

        screen.Pixels.Span.Clear();
        host.DrawLines(screen, true);
        host.DrawLines(screen, false);
        screen.Rectangle(0, 0, 319, 199, 37);
        screen.Rectangle(1, 1, 318, 198, 35);
        host.VerticalHeight = 9;
        host.DrawText(screen, 10, 2, Encoding.Latin1.GetBytes(ship.Name), 12, 3);
        host.DrawHelpBox(screen, 100, 20, shipDescription[0], 40);
        host.DrawHelpBox(screen, 100, 100, shipDescription[1], 40);
        host.VerticalHeight = 7;
        host.DrawText(screen, host.Center(doneLabel, Font.Small), 190, doneLabel, 12, 2);

        if (ship.BigShipGraphic == 0)
            throw new InvalidDataException("Ship specification graphic must be one-based.");
        temporaryScreen.Pixels.Span.Clear();
        optionShapes.Blit(temporaryScreen, ship.BigShipGraphX - 30, ship.BigShipGraphY,
            ship.BigShipGraphic - 1);

        var source = temporaryScreen.Pixels.Span;
        var destination = screen.Pixels.Span;
        for (var y = 0; y < screen.Height; y++)
        {
            for (var x = 0; x < screen.Pitch; x++)
            {
                var offset = y * screen.Pitch + x;
                var average = 0;
                if (y > 0) average += source[offset - screen.Pitch] & 15;
                if (y < screen.Height - 1) average += source[offset + screen.Pitch] & 15;
                if (x > 0) average += source[offset - 1] & 15;
                if (x < screen.Pitch - 1) average += source[offset + 1] & 15;
                average /= 4;
                if ((source[offset] & 15) > average)
                    destination[offset] = (byte)((source[offset] & 15) | 0xc0);
            }
        }
    }
}

public sealed class WeaponSimulationState
{
    public byte UpgradeSelection { get; set; }
    public byte SubmenuSelection { get; set; }
    public byte SubmenuChoiceCount { get; set; }
    public bool LeftPowerAvailable { get; set; }
    public bool RightPowerAvailable { get; set; }
    public bool RightPowerAffordable { get; set; }
    public int DowngradeCost { get; set; }
    public int UpgradeCost { get; set; }
}

public interface IWeaponSimulationHost
{
    void UpdateWeaponViewFrame();
    void DrawText(int x, int y, string text, byte hue, int brightness);
    void DrawOptionSprite(int x, int y, int sprite);
    void FillRectangle(int left, int top, int right, int bottom, byte color);
    void DrawPixel(int x, int y, byte color);
    void DrawShip(int x, int y);
}

public static class WeaponSimulationDrawing
{
    public static void Update(WeaponSimulationState state, PlayerState player,
        EpisodeItemAvailabilityState availability, ItemDatabase items, IWeaponSimulationHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(host);

        host.UpdateWeaponViewFrame();
        if (state.UpgradeSelection is 3 or 4 &&
            state.SubmenuSelection < state.SubmenuChoiceCount)
        {
            var weaponSlot = state.UpgradeSelection - 3;
            var availabilityRow = weaponSlot + 1;
            var itemSlot = state.SubmenuSelection - 2;
            if (itemSlot < 0 || itemSlot >= availability.Counts[availabilityRow] ||
                itemSlot >= availability.Items.GetLength(1))
                throw new InvalidDataException("Weapon simulator selection exceeds its available item row.");

            var itemId = availability.Items[availabilityRow, itemSlot];
            if (itemId != 0)
            {
                DrawSelectedWeapon(weaponSlot, itemId);
            }
            else
            {
                HidePowerControls();
            }
        }
        else
        {
            HidePowerControls();
        }

        host.DrawShip(player.X - 5, player.Y - 7);

        void DrawSelectedWeapon(int weaponSlot, byte itemId)
        {
            var weapon = player.Items.Weapons[weaponSlot];
            if (weapon.Id >= items.Weapons.Count || itemId >= items.Weapons.Count ||
                weapon.Power == 0 || weapon.Power > 11)
                throw new InvalidDataException("Weapon simulator has invalid weapon or power data.");

            if (state.LeftPowerAvailable)
                host.DrawText(26, 137, state.DowngradeCost.ToString(System.Globalization.CultureInfo.InvariantCulture), 1, 4);
            else
                host.DrawOptionSprite(24, 149, 13);

            if (state.RightPowerAvailable)
            {
                var affordable = state.RightPowerAffordable;
                host.DrawText(108, 137, state.UpgradeCost.ToString(System.Globalization.CultureInfo.InvariantCulture),
                    affordable ? (byte)1 : (byte)7, 4);
                if (!affordable)
                    host.DrawOptionSprite(119, 149, 14);
            }
            else
            {
                host.DrawOptionSprite(119, 149, 14);
            }

            var power = weapon.Power;
            for (var level = 1; level <= power; level++)
            {
                var x = 39 + level * 6;
                host.FillRectangle(x, 151, x + 4, 151, 251);
                host.DrawPixel(x, 151, 252);
                host.FillRectangle(x, 152, x + 4, 164, 250);
                host.FillRectangle(x, 165, x + 4, 165, 249);
            }

            host.DrawText(58, 137, $"POWER: {power}", 15, 4);
            var shotIndex = items.Weapons[weapon.Id].Op[0][power - 1];
            if (shotIndex >= items.Shots.Count)
                throw new InvalidDataException($"Weapon {weapon.Id} power {power} references missing shot {shotIndex}.");
            host.DrawText(58, 127, $"Shot: {items.Shots[shotIndex].Id}", 15, 4);
        }

        void HidePowerControls()
        {
            state.LeftPowerAvailable = false;
            state.RightPowerAvailable = false;
            host.DrawOptionSprite(20, 146, 17);
        }
    }
}

public interface IWeaponViewFrameHost
{
    void UpdateStarfield(IndexedSurface screen, int moveSpeed);
    void DrawSidekick(int sheet, int x, int y, int graphic, bool large);
    void DrawUpgradeInterface();
}

public static class WeaponViewFrame
{
    public static void Update(IndexedSurface screen, PlayerState player, ItemDatabase items,
        PlayerShots shots, LevelHudState hud, uint powerAdd, IWeaponViewFrameHost host)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(shots);
        ArgumentNullException.ThrowIfNull(hud);
        ArgumentNullException.ThrowIfNull(host);

        screen.FillRectangle(8, 8, 143, 182, 0);
        host.UpdateStarfield(screen, 1);
        var mouseX = player.X;
        var mouseY = player.Y;

        for (var port = 0; port < 2; port++)
        {
            if (shots.Repeat[port] > 0)
            {
                shots.Repeat[port]--;
                continue;
            }

            var weapon = player.Items.Weapons[port];
            var shotIndex = GetShotIndex(weapon.Id, port == 1
                ? checked((int)player.WeaponMode - 1) : 0, weapon.Power - 1);
            shots.Create(weapon.Id, (uint)port, unchecked((ushort)player.X), unchecked((ushort)player.Y),
                unchecked((ushort)mouseX), unchecked((ushort)mouseY), shotIndex, 1);
        }

        var leftSidekickId = player.Items.Sidekicks[(int)SidekickPort.Left];
        if (GetSidekick(leftSidekickId).WPort > 0)
        {
            const int bay = 2;
            if (shots.Repeat[bay] > 0)
                shots.Repeat[bay]--;
            else
            {
                var sidekick = GetSidekick(leftSidekickId);
                shots.Create(checked((ushort)sidekick.WPort), bay,
                    unchecked((ushort)player.Sidekicks[(int)SidekickPort.Left].X),
                    unchecked((ushort)player.Sidekicks[(int)SidekickPort.Left].Y),
                    unchecked((ushort)mouseX), unchecked((ushort)mouseY), sidekick.Wpnum, 1);
            }
        }

        var rightSidekickId = player.Items.Sidekicks[(int)SidekickPort.Right];
        var rightSidekickItem = GetSidekick(rightSidekickId);
        var rightSidekick = player.Sidekicks[(int)SidekickPort.Right];
        if (rightSidekickItem.Tr == 2)
        {
            rightSidekick.X = player.X;
            rightSidekick.Y = Math.Max(10, player.Y - 20);
        }
        else
        {
            rightSidekick.X = 87;
            rightSidekick.Y = 120;
        }

        if (rightSidekickItem.WPort > 0)
        {
            const int bay = 3;
            if (shots.Repeat[bay] > 0)
                shots.Repeat[bay]--;
            else
                shots.Create(checked((ushort)rightSidekickItem.WPort), bay, unchecked((ushort)rightSidekick.X),
                    unchecked((ushort)rightSidekick.Y), unchecked((ushort)mouseX), unchecked((ushort)mouseY),
                    rightSidekickItem.Wpnum, 1);
        }

        shots.Simulate();
        for (var index = 0; index < player.Sidekicks.Length; index++)
        {
            var itemId = player.Items.Sidekicks[index];
            if (itemId == 0)
                continue;
            var item = GetSidekick(itemId);
            var sidekick = player.Sidekicks[index];
            if (item.Option > 0)
            {
                if (sidekick.AnimationEnabled)
                {
                    sidekick.AnimationFrame++;
                    if (sidekick.AnimationFrame >= item.Ani)
                        sidekick.AnimationFrame = 0;
                }

                if (sidekick.Charge >= item.Gr.Length || sidekick.AnimationFrame >= item.Gr[sidekick.Charge].Length)
                    throw new InvalidDataException("Sidekick animation frame exceeds its sprite table.");
                var graphic = item.Gr[sidekick.Charge][sidekick.AnimationFrame];
                var large = sidekick.Style is 1 or 2;
                host.DrawSidekick(large ? 10 : 9, sidekick.X - (large ? 6 : 0), sidekick.Y,
                    graphic, large);
            }

            if (item.Pwr > 0)
            {
                sidekick.ChargeTicks = unchecked(sidekick.ChargeTicks - 1);
                if (sidekick.ChargeTicks == 0)
                {
                    if (sidekick.Charge < item.Pwr)
                        sidekick.Charge++;
                    sidekick.ChargeTicks = 20;
                }
            }
        }

        host.DrawUpgradeInterface();
        UpdatePowerBar();

        void UpdatePowerBar()
        {
            shots.Power = Math.Min(900, unchecked(shots.Power + powerAdd));
            var top = 147 - (int)(shots.Power / 10);
            for (var y = top; y <= 146; y++)
            {
                var color = 113 + (146 - y) / 9 + 2;
                var phase = (y + 1) % 6;
                if (phase == 1) color += 3;
                else if (phase != 0) color += 2;
                screen.Pixel(141, y, unchecked((byte)(color - 3)));
                screen.Pixel(142, y, unchecked((byte)(color - 3)));
                screen.Pixel(143, y, unchecked((byte)(color - 2)));
                screen.Pixel(144, y, unchecked((byte)(color - 1)));
                screen.FillRectangle(145, y, 149, y, unchecked((byte)color));
                if (color - 3 < 112)
                    color++;
            }

            var markerY = 147 - (int)(shots.Power / 10);
            var markerColor = 113 + (146 - markerY) / 9 + 4;
            for (var x = 141; x <= 144; x++)
                screen.Pixel(x, markerY - 1, unchecked((byte)(markerColor - 1)));
            screen.FillRectangle(145, markerY - 1, 149, markerY - 1, unchecked((byte)markerColor));
            hud.LastPower = (uint)markerY;
        }

        SidekickItem GetSidekick(byte itemId)
            => itemId < items.Sidekicks.Count ? items.Sidekicks[itemId] :
                throw new InvalidDataException($"Weapon simulator has invalid sidekick item {itemId}.");

        ushort GetShotIndex(byte weaponId, int mode, int powerIndex)
        {
            if (weaponId >= items.Weapons.Count || mode is < 0 or >= 2 ||
                powerIndex < 0 || powerIndex >= 11)
                throw new InvalidDataException("Weapon simulator has invalid equipped weapon mode or power.");
            return items.Weapons[weaponId].Op[mode][powerIndex];
        }
    }
}

public static class UpgradeSubmenuBuilder
{
    private static readonly (ItemType Type, int AvailabilityRow)[] MenuItems =
    [
        (ItemType.Ship, 0),
        (ItemType.WeaponFront, 1),
        (ItemType.WeaponRear, 2),
        (ItemType.Shield, 8),
        (ItemType.Generator, 3),
        (ItemType.SidekickLeft, 5),
        (ItemType.SidekickRight, 6)
    ];

    public static UpgradeSubmenuState Build(byte itemNumber, ItemDatabase items,
        EpisodeItemAvailabilityState availability, PlayerItems playerItems,
        IReadOnlyList<ReadOnlyMemory<byte>> existingLabels, ReadOnlyMemory<byte> headerLabel,
        ReadOnlyMemory<byte> doneLabel)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(playerItems);
        ArgumentNullException.ThrowIfNull(existingLabels);
        if (itemNumber is < 2 or > 8)
            throw new ArgumentOutOfRangeException(nameof(itemNumber));

        var (type, row) = MenuItems[itemNumber - 2];
        var count = availability.Counts[row];
        if (count > availability.Items.GetLength(1) || existingLabels.Count <= count)
            throw new ArgumentException("The item availability or menu label buffer is too small.");

        var labels = existingLabels.ToArray();
        labels[0] = headerLabel;
        var selectedChoice = (byte)2;
        var selectedItem = playerItems.GetMenuItemId(itemNumber - 2);
        var catalog = items.Items(type);
        for (var index = 0; index < count; index++)
        {
            var itemIndex = availability.Items[row, index];
            if (itemIndex >= catalog.Count || catalog[itemIndex] is not NamedItem named)
                throw new InvalidDataException($"Shop row {row} contains invalid item index {itemIndex} for {type}.");
            labels[index] = System.Text.Encoding.Latin1.GetBytes(named.Name);
            if (itemIndex == selectedItem)
                selectedChoice = unchecked((byte)(index + 2));
        }

        labels[count] = doneLabel;
        return new UpgradeSubmenuState(unchecked((byte)(count + 2)), selectedChoice, labels);
    }
}

public static class MenuQuitRequest
{
    public static void Run(bool haltNetwork, bool networkGame, LegacyGameState game,
        Func<bool> confirmQuit, Action haltNetworkGame)
    {
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(confirmQuit);
        ArgumentNullException.ThrowIfNull(haltNetworkGame);
        if (!confirmQuit())
            return;

        if (haltNetwork && networkGame)
            haltNetworkGame();

        game.GameLoaded = true;
        game.MainLevel = 0;
    }
}

public interface IKeyboardRebindingHost
{
    InputState Input { get; }
    IKeyboardNames KeyboardNames { get; }
    void DrawKeyName(int x, int y, string name, int foreground, int brightness);
    void Present();
    void SetDelay(uint ticks);
    void DrawSelectionRectangle(int left, int top, int right, int bottom, int color);
    void WaitDelay();
    void PlayClick();
    void WipeKey();
}

public static class KeyboardConfigurationSelection
{
    public static void Handle(ushort selection, int[] keySettings, ReadOnlySpan<int> defaultKeySettings,
        bool networkGame, Action<byte> setCurrentMenu, Action<ushort> rebindKey)
    {
        ArgumentNullException.ThrowIfNull(keySettings);
        ArgumentNullException.ThrowIfNull(setCurrentMenu);
        ArgumentNullException.ThrowIfNull(rebindKey);

        if (selection == 10)
        {
            if (defaultKeySettings.Length != keySettings.Length)
                throw new ArgumentException("Default and current keyboard settings must have equal lengths.", nameof(defaultKeySettings));
            defaultKeySettings.CopyTo(keySettings);
        }
        else if (selection == 11)
        {
            setCurrentMenu(networkGame ? (byte)11 : (byte)2);
        }
        else
        {
            rebindKey(selection);
        }
    }

    public static void Rebind(ushort selection, int[] keySettings, IKeyboardRebindingHost host)
    {
        ArgumentNullException.ThrowIfNull(keySettings);
        ArgumentNullException.ThrowIfNull(host);
        if (selection < 2 || selection >= 2 + keySettings.Length)
            throw new ArgumentOutOfRangeException(nameof(selection));

        var input = host.Input;
        var keyIndex = selection - 2;
        var y = 38 + keyIndex * 12;
        host.DrawKeyName(236, y, host.KeyboardNames.Name(keySettings[keyIndex]), 254 / 16, (254 % 16) - 8);
        host.Present();
        input.WaitNoInput(true, true, true);

        var color = 248;
        var colorDirection = 1;
        do
        {
            host.SetDelay(1);
            color += colorDirection;
            if (color < 243 || color > 248)
                colorDirection *= -1;

            host.DrawSelectionRectangle(230, y - 2, 300, y + 7, color);
            input.PollJoysticks();
            input.ServiceEvents(true);
            host.Present();
            host.WaitDelay();
        } while (!input.NewKey && !input.MouseDown && !input.JoystickDown);

        if (!input.NewKey)
            return;

        var newScancode = input.LastScancode;
        for (var i = 0; i < keySettings.Length; i++)
        {
            if (keySettings[i] == newScancode)
            {
                keySettings[i] = keySettings[keyIndex];
                break;
            }
        }

        if (newScancode is not (41 or 68 or 19))
        {
            host.PlayClick();
            keySettings[keyIndex] = newScancode;
        }
        host.WipeKey();
    }
}

public interface IQuitConfirmationHost
{
    void ClearKeyboard();
    void WipeKey();
    void WaitNoInput(bool keyboard, bool mouse, bool joystick);
    void ShadeDialog(int left, int top, int right, int bottom);
    void ServiceEvents(bool clearNew);
    void SetDelay(int ticks);
    void DrawMessageBox(int x, int y, int shape);
    void DrawTitle(int x, int y, ReadOnlyMemory<byte> text);
    void DrawMessage(int x, int y, ReadOnlyMemory<byte> text, int width);
    int TextWidth(ReadOnlyMemory<byte> text);
    void DrawChoice(int x, int y, ReadOnlyMemory<byte> text, int brightness);
    bool HasMouse { get; }
    void MouseStart();
    void Present();
    void MouseReplace();
    void WaitDelay();
    void PushJoysticksAsKeyboard();
    void PlaySound(byte sample);
    bool IsNetworkGame { get; }
    void SendQuitAndHaltNetwork();
}

public sealed class QuitConfirmationScreen(InputState input, IQuitConfirmationHost host)
{
    public bool Run(ReadOnlyMemory<byte> title, ReadOnlyMemory<byte> message,
        ReadOnlyMemory<byte> yes, ReadOnlyMemory<byte> no, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(host);
        host.ClearKeyboard();
        host.WipeKey();
        host.WaitNoInput(true, true, true);
        host.ShadeDialog(65, 55, 255, 155);

        var quitSelected = true;
        var done = false;
        while (!done)
        {
            byte color = 8;
            var colorDirection = 1;
            do
            {
                cancellation.ThrowIfCancellationRequested();
                host.ServiceEvents(true);
                host.SetDelay(4);
                host.DrawMessageBox(50, 50, 35);
                host.DrawTitle(70, 60, title);
                host.DrawMessage(70, 90, message, 30);

                color = unchecked((byte)(color + colorDirection));
                if (color > 8 || color < 2)
                    colorDirection = -colorDirection;

                host.DrawChoice(99 - host.TextWidth(yes) / 2, 128, yes, quitSelected ? color - 12 : -5);
                host.DrawChoice(194 - host.TextWidth(no) / 2, 128, no, quitSelected ? -5 : color - 12);
                if (host.HasMouse)
                {
                    host.MouseStart();
                    host.Present();
                    host.MouseReplace();
                }
                else
                {
                    host.Present();
                }

                host.WaitDelay();
                host.PushJoysticksAsKeyboard();
                host.ServiceEvents(false);
            } while (!input.NewKey && !input.NewMouse);

            if (input.NewMouse)
            {
                if (input.LastMouseY is > 123 and < 149)
                {
                    if (input.LastMouseX is > 56 and < 142)
                    {
                        quitSelected = true;
                        done = true;
                    }
                    else if (input.LastMouseX is > 151 and < 237)
                    {
                        quitSelected = false;
                        done = true;
                    }
                }
                input.NewMouse = false;
            }
            else if (input.NewKey)
            {
                switch (input.LastScancode)
                {
                    case 80:
                    case 79:
                    case 43:
                        quitSelected = !quitSelected;
                        host.PlaySound(28);
                        break;
                    case 40:
                    case 88:
                    case 44:
                        done = true;
                        break;
                    case 41:
                        quitSelected = false;
                        done = true;
                        break;
                }
            }
        }

        host.PlaySound(quitSelected ? (byte)16 : (byte)8);
        if (quitSelected && host.IsNetworkGame)
            host.SendQuitAndHaltNetwork();
        return quitSelected;
    }
}

public sealed class NextLevelMenuState
{
    public byte CurrentMenu { get; internal set; }
    public byte NewPalette { get; internal set; }
    public float NavigationX { get; internal set; }
    public float NavigationY { get; internal set; }
    public float TargetX { get; internal set; }
    public float TargetY { get; internal set; }
    public byte ChoiceCount { get; internal set; }
    public byte SelectedChoice { get; internal set; }
    public ReadOnlyMemory<byte>[] Labels { get; internal set; } = [];
    public byte[] Route { get; internal set; } = [];
    public NavigationDotLayout Dots { get; internal set; } = new([], new short[0, 10], new short[0, 10]);
    public byte MainLevel { get; private set; }
    public bool JumpSection { get; private set; }

    public void StartCurrentLevel(ReadOnlySpan<byte> mapSections)
    {
        if (Route.Length == 0 || mapSections.Length < Route.Length)
            throw new ArgumentException("The selected navigation route requires a section for each destination.", nameof(mapSections));
        MainLevel = mapSections[Route.Length - 1];
        JumpSection = true;
    }

    public void ReturnToOptions(bool networkGame) => CurrentMenu = networkGame ? (byte)11 : (byte)2;
}

public sealed class MainMenuActionState
{
    public byte CurrentMenu { get; set; }
    public byte NewPalette { get; set; }
    public byte ChoiceCount { get; set; }
    public int MenuColor { get; set; }
    public int MenuColorDirection { get; set; }
    public byte[] InputDevices { get; } = [1, 2];
    public byte[] MapSections { get; set; } = [];
    public bool PerformSave { get; set; }
    public bool QuickSave { get; set; }
    public byte SaveGamePageIndex { get; set; }
    public byte OldMenu { get; set; }
    public byte OldPalette { get; set; }
    public bool TwoPlayerMode { get; set; }
    public byte MainLevel { get; set; }
    public bool JumpSection { get; set; }
    public byte LastUpgradeSelection { get; set; }
    public int LastDirection { get; set; }
    public bool FirstDataCubePage { get; set; }
    public short DataCubeScroll { get; set; }
    public short DataCubeScrollDelta { get; set; }
    public ushort CurrentDataCube { get; set; }
}

public static class MainMenuActionHandlers
{
    public static void SelectFullGame(byte selection, MainMenuActionState state,
        Action showShipSpecifications, Action setupNextLevel, Action requestQuit)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(showShipSpecifications);
        ArgumentNullException.ThrowIfNull(setupNextLevel);
        ArgumentNullException.ThrowIfNull(requestQuit);
        switch (selection)
        {
            case 2: state.CurrentMenu = 7; break;
            case 3: showShipSpecifications(); break;
            case 4: state.CurrentMenu = 1; break;
            case 5: state.CurrentMenu = 2; break;
            case 6: setupNextLevel(); break;
            case 7: requestQuit(); break;
        }
    }

    public static void SelectTwoPlayer(byte selection, MainMenuActionState state, int joystickCount,
        Action startLevel, Action<byte> playSound, Action requestQuit)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(startLevel);
        ArgumentNullException.ThrowIfNull(playSound);
        ArgumentNullException.ThrowIfNull(requestQuit);
        if (selection == 2)
        {
            startLevel();
            return;
        }
        if (selection is 3 or 4)
        {
            ChangeTwoPlayerInputDevice(selection, 1, joystickCount, state.InputDevices, playSound);
            return;
        }
        if (selection == 5) state.CurrentMenu = 2;
        else if (selection == 6) requestQuit();
    }

    public static void ChangeTwoPlayerInputDevice(byte selection, int direction, int joystickCount,
        byte[] inputDevices, Action<byte> playSound)
    {
        ArgumentNullException.ThrowIfNull(inputDevices);
        ArgumentNullException.ThrowIfNull(playSound);
        if (inputDevices.Length < 2)
            throw new ArgumentException("Two-player input selection requires two device slots.", nameof(inputDevices));
        if (joystickCount < 0)
            throw new ArgumentOutOfRangeException(nameof(joystickCount));

        var player = selection - 3;
        if (player is < 0 or > 1)
            return;

        var other = 1 - player;
        playSound(28);
        do
        {
            if (joystickCount == 0)
                inputDevices[other] = inputDevices[player];

            inputDevices[player] = direction > 0
                ? inputDevices[player] >= 2 + joystickCount
                    ? (byte)1
                    : unchecked((byte)(inputDevices[player] + 1))
                : inputDevices[player] <= 1
                    ? unchecked((byte)(2 + joystickCount))
                    : unchecked((byte)(inputDevices[player] - 1));
        } while (inputDevices[player] == inputDevices[other]);
    }

    public static void ChangeUpgradeWeaponPower(byte upgradeSelection, byte submenuSelection,
        int direction, bool canChange, int[] temporaryWeaponPower, PlayerState player,
        Action<byte> playSound)
    {
        ArgumentNullException.ThrowIfNull(temporaryWeaponPower);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(playSound);
        if (!canChange)
        {
            playSound(23);
            return;
        }

        var weapon = upgradeSelection - 3;
        var choice = submenuSelection - 2;
        if (weapon is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(upgradeSelection));
        if ((uint)choice >= (uint)temporaryWeaponPower.Length)
            throw new ArgumentOutOfRangeException(nameof(submenuSelection));

        temporaryWeaponPower[choice] = unchecked(temporaryWeaponPower[choice] + direction);
        player.Items.Weapons[weapon].Power = unchecked((byte)temporaryWeaponPower[choice]);
    }

    public static void SelectOnePlayer(byte selection, MainMenuActionState state, bool networkGame,
        Action startLevel, Action<byte> setCurrentMenu, Action requestQuit)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(startLevel);
        ArgumentNullException.ThrowIfNull(setCurrentMenu);
        ArgumentNullException.ThrowIfNull(requestQuit);
        switch (selection)
        {
            case 2: startLevel(); break;
            case 3: setCurrentMenu(networkGame ? (byte)11 : (byte)2); break;
            case 4: requestQuit(); break;
        }
    }

    public static void SelectLimitedOptions(byte selection, MainMenuActionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (selection)
        {
            case 2: state.CurrentMenu = 12; break;
            case 3: state.CurrentMenu = 5; break;
            case 6: state.CurrentMenu = 10; break;
        }
    }

    public static void SelectSuperTyrian(byte selection, MainMenuActionState state,
        Action startLevel, Action showShipSpecifications, Action requestQuit)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(startLevel);
        ArgumentNullException.ThrowIfNull(showShipSpecifications);
        ArgumentNullException.ThrowIfNull(requestQuit);
        switch (selection)
        {
            case 2: startLevel(); break;
            case 3: showShipSpecifications(); break;
            case 4: state.CurrentMenu = 2; break;
            case 5: requestQuit(); break;
        }
    }

    public static void SelectOptions(byte selection, MainMenuActionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        switch (selection)
        {
            case 2:
            case 3:
                state.CurrentMenu = 6;
                state.PerformSave = selection == 3;
                state.QuickSave = false;
                state.SaveGamePageIndex = 0;
                break;
            case 6: state.CurrentMenu = 12; break;
            case 7: state.CurrentMenu = 5; break;
            case 8: state.CurrentMenu = 0; break;
        }
    }

    public static void SelectNextLevel(ushort selection, MainMenuActionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (selection == state.ChoiceCount)
        {
            state.CurrentMenu = 0;
            state.NewPalette = 1;
            return;
        }

        var index = selection - 2;
        if (index >= state.MapSections.Length)
            throw new ArgumentOutOfRangeException(nameof(selection));
        state.MainLevel = state.MapSections[index];
        state.JumpSection = true;
    }

    public static void SelectUpgrade(byte selection, MainMenuActionState state, LegacyGameState game,
        Func<byte, int> cashLeft, Func<byte, UpgradeSubmenuState> generateSubmenu,
        Action initializeWeaponView)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(game);
        ArgumentNullException.ThrowIfNull(cashLeft);
        ArgumentNullException.ThrowIfNull(generateSubmenu);
        ArgumentNullException.ThrowIfNull(initializeWeaponView);
        if (selection == 9)
        {
            state.CurrentMenu = 0;
            return;
        }

        var player = game.Players[0];
        player.LastItems = CopyItems(player.Items);
        state.LastDirection = 1;
        var submenu = generateSubmenu(selection);
        ArgumentNullException.ThrowIfNull(submenu);
        initializeWeaponView();
        state.CurrentMenu = 4;
        state.LastUpgradeSelection = submenu.SelectedChoice;
        player.Cash = unchecked(player.Cash * 2 - (ulong)(long)cashLeft(selection));
    }

    public static void SelectUpgradeSubmenu(byte selection, byte choiceCount,
        MainMenuActionState state, PlayerState player, Func<int> cashLeft, Action<byte> playSound)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(cashLeft);
        ArgumentNullException.ThrowIfNull(playSound);
        if (selection < choiceCount)
        {
            state.ChoiceCount = choiceCount;
            state.LastUpgradeSelection = choiceCount;
            return;
        }

        playSound(9);
        player.Cash = unchecked((ulong)(long)cashLeft());
        state.CurrentMenu = 1;
    }

    public static void SelectDataCube(byte selection, byte choiceCount, int cubeCount,
        MainMenuActionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (selection == choiceCount || cubeCount == 0)
        {
            state.CurrentMenu = 0;
            state.NewPalette = 1;
            return;
        }

        state.FirstDataCubePage = true;
        state.CurrentMenu = 8;
        state.DataCubeScroll = 0;
        state.DataCubeScrollDelta = 0;
        state.CurrentDataCube = unchecked((ushort)(selection - 2));
    }

    public static void SelectLoadSave(ushort selection, MainMenuActionState state,
        ISaveMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (selection == 13)
        {
            state.CurrentMenu = state.QuickSave ? state.OldMenu : (byte)2;
            if (state.QuickSave)
                state.NewPalette = state.OldPalette;
            return;
        }

        if (state.PerformSave && selection == 2)
        {
            host.ShowSaveNamePopup(true);
            return;
        }

        using var savedGames = host.BuildSaveGameList(state.TwoPlayerMode ? (byte)2 : (byte)1);
        var saveGameIndex = (int)state.SaveGamePageIndex * 10 + selection - 2 -
            (state.PerformSave ? 1 : 0);
        if (saveGameIndex >= 0 && saveGameIndex < savedGames.Count)
        {
            if (state.PerformSave)
                host.SaveGame(savedGames[saveGameIndex]);
            else
                host.LoadGame(savedGames[saveGameIndex]);
        }

        if (state.QuickSave)
        {
            state.CurrentMenu = state.OldMenu;
            state.NewPalette = state.OldPalette;
        }
    }

    private static PlayerItems CopyItems(PlayerItems source)
    {
        var copy = new PlayerItems
        {
            Ship = source.Ship,
            Generator = source.Generator,
            Shield = source.Shield,
            Special = source.Special,
            SidekickSeries = source.SidekickSeries,
            SidekickLevel = source.SidekickLevel,
            SuperArcadeMode = source.SuperArcadeMode
        };
        for (var i = 0; i < source.Weapons.Length; i++)
            copy.Weapons[i] = new PlayerWeapon { Id = source.Weapons[i].Id, Power = source.Weapons[i].Power };
        source.Sidekicks.CopyTo(copy.Sidekicks, 0);
        return copy;
    }
}

public sealed class ItemScreenHorizontalState
{
    public byte UpgradeSelection { get; set; }
    public byte JoystickSelection { get; set; }
    public bool LeftPowerAvailable { get; set; }
    public bool RightPowerAvailable { get; set; }
    public bool KeyboardUsed { get; set; }
    public bool RightPowerAffordable { get; set; }
    public bool MusicDisabled { get; set; }
    public bool SamplesDisabled { get; set; }
}

public interface IItemScreenHorizontalHost
{
    void AdjustJoystick(byte selection, int direction);
    void ChangePlayerDevice(int direction);
    void PlaySound(byte sample);
    void ChangeVolume(int musicDelta, int effectsDelta);
    void RestartSong();
    void ChangeWeaponPower(int direction, bool canChange);
}

public static class ItemScreenInputHandlers
{
    public static void DispatchInput(int mouseButton, bool newKey,
        Action handleMouseInput, Action handleKeyboardInput)
    {
        ArgumentNullException.ThrowIfNull(handleMouseInput);
        ArgumentNullException.ThrowIfNull(handleKeyboardInput);
        if (mouseButton > 0)
            handleMouseInput();
        else if (newKey)
            handleKeyboardInput();
    }

    private static readonly byte[] MouseRowHeights = [16, 16, 16, 16, 26, 12, 11, 28, 0, 16, 16, 16, 8, 16];

    public static int GetMouseSelection(byte menu, int mouseY, byte choiceCount)
    {
        if (menu >= MouseRowHeights.Length)
            throw new ArgumentOutOfRangeException(nameof(menu));
        var rowHeight = MouseRowHeights[menu];
        if (rowHeight == 0)
            throw new InvalidOperationException("The data-cube text page has no selectable menu rows.");

        var selection = (mouseY - 38) / rowHeight + 2;
        if (menu == 9)
        {
            if (selection > 5)
                selection--;
            if (selection > 3)
                selection--;
        }
        else if (menu == 0 && selection > 7)
        {
            selection = 7;
        }
        else if (menu == 3 && selection == choiceCount + 1)
        {
            selection = choiceCount;
        }

        return selection;
    }

    public static void OpenQuickSave(byte currentMenu, bool save, byte currentPalette,
        MainMenuActionState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (currentMenu is 7 or 8)
            currentMenu = 0;

        state.QuickSave = true;
        state.OldMenu = currentMenu;
        state.CurrentMenu = 6;
        state.SaveGamePageIndex = 0;
        state.PerformSave = save;
        state.NewPalette = 1;
        state.OldPalette = currentPalette;
    }

    public static void HandleHorizontal(byte menu, byte selection, int direction,
        ItemScreenHorizontalState state, IItemScreenHorizontalHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (menu == 12)
            host.AdjustJoystick(state.JoystickSelection, direction);
        if (menu == 9)
            host.ChangePlayerDevice(direction);
        if (menu is 2 or 4 or 11)
            host.PlaySound(28);

        switch (menu)
        {
            case 2:
            case 11:
                if (selection == 4)
                {
                    host.ChangeVolume(direction * 12, 0);
                    if (state.MusicDisabled)
                    {
                        state.MusicDisabled = false;
                        host.RestartSong();
                    }
                }
                else if (selection == 5)
                {
                    host.ChangeVolume(0, direction * 12);
                    state.SamplesDisabled = false;
                }
                break;
            case 4:
                if (state.UpgradeSelection is 3 or 4)
                {
                    var canChange = direction < 0
                        ? state.LeftPowerAvailable
                        : state.RightPowerAvailable && state.RightPowerAffordable;
                    host.ChangeWeaponPower(direction, canChange);
                }
                break;
        }
    }

    public static void ActivateSelection(byte menu, byte selection, byte upgradeSelection,
        int upgradeItemCount, int[] temporaryWeaponPower, PlayerState player, ref bool keyboardUsed,
        MainMenuActionState menuState, IMainMenuFunctionHost menuHost)
    {
        ArgumentNullException.ThrowIfNull(temporaryWeaponPower);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(menuState);
        ArgumentNullException.ThrowIfNull(menuHost);

        keyboardUsed = true;
        if (menu == 4 && upgradeSelection is 3 or 4)
        {
            if ((uint)upgradeItemCount >= (uint)temporaryWeaponPower.Length)
                throw new ArgumentOutOfRangeException(nameof(upgradeItemCount));
            temporaryWeaponPower[upgradeItemCount] = player.Items.Weapons[upgradeSelection - 3].Power;
        }

        MainMenuFunction.Dispatch(menu, selection, selection, menuState, menuHost);
    }

    public static void AdjustWeaponPowerOnMouse(byte menu, int mouseX, int mouseY,
        byte submenuSelection, ItemScreenHorizontalState state, int[] temporaryWeaponPower,
        PlayerState player, Action<byte> playSound, Action waitNoInput)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(temporaryWeaponPower);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(playSound);
        ArgumentNullException.ThrowIfNull(waitNoInput);
        if (menu != 4 || state.UpgradeSelection is not (3 or 4))
            return;

        if (mouseX is >= 23 and <= 36 && mouseY is >= 149 and <= 168)
        {
            playSound(28);
            MainMenuActionHandlers.ChangeUpgradeWeaponPower(state.UpgradeSelection,
                submenuSelection, -1, state.LeftPowerAvailable, temporaryWeaponPower, player, playSound);
            waitNoInput();
        }

        if (mouseX is >= 119 and <= 131 && mouseY is >= 149 and <= 168)
        {
            playSound(28);
            MainMenuActionHandlers.ChangeUpgradeWeaponPower(state.UpgradeSelection,
                submenuSelection, 1, state.RightPowerAvailable && state.RightPowerAffordable,
                temporaryWeaponPower, player, playSound);
            waitNoInput();
        }
    }

    public static void CancelSelection(byte menu, byte escapeDestination, MainMenuActionState menuState,
        PlayerState player, ref byte submenuSelection, ref bool keyboardUsed, Func<ulong> calculateCashLeft,
        Action playSpring, Action requestMenuQuit)
    {
        ArgumentNullException.ThrowIfNull(menuState);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(calculateCashLeft);
        ArgumentNullException.ThrowIfNull(playSpring);
        ArgumentNullException.ThrowIfNull(requestMenuQuit);

        keyboardUsed = true;
        playSpring();
        if (menu == 6 && menuState.QuickSave)
        {
            menuState.CurrentMenu = menuState.OldMenu;
            menuState.NewPalette = menuState.OldPalette;
            return;
        }

        if (escapeDestination == 0)
        {
            requestMenuQuit();
            return;
        }

        if (menu == 4)
        {
            player.Items = player.LastItems.Copy();
            submenuSelection = menuState.LastUpgradeSelection;
            player.Cash = calculateCashLeft();
        }

        if (menu != 8)
            menuState.NewPalette = 1;
        menuState.CurrentMenu = unchecked((byte)(escapeDestination - 1));
    }
}

public sealed class ItemScreenHelpState
{
    public byte NewPalette { get; set; }
    public byte CurrentPalette { get; set; }
    public bool BackFromHelp { get; set; }
}

public sealed class ItemScreenSelectionState
{
    public byte CurrentMenu { get; set; }
    public byte CurrentSelection { get; set; }
    public byte ChoiceCount { get; set; }
    public byte UpgradeSelection { get; set; }
}

public interface IItemScreenSelectionHost
{
    void PlaySound(byte sample);
    ulong GetUpgradeCost(byte upgradeSelection, byte itemId);
    ulong CalculateCashLeft();
    void DispatchMenuFunction(byte menu, byte selection);
}

public static class ItemScreenMenuSelection
{
    public static void SelectOption(int selection, ReadOnlySpan<byte> availableItems,
        int[] temporaryWeaponPower, PlayerState player, ItemScreenSelectionState state,
        IItemScreenSelectionHost host)
    {
        ArgumentNullException.ThrowIfNull(temporaryWeaponPower);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (selection > state.ChoiceCount)
            return;

        if (state.CurrentMenu == 4 && selection == state.ChoiceCount)
        {
            player.Cash = host.CalculateCashLeft();
            state.CurrentMenu = 1;
            host.PlaySound(9);
            return;
        }

        host.PlaySound(8);
        if (state.CurrentSelection == selection)
        {
            host.DispatchMenuFunction(state.CurrentMenu, state.CurrentSelection);
            return;
        }

        if (state.CurrentMenu == 4)
        {
            var itemIndex = selection - 2;
            if ((uint)itemIndex >= (uint)availableItems.Length)
                throw new ArgumentOutOfRangeException(nameof(selection));
            if (host.GetUpgradeCost(state.UpgradeSelection, availableItems[itemIndex]) > player.Cash)
            {
                host.PlaySound(23);
            }
            else
            {
                if (state.UpgradeSelection == 4)
                    player.WeaponMode = 1;
                state.CurrentSelection = unchecked((byte)selection);
            }
        }
        else
        {
            state.CurrentSelection = unchecked((byte)selection);
        }

        if (state.CurrentMenu == 4 && state.UpgradeSelection is 3 or 4)
        {
            var powerIndex = state.CurrentSelection - 2;
            if ((uint)powerIndex >= (uint)temporaryWeaponPower.Length)
                throw new ArgumentOutOfRangeException(nameof(state.CurrentSelection));
            player.Items.Weapons[state.UpgradeSelection - 3].Power =
                unchecked((byte)temporaryWeaponPower[powerIndex]);
        }
    }
}

public sealed class ItemScreenVolumeState
{
    public byte OptionsSelection { get; set; }
    public ushort MusicVolume { get; set; }
    public ushort EffectsVolume { get; set; }
    public bool MusicDisabled { get; set; }
    public bool SamplesDisabled { get; set; }
}

public sealed class ItemScreenKeyState
{
    public byte CurrentMenu { get; set; }
    public byte UpgradeSelection { get; set; }
    public int CubeScrollLocation { get; set; }
    public int CubeLastLine { get; set; }
}

public sealed class ItemScreenMoveSelectionState
{
    public byte CurrentMenu { get; set; }
    public byte CurrentSelection { get; set; }
    public byte ChoiceCount { get; set; }
    public byte UpgradeSelection { get; set; }
    public bool KeyboardUsed { get; set; }
    public int LastDirection { get; set; }
    public int JoystickCount { get; set; }
    public int JoystickConfig { get; set; }
    public IReadOnlyList<bool> JoystickAnalog { get; set; } = [];
}

public static class ItemScreenSelectionMovement
{
    public static void Move(int direction, ItemScreenMoveSelectionState state, PlayerState player,
        int[] temporaryPower, Action<byte> playSound)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(temporaryPower);
        ArgumentNullException.ThrowIfNull(playSound);

        state.KeyboardUsed = true;
        state.LastDirection = direction;
        if (state.CurrentMenu != 8)
            playSound(28);

        state.CurrentSelection = unchecked((byte)(state.CurrentSelection + direction));
        if (state.CurrentSelection < 2)
            state.CurrentSelection = state.ChoiceCount;
        else if (state.CurrentSelection > state.ChoiceCount)
            state.CurrentSelection = 2;

        if (state.CurrentMenu == 4 && state.UpgradeSelection is 3 or 4)
        {
            var weaponIndex = state.UpgradeSelection - 3;
            var powerIndex = state.CurrentSelection - 2;
            player.Items.Weapons[weaponIndex].Power = unchecked((byte)temporaryPower[powerIndex]);
            if (state.UpgradeSelection == 4)
                player.WeaponMode = 1;
        }

        if (state.CurrentMenu == 12 && state.JoystickCount > 0)
        {
            if (state.JoystickConfig < 0 || state.JoystickConfig >= state.JoystickAnalog.Count)
                throw new InvalidDataException("The selected joystick configuration is unavailable.");
            if (!state.JoystickAnalog[state.JoystickConfig])
            {
                if (direction < 0 && state.CurrentSelection == 5)
                    state.CurrentSelection = 3;
                else if (direction > 0 && state.CurrentSelection == 4)
                    state.CurrentSelection = 6;
            }
        }
    }
}

public sealed class ItemScreenMouseInputState
{
    public byte CurrentMenu { get; set; }
    public byte ChoiceCount { get; set; }
    public byte NewPalette { get; set; }
    public int CubeCount { get; set; }
    public int LastDirection { get; set; }
    public int MouseButton { get; set; }
    public int MouseX { get; set; }
    public int MouseY { get; set; }
}

public interface IItemScreenMouseInputHost
{
    (int Button, int X, int Y) GetMousePosition();
    void PlaySound(byte sample);
    void WipeKey();
    void AdjustVolume(int mouseX, int mouseY);
    void SelectMenuOption(byte menu, int selection, int[] temporaryPower);
    void WaitNoInput(bool keyboard, bool mouse, bool joystick);
    void AdjustWeaponPower(byte menu, int mouseX, int mouseY, int[] temporaryPower);
}

public static class ItemScreenMouseInput
{
    public static void Handle(ItemScreenMouseInputState state, int[] temporaryPower,
        IItemScreenMouseInputHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(temporaryPower);
        ArgumentNullException.ThrowIfNull(host);

        state.LastDirection = 1;
        (state.MouseButton, state.MouseX, state.MouseY) = host.GetMousePosition();

        if (state.CurrentMenu == 7 && state.CubeCount == 0)
        {
            state.CurrentMenu = 0;
            host.PlaySound(16);
            state.NewPalette = 1;
            host.WipeKey();
        }

        if (state.CurrentMenu == 8 &&
            state.MouseX > 258 && state.MouseX < 290 && state.MouseY > 159 && state.MouseY < 171)
        {
            state.CurrentMenu = 7;
            host.PlaySound(16);
        }

        if (state.CurrentMenu is 2 or 11)
            host.AdjustVolume(state.MouseX, state.MouseY);

        if (state.MouseY > 20 && state.MouseX > 170 && state.MouseX < 308 && state.CurrentMenu != 8)
        {
            var selection = ItemScreenInputHandlers.GetMouseSelection(
                state.CurrentMenu, state.MouseY, state.ChoiceCount);
            host.SelectMenuOption(state.CurrentMenu, selection, temporaryPower);
            host.WaitNoInput(false, true, false);
        }

        host.AdjustWeaponPower(state.CurrentMenu, state.MouseX, state.MouseY, temporaryPower);
    }
}

public interface IItemScreenKeyHost
{
    void ActivateSelection(int[] temporaryPower);
    void CancelSelection();
    void ShowHelp();
    void MoveSelection(int direction, int[] temporaryPower);
    void HandleHorizontalKey(int direction, int[] temporaryPower);
}

public static class ItemScreenKeyHandler
{
    private const int EqualsKey = 46;
    private const int Minus = 45;
    private const int Slash = 56;
    private const int Space = 44;
    private const int Return = 40;
    private const int KeypadEnter = 88;
    private const int Escape = 41;
    private const int F1 = 58;
    private const int Up = 82;
    private const int Down = 81;
    private const int Home = 74;
    private const int End = 77;
    private const int Left = 80;
    private const int Right = 79;

    public static void Handle(int lastKeyScan, ItemScreenKeyState state, PlayerState player,
        IReadOnlyList<WeaponItem> weaponPorts, int[] temporaryPower, IItemScreenKeyHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(weaponPorts);
        ArgumentNullException.ThrowIfNull(temporaryPower);
        ArgumentNullException.ThrowIfNull(host);

        switch (lastKeyScan)
        {
            case EqualsKey:
            case Minus:
                if (state.CurrentMenu == 4 && state.UpgradeSelection is 3 or 4)
                {
                    var weaponType = state.UpgradeSelection == 3 ? WeaponPort.Front : WeaponPort.Rear;
                    var weapon = player.Items.Weapons[(int)weaponType];
                    var port = GetWeaponPort(weapon.Id, weaponPorts);
                    var powerIndex = weapon.Power - 1;
                    if (powerIndex < 0 || powerIndex >= port.Op[0].Length)
                        throw new InvalidDataException("The selected weapon power is outside its shot-pattern table.");
                    port.Op[0][powerIndex] = unchecked((ushort)(port.Op[0][powerIndex] +
                        (lastKeyScan == EqualsKey ? 1 : -1)));
                }
                break;
            case Slash:
                if (state.CurrentMenu == 4 && state.UpgradeSelection == 4)
                {
                    var rearWeapon = player.Items.Weapons[(int)WeaponPort.Rear];
                    var port = GetWeaponPort(rearWeapon.Id, weaponPorts);
                    player.WeaponMode = unchecked(player.WeaponMode + 1);
                    if (player.WeaponMode > port.OpNum)
                        player.WeaponMode = 1;
                }
                break;
            case Space:
            case Return:
            case KeypadEnter:
                host.ActivateSelection(temporaryPower);
                break;
            case Escape:
                host.CancelSelection();
                break;
            case F1:
                host.ShowHelp();
                break;
            case Up:
                host.MoveSelection(-1, temporaryPower);
                break;
            case Down:
                host.MoveSelection(1, temporaryPower);
                break;
            case Home:
                if (state.CurrentMenu == 8)
                    state.CubeScrollLocation = 0;
                break;
            case End:
                if (state.CurrentMenu == 8)
                    state.CubeScrollLocation = unchecked((state.CubeLastLine - 9) * 12);
                break;
            case Left:
                host.HandleHorizontalKey(-1, temporaryPower);
                break;
            case Right:
                host.HandleHorizontalKey(1, temporaryPower);
                break;
        }
    }

    private static WeaponItem GetWeaponPort(byte itemId, IReadOnlyList<WeaponItem> weaponPorts)
        => itemId < weaponPorts.Count ? weaponPorts[itemId] :
            throw new InvalidDataException("The selected weapon ID exceeds the loaded weapon-port table.");
}

public enum ItemScreenMouseCursor { Normal, Up, Down, Other }

public readonly record struct ItemScreenJoystickDirection(bool Up, bool Down);

public sealed class ItemScreenWaitState
{
    public MainMenuActionState Menu { get; } = new();
    public byte CurrentPalette { get; set; }
    public bool NewKey { get; set; }
    public bool AltSavePressed { get; set; }
    public bool AltLoadPressed { get; set; }
    public bool PageUpPressed { get; set; }
    public bool PageDownPressed { get; set; }
    public bool UpPressed { get; set; }
    public bool DownPressed { get; set; }
    public ItemScreenMouseCursor MouseCursor { get; set; }
    public int MouseButton { get; set; }
    public int MouseX { get; set; }
    public int MouseY { get; set; }
    public bool InputDetected { get; set; }
    public int YChange { get; set; }
    public int YLocation { get; set; }
    public int CubeLastLine { get; set; }
}

public interface IItemScreenWaitHost
{
    void WaitDelay();
    void PushJoysticksAsKeyboard();
    void ServiceEvents(ItemScreenWaitState state);
    (int Button, int X, int Y) GetMousePosition();
}

public static class ItemScreenInputWait
{
    public static bool WaitForInput(ItemScreenWaitState state, IItemScreenWaitHost host,
        IReadOnlyList<ItemScreenJoystickDirection> joystickDirections)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        ArgumentNullException.ThrowIfNull(joystickDirections);
        host.WaitDelay();
        host.PushJoysticksAsKeyboard();
        host.ServiceEvents(state);
        (state.MouseButton, state.MouseX, state.MouseY) = host.GetMousePosition();
        state.InputDetected = state.NewKey || state.MouseButton > 0;

        if (state.Menu.CurrentMenu != 6)
        {
            if (state.AltSavePressed)
                ItemScreenInputHandlers.OpenQuickSave(state.Menu.CurrentMenu, true,
                    state.CurrentPalette, state.Menu);
            if (state.AltLoadPressed)
                ItemScreenInputHandlers.OpenQuickSave(state.Menu.CurrentMenu, false,
                    state.CurrentPalette, state.Menu);
        }

        if (state.Menu.CurrentMenu == 8)
        {
            if (state.MouseButton > 0 && state.MouseCursor != ItemScreenMouseCursor.Normal)
            {
                state.InputDetected = false;
                state.YChange = state.MouseCursor == ItemScreenMouseCursor.Up ? -1 : 1;
            }

            if (state.PageUpPressed)
            {
                state.YChange = -2;
                state.InputDetected = false;
            }
            if (state.PageDownPressed)
            {
                state.YChange = 2;
                state.InputDetected = false;
            }

            var joystickUp = joystickDirections.Any(direction => direction.Up);
            var joystickDown = joystickDirections.Any(direction => direction.Down);
            if (state.UpPressed || joystickUp)
            {
                state.YChange = -1;
                state.InputDetected = false;
            }
            if (state.DownPressed || joystickDown)
            {
                state.YChange = 1;
                state.InputDetected = false;
            }

            if (state.YChange < 0 && state.YLocation == 0)
                state.YChange = 0;
            if (state.YChange > 0 && state.YLocation / 12 > state.CubeLastLine - 10)
                state.YChange = 0;
        }

        return state.InputDetected;
    }
}

public sealed class ItemScreenInitializationState
{
    public byte[] MenuChoices { get; } = new byte[14];
    public byte[] CurrentSelections { get; } = new byte[14];
    public bool ShopSpriteSheetLoaded { get; set; }
    public IndexedSurface? ActiveScreen { get; set; }
    public int CurrentPalette { get; set; }
    public int NewPalette { get; set; }
    public int Color { get; set; }
    public bool GameLoaded { get; set; }
    public int CurrentItemType { get; set; }
    public int Cursor { get; set; }
    public int CurrentItem { get; set; }
    public byte CurrentMenu { get; set; }
    public bool KeyboardUsed { get; set; }
    public bool FirstMenu9 { get; set; }
    public bool BackFromHelp { get; set; }
}

public interface IItemScreenInitializationHost
{
    void LoadShopSpriteSheet(char code);
    void LoadCubes();
    void PlaySong(int song);
    void LoadPicture(IndexedSurface screen, int picture, bool loadPalette);
    void Present();
    void SetPalette(ReadOnlyMemory<byte> colors, int first, int last);
}

public static class ItemScreenInitializer
{
    private static readonly int[] AvailabilityRows = [1, 2, 3, 9, 4, 6, 7];

    public static void Initialize(ItemScreenInitializationState state,
        EpisodeItemAvailabilityState availability, PlayerItems lastItems,
        ReadOnlyMemory<byte> menuChoicesDefault, ReadOnlyMemory<byte> colors,
        IndexedSurface gameScreen, IndexedSurface backgroundScreen,
        IItemScreenInitializationHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(lastItems);
        ArgumentNullException.ThrowIfNull(gameScreen);
        ArgumentNullException.ThrowIfNull(backgroundScreen);
        ArgumentNullException.ThrowIfNull(host);
        if (availability.Items.GetLength(0) != 9 || availability.Items.GetLength(1) != 10 ||
            availability.Counts.Length != 9)
            throw new ArgumentException("Item availability must contain nine rows of ten slots.", nameof(availability));
        if (menuChoicesDefault.Length != state.MenuChoices.Length)
            throw new ArgumentException("The default menu choice table has an incompatible length.", nameof(menuChoicesDefault));
        if (colors.Length < 256 * 3)
            throw new ArgumentException("The palette must contain at least 256 RGB entries.", nameof(colors));
        var copyLength = checked(backgroundScreen.Pitch * backgroundScreen.Height);
        if (gameScreen.Pixels.Length < copyLength)
            throw new ArgumentException("The game screen is too small for the background-screen copy.", nameof(gameScreen));
        for (var row = 0; row < availability.Counts.Length; row++)
            if (availability.Counts[row] > availability.Items.GetLength(1))
                throw new InvalidDataException($"Item availability row {row} exceeds its slot capacity.");

        if (!state.ShopSpriteSheetLoaded)
        {
            host.LoadShopSpriteSheet('1');
            state.ShopSpriteSheetLoaded = true;
        }

        host.LoadCubes();
        state.ActiveScreen = gameScreen;
        menuChoicesDefault.Span.CopyTo(state.MenuChoices);
        host.PlaySong(5);
        host.LoadPicture(gameScreen, 1, false);

        state.CurrentPalette = 1;
        state.NewPalette = 0;
        host.Present();
        host.SetPalette(colors, 0, 255);

        state.Color = 1;
        state.GameLoaded = false;
        state.CurrentItemType = 1;
        state.Cursor = 1;
        state.CurrentItem = 0;
        Array.Fill(state.CurrentSelections, (byte)2);
        state.CurrentMenu = 0;

        var ownedItems = new byte[]
        {
            lastItems.Ship, lastItems.Weapons[0].Id, lastItems.Weapons[1].Id,
            lastItems.Shield, lastItems.Generator, lastItems.Sidekicks[0], lastItems.Sidekicks[1],
        };
        for (var i = 0; i < ownedItems.Length; i++)
        {
            var row = AvailabilityRows[i] - 1;
            var count = availability.Counts[row];
            var slot = 0;
            while (slot < count && availability.Items[row, slot] != ownedItems[i])
                slot++;

            if (slot != count)
                continue;
            if (count > 4)
            {
                availability.Items[row, 4] = ownedItems[i];
                availability.Items[row, 5] = 0;
                availability.Counts[row] = 5;
            }
            else
            {
                availability.Items[row, slot] = ownedItems[i];
                availability.Counts[row] = unchecked((byte)(count + 1));
            }
        }

        gameScreen.Pixels.Span[..copyLength].CopyTo(backgroundScreen.Pixels.Span);
        state.KeyboardUsed = false;
        state.FirstMenu9 = false;
        state.BackFromHelp = false;

        for (var row = 0; row < availability.Counts.Length; row++)
        {
            var count = availability.Counts[row];
            for (var slot = 0; slot + 1 < count; slot++)
            {
                for (var next = slot; next < count; next++)
                {
                    var item = availability.Items[row, slot];
                    var other = availability.Items[row, next];
                    if (item == 0 || (item > other && other != 0))
                    {
                        availability.Items[row, slot] = other;
                        availability.Items[row, next] = item;
                    }
                }
            }
        }
    }
}

public sealed class ItemScreenFramePreparationState
{
    public byte CurrentMenu { get; set; }
    public byte[] CurrentSelections { get; } = new byte[14];
    public byte[] MenuChoices { get; } = new byte[14];
    public bool TwoPlayerMode { get; set; }
    public bool NetworkGame { get; set; }
    public bool OnePlayerAction { get; set; }
    public bool SuperTyrian { get; set; }
    public bool FirstMenu9 { get; set; }
    public bool PaletteChanged { get; set; }
    public bool LeftPowerAvailable { get; set; }
    public bool RightPowerAvailable { get; set; }
    public bool KeyboardUsed { get; set; }
    public byte PlanetAnimation { get; set; }
    public byte CurrentDotNumber { get; set; }
    public byte CurrentDotWait { get; set; }
    public byte PlanetAnimationWait { get; set; }
}

public interface IItemScreenFramePreparationHost
{
    void GetShipInfo();
    void UpdateNavigationScreen();
}

public static class ItemScreenFramePreparation
{
    private static readonly int[] AvailabilityRows = [1, 2, 3, 9, 4, 6, 7];

    public static void Prepare(ItemScreenFramePreparationState state, int[] temporaryWeaponPower,
        EpisodeItemAvailabilityState availability, PlayerState player,
        IndexedSurface gameScreen, IndexedSurface backgroundScreen,
        IItemScreenFramePreparationHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(temporaryWeaponPower);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(gameScreen);
        ArgumentNullException.ThrowIfNull(backgroundScreen);
        ArgumentNullException.ThrowIfNull(host);
        host.GetShipInfo();

        if (state.CurrentMenu == 0)
        {
            if (state.TwoPlayerMode)
                state.CurrentMenu = 9;
            if (state.NetworkGame || state.OnePlayerAction)
                state.CurrentMenu = 10;
            if (state.SuperTyrian)
                state.CurrentMenu = 13;
        }

        state.PaletteChanged = false;
        state.LeftPowerAvailable = false;
        state.RightPowerAvailable = false;

        var copyLength = checked(gameScreen.Pitch * gameScreen.Height);
        if (backgroundScreen.Pixels.Length < copyLength)
            throw new ArgumentException("The background screen is too small for the menu frame copy.", nameof(backgroundScreen));
        if (state.CurrentMenu != 8 || state.FirstMenu9)
            backgroundScreen.Pixels.Span[..copyLength].CopyTo(gameScreen.Pixels.Span);

        if (state.CurrentMenu == 1 && state.CurrentSelections[1] is 3 or 4)
        {
            var selection = state.CurrentSelections[1];
            var weaponIndex = selection - 3;
            var row = AvailabilityRows[selection - 2] - 1;
            var count = availability.Counts[row];
            if (count >= temporaryWeaponPower.Length)
                throw new InvalidDataException("Weapon availability exceeds the temporary power buffer.");
            var weapon = player.Items.Weapons[weaponIndex];
            for (var slot = 0; slot < count; slot++)
                temporaryWeaponPower[slot] = availability.Items[row, slot] == weapon.Id ? weapon.Power : 1;
            temporaryWeaponPower[count] = weapon.Power;
        }

        if (state.CurrentMenu == 3)
        {
            state.KeyboardUsed = false;
            state.PlanetAnimation = 0;
            state.CurrentDotNumber = 0;
            state.CurrentDotWait = 8;
            state.PlanetAnimationWait = 3;
            host.UpdateNavigationScreen();
        }
    }
}

public interface IItemScreenDrawingHost
{
    void DrawHeader();
    void DrawChoices();
    void DrawDataCubeIcon(bool dark, int x, int y, int spriteSheet, int spriteId);
    void DrawLoadSave();
    void DrawKeyboardConfiguration();
    void DrawJoystickConfiguration();
    void DrawUpgradeMenu();
    void DrawStatus();
    void DrawDataCubes();
    void DrawFooter();
}

public static class ItemScreenDrawing
{
    public static void Draw(byte currentMenu, int cubeCount, IItemScreenDrawingHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (currentMenu != 4)
            host.DrawHeader();

        if (currentMenu <= 3 || currentMenu is >= 9 and <= 11 || currentMenu == 13)
            host.DrawChoices();

        if (currentMenu == 0)
        {
            for (var cube = 1; cube <= cubeCount; cube++)
            {
                host.DrawDataCubeIcon(true, 192 + cube * 18, 38, 4, 34);
                host.DrawDataCubeIcon(false, 190 + cube * 18, 37, 4, 34);
            }
        }

        host.DrawLoadSave();
        host.DrawKeyboardConfiguration();
        host.DrawJoystickConfiguration();
        host.DrawUpgradeMenu();
        host.DrawStatus();
        host.DrawDataCubes();
        host.DrawFooter();
    }
}

public interface IItemScreenLoopHost
{
    bool ConstantPlay { get; }
    bool NetworkGame { get; }
    bool GameLoaded { get; }
    bool JumpSection { get; }
    bool KeyboardUsed { get; set; }
    bool WaitingPacketReceived { get; }
    bool NetworkSynchronized { get; }
    void Initialize();
    void PrepareFrame(int[] temporaryWeaponPower);
    void Draw();
    void ServiceEvents(bool clearNew);
    void StartCurrentLevel();
    void UpdateFrame();
    bool WaitForInput();
    void HandleInput(int[] temporaryWeaponPower);
    void Present();
    void PrepareWaitingPacket();
    void SendNetworkPacket();
    void NetworkUpdate();
    void NetworkCheck();
    void Delay(int milliseconds);
    void ResetNetworkState();
    void FadeBlack(int steps);
}

public static class ItemScreenLoop
{
    public static void Run(IItemScreenLoopHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        var temporaryWeaponPower = new int[7];
        host.Initialize();

        do
        {
            host.PrepareFrame(temporaryWeaponPower);
            host.Draw();
            host.ServiceEvents(true);
            if (host.ConstantPlay)
            {
                host.StartCurrentLevel();
            }
            else
            {
                do
                {
                    host.UpdateFrame();
                } while (!host.WaitForInput());
            }

            host.KeyboardUsed = false;
            host.HandleInput(temporaryWeaponPower);
        } while (!host.GameLoaded && !host.JumpSection);

        if (host.NetworkGame)
        {
            host.PrepareWaitingPacket();
            host.SendNetworkPacket();
            while (true)
            {
                host.ServiceEvents(false);
                host.Present();
                if (host.WaitingPacketReceived)
                {
                    host.NetworkUpdate();
                    break;
                }

                host.NetworkUpdate();
                host.NetworkCheck();
                host.Delay(16);
            }

            host.ResetNetworkState();
            while (!host.NetworkSynchronized)
            {
                host.ServiceEvents(false);
                host.Present();
                host.NetworkCheck();
                host.Delay(16);
            }
        }

        if (host.GameLoaded)
            host.FadeBlack(10);
    }
}

public sealed record ItemScreenReadingCube(int LastLine, IReadOnlyList<ReadOnlyMemory<byte>> Lines);

public sealed class DataCubeText
{
    public string Title { get; internal set; } = string.Empty;
    public string Header { get; internal set; } = string.Empty;
    public int FaceSprite { get; internal set; }
    public string[] Lines { get; } = new string[90];
    public int LastLine { get; internal set; }

    public DataCubeText() => Array.Fill(Lines, string.Empty);
}

public static class DataCubeTextLoader
{
    private const int TinyFont = 2;
    private const int LineWidth = 150;
    private const int LineCharacters = 35;
    private const int PascalBufferSize = 256;

    public static IReadOnlyList<DataCubeText> LoadCubes(IReadOnlyList<int> cubeIndices,
        Func<Stream> openArchive, Func<ReadOnlyMemory<byte>, int> measureTinyText)
    {
        ArgumentNullException.ThrowIfNull(cubeIndices);
        ArgumentNullException.ThrowIfNull(openArchive);
        ArgumentNullException.ThrowIfNull(measureTinyText);
        var cubes = new DataCubeText[cubeIndices.Count];
        for (var slot = 0; slot < cubes.Length; slot++)
            cubes[slot] = LoadCube(openArchive, cubeIndices[slot], measureTinyText);
        return Array.AsReadOnly(cubes);
    }

    public static DataCubeText LoadCube(Func<Stream> openArchive, int cubeIndex,
        Func<ReadOnlyMemory<byte>, int> measureTinyText)
    {
        ArgumentNullException.ThrowIfNull(openArchive);
        ArgumentNullException.ThrowIfNull(measureTinyText);
        if (cubeIndex <= 0)
            throw new ArgumentOutOfRangeException(nameof(cubeIndex), "Native data-cube indices are one-based.");

        using var stream = openArchive() ?? throw new IOException("The data-cube archive opener returned no stream.");
        var record = new DataCubeText();
        var marker = string.Empty;
        for (var skipped = 0; skipped < cubeIndex; )
        {
            marker = ReadPascalCString(stream);
            if (marker.StartsWith('*'))
                skipped++;
        }

        record.FaceSprite = checked(ParseIntegerAt(marker, 4) - 1);
        record.Title = ReadPascalCString(stream);
        record.Header = ReadPascalCString(stream);

        var line = 0;
        var lineCharacters = 0;
        var lineWidth = 0;
        while (true)
        {
            var text = ReadPascalCString(stream);
            if (text.StartsWith('*'))
                return record;

            if (text.Length == 0)
            {
                line = unchecked(line + (lineCharacters == 0 ? 4 : 1));
                lineCharacters = 0;
                lineWidth = 0;
                continue;
            }

            var wordStart = 0;
            for (var index = 0; ; index++)
            {
                var endOfLine = index == text.Length;
                var endOfWord = endOfLine || text[index] == ' ';
                if (endOfWord)
                {
                    var word = text[wordStart..index];
                    wordStart = index + 1;
                    var wordBytes = Encoding.Latin1.GetBytes(word);
                    var wordWidth = measureTinyText(wordBytes);
                    if (word.Length > LineCharacters || wordWidth > LineWidth)
                        break;

                    var prependSpace = true;
                    lineCharacters = unchecked(lineCharacters + word.Length + 1);
                    lineWidth = unchecked(lineWidth + wordWidth + 6);
                    if (lineCharacters > LineCharacters || lineWidth > LineWidth)
                    {
                        line = unchecked(line + 1);
                        lineCharacters = word.Length;
                        lineWidth = wordWidth;
                        prependSpace = false;
                    }

                    if (line < record.Lines.Length)
                    {
                        if (prependSpace)
                            record.Lines[line] += " ";
                        record.Lines[line] += word;
                        record.LastLine = line + 1;
                        if (Encoding.Latin1.GetByteCount(record.Lines[line]) > LineCharacters)
                            throw new InvalidDataException($"Wrapped data-cube line {line} exceeds its native buffer.");
                    }
                }

                if (endOfLine)
                    break;
            }
        }
    }

    private static string ReadPascalCString(Stream stream)
    {
        Span<byte> length = stackalloc byte[1];
        BinaryFile.ReadExactly(stream, length);
        Span<byte> text = stackalloc byte[length[0]];
        BinaryFile.ReadExactly(stream, text);
        EncryptedText.Decrypt(text);
        var nul = text.IndexOf((byte)0);
        return Encoding.Latin1.GetString(nul < 0 ? text : text[..nul]);
    }

    private static int ParseIntegerAt(string text, int offset)
    {
        if (offset >= text.Length)
            throw new InvalidDataException("Data-cube marker has no face-sprite index.");
        var index = offset;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        var start = index;
        if (index < text.Length && text[index] is '+' or '-')
            index++;
        var digits = index;
        while (index < text.Length && text[index] is >= '0' and <= '9')
            index++;
        if (index == digits || !int.TryParse(text.AsSpan(start, index - start),
                System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture, out var value))
            throw new InvalidDataException("Data-cube marker has an invalid face-sprite index.");
        return value;
    }
}

public sealed class ItemScreenFrameUpdateState
{
    public const byte NextLevelMenu = 3;
    public const byte UpgradeSubmenu = 4;
    public const byte DataCubeMenu = 7;
    public const byte DataCubeReadingMenu = 8;
    public byte CurrentMenu { get; set; }
    public byte UpgradeSelection { get; set; }
    public byte DataCubeSelection { get; set; }
    public int CurrentCube { get; set; }
    public byte[] MenuChoices { get; } = new byte[14];
    public int MouseX { get; set; }
    public int MouseY { get; set; }
    public int MouseCursor { get; set; }
    public int Color { get; set; }
    public int ColorDirection { get; set; }
    public int ScrollLocation { get; set; }
    public int ScrollChange { get; set; }
    public bool BackFromHelp { get; set; }
    public bool PaletteChanged { get; set; }
    public bool ExtraGame { get; set; }
    public bool LeftPower { get; set; }
    public bool RightPower { get; set; }
    public bool RightPowerAffordable { get; set; }
    public int CurrentPalette { get; set; }
    public int NewPalette { get; set; }
    public string ReadPercentLabel { get; set; } = string.Empty;
    public string DoneLabel { get; set; } = string.Empty;
    public string ExtraGameLabel { get; set; } = string.Empty;
    public byte[] Colors { get; set; } = [];
    public byte[][] Palettes { get; set; } = [];
    public IReadOnlyList<ItemScreenReadingCube> Cubes { get; set; } = [];
}

public interface IItemScreenFrameUpdateHost
{
    void KeepAlive();
    void FillRectangle(int x1, int y1, int x2, int y2, int color);
    void DrawReadingLine(int x, int y, ReadOnlyMemory<byte> text, int color, int brightness, int font);
    void DrawPercentage(int x, int y, string text, int color, int brightness, int font);
    void DrawString(int x, int y, string text, int font);
    void DrawNavigationScreen();
    void DrawMainMenuHelpText();
    void DrawMenuHeader();
    void DrawMenuChoices();
    void DrawCubeFlash(int x, int y, int spriteSheet, int spriteId, int color, int brightness);
    void UpdateWeaponSimulation();
    void DrawScore();
    void ServiceEvents(bool clear);
    void SetPalette(ReadOnlyMemory<byte> palette, int first, int last);
    void MouseStart();
    void Present();
    void FadePalette(ReadOnlyMemory<byte> colors, int steps, int first, int last);
    void MouseReplace();
    void SetDelay(int ticks);
}

public static class ItemScreenFrameUpdate
{
    private const int MouseNormal = 0;
    private const int MouseUp = 1;
    private const int MouseDown = 2;
    private const int OptionShapes = 4;
    private const int SmallFontShapes = 1;
    private const int TinyFont = 2;
    private const int FontShapes = 0;

    public static void Update(ItemScreenFrameUpdateState state, IItemScreenFrameUpdateHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        host.KeepAlive();
        state.MouseCursor = MouseNormal;
        state.Color = unchecked(state.Color + state.ColorDirection);
        if (state.Color < -2 || state.Color > 6)
            state.ColorDirection = unchecked(-state.ColorDirection);

        if (state.CurrentMenu == ItemScreenFrameUpdateState.DataCubeReadingMenu)
        {
            if (state.MouseX is > 164 and < 299 && state.MouseY is > 47 and < 153)
                state.MouseCursor = state.MouseY > 100 ? MouseDown : MouseUp;
            host.FillRectangle(160, 49, 310, 158, 228);
            if (unchecked(state.ScrollLocation + state.ScrollChange) < 0)
            {
                state.ScrollChange = 0;
                state.ScrollLocation = 0;
            }

            state.ScrollLocation = unchecked(state.ScrollLocation + state.ScrollChange);
            var firstLine = state.ScrollLocation / 12;
            var lineOffset = state.ScrollLocation % 12;
            var y = 50 - lineOffset;
            var cube = GetCube(state, state.DataCubeSelection - 2);
            for (var line = firstLine + 1; line <= firstLine + 10; line++)
            {
                if (line <= cube.LastLine)
                {
                    if ((uint)(line - 1) >= (uint)cube.Lines.Count)
                        throw new InvalidDataException($"Data-cube line {line} is unavailable.");
                    host.DrawReadingLine(161, y, cube.Lines[line - 1], 14, 3, TinyFont);
                    y += 12;
                }
            }

            host.FillRectangle(160, 39, 310, 48, 228);
            host.FillRectangle(160, 157, 310, 166, 228);
            var currentCube = GetCube(state, state.CurrentCube);
            var percentRead = currentCube.LastLine <= 9
                ? 100
                : unchecked(state.ScrollLocation * 100) / ((currentCube.LastLine - 9) * 12);
            var percentage = state.ReadPercentLabel + " " +
                percentRead.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            host.DrawPercentage(176, 160, percentage, 14, 1, TinyFont);
            host.DrawString(260, 160, state.DoneLabel, SmallFontShapes);
            if (lineOffset == 0)
                state.ScrollChange = 0;

            host.MouseStart();
            host.Present();
            if (state.BackFromHelp)
            {
                host.FadePalette(state.Colors, 10, 0, 255);
                state.BackFromHelp = false;
            }
            host.MouseReplace();
            host.SetDelay(1);
            return;
        }

        if (state.CurrentMenu == ItemScreenFrameUpdateState.NextLevelMenu)
        {
            host.DrawNavigationScreen();
            host.DrawMainMenuHelpText();
            host.DrawMenuHeader();
            host.DrawMenuChoices();
            if (state.ExtraGame)
                host.DrawString(170, 140, state.ExtraGameLabel, FontShapes);
        }

        if (state.CurrentMenu == ItemScreenFrameUpdateState.DataCubeMenu &&
            state.DataCubeSelection < state.MenuChoices[ItemScreenFrameUpdateState.DataCubeMenu])
        {
            host.DrawCubeFlash(166,
                38 + (state.DataCubeSelection - 2) * 28, OptionShapes, 25, 13, state.Color);
        }

        if (IsWeaponSimulation(state))
        {
            host.SetDelay(3);
            host.UpdateWeaponSimulation();
            host.DrawScore();
            host.ServiceEvents(false);
            ApplySelectedPalette(state, host);
            host.MouseStart();
            RestorePaletteIfChanged(state, host);
            host.Present();
            FadeBackFromHelp(state, host);
            host.MouseReplace();
        }
        else
        {
            host.SetDelay(2);
            host.DrawScore();
            ApplySelectedPalette(state, host);
            host.MouseStart();
            RestorePaletteIfChanged(state, host);
            host.Present();
            FadeBackFromHelp(state, host);
            host.MouseReplace();
        }
    }

    private static bool IsWeaponSimulation(ItemScreenFrameUpdateState state)
        => state.CurrentMenu == ItemScreenFrameUpdateState.UpgradeSubmenu &&
            (state.UpgradeSelection is 3 or 4 or >= 6 and <= 8);

    private static void ApplySelectedPalette(ItemScreenFrameUpdateState state,
        IItemScreenFrameUpdateHost host)
    {
        if (state.NewPalette <= 0)
            return;
        var index = state.NewPalette - 1;
        if ((uint)index >= (uint)state.Palettes.Length)
            throw new InvalidDataException($"Item-screen palette {state.NewPalette} is unavailable.");
        state.CurrentPalette = state.NewPalette;
        host.SetPalette(state.Palettes[index], 0, 255);
        state.NewPalette = 0;
    }

    private static void RestorePaletteIfChanged(ItemScreenFrameUpdateState state,
        IItemScreenFrameUpdateHost host)
    {
        if (!state.PaletteChanged)
            return;
        host.SetPalette(state.Colors, 0, 255);
        state.PaletteChanged = false;
    }

    private static void FadeBackFromHelp(ItemScreenFrameUpdateState state,
        IItemScreenFrameUpdateHost host)
    {
        if (!state.BackFromHelp)
            return;
        host.FadePalette(state.Colors, 10, 0, 255);
        state.BackFromHelp = false;
    }

    private static ItemScreenReadingCube GetCube(ItemScreenFrameUpdateState state, int index)
    {
        if ((uint)index >= (uint)state.Cubes.Count)
            throw new InvalidDataException($"Data-cube selection {index + 2} has no text record.");
        return state.Cubes[index];
    }
}

public sealed class ItemScreenStatusState
{
    public byte CurrentMenu { get; set; }
    public byte[] CurrentSelections { get; } = new byte[14];
    public bool TwoPlayerMode { get; set; }
    public bool SuperTyrian { get; set; }
    public int SuperArcadeMode { get; set; }
    public int SuperArcadeNone { get; set; }
    public int SuperArcadeShipOffset { get; set; }
    public string[] PlayerCashLabels { get; init; } = ["", ""];
    public string[] SuperShipLabels { get; init; } = [];
    public string[] FrontWeaponNames { get; init; } = [];
    public string[] SpecialNames { get; init; } = [];
    public int[] ShieldMaximumPower { get; init; } = [];
    public bool MusicDisabled { get; set; }
    public bool SamplesDisabled { get; set; }
    public ushort MusicVolume { get; set; }
    public ushort EffectsVolume { get; set; }
    public int HelpBoxColor { get; set; }
    public int HelpBoxBrightness { get; set; } = 1;
}

public interface IItemScreenStatusHost
{
    void DrawTextShade(int x, int y, string text, int font, int shade, int darken);
    void DrawBar(int x, int y, int color, int height, int value, int shade, int darken);
    void DrawHelpBox(int x, int y, string text, int width, int color, int brightness);
    void DrawShipIllustration();
}

public static class ItemScreenStatusDrawing
{
    public static void Draw(ItemScreenStatusState state, IReadOnlyList<PlayerState> players,
        IItemScreenStatusHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(players);
        ArgumentNullException.ThrowIfNull(host);
        if (players.Count < 2)
            throw new ArgumentException("Item-screen status requires both player states.", nameof(players));

        var menu = state.CurrentMenu;
        var upgradeSelection = state.CurrentSelections[1];
        if (((menu <= 2 || menu is 5 or 6 || menu >= 10) && !state.TwoPlayerMode) ||
            (menu == 4 && upgradeSelection is >= 1 and <= 6))
        {
            if (menu != 4)
                host.DrawTextShade(65, 173, FormatCash(players[0].Cash), 1, 6, 0);
            var shield = players[0].Items.Shield;
            if (shield >= state.ShieldMaximumPower.Length)
                throw new InvalidDataException($"Shield item {shield} has no maximum-power record.");
            host.DrawBar(42, 152, 3, 14, checked((int)players[0].Armor), 2, 13);
            host.DrawBar(104, 152, 2, 14, checked(state.ShieldMaximumPower[shield] * 2), 2, 13);
        }

        if ((menu <= 2 || menu is 5 or 6 || menu >= 9) ||
            (menu == 4 && upgradeSelection is 2 or 5))
        {
            if (state.TwoPlayerMode)
            {
                for (var player = 0; player < 2; player++)
                    host.DrawTextShade(25, 50 + 10 * player,
                        $"{state.PlayerCashLabels[player]} {FormatCash(players[player].Cash)}",
                        15, 0, 0);
            }
            else if (state.SuperArcadeMode != state.SuperArcadeNone || state.SuperTyrian)
            {
                var shipIndex = state.SuperTyrian
                    ? checked(state.SuperArcadeMode + state.SuperArcadeShipOffset)
                    : state.SuperArcadeMode;
                state.HelpBoxColor = 15;
                state.HelpBoxBrightness = 4;
                host.DrawHelpBox(35, 25, GetLabel(state.SuperShipLabels, shipIndex), 18,
                    state.HelpBoxColor, state.HelpBoxBrightness);
                state.HelpBoxBrightness = 1;
                host.DrawTextShade(25, 50,
                    GetLabel(state.SuperShipLabels, state.SuperArcadeNone + 1), 15, 0, 0);
                var weapon = players[0].Items.Weapons[0].Id;
                host.DrawHelpBox(25, 60, GetLabel(state.FrontWeaponNames, weapon), 22,
                    state.HelpBoxColor, state.HelpBoxBrightness);
                host.DrawTextShade(25, 120,
                    GetLabel(state.SuperShipLabels, state.SuperArcadeNone + 2), 15, 0, 0);
                host.DrawHelpBox(25, 130,
                    GetLabel(state.SpecialNames, players[0].Items.Special), 22,
                    state.HelpBoxColor, state.HelpBoxBrightness);
            }
            else
            {
                host.DrawShipIllustration();
            }
        }

        if (menu is 2 or 11)
        {
            host.DrawBar(225, 70, 1, state.MusicDisabled ? 12 : 16,
                state.MusicVolume / 12, 3, 13);
            host.DrawBar(225, 86, 1, state.SamplesDisabled ? 12 : 16,
                state.EffectsVolume / 12, 3, 13);
        }
    }

    private static string FormatCash(ulong cash)
    {
        var value = OperatingSystem.IsWindows() || IntPtr.Size == 4 ? unchecked((uint)cash) : cash;
        return value.ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string GetLabel(IReadOnlyList<string> labels, int index)
        => (uint)index < (uint)labels.Count
            ? labels[index]
            : throw new InvalidDataException($"Item-screen status label {index} is unavailable.");
}

public sealed record ItemScreenDataCube(string Title, string Header, int FaceSprite);

public sealed class ItemScreenDataCubeState
{
    public const byte DataCubeMenu = 7;
    public const byte DataCubeSubmenu = 8;

    public byte CurrentMenu { get; set; }
    public bool FirstMenu9 { get; set; }
    public bool BackFromHelp { get; set; }
    public int CubeCount { get; set; }
    public string NoDataCubesText { get; set; } = string.Empty;
    public string DoneText { get; set; } = string.Empty;
    public byte[] MenuChoices { get; } = new byte[14];
    public byte[] CurrentSelections { get; } = new byte[14];
    public IReadOnlyList<ItemScreenDataCube> Cubes { get; set; } = [];
    public byte[] Colors { get; set; } = [];
    public byte[][] Palettes { get; set; } = [];
    public byte[] FacePalettes { get; set; } = [];
    public bool PaletteChanged { get; set; }
    public int NewPalette { get; set; }
    public int TemporaryWord { get; set; }
    public int TemporaryWord2 { get; set; }
    public int TemporaryByte { get; set; }
    public int HelpBoxColor { get; set; }
    public int HelpBoxBrightness { get; set; } = 1;
    public int HelpBoxShadeType { get; set; }
}

public interface IItemScreenDataCubeHost
{
    void FillRectangle(int x1, int y1, int x2, int y2, int color);
    void DrawPortraitBackground(int x, int y, int spriteSheet, int spriteId);
    void DrawHelpBox(int x, int y, string text, int width, int color, int brightness, int shadeType);
    void DrawCube(int x, int y, int filter, int brightness);
    void DrawTextShade(int x, int y, string text, int color, int brightness, int shadeType);
    (int Width, int Height) GetFaceSpriteSize(int spriteSheet, int spriteId);
    void DrawFaceSprite(int x, int y, int spriteSheet, int spriteId);
}

public static class ItemScreenDataCubeDrawing
{
    private const int OptionShapes = 4;
    private const int FaceShapes = 12;
    private const int Darken = 0;

    public static void Draw(ItemScreenDataCubeState state, IItemScreenDataCubeHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.CubeCount < 0 || state.CubeCount > state.Cubes.Count)
            throw new ArgumentOutOfRangeException(nameof(state), "The data-cube count exceeds its records.");

        if (state.CurrentMenu != ItemScreenDataCubeState.DataCubeMenu &&
            (state.CurrentMenu != ItemScreenDataCubeState.DataCubeSubmenu ||
             (!state.FirstMenu9 && !state.BackFromHelp)))
            return;

        state.FirstMenu9 = false;
        state.MenuChoices[ItemScreenDataCubeState.DataCubeMenu] =
            unchecked((byte)(state.CubeCount + 2));
        host.FillRectangle(1, 1, 145, 170, 0);
        host.DrawPortraitBackground(1, 1, OptionShapes, 20);

        if (state.CurrentMenu == ItemScreenDataCubeState.DataCubeMenu)
        {
            if (state.CubeCount == 0)
            {
                host.DrawHelpBox(166, 80, state.NoDataCubesText, 30,
                    state.HelpBoxColor, state.HelpBoxBrightness, state.HelpBoxShadeType);
                state.TemporaryWord = 160;
                state.TemporaryWord2 = 252;
            }
            else
            {
                for (var cube = 1; cube <= state.CubeCount; cube++)
                {
                    host.DrawCube(166, 38 + (cube - 1) * 28, 13, 0);
                    state.TemporaryWord2 =
                        cube + 1 == state.CurrentSelections[state.CurrentMenu] ? 252 : 250;
                    state.HelpBoxColor = state.TemporaryWord2 / 16;
                    state.HelpBoxBrightness = state.TemporaryWord2 % 16 - 8;
                    state.HelpBoxShadeType = Darken;
                    host.DrawHelpBox(192, 44 + (cube - 1) * 28,
                        state.Cubes[cube - 1].Title, 24, state.HelpBoxColor,
                        state.HelpBoxBrightness, state.HelpBoxShadeType);
                }

                var finalCube = state.CubeCount + 1;
                state.TemporaryWord2 =
                    finalCube + 1 == state.CurrentSelections[state.CurrentMenu] ? 252 : 250;
                state.TemporaryWord = 44 + (finalCube - 1) * 28;
            }

            host.DrawTextShade(172, state.TemporaryWord, state.DoneText,
                state.TemporaryWord2 / 16, state.TemporaryWord2 % 16 - 8, Darken);
        }

        var selection = state.CurrentSelections[ItemScreenDataCubeState.DataCubeMenu];
        if (selection >= state.MenuChoices[ItemScreenDataCubeState.DataCubeMenu])
            return;

        var cubeIndex = selection - 2;
        if ((uint)cubeIndex >= (uint)state.Cubes.Count)
            throw new InvalidDataException($"Data-cube selection {selection} has no record.");
        var faceSprite = state.Cubes[cubeIndex].FaceSprite;
        if (faceSprite == -1)
            return;
        if ((uint)faceSprite >= (uint)state.FacePalettes.Length)
            throw new InvalidDataException($"Data-cube face sprite {faceSprite} has no palette.");
        if (state.Colors.Length <= 207 ||
            state.FacePalettes[faceSprite] >= state.Palettes.Length ||
            state.Palettes[state.FacePalettes[faceSprite]].Length <= 207)
            throw new InvalidDataException("Data-cube face palette data is incomplete.");

        var (width, height) = host.GetFaceSpriteSize(FaceShapes, faceSprite);
        if (width < 0 || height < 0)
            throw new InvalidDataException("Data-cube face sprite dimensions cannot be negative.");
        var faceX = 77 - width / 2;
        var faceY = 92 - height / 2;
        host.DrawFaceSprite(faceX, faceY, FaceShapes, faceSprite);
        state.PaletteChanged = true;
        state.TemporaryWord2 = state.FacePalettes[faceSprite];
        state.NewPalette = 0;
        for (state.TemporaryByte = 1; state.TemporaryByte <= 207; state.TemporaryByte++)
            state.Colors[state.TemporaryByte] =
                state.Palettes[state.TemporaryWord2][state.TemporaryByte];
    }
}

public sealed class ItemScreenFooterState
{
    public byte CurrentMenu { get; set; }
    public byte[] InputDevices { get; } = [1, 2];
    public string[] InputDeviceNames { get; set; } = ["Keyboard", "Mouse", "Joystick"];
    public int JoystickCount { get; set; }
    public bool Flash { get; set; }
    public byte[] ShotMultiPositions { get; } = new byte[11];
    public int CurrentPalette { get; set; }
    public int NewPalette { get; set; }
    public byte[] Colors { get; set; } = [];
    public byte[][] Palettes { get; set; } = [];
    public byte[] MenuChoices { get; } = new byte[14];
    public byte[] CurrentSelections { get; } = new byte[14];
    public IReadOnlyList<ItemScreenDataCube> Cubes { get; set; } = [];
}

public interface IItemScreenFooterHost
{
    void DrawInputDevice(int x, int y, string text, int font);
    void DrawScore();
    void DrawMainMenuHelpText();
    void SetPalette(ReadOnlyMemory<byte> palette, int first, int last);
    int TextWidth(string text, int font);
    void DrawTextShade(int x, int y, string text, int color, int brightness, int shadeType);
}

public static class ItemScreenFooterDrawing
{
    private const byte TwoPlayerArcadeMenu = 9;
    private const byte DataCubeMenu = 7;
    private const byte DataCubeSubmenu = 8;
    private const int SmallFont = 1;
    private const int TinyFont = 2;
    private const int Darken = 0;

    public static void Draw(ItemScreenFooterState state, IItemScreenFooterHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.JoystickCount < 0)
            throw new ArgumentOutOfRangeException(nameof(state), "Joystick count cannot be negative.");

        if (state.CurrentMenu == TwoPlayerArcadeMenu)
        {
            if (state.InputDevices.Length < 2)
                throw new InvalidDataException("Two-player item-screen input requires two device assignments.");
            for (var player = 0; player < 2; player++)
            {
                if (state.InputDevices[player] > 2 + state.JoystickCount)
                    state.InputDevices[player] = state.InputDevices[player == 0 ? 1 : 0] == 1
                        ? (byte)2
                        : (byte)1;

                var inputDevice = state.InputDevices[player];
                if (inputDevice == 0)
                    throw new InvalidDataException("Item-screen input-device assignments are one-based.");
                string label;
                if (state.JoystickCount > 1 && inputDevice > 2)
                {
                    label = GetInputDeviceLabel(state.InputDeviceNames, 2) + " " +
                        (inputDevice - 2).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    label = GetInputDeviceLabel(state.InputDeviceNames, inputDevice - 1);
                }

                host.DrawInputDevice(186, 38 + 2 * (player + 1) * 16, label, SmallFont);
            }
        }

        state.Flash = false;
        Array.Clear(state.ShotMultiPositions);
        host.DrawScore();
        host.DrawMainMenuHelpText();

        if (state.NewPalette > 0)
        {
            var paletteIndex = state.NewPalette - 1;
            if ((uint)paletteIndex >= (uint)state.Palettes.Length ||
                state.Palettes[paletteIndex].Length < state.Colors.Length ||
                state.Palettes[paletteIndex].Length < 256)
                throw new InvalidDataException($"Item-screen palette {state.NewPalette} is unavailable.");
            state.CurrentPalette = state.NewPalette;
            state.Palettes[paletteIndex].AsSpan(0, state.Colors.Length).CopyTo(state.Colors);
            host.SetPalette(state.Palettes[paletteIndex].AsMemory(), 0, 255);
            state.NewPalette = 0;
        }

        var selection = state.CurrentSelections[DataCubeMenu];
        if ((state.CurrentMenu == DataCubeMenu || state.CurrentMenu == DataCubeSubmenu) &&
            selection < state.MenuChoices[DataCubeMenu])
        {
            var cubeIndex = selection - 2;
            if ((uint)cubeIndex >= (uint)state.Cubes.Count)
                throw new InvalidDataException($"Data-cube selection {selection} has no header.");
            var header = state.Cubes[cubeIndex].Header;
            var x = 75 - host.TextWidth(header, TinyFont) / 2;
            host.DrawTextShade(x, 173, header, 14, 3, Darken);
        }
    }

    private static string GetInputDeviceLabel(IReadOnlyList<string> labels, int index)
        => (uint)index < (uint)labels.Count
            ? labels[index]
            : throw new InvalidDataException($"Item-screen input-device label {index} is unavailable.");
}

public sealed class ItemScreenLoadSaveState
{
    public byte CurrentMenu { get; set; }
    public bool TwoPlayerMode { get; set; }
    public bool PerformSave { get; set; }
    public byte SaveGamePageIndex { get; set; }
    public byte[] CurrentSelections { get; } = new byte[14];
    public byte[] ReturnLabel { get; set; } = [];
    public byte[] EpisodePrefix { get; set; } = [];
    public int TemporaryColor { get; set; }
}

public interface IItemScreenLoadSaveHost
{
    IReadOnlyList<SavedGameMetadata> BuildSaveGameList(byte players);
    void DrawTextShade(int x, int y, ReadOnlyMemory<byte> text, int color, int brightness, int shadeType);
    void DrawMenuHeader();
}

public static class ItemScreenLoadSaveDrawing
{
    private const byte LoadSaveMenu = 6;
    private const int Darken = 0;

    public static void Draw(ItemScreenLoadSaveState state, IItemScreenLoadSaveHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.CurrentMenu != LoadSaveMenu)
            return;

        var savedGames = host.BuildSaveGameList(state.TwoPlayerMode ? (byte)2 : (byte)1)
            ?? throw new InvalidDataException("The save-game list host returned no list.");
        for (var row = 0; row < 12; row++)
        {
            var y = 38 + row * 11;
            var isSelected = row + 2 == state.CurrentSelections[state.CurrentMenu];
            state.TemporaryColor = isSelected ? 15 : 28;
            var saveGameIndex = (int)state.SaveGamePageIndex * 10 + row -
                (state.PerformSave ? 1 : 0);
            var isSaveFile = (uint)saveGameIndex < (uint)savedGames.Count;
            ReadOnlyMemory<byte> name;
            if (state.PerformSave && row == 0)
            {
                isSaveFile = false;
                name = "Create New Save"u8.ToArray();
            }
            else if (row == 11)
                name = state.ReturnLabel;
            else if (!isSaveFile)
                name = ReadOnlyMemory<byte>.Empty;
            else
                name = System.Text.Encoding.Latin1.GetBytes(savedGames[saveGameIndex].Name);

            host.DrawTextShade(163, y, name, state.TemporaryColor / 16,
                state.TemporaryColor % 16 - 8, Darken);

            if (row < 11)
            {
                state.TemporaryColor = isSelected ? 252 : 250;
                if (!isSaveFile)
                {
                    host.DrawTextShade(245, y, ReadOnlyMemory<byte>.Empty,
                        state.TemporaryColor / 16, state.TemporaryColor % 16 - 8, Darken);
                }
                else
                {
                    var save = savedGames[saveGameIndex];
                    host.DrawTextShade(297, y, FormatEpisode(state.EpisodePrefix, save.Episode),
                        state.TemporaryColor / 16, state.TemporaryColor % 16 - 8, Darken);
                    host.DrawTextShade(245, y, save.LevelName,
                        state.TemporaryColor / 16, state.TemporaryColor % 16 - 8, Darken);
                }
            }

            host.DrawMenuHeader();
        }
    }

    private static byte[] FormatEpisode(ReadOnlyMemory<byte> prefix, byte episode)
    {
        var suffix = System.Text.Encoding.ASCII.GetBytes(
            episode.ToString(System.Globalization.CultureInfo.InvariantCulture));
        var result = new byte[Math.Min(19, prefix.Length + suffix.Length)];
        var prefixLength = Math.Min(prefix.Length, result.Length);
        prefix.Span[..prefixLength].CopyTo(result);
        suffix.AsSpan(0, Math.Min(suffix.Length, result.Length - prefixLength))
            .CopyTo(result.AsSpan(prefixLength));
        return result;
    }
}

public sealed class ItemScreenKeyboardConfigurationState
{
    public const byte KeyboardConfigurationMenu = 5;
    public byte CurrentMenu { get; set; }
    public byte[] CurrentSelections { get; } = new byte[14];
    public byte[] MenuChoices { get; } = new byte[14];
    public ReadOnlyMemory<byte>[] MenuLabels { get; set; } = [];
    public IReadOnlyList<string> KeyNames { get; set; } = [];
    public int TemporaryColor { get; set; }
}

public interface IItemScreenKeyboardConfigurationHost
{
    void DrawTextShade(int x, int y, ReadOnlyMemory<byte> text, int color, int brightness, int shadeType);
    void DrawKeyName(int x, int y, string name, int color, int brightness, int shadeType);
}

public static class ItemScreenKeyboardConfigurationDrawing
{
    private const int Darken = 0;

    public static void Draw(ItemScreenKeyboardConfigurationState state,
        IItemScreenKeyboardConfigurationHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.CurrentMenu != ItemScreenKeyboardConfigurationState.KeyboardConfigurationMenu)
            return;
        if (state.MenuLabels.Length < 11 || state.KeyNames.Count < 8)
            throw new InvalidDataException("Keyboard-configuration labels or key names are incomplete.");

        var selected = state.CurrentSelections[state.CurrentMenu];
        for (var row = 0; row < 10; row++)
        {
            var choice = row + 2;
            var y = 38 + row * 12;
            var isSelected = choice == selected;
            state.TemporaryColor = isSelected ? 15 : 28;
            host.DrawTextShade(166, y, state.MenuLabels[choice - 1],
                state.TemporaryColor / 16, state.TemporaryColor % 16 - 8, Darken);

            if (choice < 10)
            {
                state.TemporaryColor = isSelected ? 252 : 250;
                host.DrawKeyName(236, y, state.KeyNames[choice - 2],
                    state.TemporaryColor / 16, state.TemporaryColor % 16 - 8, Darken);
            }
        }

        state.MenuChoices[state.CurrentMenu] = 11;
    }
}

public sealed class ItemScreenJoystickConfigurationState
{
    public const byte JoystickConfigurationMenu = 12;
    public byte CurrentMenu { get; set; }
    public byte[] CurrentSelections { get; } = new byte[14];
    public byte[] MenuChoices { get; } = new byte[14];
    public ReadOnlyMemory<byte>[] MenuLabels { get; set; } = [];
    public int JoystickCount { get; set; }
    public int JoystickConfig { get; set; }
    public IReadOnlyList<JoystickState> Joysticks { get; set; } = [];
}

public interface IItemScreenJoystickConfigurationHost
{
    void DrawTextShade(int x, int y, ReadOnlyMemory<byte> text, int color, int brightness, int shadeType);
}

public static class ItemScreenJoystickConfigurationDrawing
{
    private const int Darken = 0;

    public static void Draw(ItemScreenJoystickConfigurationState state,
        IItemScreenJoystickConfigurationHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.CurrentMenu != ItemScreenJoystickConfigurationState.JoystickConfigurationMenu)
            return;
        if (state.JoystickCount < 0 || state.MenuLabels.Length < 11)
            throw new InvalidDataException("Joystick-configuration state is incomplete.");
        if (state.JoystickCount > 0 &&
            (uint)state.JoystickConfig >= (uint)state.Joysticks.Count)
            throw new InvalidDataException("The configured joystick is unavailable.");

        var selectedRow = state.CurrentSelections[state.CurrentMenu] - 2;
        for (var row = 0; row < 16; row++)
        {
            var selected = row == selectedRow;
            var color = selected ? 15 : 28;
            host.DrawTextShade(166, 38 + row * 8, GetMenuItemLabel(state.MenuLabels, row),
                color / 16, color % 16 - 8, Darken);

            color = selected ? 252 : 250;
            string value;
            if (state.JoystickCount == 0 && row < 14)
            {
                value = "-";
            }
            else if (row == 0)
            {
                value = (state.JoystickConfig + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (row == 1)
            {
                value = state.Joysticks[state.JoystickConfig].Analog ? "TRUE" : "FALSE";
            }
            else if (row < 4)
            {
                var joystick = state.Joysticks[state.JoystickConfig];
                if (!joystick.Analog)
                    color -= 3;
                value = (row == 2 ? joystick.Sensitivity : joystick.Threshold)
                    .ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (row < 14)
            {
                var assignments = state.Joysticks[state.JoystickConfig].Assignments;
                Span<JoystickAssignment> pair =
                    [assignments[row - 4, 0], assignments[row - 4, 1]];
                value = JoystickAssignment.Describe(pair);
            }
            else
            {
                value = string.Empty;
            }

            host.DrawTextShade(236, 38 + row * 8, System.Text.Encoding.ASCII.GetBytes(value),
                color / 16, color % 16 - 8, Darken);
        }

        state.MenuChoices[state.CurrentMenu] = 17;
    }

    private static ReadOnlyMemory<byte> GetMenuItemLabel(
        IReadOnlyList<ReadOnlyMemory<byte>> labels, int row)
    {
        var labelIndex = row switch
        {
            0 => -1,
            1 => -2,
            2 => -3,
            3 => -4,
            4 => 1,
            5 => 4,
            6 => 2,
            7 => 3,
            8 => 5,
            9 => 6,
            10 => 7,
            11 => 8,
            12 => -5,
            13 => -6,
            14 => 9,
            15 => 10,
            _ => throw new ArgumentOutOfRangeException(nameof(row))
        };
        return labelIndex switch
        {
            -1 => "JOYSTICK"u8.ToArray(),
            -2 => "ANALOG AXES"u8.ToArray(),
            -3 => " SENSITIVITY"u8.ToArray(),
            -4 => " THRESHOLD"u8.ToArray(),
            -5 => "MENU"u8.ToArray(),
            -6 => "PAUSE"u8.ToArray(),
            _ => labels[labelIndex]
        };
    }
}

public sealed record UpgradeMenuItem(byte Id, string Name, ulong Cost,
    uint SidekickStyle = 0, byte SidekickAnimation = 0);

public sealed class ItemScreenUpgradeMenuState
{
    public const byte UpgradeSubmenu = 4;
    private static readonly (ItemType Type, int AvailabilityRow)[] UpgradeItems =
    [
        (ItemType.Ship, 0),
        (ItemType.WeaponFront, 1),
        (ItemType.WeaponRear, 2),
        (ItemType.Shield, 8),
        (ItemType.Generator, 3),
        (ItemType.SidekickLeft, 5),
        (ItemType.SidekickRight, 6)
    ];

    public byte CurrentMenu { get; set; }
    public byte UpgradeSelection { get; set; }
    public byte SelectedChoice { get; set; }
    public byte ChoiceCount { get; set; }
    public int LastDirection { get; set; }
    public PlayerState Player { get; set; } = new(0);
    public ulong PlayerCash { get => Player.Cash; set => Player.Cash = value; }
    public PlayerItems PlayerItems { get => Player.Items; set => Player.Items = value; }
    public PlayerItems PreviousItems { get; set; } = new();
    public PlayerSidekick[] Sidekicks => Player.Sidekicks;
    public IReadOnlyList<UpgradeMenuItem>[] ChoicesByUpgrade { get; set; } =
        Enumerable.Range(0, 9).Select(_ => (IReadOnlyList<UpgradeMenuItem>)Array.Empty<UpgradeMenuItem>()).ToArray();
    public IReadOnlyList<string> Titles { get; set; } = [];
    public string DoneLabel { get; set; } = string.Empty;
    public bool LeftPower { get; set; }
    public bool RightPower { get; set; }
    public bool RightPowerAffordable { get; set; }

    public static ItemScreenUpgradeMenuState Create(byte currentMenu, byte upgradeSelection,
        byte selectedChoice, int lastDirection, PlayerState player, PlayerItems previousItems,
        ItemDatabase items, EpisodeItemAvailabilityState availability,
        IReadOnlyList<string> titles, string doneLabel)
    {
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(previousItems);
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(availability);
        ArgumentNullException.ThrowIfNull(titles);
        if (upgradeSelection is < 2 or > 8)
            throw new ArgumentOutOfRangeException(nameof(upgradeSelection));
        if (availability.Items.GetLength(0) < 9 || availability.Items.GetLength(1) < 10 ||
            availability.Counts.Length < 9)
            throw new ArgumentException("Item availability does not contain the native shop rows.", nameof(availability));

        var choices = Enumerable.Range(0, 9)
            .Select(_ => (IReadOnlyList<UpgradeMenuItem>)Array.Empty<UpgradeMenuItem>()).ToArray();
        foreach (var (type, row) in UpgradeItems)
        {
            var count = availability.Counts[row];
            if (count >= availability.Items.GetLength(1))
                throw new InvalidDataException($"Shop availability row {row} leaves no slot for the Done entry.");
            var catalog = items.Items(type);
            var rowChoices = new List<UpgradeMenuItem>(count + 1);
            for (var slot = 0; slot < count; slot++)
            {
                var id = availability.Items[row, slot];
                if (id >= catalog.Count || catalog[id] is not NamedItem named)
                {
                    if (type == ItemType.Ship && id > 90)
                    {
                        rowChoices.Add(new UpgradeMenuItem(id, $"Custom Ship {id - 90}", 100));
                        continue;
                    }
                    throw new InvalidDataException($"Shop row {row} contains invalid item index {id} for {type}.");
                }

                var cost = catalog[id] is ShopItem shop
                    ? unchecked((ulong)(long)shop.Cost)
                    : 0;
                var sidekick = catalog[id] as SidekickItem;
                rowChoices.Add(new UpgradeMenuItem(id, named.Name, cost,
                    sidekick?.Tr ?? 0, sidekick?.Ani ?? 0));
            }
            rowChoices.Add(new UpgradeMenuItem(0, string.Empty, 0));
            var upgradeIndex = type switch
            {
                ItemType.Ship => 2,
                ItemType.WeaponFront => 3,
                ItemType.WeaponRear => 4,
                ItemType.Shield => 5,
                ItemType.Generator => 6,
                ItemType.SidekickLeft => 7,
                ItemType.SidekickRight => 8,
                _ => throw new InvalidDataException($"Unsupported upgrade item type {type}.")
            };
            choices[upgradeIndex] = rowChoices;
        }

        var selectedCount = availability.Counts[UpgradeItems[upgradeSelection - 2].AvailabilityRow];
        return new ItemScreenUpgradeMenuState
        {
            CurrentMenu = currentMenu,
            UpgradeSelection = upgradeSelection,
            SelectedChoice = selectedChoice,
            ChoiceCount = checked((byte)(selectedCount + 2)),
            LastDirection = lastDirection,
            Player = player,
            PreviousItems = previousItems,
            ChoicesByUpgrade = choices,
            Titles = titles,
            DoneLabel = doneLabel
        };
    }
}

public interface IItemScreenUpgradeMenuHost
{
    ulong GetUpgradeCost(byte upgradeSelection, byte itemId);
    long CalculateCashLeft(byte upgradeSelection);
    ushort UpgradeCost { get; }
    ushort DowngradeCost { get; }
    void DrawString(int x, int y, string text, int font);
    int FontCenter(string text, int font);
    void DrawShipInfo();
    void FillRectangle(int x1, int y1, int x2, int y2, int color);
    void DrawOwnedMarker(int x, int y, int spriteSheet, int spriteId);
    void DrawTextShade(int x, int y, string text, int font, int shade, int darken);
    void DrawItem(int itemType, byte itemId, int x, int y);
}

public static class ItemScreenUpgradeMenuDrawing
{
    private const int Darken = 0;
    private const int FontShapes = 0;

    public static void Draw(ItemScreenUpgradeMenuState state, IItemScreenUpgradeMenuHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (state.CurrentMenu != ItemScreenUpgradeMenuState.UpgradeSubmenu)
            return;
        if (state.UpgradeSelection is < 2 or > 8 || state.ChoicesByUpgrade.Length <= state.UpgradeSelection ||
            state.Titles.Count <= state.UpgradeSelection - 1)
            throw new InvalidDataException("Upgrade-menu state is incomplete.");

        var choices = state.ChoicesByUpgrade[state.UpgradeSelection];
        if (state.ChoiceCount < 2 || choices.Count < state.ChoiceCount - 1)
            throw new InvalidDataException("Upgrade-menu choices do not match the native menu count.");
        if (state.SelectedChoice < 2)
            throw new InvalidDataException("Upgrade-menu selection is outside the native choice range.");

        while (state.SelectedChoice < state.ChoiceCount &&
            host.GetUpgradeCost(state.UpgradeSelection, GetChoice(choices, state.SelectedChoice - 2).Id) > state.PlayerCash)
        {
            if (state.LastDirection == 0)
                throw new InvalidDataException("An unaffordable upgrade selection cannot advance without a direction.");
            state.SelectedChoice = unchecked((byte)(state.SelectedChoice + state.LastDirection));
            if (state.SelectedChoice < 2)
                state.SelectedChoice = state.ChoiceCount;
            else if (state.SelectedChoice > state.ChoiceCount)
                state.SelectedChoice = 2;
        }

        if (state.SelectedChoice == state.ChoiceCount)
        {
            state.PlayerItems.SetMenuItemId(state.UpgradeSelection - 2,
                state.PreviousItems.GetMenuItemId(state.UpgradeSelection - 2));
        }
        else
        {
            var selectedItem = GetChoice(choices, state.SelectedChoice - 2);
            state.PlayerItems.SetMenuItemId(state.UpgradeSelection - 2, selectedItem.Id);
        }

        if (state.UpgradeSelection is 3 or 4 && state.SelectedChoice < state.ChoiceCount)
        {
            var selectedItem = GetChoice(choices, state.SelectedChoice - 2);
            if (selectedItem.Id != 0)
            {
                var weapon = state.PlayerItems.Weapons[state.UpgradeSelection - 3];
                host.GetUpgradeCost(state.UpgradeSelection, selectedItem.Id);
                state.LeftPower = weapon.Power > 1;
                state.RightPower = weapon.Power < 11;
                if (state.RightPower)
                    state.RightPowerAffordable = host.CalculateCashLeft(state.UpgradeSelection) >= host.UpgradeCost;
            }
            else
            {
                state.LeftPower = false;
                state.RightPower = false;
            }
        }
        else
        {
            state.LeftPower = false;
            state.RightPower = false;
        }

        if (state.UpgradeSelection is 7 or 8)
        {
            var side = state.UpgradeSelection == 7 ? (int)SidekickPort.Left : (int)SidekickPort.Right;
            var selectedSidekick = GetChoiceById(choices, state.PlayerItems.Sidekicks[side]);
            var playerSidekick = state.Sidekicks[side];
            playerSidekick.ChargeTicks = 20;
            playerSidekick.Charge = 0;
            playerSidekick.Style = selectedSidekick.SidekickStyle;
            playerSidekick.AnimationEnabled = selectedSidekick.SidekickAnimation > 1;
            playerSidekick.AnimationFrame = 0;
        }

        var title = state.Titles[state.UpgradeSelection - 1];
        host.DrawString(74 + host.FontCenter(title, FontShapes), 10, title, FontShapes);

        for (var row = 1; row < state.ChoiceCount; row++)
        {
            var y = 40 + (row - 1) * 26;
            var item = GetChoice(choices, row - 1);
            var cost = row < state.ChoiceCount - 1
                ? host.GetUpgradeCost(state.UpgradeSelection, item.Id)
                : 0;
            var affordShade = cost > state.PlayerCash ? 4 : 0;
            var label = item.Name;
            if (state.UpgradeSelection == 2 && item.Id > 90)
                label = $"Custom Ship {item.Id - 90}";
            var shade = row == state.SelectedChoice - 1 ? 15 : 28;

            host.DrawShipInfo();
            if (item.Id == state.PreviousItems.GetMenuItemId(state.UpgradeSelection - 2) &&
                item.Id != 0 && row != state.ChoiceCount - 1)
            {
                host.FillRectangle(160, y + 7, 300, y + 11, 227);
                host.DrawOwnedMarker(298, y + 2, 0, 247);
            }

            if (row == state.ChoiceCount - 1)
                label = state.DoneLabel;
            host.DrawTextShade(185, y, label, shade / 16, shade % 16 - 8 - affordShade, Darken);
            if (row < state.ChoiceCount - 1)
                host.DrawItem(state.UpgradeSelection - 1, item.Id, 160, y - 4);

            shade = row == state.SelectedChoice - 1 ? 15 : 28;
            if (row != state.ChoiceCount - 1)
            {
                var costText = $"Cost: {cost.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                host.DrawTextShade(187, y + 10, costText, shade / 16,
                    shade % 16 - 8 - affordShade, Darken);
            }
        }
    }

    private static UpgradeMenuItem GetChoice(IReadOnlyList<UpgradeMenuItem> choices, int index)
    {
        if ((uint)index >= (uint)choices.Count)
            throw new InvalidDataException($"Upgrade-menu choice {index} is unavailable.");
        return choices[index];
    }

    private static UpgradeMenuItem GetChoiceById(IReadOnlyList<UpgradeMenuItem> choices, byte itemId)
    {
        foreach (var item in choices)
            if (item.Id == itemId)
                return item;
        throw new InvalidDataException($"Sidekick item {itemId} is unavailable.");
    }
}

public interface IItemScreenVolumeHost
{
    void RestartSong();
    void SetVolume(ushort music, ushort effects);
    void PlaySound(byte sample);
}

public static class ItemScreenVolumeAdjustment
{
    public static void Adjust(int mouseX, int mouseY, ItemScreenVolumeState state, IItemScreenVolumeHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (mouseX >= 221 && mouseY is >= 70 and <= 82)
        {
            if (state.MusicDisabled)
            {
                state.MusicDisabled = false;
                host.RestartSong();
            }

            state.OptionsSelection = 4;
            state.MusicVolume = (ushort)Math.Min(((mouseX - 221) / 4) * 12, 255);
        }

        if (mouseX >= 221 && mouseY is >= 86 and <= 98)
        {
            state.SamplesDisabled = false;
            state.OptionsSelection = 5;
            state.EffectsVolume = (ushort)Math.Min(((mouseX - 221) / 4) * 12, 255);
        }

        host.SetVolume(state.MusicVolume, state.EffectsVolume);
        host.PlaySound(28);
    }
}

public interface IItemScreenHelpHost
{
    void FadeBlack(int steps);
    void RunHelpSystem(byte topic);
    void PlaySong(byte song);
    void LoadPicture(byte picture, bool loadPalette);
    void CopyPrimaryToBackground();
    void ApplyPalette(byte palette);
    void Present();
}

public static class ItemScreenHelp
{
    public static void Show(byte currentMenu, bool networkGame, byte buySong,
        ItemScreenHelpState state, IItemScreenHelpHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        if (networkGame)
            return;

        host.FadeBlack(10);
        host.RunHelpSystem(2);
        host.PlaySong(buySong);
        host.LoadPicture(1, false);
        state.NewPalette = 1;
        if (currentMenu == 3)
            state.NewPalette = 18;

        host.CopyPrimaryToBackground();
        state.CurrentPalette = state.NewPalette;
        host.ApplyPalette(state.NewPalette);
        host.Present();
        state.NewPalette = 0;
        state.BackFromHelp = true;
    }
}

public interface ISaveMenuHost
{
    ISaveMenuList BuildSaveGameList(byte players);
    void ShowSaveNamePopup(bool saving);
    void SaveGame(string name);
    void LoadGame(string name);
}

public interface ISaveMenuList : IReadOnlyList<string>, IDisposable { }

public interface IMainMenuFunctionHost
{
    void PlaySound(byte sample);
    void HandleFullGame(byte selection);
    void HandleUpgradeMenu(byte selection);
    void HandleOptions(byte selection);
    void HandleNextLevel(ushort selection);
    void HandleUpgradeSubmenu();
    void HandleKeyboardConfiguration(ushort selection);
    void HandleLoadSave(ushort selection);
    void HandleDataCubes(ushort selection);
    void HandleTwoPlayer();
    void HandleOnePlayer();
    void HandleLimitedOptions(byte selection);
    void HandleJoystickConfiguration(byte selection);
    void HandleSuperTyrian();
    void SnapshotPlayerItems();
}

public static class MainMenuFunction
{
    public static void Dispatch(byte menu, byte selection, ushort currentSelection,
        MainMenuActionState state, IMainMenuFunctionHost host)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(host);
        state.MenuColor = 0;
        state.MenuColorDirection = -1;
        host.PlaySound(8);

        switch (menu)
        {
            case 0: host.HandleFullGame(selection); break;
            case 1: host.HandleUpgradeMenu(selection); break;
            case 2: host.HandleOptions(selection); break;
            case 3: host.HandleNextLevel(currentSelection); break;
            case 4: host.HandleUpgradeSubmenu(); break;
            case 5: host.HandleKeyboardConfiguration(currentSelection); break;
            case 6: host.HandleLoadSave(currentSelection); break;
            case 7: host.HandleDataCubes(currentSelection); break;
            case 8: state.CurrentMenu = 7; break;
            case 9: host.HandleTwoPlayer(); break;
            case 10: host.HandleOnePlayer(); break;
            case 11: host.HandleLimitedOptions(selection); break;
            case 12: host.HandleJoystickConfiguration(selection); break;
            case 13: host.HandleSuperTyrian(); break;
        }

        host.SnapshotPlayerItems();
    }
}

public static class NextLevelMenuSetup
{
    public static NextLevelMenuState Initialize(ReadOnlySpan<byte> route, byte origin,
        IReadOnlyList<ReadOnlyMemory<byte>> planetNames, ReadOnlyMemory<byte> doneLabel)
    {
        ArgumentNullException.ThrowIfNull(planetNames);
        if (route.Length is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(route), "A next-level menu requires one to five destinations.");
        if (origin is 0 or > 21)
            throw new ArgumentOutOfRangeException(nameof(origin));
        if (planetNames.Count < 21)
            throw new ArgumentException("Planet names must include all 21 map entries.", nameof(planetNames));

        var labels = new ReadOnlyMemory<byte>[route.Length + 2];
        labels[0] = "Next Level"u8.ToArray();
        for (var i = 0; i < route.Length; i++)
        {
            var planet = route[i];
            if (planet is 0 or > 21)
                throw new ArgumentOutOfRangeException(nameof(route), $"Planet ID {planet} is outside the navigation map.");
            labels[i + 1] = planetNames[planet - 1];
        }
        labels[^1] = doneLabel;

        var position = NavigationMapDots.GetPlanetPosition(origin - 1);
        return new NextLevelMenuState
        {
            CurrentMenu = 3,
            NewPalette = 18,
            NavigationX = position.X,
            NavigationY = position.Y,
            TargetX = position.X,
            TargetY = position.Y,
            ChoiceCount = (byte)(route.Length + 2),
            SelectedChoice = 2,
            Labels = labels,
            Route = route.ToArray(),
            Dots = NavigationMapDots.Compute(route, origin)
        };
    }
}

public sealed record NavigationDotLayout(byte[] Counts, short[,] X, short[,] Y);

public static class NavigationMapDots
{
    private static readonly ushort[] PlanetX = [200, 150, 240, 300, 270, 280, 320, 260, 220, 150, 160,
        210, 80, 240, 220, 180, 310, 330, 150, 240, 200];
    private static readonly ushort[] PlanetY = [40, 90, 90, 80, 170, 30, 50, 130, 120, 150, 220,
        200, 80, 50, 160, 10, 55, 55, 90, 90, 40];

    internal static (ushort X, ushort Y) GetPlanetPosition(int index)
    {
        if ((uint)index >= (uint)PlanetX.Length)
            throw new ArgumentOutOfRangeException(nameof(index));
        return (PlanetX[index], PlanetY[index]);
    }

    public static NavigationDotLayout Compute(ReadOnlySpan<byte> route, byte origin)
    {
        if (route.Length > 5)
            throw new ArgumentOutOfRangeException(nameof(route), "The native navigation map supports at most five destinations.");
        if (origin is 0 || origin > PlanetX.Length)
            throw new ArgumentOutOfRangeException(nameof(origin));

        var originX = PlanetX[origin - 1];
        var originY = PlanetY[origin - 1];
        var counts = new byte[route.Length];
        var xCoordinates = new short[route.Length, 10];
        var yCoordinates = new short[route.Length, 10];
        for (var i = 0; i < route.Length; i++)
        {
            var destination = route[i];
            if (destination is 0 || destination > PlanetX.Length)
                throw new ArgumentOutOfRangeException(nameof(route), $"Planet ID {destination} is outside the navigation map.");

            var destinationX = PlanetX[destination - 1];
            var destinationY = PlanetY[destination - 1];
            var deltaX = destinationX - originX;
            var deltaY = destinationY - originY;
            var manhattanDistance = Math.Abs(deltaX) + Math.Abs(deltaY);
            var dotCount = manhattanDistance == 0 ? 0 :
                (int)MathF.Round(MathF.Sqrt(MathF.Sqrt(deltaX * deltaX + deltaY * deltaY)),
                    MidpointRounding.AwayFromZero) - 1;
            counts[i] = (byte)Math.Min(dotCount, 10);
            for (var dot = 0; dot < counts[i]; dot++)
            {
                xCoordinates[i, dot] = MainMenuChoiceLayout.PartWay(
                    (short)originX, (short)destinationX, counts[i], (byte)dot);
                yCoordinates[i, dot] = MainMenuChoiceLayout.PartWay(
                    (short)originY, (short)destinationY, counts[i], (byte)dot);
            }
        }

        return new NavigationDotLayout(counts, xCoordinates, yCoordinates);
    }

    public static void Draw(NavigationDotLayout dots, short viewX, short viewY,
        byte selectedChoice, byte currentDotNumber, Action<int, int, int> drawSprite)
    {
        ArgumentNullException.ThrowIfNull(dots);
        ArgumentNullException.ThrowIfNull(drawSprite);
        if (dots.X.GetLength(0) != dots.Counts.Length || dots.Y.GetLength(0) != dots.Counts.Length ||
            dots.X.GetLength(1) < 10 || dots.Y.GetLength(1) < 10)
            throw new ArgumentException("Navigation dot coordinates do not match their counts.", nameof(dots));

        var selectedRoute = selectedChoice - 2;
        for (var route = 0; route < dots.Counts.Length; route++)
        {
            if (dots.Counts[route] > 10)
                throw new ArgumentException("A navigation route exceeds the native dot capacity.", nameof(dots));
            for (var dot = 0; dot < dots.Counts[route]; dot++)
            {
                var x = dots.X[route, dot] - viewX + 64;
                var y = dots.Y[route, dot] - viewY + 83;
                if (x > 0 && x < 140 && y > 0 && y < 168)
                    drawSprite(x, y, route == selectedRoute && dot < currentDotNumber ? 30 : 29);
            }
        }
    }
}

public static class NavigationMapPlanets
{
    private static readonly ushort[] SpriteIds = [4, 1, 2, 3, 20, 36, 52, 68, 84, 100, 116,
        132, 151, 151, 151, 151, 52, 52, 1, 2, 4];
    private static readonly bool[] Animated = [true, false, false, false, true, true, true,
        true, true, true, true, true, false, false, false, false, false, false, false, false, true];

    internal static int GetBaseSprite(byte planetNumber)
    {
        if (planetNumber >= SpriteIds.Length)
            throw new ArgumentOutOfRangeException(nameof(planetNumber));
        return SpriteIds[planetNumber] - 1;
    }

    public static void Draw(byte planetNumber, short viewX, short viewY, byte animationFrame,
        Func<int, (ushort Width, ushort Height)> spriteSize,
        Action<int, int, int, bool> drawDarkSprite, Action<int, int, int> drawSprite)
    {
        ArgumentNullException.ThrowIfNull(spriteSize);
        ArgumentNullException.ThrowIfNull(drawDarkSprite);
        ArgumentNullException.ThrowIfNull(drawSprite);
        if (planetNumber >= SpriteIds.Length)
            throw new ArgumentOutOfRangeException(nameof(planetNumber));

        var baseSprite = GetBaseSprite(planetNumber);
        var size = spriteSize(baseSprite);
        var position = NavigationMapDots.GetPlanetPosition(planetNumber);
        var x = unchecked((short)(position.X + 66 - viewX - size.Width / 2));
        var y = unchecked((short)(position.Y + 85 - viewY - size.Height / 2));

        if (x > -7 && x + size.Width < 170 && y > 0 && y < 160)
        {
            var sprite = baseSprite;
            if (Animated[planetNumber])
                sprite = unchecked((short)(sprite + animationFrame));
            drawDarkSprite(x + 3, y + 3, sprite, false);
            drawSprite(x, y, sprite);
        }
    }
}

public sealed class NavigationMapScreenState
{
    public float X { get; set; }
    public float Y { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }
    public short ViewX { get; internal set; }
    public short ViewY { get; internal set; }
    public byte AnimationFrame { get; set; }
    public byte AnimationWait { get; set; }
    public byte CurrentDotNumber { get; set; }
    public byte CurrentDotWait { get; set; }
}

public static class NavigationMapScreen
{
    public static void Update(NavigationMapScreenState state, ReadOnlySpan<byte> route, byte origin,
        byte choiceCount, byte selectedChoice, NavigationDotLayout dots,
        Action<int, int, int, int, int> fillRectangle,
        Action<int, int, int, int, int> rectangle, Action<int, int, int> pixel,
        Func<int, (ushort Width, ushort Height)> spriteSize,
        Action<int, int, int, bool> drawDarkSprite, Action<int, int, int> drawPlanetSprite,
        Action<int, int, int> drawSprite,
        Action<int, int, int, int> drawInterface)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(dots);
        ArgumentNullException.ThrowIfNull(fillRectangle);
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(pixel);
        ArgumentNullException.ThrowIfNull(spriteSize);
        ArgumentNullException.ThrowIfNull(drawDarkSprite);
        ArgumentNullException.ThrowIfNull(drawPlanetSprite);
        ArgumentNullException.ThrowIfNull(drawSprite);
        ArgumentNullException.ThrowIfNull(drawInterface);
        if (origin is 0 or > 21 || route.Length != dots.Counts.Length || choiceCount < 2)
            throw new ArgumentException("Navigation screen route or origin is invalid.");

        state.ViewX = unchecked((short)MathF.Round(state.X, MidpointRounding.AwayFromZero));
        state.ViewY = unchecked((short)MathF.Round(state.Y, MidpointRounding.AwayFromZero));
        fillRectangle(19, 16, 135, 169, 2);
        NavigationMapGrid.DrawViewport(state.ViewX, state.ViewY, true, rectangle, pixel);
        NavigationMapGrid.DrawViewport(state.ViewX, state.ViewY, false, rectangle, pixel);
        NavigationMapDots.Draw(dots, state.ViewX, state.ViewY, selectedChoice,
            state.CurrentDotNumber, (x, y, sprite) => drawSprite(x, y, sprite));

        for (byte planet = 0; planet < 11; planet++)
            NavigationMapPlanets.Draw(planet, state.ViewX, state.ViewY, state.AnimationFrame,
                spriteSize, drawDarkSprite, drawPlanetSprite);
        for (var index = 0; index < route.Length; index++)
            if (route[index] > 11)
                NavigationMapPlanets.Draw((byte)(route[index] - 1), state.ViewX, state.ViewY,
                    state.AnimationFrame, spriteSize, drawDarkSprite, drawPlanetSprite);
        if (origin > 11)
            NavigationMapPlanets.Draw((byte)(origin - 1), state.ViewX, state.ViewY,
                state.AnimationFrame, spriteSize, drawDarkSprite, drawPlanetSprite);

        drawInterface(0, 0, 4, 28);
        if (selectedChoice < choiceCount)
        {
            var routeIndex = selectedChoice - 2;
            if ((uint)routeIndex >= (uint)route.Length)
                throw new ArgumentOutOfRangeException(nameof(selectedChoice));
            var originPosition = NavigationMapDots.GetPlanetPosition(origin - 1);
            var destinationPosition = NavigationMapDots.GetPlanetPosition(route[routeIndex] - 1);
            var originSize = spriteSize(NavigationMapPlanets.GetBaseSprite((byte)(origin - 1)));
            var destinationSize = spriteSize(NavigationMapPlanets.GetBaseSprite((byte)(route[routeIndex] - 1)));
            state.TargetX = ((originPosition.X - originSize.Width / 2) +
                (destinationPosition.X - destinationSize.Width / 2)) / 2f;
            state.TargetY = ((originPosition.Y - originSize.Height / 2) +
                (destinationPosition.Y - destinationSize.Height / 2)) / 2f;
        }

        state.X += (state.TargetX - state.X) / 2f;
        state.Y += (state.TargetY - state.Y) / 2f;
        if (MathF.Abs(state.TargetX - state.X) < 1) state.X = state.TargetX;
        if (MathF.Abs(state.TargetY - state.Y) < 1) state.Y = state.TargetY;

        fillRectangle(314, 0, 319, 199, 230);
        if (state.AnimationWait > 0)
            state.AnimationWait--;
        else
        {
            state.AnimationFrame = unchecked((byte)(state.AnimationFrame + 1));
            if (state.AnimationFrame > 14) state.AnimationFrame = 0;
            state.AnimationWait = 3;
        }

        if (state.CurrentDotWait > 0)
            state.CurrentDotWait--;
        else
        {
            if (selectedChoice >= 2 && selectedChoice - 2 < dots.Counts.Length &&
                state.CurrentDotNumber < dots.Counts[selectedChoice - 2])
                state.CurrentDotNumber = unchecked((byte)(state.CurrentDotNumber + 1));
            state.CurrentDotWait = 5;
        }
    }
}

public static class NavigationMapGrid
{
    public static void DrawBackground(bool dark, Action<int, int, int, int, int> rectangle,
        Action<int, int, int> pixel)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(pixel);

        for (var x = 15; x <= 300; x += 15)
        {
            var screenX = x + 10;
            if (screenX > 18 && screenX < 135)
                rectangle(screenX + (dark ? 1 : 0), 0, screenX + (dark ? 1 : 0), 199, dark ? 35 : 37);
        }

        for (var y = 15; y <= 300; y += 15)
        {
            var screenY = y;
            if (screenY > 15 && screenY < 169)
            {
                rectangle(0, screenY + (dark ? 1 : 0), 319, screenY + (dark ? 1 : 0), dark ? 35 : 37);
                for (var x = 15; x <= 300; x += 15)
                {
                    var screenX = x + 10;
                    if (screenX > 18 && screenX < 135)
                        pixel(screenX, screenY, 38);
                }
            }
        }
    }

    public static void DrawViewport(short viewX, short viewY, bool dark,
        Action<int, int, int, int, int> rectangle, Action<int, int, int> pixel)
    {
        ArgumentNullException.ThrowIfNull(rectangle);
        ArgumentNullException.ThrowIfNull(pixel);
        var halfX = viewX >> 1;
        var halfY = viewY >> 1;

        for (var x = 1; x <= 20; x++)
        {
            var screenX = x * 15 - halfX;
            if (screenX > 18 && screenX < 135)
                rectangle(screenX + (dark ? 1 : 0), 16, screenX + (dark ? 1 : 0), 169, dark ? 1 : 5);
        }

        for (var y = 1; y <= 20; y++)
        {
            var screenY = y * 15 - halfY;
            if (screenY > 15 && screenY < 169)
            {
                rectangle(dark ? 19 : 8, screenY + (dark ? 1 : 0), dark ? 135 : 160,
                    screenY + (dark ? 1 : 0), dark ? 1 : 5);
                for (var x = 0; x < 20; x++)
                {
                    var screenX = (x + 1) * 15 - halfX;
                    if (screenX > 18 && screenX < 135)
                        pixel(screenX, screenY, 7);
                }
            }
        }
    }
}

public static class SelectionBitmapScaler
{
    public static void Scale(IndexedSurface destination, IndexedSurface source,
        int x1, int y1, int x2, int y2)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);
        if (x1 < 0 || y1 < 0 || x2 < x1 || y2 < y1 ||
            x2 >= source.Pitch || y2 >= source.Height ||
            x2 >= destination.Pitch || y2 >= destination.Height)
            throw new ArgumentOutOfRangeException(nameof(x2), "The scaling rectangle must fit both surfaces and the source pitch.");

        var width = x2 - x1 + 1;
        var height = y2 - y1 + 1;
        var baseSkipWidth = source.Pitch / (float)width;
        var baseSkipHeight = source.Height / (float)height;
        var sourcePixels = source.Pixels.Span;
        var destinationPixels = destination.Pixels.Span;
        var destinationOffset = y1 * destination.Pitch + x1;
        var cumulativeSkipHeight = 0f;

        for (var row = 0; row < height; row++)
        {
            var sourceRowOffset = source.Width * (int)cumulativeSkipHeight;
            cumulativeSkipHeight += baseSkipHeight;
            var cumulativeSkipWidth = 0f;
            for (var column = 0; column < width; column++)
            {
                var sourceOffset = sourceRowOffset + (int)cumulativeSkipWidth;
                destinationPixels[destinationOffset++] = sourcePixels[sourceOffset];
                cumulativeSkipWidth += baseSkipWidth;
            }

            destinationOffset += destination.Pitch - width;
        }
    }
}

public static class ShipSpecificationsPresentation
{
    public static void Run(IndexedSurface display, IndexedSurface gameScreen, IndexedSurface temporaryScreen,
        Action waitNoInput, Action<IndexedSurface, IndexedSurface> drawShipSpecs,
        Action<IndexedSurface, int, bool> loadBackground, Action<byte> playSound,
        Func<bool> anyButton, Action<IndexedSurface> present, Action<int> delay,
        Action waitInput)
    {
        ArgumentNullException.ThrowIfNull(display);
        ArgumentNullException.ThrowIfNull(gameScreen);
        ArgumentNullException.ThrowIfNull(temporaryScreen);
        ArgumentNullException.ThrowIfNull(waitNoInput);
        ArgumentNullException.ThrowIfNull(drawShipSpecs);
        ArgumentNullException.ThrowIfNull(loadBackground);
        ArgumentNullException.ThrowIfNull(playSound);
        ArgumentNullException.ThrowIfNull(anyButton);
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(delay);
        ArgumentNullException.ThrowIfNull(waitInput);

        waitNoInput();
        drawShipSpecs(gameScreen, temporaryScreen);
        loadBackground(temporaryScreen, 1, false);
        playSound(16);
        ScaleInPicture(display, gameScreen, anyButton, present, delay);
        waitInput();
    }

    public static void ScaleInPicture(IndexedSurface destination, IndexedSurface source,
        Func<bool> anyButton, Action<IndexedSurface> present, Action<int> delay)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(anyButton);
        ArgumentNullException.ThrowIfNull(present);
        ArgumentNullException.ThrowIfNull(delay);

        for (var i = 2; i <= 160; i += 2)
        {
            if (anyButton())
                break;

            SelectionBitmapScaler.Scale(destination, source, 160 - i, 0,
                160 + i - 1, 100 + (int)MathF.Round(i * 0.625f, MidpointRounding.AwayFromZero) - 1);
            present(destination);
            delay(1);
        }
    }
}

public static class WeaponViewInitializer
{
    public static void Initialize(IndexedSurface screen, PlayerState player, PlayerShots shots,
        LevelHudState hud, Starfield starfield)
    {
        ArgumentNullException.ThrowIfNull(screen);
        ArgumentNullException.ThrowIfNull(player);
        ArgumentNullException.ThrowIfNull(shots);
        ArgumentNullException.ThrowIfNull(hud);
        ArgumentNullException.ThrowIfNull(starfield);

        screen.FillRectangle(8, 8, 144, 177, 0);
        player.Sidekicks[(int)SidekickPort.Left].X = 72 - 15;
        player.Sidekicks[(int)SidekickPort.Left].Y = 120;
        player.Sidekicks[(int)SidekickPort.Right].X = 72 + 15;
        player.Sidekicks[(int)SidekickPort.Right].Y = 120;
        player.X = 72;
        player.Y = 110;
        player.DeltaXShotMove = 0;
        player.DeltaYShotMove = 0;
        player.LastXExplosionFollow = 72;
        player.LastYExplosionFollow = 110;
        shots.Power = 500;
        hud.LastPower = 500;
        Array.Clear(shots.Availability);
        Array.Fill(shots.Repeat, (byte)1);
        Array.Clear(shots.MultiPosition);
        starfield.Initialize(screen.Pitch);
    }
}

public sealed class SelectionMenuState
{
    public bool OnePlayerAction { get; set; }
    public bool TwoPlayerMode { get; set; }
    public int Episode { get; set; }
    public int InitialEpisode { get; set; }
    public int Difficulty { get; set; } = 2;
}

public sealed class SelectionMenus(InputState input, ISelectionMenuHost host, SelectionMenuState state)
{
    public bool SelectGameplay(IReadOnlyList<ReadOnlyMemory<byte>> names)
        => Select(Kind.Gameplay, names, null);

    public bool SelectEpisode(IReadOnlyList<ReadOnlyMemory<byte>> names, IReadOnlyList<bool> available)
        => Select(Kind.Episode, names, available);

    public bool SelectDifficulty(IReadOnlyList<ReadOnlyMemory<byte>> names)
        => Select(Kind.Difficulty, names, null);

    private enum Kind { Gameplay, Episode, Difficulty }

    private bool Select(Kind kind, IReadOnlyList<ReadOnlyMemory<byte>> names, IReadOnlyList<bool>? available)
    {
        ArgumentNullException.ThrowIfNull(names);
        var count = kind == Kind.Difficulty ? 6 : 4;
        if (names.Count < count + 1)
            throw new ArgumentException("Menu labels must include the header and every native menu entry.", nameof(names));
        if (kind == Kind.Episode && (available == null || available.Count < 4))
            throw new ArgumentException("Episode selection requires four availability flags.", nameof(available));
        host.EnsureCursorSprites();
        var visible = kind == Kind.Difficulty ? 3 : count;
        var selected = kind == Kind.Difficulty ? 1 : 0;
        var lordProgress = 0;
        var widths = new int[count];
        host.LoadBackground();
        host.DrawText(true, 160, 20, names[0], Font.Large, FontAlignment.Centered, -3);
        var restart = true;
        while (true)
        {
            host.RestoreBackground();
            for (var i = 0; i < visible; i++)
            {
                widths[i] = host.TextWidth(names[i + 1]);
                var x = kind == Kind.Episode ? 20 : 160 - widths[i] / 2;
                var y = kind == Kind.Episode ? 50 + 30 * i : 54 + 24 * i;
                var disabled = kind == Kind.Gameplay ? i == 3 : kind == Kind.Episode && !available![i];
                host.DrawText(false, x, y, names[i + 1], Font.Normal, FontAlignment.Left,
                    (sbyte)(-4 + (i == selected ? 2 : 0) + (disabled ? -4 : 0)));
            }
            if (restart)
            {
                host.FadeIn();
                restart = false;
            }
            input.ServiceEvents(true);
            host.PresentWithCursor();
            bool mouseMoved;
            do
            {
                input.Delay(16);
                var oldX = unchecked((ushort)input.MouseX);
                var oldY = unchecked((ushort)input.MouseY);
                host.PushJoysticksAsKeyboard();
                input.ServiceEvents(false);
                if (kind != Kind.Gameplay) host.KeepAlive();
                mouseMoved = input.MouseX != oldX || input.MouseY != oldY;
            } while (!input.NewKey && !input.NewMouse && !mouseMoved);

            if (kind == Kind.Difficulty)
            {
                if (visible == 5)
                {
                    int[] lord = [15, 18, 21, 7];
                    if (lord.All(key => input.KeysActive[key] != 0)) visible = 6;
                    if (input.NewKey)
                    {
                        if (lordProgress < lord.Length && input.LastScancode == lord[lordProgress])
                        {
                            if (++lordProgress == lord.Length) visible = 6;
                        }
                        else lordProgress = 0;
                    }
                }
                if ((host.Modifiers & 3) != 0)
                {
                    if (visible == 4 && input.KeysActive[48] != 0) visible = 5;
                    if (visible == 3 && input.KeysActive[10] != 0) visible = 4;
                }
            }
            var action = false;
            var cancel = false;
            if (mouseMoved || input.NewMouse)
            {
                for (var i = 0; i < visible; i++)
                {
                    var x = kind == Kind.Episode ? 20 : 160 - widths[i] / 2;
                    var y = kind == Kind.Episode ? 50 + 30 * i : 54 + 24 * i;
                    if (input.MouseX < x || input.MouseX >= x + widths[i] ||
                        input.MouseY < y || input.MouseY >= y + 13) continue;
                    if (selected != i)
                    {
                        host.PlaySound(28);
                        selected = i;
                    }
                    if (input.NewMouse && input.LastMouseButton == 1 &&
                        input.LastMouseX >= x && input.LastMouseX < x + widths[i] &&
                        input.LastMouseY >= y && input.LastMouseY < y + 13) action = true;
                    break;
                }
            }
            if (input.NewMouse)
            {
                if (input.LastMouseButton == 3)
                {
                    host.PlaySound(16);
                    cancel = true;
                }
            }
            else if (input.NewKey)
            {
                switch (input.LastScancode)
                {
                    case 82:
                        host.PlaySound(28);
                        selected = selected == 0 ? visible - 1 : selected - 1;
                        break;
                    case 81:
                        host.PlaySound(28);
                        selected = selected == visible - 1 ? 0 : selected + 1;
                        break;
                    case 44 or 40 or 88: action = true; break;
                    case 41: host.PlaySound(16); cancel = true; break;
                }
            }
            if (action)
            {
                if (kind == Kind.Gameplay && selected == 3 ||
                    kind == Kind.Episode && !available![selected])
                {
                    host.PlaySound(16);
                }
                else
                {
                    host.PlaySound(8);
                    if (kind == Kind.Difficulty)
                    {
                        int[] levels = [1, 2, 3, 4, 6, 8];
                        state.Difficulty = levels[selected];
                    }
                    host.FadeBlack(10);
                    if (kind == Kind.Gameplay)
                    {
                        state.OnePlayerAction = selected == 1;
                        state.TwoPlayerMode = selected == 2;
                    }
                    else if (kind == Kind.Episode)
                    {
                        state.Episode = host.InitializeEpisode(selected + 1);
                        state.InitialEpisode = state.Episode;
                    }
                    return true;
                }
            }
            if (cancel)
            {
                host.FadeBlack(15);
                return false;
            }
        }
    }
}

public sealed class SelectionMenuPresentation(
    IndexedSurface screen, IndexedSurface background, FontRenderer fonts, MouseCursor cursor,
    Action ensureCursorSprites, Action loadBackground, Action fadeIn, Action present,
    Action<byte> playSound, Action<int> fadeBlack, Action pushJoysticks, Action keepAlive,
    Func<ushort> modifiers, Func<int, int> initializeEpisode) : ISelectionMenuHost
{
    public void EnsureCursorSprites() => ensureCursorSprites();
    public void LoadBackground() => loadBackground();
    public void RestoreBackground()
    {
        if (screen.Width != background.Width || screen.Height != background.Height || screen.Pitch != background.Pitch)
            throw new ArgumentException("Menu screen and background must have matching dimensions and pitch.");
        background.Pixels.CopyTo(screen.Pixels);
    }
    public int TextWidth(ReadOnlyMemory<byte> text) => fonts.TextWidth(text.Span, Font.Normal);
    public void DrawText(bool onBackground, int x, int y, ReadOnlyMemory<byte> text, Font font,
        FontAlignment alignment, sbyte brightness)
        => fonts.DrawShadow(onBackground ? background : screen, x, y, text.Span, font, alignment, 15, brightness, false, 2);
    public void FadeIn() { cursor.Cursor = 0; fadeIn(); }
    public void PresentWithCursor()
    {
        cursor.Start();
        try { present(); }
        finally { cursor.Replace(); }
    }
    public void PlaySound(byte sample) => playSound(sample);
    public void FadeBlack(int steps) => fadeBlack(steps);
    public void PushJoysticksAsKeyboard() => pushJoysticks();
    public void KeepAlive() => keepAlive();
    public ushort Modifiers => modifiers();
    public int InitializeEpisode(int episode) => initializeEpisode(episode);
}
