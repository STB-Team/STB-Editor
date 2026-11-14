using StbEditor.Data;

namespace StbEditor.App.Ui;

internal sealed record UiFontFace(string Family, string Weight, string Path);

/// <summary>Known Windows faces with Cyrillic. Only installed faces are offered; no synthetic bold.</summary>
internal sealed record UiFonts(string? Body, string? Header)
{
    private static readonly string[] HeaderNames = ["SovngardeBold.ttf", "Sovngarde.ttf", "SovngardeLight.ttf"];
    public static readonly UiFontFace[] Faces = InstalledFaces();
    public static readonly string[] Families = Faces.Select(face => face.Family).Distinct().ToArray();

    private static UiFontFace[] InstalledFaces()
    {
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        UiFontFace[] candidates =
        [
            new("Segoe UI", "Regular", "segoeui.ttf"),
            new("Segoe UI", "Semibold", "seguisb.ttf"),
            new("Segoe UI", "Bold", "segoeuib.ttf"),
            new("Arial", "Regular", "arial.ttf"),
            new("Arial", "Bold", "arialbd.ttf"),
            new("Tahoma", "Regular", "tahoma.ttf"),
            new("Tahoma", "Bold", "tahomabd.ttf"),
            new("Verdana", "Regular", "verdana.ttf"),
            new("Verdana", "Bold", "verdanab.ttf"),
            new("Calibri", "Regular", "calibri.ttf"),
            new("Calibri", "Bold", "calibrib.ttf"),
            new("Consolas", "Regular", "consola.ttf"),
            new("Consolas", "Bold", "consolab.ttf"),
        ];
        return candidates.Select(face => face with { Path = Path.Combine(folder, face.Path) })
            .Where(face => File.Exists(face.Path)).ToArray();
    }

    public static UiFontFace? Resolve(string? family, string? weight)
    {
        UiFontFace? first = null;
        foreach (var face in Faces)
        {
            if (face.Family != family) continue;
            first ??= face;
            if (face.Weight == weight) return face;
        }
        return first ?? (Faces.Length > 0 ? Faces[0] : null);
    }

    public static UiFonts Find(GameInstall? game, UiFontFace? face)
    {
        var body = face?.Path;
        if (game is null) return new UiFonts(body, null);

        var folders = new List<string>
        {
            Path.Combine(game.DataFolder, "SKSE", "Plugins", "fonts"),
            Path.Combine(game.DataFolder, "Interface", "fonts"),
        };
        if (game.Mo2 is { } mo2)
        {
            // EnabledModFolders runs from low to high priority; the winner is searched first.
            foreach (var mod in mo2.EnabledModFolders().Reverse())
            {
                folders.Add(Path.Combine(mod, "SKSE", "Plugins", "fonts"));
                folders.Add(Path.Combine(mod, "Interface", "fonts"));
            }
        }
        foreach (var folder in folders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var name in HeaderNames)
            {
                var path = Path.Combine(folder, name);
                if (File.Exists(path)) return new UiFonts(body, path);
            }
        }
        return new UiFonts(body, null);
    }
}
