using System;
using System.Collections.Generic;
using System.IO;
using MyBot.Core.CV;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class TempTemplatePreviewTests
{
    [Fact]
    public void Preview_RealTemplateMatches()
    {
        string framePath = Path.Combine(Path.GetTempPath(), "after_zoom.png");
        if (!File.Exists(framePath))
        {
            return;
        }

        string lib = Path.Combine(AppContext.BaseDirectory, "lib");
        string templates = Path.Combine(AppContext.BaseDirectory, "imgxml", "village");
        string output = Path.Combine(Path.GetTempPath(), "mybot-tpl-preview");
        Directory.CreateDirectory(output);

        using var searcher = new NativeTileSearcher();
        Assert.True(searcher.Initialize(lib), "MyBot.run.dll load/export");

        using Mat frame = Cv2.ImRead(framePath);
        Assert.False(frame.Empty());
        var area = new Rect(0, 0, frame.Width, frame.Height);

        var files = new List<string>();
        files.AddRange(Directory.EnumerateFiles(Path.Combine(templates, "NormalVillage"), "stone*.xml"));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(templates, "NormalVillage"), "tree*.xml"));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(templates, "Custom"), "stone*.xml"));
        files.AddRange(Directory.EnumerateFiles(Path.Combine(templates, "Custom"), "tree*.xml"));

        var found = new List<(string Name, Point Point, Mat Crop)>();

        var diagnostics = new List<string>();
        IReadOnlyList<NamedTilePoint> sceneryAll =
            searcher.SearchDirectory(frame, Path.Combine(templates, "VillageScenery"), area);
        diagnostics.Add($"scenery full-frame: {sceneryAll.Count}");
        foreach (NamedTilePoint scenery in sceneryAll)
        {
            diagnostics.Add($"  {scenery.Name} at {scenery.X},{scenery.Y}");
        }

        IReadOnlyList<NamedTilePoint> sceneryBand = searcher.SearchDirectory(
            frame,
            Path.Combine(templates, "VillageScenery"),
            new Rect(185, 600, 680 - 185, 730 - 600));
        diagnostics.Add($"scenery band: {sceneryBand.Count}");
        foreach (NamedTilePoint scenery in sceneryBand)
        {
            diagnostics.Add($"  {scenery.Name} at {scenery.X},{scenery.Y}");
        }

        File.WriteAllLines(Path.Combine(output, "diagnostics.txt"), diagnostics);

        string buttons = @"F:\MyBot-MBR_v8.2.0\imgxml\imglocbuttons";
        foreach (string name in new[] { "ArmyTab_0_92.xml", "AttackButton_0_89.xml", "ShopButton_0_92.xml", "CloseWindow_0_88.xml" })
        {
            string path = Path.Combine(buttons, name);
            if (!File.Exists(path))
            {
                diagnostics.Add($"button missing: {name}");
                continue;
            }

            TilePoint? hit = searcher.Find(frame, path, area);
            string raw = searcher.DebugFindRaw(frame, path, area);
            diagnostics.Add($"button {name}: {(hit is null ? "MISS" : $"{hit.Value.X},{hit.Value.Y}")} raw='{raw}'");
        }

        File.WriteAllLines(Path.Combine(output, "diagnostics.txt"), diagnostics);

        foreach (string file in files)
        {
            TilePoint? tile = searcher.Find(frame, file, area);
            if (tile is null)
            {
                diagnostics.Add($"MISS {Path.GetFileName(file)}");
                continue;
            }

            int half = 45;
            int x = System.Math.Clamp(tile.Value.X - half, 0, frame.Width - 1);
            int y = System.Math.Clamp(tile.Value.Y - half, 0, frame.Height - 1);
            int w = System.Math.Min(2 * half + 1, frame.Width - x);
            int h = System.Math.Min(2 * half + 1, frame.Height - y);
            Mat crop = new Mat(frame, new Rect(x, y, w, h)).Clone();
            Cv2.PutText(crop, Path.GetFileNameWithoutExtension(file)[..8], new Point(2, 12),
                HersheyFonts.HersheySimplex, 0.35, Scalar.Yellow, 1);
            found.Add((Path.GetFileNameWithoutExtension(file), new Point(tile.Value.X, tile.Value.Y), crop));
        }

        File.WriteAllLines(Path.Combine(output, "diagnostics.txt"), diagnostics);

        if (found.Count == 0)
        {
            return;
        }

        // Ghép thành bảng: mỗi ô 91x91, tối đa 8 cột.
        const int cell = 91;
        const int cols = 8;
        int rows = (found.Count + cols - 1) / cols;
        using var sheet = new Mat(rows * cell, cols * cell, MatType.CV_8UC3, Scalar.Black);
        for (int i = 0; i < found.Count; i++)
        {
            int cx = (i % cols) * cell;
            int cy = (i / cols) * cell;
            found[i].Crop.CopyTo(new Mat(sheet, new Rect(cx, cy, found[i].Crop.Width, found[i].Crop.Height)));
        }

        Cv2.ImWrite(Path.Combine(output, "sheet.png"), sheet);
        foreach (var item in found)
        {
            item.Crop.Dispose();
        }

        File.WriteAllLines(
            Path.Combine(output, "found.txt"),
            found.ConvertAll(f => $"{f.Name} at {f.Point.X},{f.Point.Y}"));
    }
}
