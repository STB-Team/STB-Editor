using System.Buffers.Binary;
using StbEditor.App.Services;

namespace StbEditor.App.Ui;

internal sealed record UiFontFace(string Family, string Weight, string Path);

/// <summary>Installed Windows faces and user-supplied font files; no synthetic bold.</summary>
internal static class UiFonts
{
    public static readonly string DirectoryPath = Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath) ?? AppContext.BaseDirectory, "fonts");
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
        var faces = candidates.Select(face => face with { Path = Path.Combine(folder, face.Path) })
            .Where(face => File.Exists(face.Path)).ToList();
        try
        {
            Directory.CreateDirectory(DirectoryPath);
            foreach (var path in Directory.EnumerateFiles(DirectoryPath).Order(StringComparer.OrdinalIgnoreCase))
            {
                var extension = Path.GetExtension(path);
                if (!extension.Equals(".ttf", StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(".otf", StringComparison.OrdinalIgnoreCase)) continue;
                if (IsSupportedFile(path))
                    faces.Add(new UiFontFace(Path.GetFileName(path), "From file", path));
                else
                    Log.Warn($"Ignoring unsupported or incomplete font: {path}");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Log.Warn($"Cannot read font folder {DirectoryPath}: {ex.Message}");
        }
        return faces.ToArray();
    }

    // Check the SFNT directory before handing local files to the native font loader.
    public static bool IsSupportedFile(string path)
    {
        try
        {
            using var file = File.OpenRead(path);
            Span<byte> header = stackalloc byte[12];
            if (file.Length < header.Length) return false;
            file.ReadExactly(header);
            var signature = BinaryPrimitives.ReadUInt32BigEndian(header);
            if (signature is not (0x00010000 or 0x74727565 or 0x4F54544F)) return false;
            var count = BinaryPrimitives.ReadUInt16BigEndian(header[4..]);
            if (12L + count * 16L > file.Length) return false;
            FontTables tables = 0;
            Span<byte> entry = stackalloc byte[16];
            for (var i = 0; i < count; i++)
            {
                file.ReadExactly(entry);
                var offset = BinaryPrimitives.ReadUInt32BigEndian(entry[8..]);
                var length = BinaryPrimitives.ReadUInt32BigEndian(entry[12..]);
                if (offset > file.Length || length > file.Length - offset) return false;
                if (length == 0) continue;
                tables |= BinaryPrimitives.ReadUInt32BigEndian(entry) switch
                {
                    0x636D6170 => FontTables.Cmap,
                    0x68656164 => FontTables.Head,
                    0x68686561 => FontTables.Hhea,
                    0x686D7478 => FontTables.Hmtx,
                    0x6D617870 => FontTables.Maxp,
                    0x676C7966 => FontTables.Glyf,
                    0x6C6F6361 => FontTables.Loca,
                    0x43464620 => FontTables.Cff,
                    _ => 0,
                };
            }
            const FontTables required = FontTables.Cmap | FontTables.Head | FontTables.Hhea | FontTables.Hmtx | FontTables.Maxp;
            const FontTables trueType = FontTables.Glyf | FontTables.Loca;
            return (tables & required) == required
                && ((tables & trueType) == trueType || (tables & FontTables.Cff) != 0);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    [Flags]
    private enum FontTables { Cmap = 1, Head = 2, Hhea = 4, Hmtx = 8, Maxp = 16, Glyf = 32, Loca = 64, Cff = 128 }

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
}
