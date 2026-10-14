/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class GameRuntimeAssets : IDisposable
{
    public DataFiles Files { get; }
    public EpisodeState Episodes { get; }
    public ItemDatabase Items { get; }
    public PaletteColor[][] Palettes { get; }
    public MainShapeTables Shapes { get; }
    public HelpTextData Help { get; }
    public EditedShips EditedShips { get; }
    public bool ChristmasEnabled { get; }
    private bool _disposed;

    private GameRuntimeAssets(DataFiles files, EpisodeState episodes, ItemDatabase items,
        PaletteColor[][] palettes, MainShapeTables shapes, HelpTextData help, EditedShips editedShips,
        bool christmasEnabled)
    {
        Files = files;
        Episodes = episodes;
        Items = items;
        Palettes = palettes;
        Shapes = shapes;
        Help = help;
        EditedShips = editedShips;
        ChristmasEnabled = christmasEnabled;
    }

    public static GameRuntimeAssets Load(string workingDirectory, string? dataDirectory,
        string userDirectory, bool christmas, TextWriter diagnostics)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(userDirectory);
        ArgumentNullException.ThrowIfNull(diagnostics);

        var files = new DataFiles(workingDirectory, dataDirectory);
        var episodes = new EpisodeState(files);
        episodes.Scan();
        var items = ItemDatabase.Load(workingDirectory);
        var christmasEnabled = christmas && files.Exists(files.Directory, "tyrianc.shp") &&
            files.Exists(files.Directory, "voicesc.snd");
        if (christmas && !christmasEnabled)
            diagnostics.WriteLine("warning: Christmas is missing.");
        PaletteColor[][] palettes;
        using (var stream = files.OpenRequired(files.Directory, "palette.dat"))
            palettes = PaletteSystem.Load(stream);

        var shapes = new MainShapeTables();
        var editedShips = new EditedShips();
        try
        {
            using (var stream = files.OpenRequired(files.Directory, christmasEnabled ? "tyrianc.shp" : "tyrian.shp"))
                shapes.Load(stream);
            var help = new HelpTextData();
            using (var stream = files.OpenRequired(files.Directory, "tyrian.hdt"))
                help.Load(stream);
            editedShips.Load(userDirectory);
            return new(files, episodes, items, palettes, shapes, help, editedShips, christmasEnabled);
        }
        catch
        {
            shapes.Clear();
            editedShips.Shapes.Clear();
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Shapes.Clear();
        EditedShips.Shapes.Clear();
        GC.SuppressFinalize(this);
    }
}
