using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using MyBot.Core.Tests.Mocks;
using MyBot.Game.CoC.Village;
using OpenCvSharp;
using Xunit;

namespace MyBot.Core.Tests;

/// <summary>Kiểm tra port <c>imglocTHSearch(myVillage=True)</c>.</summary>
public class TownHallSearcherTests : IDisposable
{
    private readonly string _root;
    private readonly FakeDirectorySearcher _tileSearcher = new();
    private readonly FakePositionConverter _converter = new();
    private readonly Mat _frame = new(732, 860, MatType.CV_8UC3, Scalar.All(0));

    public TownHallSearcherTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mybot-th-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "Townhall"));
        Directory.CreateDirectory(Path.Combine(_root, "Townhall2"));
    }

    public void Dispose()
    {
        _frame.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Bỏ qua lỗi dọn thư mục tạm.
        }
    }

    [Fact]
    public void Search_Found_ConvertsScreenToVillage()
    {
        _tileSearcher.OnDirectory = name => name == "Townhall"
            ? new[] { new NamedTilePoint("Townhall12", 200, 300, 12) }
            : Array.Empty<NamedTilePoint>();
        _converter.ScreenToVillage = (1000, 900);

        TownHallSearchResult result = CreateSearcher().Search(_frame, FullArea(), singlePass: false);

        result.Found.Should().BeTrue();
        result.VillageX.Should().Be(1000);
        result.VillageY.Should().Be(900);
        result.Level.Should().Be(12);
        result.Template.Should().Be("Townhall12");
        result.CoordinateConversionFailed.Should().BeFalse();
        _tileSearcher.Calls[0].MinLevel.Should().Be(6);
        _tileSearcher.Calls[0].MaxLevel.Should().Be(100);
    }

    [Fact]
    public void Search_RetriesSecondDirectory()
    {
        _tileSearcher.OnDirectory = name => name == "Townhall2"
            ? new[] { new NamedTilePoint("Townhall2-9", 220, 320, 9) }
            : Array.Empty<NamedTilePoint>();

        TownHallSearchResult result = CreateSearcher().Search(_frame, FullArea(), singlePass: false);

        result.Found.Should().BeTrue();
        _tileSearcher.Calls.Should().HaveCount(2);
        Path.GetFileName(_tileSearcher.Calls[0].Directory).Should().Be("Townhall");
        Path.GetFileName(_tileSearcher.Calls[1].Directory).Should().Be("Townhall2");
    }

    [Fact]
    public void Search_SinglePass_SearchesOneDirectoryOnly()
    {
        TownHallSearchResult result = CreateSearcher().Search(_frame, FullArea(), singlePass: true);

        result.Found.Should().BeFalse();
        _tileSearcher.Calls.Should().HaveCount(1);
        Path.GetFileName(_tileSearcher.Calls[0].Directory).Should().Be("Townhall");
    }

    [Fact]
    public void Search_PicksHighestLevelTemplate()
    {
        _tileSearcher.OnDirectory = name => name == "Townhall"
            ? new[]
            {
                new NamedTilePoint("Townhall6", 200, 300, 6),
                new NamedTilePoint("Townhall15", 210, 310, 15),
            }
            : Array.Empty<NamedTilePoint>();

        TownHallSearchResult result = CreateSearcher().Search(_frame, FullArea(), singlePass: false);

        result.Level.Should().Be(15);
        result.Template.Should().Be("Townhall15");
    }

    [Fact]
    public void Search_ConversionFails_RefreshesOffsetAndRetries()
    {
        _tileSearcher.OnDirectory = name => name == "Townhall"
            ? new[] { new NamedTilePoint("Townhall10", 200, 300, 10) }
            : Array.Empty<NamedTilePoint>();
        _converter.FromScreenResults.Enqueue(false);
        _converter.FromScreenResults.Enqueue(true);
        _converter.RefreshResult = true;

        TownHallSearchResult result = CreateSearcher().Search(_frame, FullArea(), singlePass: false);

        result.Found.Should().BeTrue();
        _converter.EnsureOffsetCalls.Should().Be(1);
    }

    [Fact]
    public void Search_ConversionStillFails_FlagsFailure()
    {
        _tileSearcher.OnDirectory = name => name == "Townhall"
            ? new[] { new NamedTilePoint("Townhall10", 200, 300, 10) }
            : Array.Empty<NamedTilePoint>();
        _converter.FromScreenResults.Enqueue(false);
        _converter.FromScreenResults.Enqueue(false);
        _converter.RefreshResult = true;

        TownHallSearchResult result = CreateSearcher().Search(_frame, FullArea(), singlePass: false);

        result.Found.Should().BeFalse();
        result.CoordinateConversionFailed.Should().BeTrue();
        result.ScreenX.Should().Be(200);
        result.ScreenY.Should().Be(300);
    }

    private TownHallSearcher CreateSearcher()
    {
        return new TownHallSearcher(
            _tileSearcher,
            _converter,
            new TownHallSearchOptions { BuildingsDirectory = _root });
    }

    private static Rect FullArea() => Rect.FromLTRB(0, 0, 860, 732);
}

/// <summary>Kiểm tra port <c>BuildingInfo</c>/<c>getNameBuilding</c>.</summary>
public class BuildingInfoReaderTests
{
    private readonly FakeOcrEngine _ocr = new();
    private readonly FakeTimeProvider _time = new();

    [Theory]
    [InlineData("Town Hall (Level 12)", "Town Hall", 12)]
    [InlineData("Cannon (Level 8)", "Cannon", 8)]
    [InlineData("Wall (Level 15)", "Wall", 15)]
    public void Parse_ExtractsNameAndLevel(string text, string name, int level)
    {
        BuildingInfoResult result = CreateReader().Parse(text);

        result.Name.Should().Be(name);
        result.Level.Should().Be(level);
        result.LevelText.Should().Be(level.ToString(System.Globalization.CultureInfo.InvariantCulture));
        result.IsBroken.Should().BeFalse();
    }

    [Theory]
    [InlineData("Helper", "Helper")]
    [InlineData("B.O.B", "B.O.B")]
    [InlineData("Cake", "Cake")]
    [InlineData("Cart", "Cart")]
    [InlineData("Tree", "Tree")]
    [InlineData("Mush", "Mush")]
    [InlineData("Trunk", "Trunk")]
    [InlineData("Bush", "Bush")]
    [InlineData("Bark", "Bark")]
    [InlineData("Gem", "Gem")]
    public void Parse_FakeLevelIsDroppedButNameIsKept(string text, string name)
    {
        BuildingInfoResult result = CreateReader().Parse(text);

        result.Name.Should().Be(name);
        result.Level.Should().Be(0);
        result.LevelText.Should().BeEmpty();
        result.Found.Should().BeTrue();
    }

    [Fact]
    public void Parse_Broken_ReturnsLevel200AndBrokenText()
    {
        BuildingInfoResult result = CreateReader().Parse("Wall (Broken)");

        result.Name.Should().Be("Wall");
        result.Level.Should().Be(0);
        result.LevelText.Should().Be("Broken");
        result.IsBroken.Should().BeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("Town Hall")]
    [InlineData("Town Hall (Level 12) (Level 13)")]
    public void Parse_WithoutSingleLevelGroup_ReturnsEmpty(string text)
    {
        BuildingInfoResult result = CreateReader().Parse(text);

        result.Found.Should().BeFalse();
    }

    [Fact]
    public void ReadRawText_NormalizesIiToEight()
    {
        _ocr.Responses.Enqueue("Town Hall (Level ii)");
        _ocr.Responses.Enqueue(string.Empty);

        string text = CreateReader().ReadRawText(Frame(), 242, 535);

        text.Should().Be("Town Hall (Level 8)");
        _ocr.Models.Should().Equal("coc-build", "coc-build2");
    }

    [Fact]
    public void ReadRawText_FallsBackToAlternateModel()
    {
        _ocr.Responses.Enqueue(string.Empty);
        _ocr.Responses.Enqueue("Town Hall (Level 7)");

        string text = CreateReader().ReadRawText(Frame(), 242, 535);

        text.Should().Be("Town Hall (Level 7)");
        _ocr.Models.Should().Equal("coc-build", "coc-build2");
    }

    [Fact]
    public void ReadRawText_ObstacleSkipsAlternateRead()
    {
        _ocr.Responses.Enqueue("Tree (Level 3)");

        string text = CreateReader().ReadRawText(Frame(), 242, 535);

        text.Should().Be("Tree (Level 3)");
        _ocr.Models.Should().ContainSingle().Which.Should().Be("coc-build");
    }

    [Fact]
    public async Task ReadAsync_RetriesOnceAfterEmptyRead()
    {
        _ocr.Responses.Enqueue(string.Empty);
        _ocr.Responses.Enqueue(string.Empty);
        _ocr.Responses.Enqueue("Town Hall (Level 9)");

        BuildingInfoResult result = await CreateReader().ReadAsync(
            () => Frame(),
            242,
            535,
            CancellationToken.None);

        result.Name.Should().Be("Town Hall");
        result.Level.Should().Be(9);
        _time.UtcNow.Should().Be(new DateTime(2026, 1, 1, 0, 0, 2, 500, DateTimeKind.Utc));
    }

    private BuildingInfoReader CreateReader() => new(_ocr, _time);

    private static Mat Frame() => new(732, 860, MatType.CV_8UC3, Scalar.All(0));
}

/// <summary>Kiểm tra port <c>ClearScreen</c>/<c>ClickAway</c>/<c>IsMainGrayed</c>.</summary>
public class MainScreenCleanerTests
{
    private readonly FakeBuildingInfoReader _buildingInfo = new();
    private readonly FakeInputBackend _input = new();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task ClearScreen_NoPanelOrGray_DoesNothing()
    {
        bool cleared = await CreateCleaner().ClearScreenAsync(() => Frame());

        cleared.Should().BeFalse();
        _input.Taps.Should().BeEmpty();
        _time.UtcNow.Should().Be(new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ClearScreen_BuildingTextPresent_TapsAwayAndWaits()
    {
        _buildingInfo.RawText = (_, _, _) => "Town Hall (Level 5)";

        bool cleared = await CreateCleaner().ClearScreenAsync(() => Frame());

        cleared.Should().BeTrue();
        _input.Taps.Should().ContainSingle();
        _time.UtcNow.Should().Be(new DateTime(2026, 1, 1, 0, 0, 1, 500, DateTimeKind.Utc));
    }

    [Fact]
    public async Task ClearScreen_GrayOverlay_TapsAway()
    {
        bool cleared = await CreateCleaner().ClearScreenAsync(() =>
        {
            var frame = new Mat(732, 860, MatType.CV_8UC3, Scalar.All(0));
            frame.At<Vec3b>(7, 370) = new Vec3b(0x7B, 0x6F, 0x57);
            frame.At<Vec3b>(7, 374) = new Vec3b(0x72, 0x5F, 0x3D);
            frame.At<Vec3b>(7, 379) = new Vec3b(0x77, 0x7B, 0x7B);
            return frame;
        });

        cleared.Should().BeTrue();
        _input.Taps.Should().ContainSingle();
    }

    [Fact]
    public void IsMainGrayed_DetectsMarkerPixels()
    {
        using var frame = new Mat(732, 860, MatType.CV_8UC3, Scalar.All(0));
        frame.At<Vec3b>(8, 380) = new Vec3b(0x7B, 0x6F, 0x57);
        frame.At<Vec3b>(8, 384) = new Vec3b(0x72, 0x5F, 0x3D);
        frame.At<Vec3b>(8, 389) = new Vec3b(0x77, 0x7B, 0x7B);

        CreateCleaner().IsMainGrayed(frame).Should().BeTrue();
    }

    [Fact]
    public void IsMainGrayed_BlankFrame_ReturnsFalse()
    {
        using var frame = new Mat(732, 860, MatType.CV_8UC3, Scalar.All(0));

        CreateCleaner().IsMainGrayed(frame).Should().BeFalse();
    }

    private MainScreenCleaner CreateCleaner() =>
        new(_buildingInfo, _input, _time, random: new Random(42));

    private static Mat Frame() => new(732, 860, MatType.CV_8UC3, Scalar.All(0));
}

/// <summary>Kiểm tra <c>AutoDetectTownHall</c>.</summary>
public class TownHallDetectorTests
{
    private readonly FakeTownHallSearcher _searcher = new();
    private readonly FakePositionConverter _converter = new();
    private readonly FakeBuildingInfoReader _buildingInfo = new();
    private readonly FakeInputBackend _input = new();
    private readonly FakeCleaner _cleaner = new();
    private readonly FakeTimeProvider _time = new();

    [Fact]
    public async Task AutoDetect_SavedPosition_SearchesLocalAreaFirst()
    {
        _searcher.Results.Enqueue(Found(400, 300));
        _buildingInfo.Info = new BuildingInfoResult { Name = "Town Hall", Level = 11 };

        TownHallDetectionResult result = await CreateDetector().AutoDetectAsync(
            () => Frame(),
            new TownHallPosition(400, 300));

        result.Success.Should().BeTrue();
        result.Level.Should().Be(11);
        result.VillageX.Should().Be(400);
        result.VillageY.Should().Be(300);
        _searcher.Areas.Should().ContainSingle();
        _searcher.Areas[0].Left.Should().Be(310);
        _searcher.Areas[0].Top.Should().Be(210);
        _searcher.Areas[0].Right.Should().Be(490);
        _searcher.Areas[0].Bottom.Should().Be(390);
        _searcher.SinglePasses.Should().Equal(true);
        _input.Taps.Should().ContainSingle().Which.Should().Be((_converter.ScreenX, _converter.ScreenY));
        _cleaner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task AutoDetect_NoSavedPosition_SearchesWholeFrame()
    {
        _searcher.Results.Enqueue(Found(400, 300));
        _buildingInfo.Info = new BuildingInfoResult { Name = "Town Hall", Level = 11 };

        await CreateDetector().AutoDetectAsync(() => Frame());

        _searcher.Areas.Should().ContainSingle();
        _searcher.Areas[0].Left.Should().Be(0);
        _searcher.Areas[0].Top.Should().Be(0);
        _searcher.Areas[0].Right.Should().Be(860);
        _searcher.Areas[0].Bottom.Should().Be(732);
        _searcher.SinglePasses.Should().Equal(false);
    }

    [Fact]
    public async Task AutoDetect_LocalMiss_FallsBackToFullSearch()
    {
        _searcher.Results.Enqueue(new TownHallSearchResult());
        _searcher.Results.Enqueue(Found(400, 300));
        _buildingInfo.Info = new BuildingInfoResult { Name = "Town Hall", Level = 11 };

        TownHallDetectionResult result = await CreateDetector().AutoDetectAsync(
            () => Frame(),
            new TownHallPosition(400, 300));

        result.Success.Should().BeTrue();
        _searcher.Areas.Should().HaveCount(2);
        _searcher.SinglePasses.Should().Equal(true, false);
    }

    [Fact]
    public async Task AutoDetect_OcrDoesNotConfirm_ReturnsFailureAndClearsScreen()
    {
        _searcher.Results.Enqueue(Found(400, 300));
        _buildingInfo.Info = new BuildingInfoResult { Name = "Barracks", Level = 8 };

        TownHallDetectionResult result = await CreateDetector().AutoDetectAsync(() => Frame());

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("OCR");
        _cleaner.Calls.Should().Be(1);
    }

    [Fact]
    public async Task AutoDetect_NotFound_DoesNotTapOrReadBuildingInfo()
    {
        TownHallDetectionResult result = await CreateDetector().AutoDetectAsync(() => Frame());

        result.Success.Should().BeFalse();
        _input.Taps.Should().BeEmpty();
        _buildingInfo.ReadCalls.Should().Be(0);
        _cleaner.Calls.Should().Be(0);
    }

    [Fact]
    public async Task AutoDetect_ScreenConversionFails_ReturnsFailure()
    {
        _searcher.Results.Enqueue(Found(400, 300));
        _converter.ToScreenResult = false;

        TownHallDetectionResult result = await CreateDetector().AutoDetectAsync(() => Frame());

        result.Success.Should().BeFalse();
        _input.Taps.Should().BeEmpty();
        _buildingInfo.ReadCalls.Should().Be(0);
    }

    private TownHallDetector CreateDetector() => new(
        _searcher,
        _converter,
        _buildingInfo,
        _input,
        _cleaner,
        _time,
        new TownHallDetectorOptions());

    private static TownHallSearchResult Found(int x, int y) => new()
    {
        Found = true,
        VillageX = x,
        VillageY = y,
        ScreenX = x,
        ScreenY = y,
        Level = 10,
        Template = "Townhall10",
    };

    private static Mat Frame() => new(732, 860, MatType.CV_8UC3, Scalar.All(0));
}

/// <summary>Kiểm tra <c>BotDetectFirstTime</c>.</summary>
public class BotFirstTimeDetectorTests
{
    private readonly FakeTownHallDetector _townHall = new();
    private readonly FakeCleaner _cleaner = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Mock<IBotLogger> _logger = new();

    [Fact]
    public async Task Detect_Success_ReturnsLevelAndPosition()
    {
        _townHall.Result = new TownHallDetectionResult
        {
            Success = true,
            Level = 12,
            VillageX = 400,
            VillageY = 300,
            Template = "Townhall12",
        };

        BotFirstTimeResult result = await CreateDetector().DetectAsync(() => Frame());

        result.Success.Should().BeTrue();
        result.Level.Should().Be(12);
        result.VillageX.Should().Be(400);
        result.VillageY.Should().Be(300);
        _cleaner.Calls.Should().Be(1);
        _time.UtcNow.Should().Be(new DateTime(2026, 1, 1, 0, 0, 3, DateTimeKind.Utc));
        _logger.Verify(l => l.Info("Detecting Buildings"), Times.Once);
    }

    [Fact]
    public async Task Detect_BelowLevelSix_LogsWarning()
    {
        _townHall.Result = new TownHallDetectionResult { Success = true, Level = 4 };

        await CreateDetector().DetectAsync(() => Frame());

        _logger.Verify(
            l => l.Warning(It.Is<string>(message => message.Contains("below 6", StringComparison.Ordinal))),
            Times.Once);
    }

    [Fact]
    public async Task Detect_Failure_LogsManualDetectionHint()
    {
        _townHall.Result = new TownHallDetectionResult();

        BotFirstTimeResult result = await CreateDetector().DetectAsync(() => Frame());

        result.Success.Should().BeFalse();
        _logger.Verify(l => l.Info("Town Hall: Unknow Position"), Times.Once);
        _logger.Verify(l => l.Info("Require manual detection"), Times.Once);
    }

    private BotFirstTimeDetector CreateDetector() =>
        new(_townHall, _cleaner, _time, _logger.Object);

    private static Mat Frame() => new(732, 860, MatType.CV_8UC3, Scalar.All(0));
}

/// <summary>Kiểm tra <c>TestLanguage</c>.</summary>
public class LanguageVerifierTests : IDisposable
{
    private readonly string _root;
    private readonly FakeDirectorySearcher _searcher = new();
    private readonly Mock<IBotLogger> _logger = new();

    public LanguageVerifierTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mybot-lang-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Bỏ qua lỗi dọn thư mục tạm.
        }
    }

    [Fact]
    public void Verify_AttackButtonFound_ReturnsTrue()
    {
        _searcher.OnDirectory = _ => new[] { new NamedTilePoint("AttackButton", 80, 660) };

        bool verified = CreateVerifier().Verify(() => Frame());

        verified.Should().BeTrue();
        _logger.Verify(l => l.Info("Language Setting: English"), Times.Once);
    }

    [Fact]
    public void Verify_AttackButtonMissing_ReturnsFalse()
    {
        bool verified = CreateVerifier().Verify(() => Frame());

        verified.Should().BeFalse();
        _logger.Verify(
            l => l.Error(It.Is<string>(message => message.Contains("English", StringComparison.Ordinal)), null),
            Times.Once);
    }

    [Fact]
    public void Verify_MissingTemplateDirectory_IsSkipped()
    {
        var verifier = new LanguageVerifier(
            _searcher,
            Path.Combine(_root, "missing"),
            _logger.Object);

        bool verified = verifier.Verify(() => throw new InvalidOperationException("must not capture"));

        verified.Should().BeTrue();
        _logger.Verify(
            l => l.Warning(It.Is<string>(message => message.Contains("bỏ qua", StringComparison.Ordinal))),
            Times.Once);
    }

    private LanguageVerifier CreateVerifier() => new(_searcher, _root, _logger.Object);

    private static Mat Frame() => new(732, 860, MatType.CV_8UC3, Scalar.All(0));
}

/// <summary>Bộ tìm template giả ghi lại thư mục/dải cấp được yêu cầu.</summary>
internal sealed class FakeDirectorySearcher : ITileSearcher
{
    public bool IsAvailable { get; set; } = true;

    public Func<string, IReadOnlyList<NamedTilePoint>> OnDirectory { get; set; } =
        _ => Array.Empty<NamedTilePoint>();

    public List<SearchCall> Calls { get; } = new();

    public TilePoint? Find(Mat frame, string templatePath, Rect area, int maxPoints = 1) => null;

    public IReadOnlyList<NamedTilePoint> SearchDirectory(
        Mat frame,
        string directory,
        Rect area,
        int maxPoints = 0)
    {
        return SearchDirectory(frame, directory, area, maxPoints, 0, 1000);
    }

    public IReadOnlyList<NamedTilePoint> SearchDirectory(
        Mat frame,
        string directory,
        Rect area,
        int maxPoints,
        int minLevel,
        int maxLevel)
    {
        Calls.Add(new SearchCall(directory, area, minLevel, maxLevel));
        return OnDirectory(Path.GetFileName(directory));
    }

    public readonly record struct SearchCall(string Directory, Rect Area, int MinLevel, int MaxLevel);
}

/// <summary>Bộ chuyển toạ độ giả có thể lập trình kết quả từng lần.</summary>
internal sealed class FakePositionConverter : IVillagePositionConverter
{
    public bool IsAvailable { get; set; } = true;

    public Queue<bool> FromScreenResults { get; } = new();

    public bool RefreshResult { get; set; } = true;

    public bool ToScreenResult { get; set; } = true;

    public int EnsureOffsetCalls { get; private set; }

    public (int X, int Y) ScreenToVillage { get; set; } = (1000, 900);

    public int ScreenX { get; set; } = 400;

    public int ScreenY { get; set; } = 300;

    public void SetOffset(int x, int y, double zoom)
    {
    }

    public (int X, int Y) Convert(int x, int y, double zoom) => (x, y);

    public bool EnsureOffset()
    {
        EnsureOffsetCalls++;
        return RefreshResult;
    }

    public bool ToScreen(int x, int y, out int screenX, out int screenY)
    {
        screenX = ScreenX;
        screenY = ScreenY;
        return ToScreenResult;
    }

    public bool FromScreen(int x, int y, out int villageX, out int villageY)
    {
        villageX = ScreenToVillage.X;
        villageY = ScreenToVillage.Y;
        return FromScreenResults.Count == 0 || FromScreenResults.Dequeue();
    }
}

/// <summary>OCR giả trả về lần lượt các chuỗi đã xếp hàng.</summary>
internal sealed class FakeOcrEngine : IOcrEngine
{
    public Queue<string> Responses { get; } = new();

    public List<string> Models { get; } = new();

    public bool Initialize(string modelPath) => true;

    public string Recognize(Mat region) => string.Empty;

    public string Recognize(Mat region, string model)
    {
        Models.Add(model);
        return Responses.Count > 0 ? Responses.Dequeue() : string.Empty;
    }
}

/// <summary>Bộ đọc bảng thông tin giả.</summary>
internal sealed class FakeBuildingInfoReader : IBuildingInfoReader
{
    public Func<Mat, int, int, string> RawText { get; set; } = (_, _, _) => string.Empty;

    public BuildingInfoResult Info { get; set; } = new();

    public int ReadCalls { get; private set; }

    public string ReadRawText(Mat frame, int x, int y) => RawText(frame, x, y);

    public BuildingInfoResult Parse(string text) => Info;

    public Task<BuildingInfoResult> ReadAsync(
        Func<Mat> capture,
        int x,
        int y,
        CancellationToken cancellationToken = default)
    {
        ReadCalls++;
        return Task.FromResult(Info);
    }
}

/// <summary>Input giả ghi lại các lần chạm.</summary>
internal sealed class FakeInputBackend : IInputBackend
{
    public List<(int X, int Y)> Taps { get; } = new();

    public bool Initialize(EmulatorTarget target) => true;

    public void Tap(int x, int y) => Taps.Add((x, y));

    public void Swipe(int x1, int y1, int x2, int y2, int durationMs)
    {
    }

    public bool Scroll(int x, int y, int delta) => false;

    public void Dispose()
    {
    }
}

/// <summary>Bộ đóng bảng giả đếm số lần gọi.</summary>
internal sealed class FakeCleaner : IMainScreenCleaner
{
    public int Calls { get; private set; }

    public Task<bool> ClearScreenAsync(Func<Mat> capture, CancellationToken cancellationToken = default)
    {
        Calls++;
        return Task.FromResult(true);
    }
}

/// <summary>Bộ tìm Town Hall giả trả về theo hàng đợi.</summary>
internal sealed class FakeTownHallSearcher : ITownHallSearcher
{
    public bool IsAvailable => true;

    public Queue<TownHallSearchResult> Results { get; } = new();

    public List<Rect> Areas { get; } = new();

    public List<bool> SinglePasses { get; } = new();

    public TownHallSearchResult Search(Mat frame, Rect searchArea, bool singlePass)
    {
        Areas.Add(searchArea);
        SinglePasses.Add(singlePass);
        return Results.Count > 0 ? Results.Dequeue() : new TownHallSearchResult();
    }
}

/// <summary>Bộ phát hiện Town Hall giả.</summary>
internal sealed class FakeTownHallDetector : ITownHallDetector
{
    public TownHallDetectionResult Result { get; set; } = new();

    public Task<TownHallDetectionResult> AutoDetectAsync(
        Func<Mat> capture,
        TownHallPosition? saved = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(Result);
    }
}
