using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.CV;
using MyBot.Core.Input;
using MyBot.Game.CoC.State.Zoom;
using OpenCvSharp;
using Xunit;

namespace MyBot.Core.Tests;

/// <summary>Kiểm tra bảng tham chiếu port từ <c>MBR Global Variables.au3</c>.</summary>
public class VillageReferenceCatalogTests
{
    private readonly VillageReferenceCatalog _catalog = new();

    [Fact]
    public void Tables_HaveExpectedShape()
    {
        VillageReferenceCatalog.SceneryCodes.Should().HaveCount(49);
        VillageReferenceCatalog.SceneryNames.Should().HaveCount(53);
        VillageReferenceCatalog.CustomMainVillages.Should().HaveCount(5);
    }

    [Theory]
    [InlineData("DSS", 499.612307505107)]
    [InlineData("CC", 463.064874687304)]
    [InlineData("EM", 527.91838832914)]
    [InlineData("BB", 374.998531943809)]
    public void GetReferenceSize_ReturnsAutoItTableValue(string code, double expected)
    {
        _catalog.GetReferenceSize(code, secondTreeSet: false).Should().BeApproximately(expected, 0.000001);
    }

    [Fact]
    public void GetReferenceSize_UsesSecondaryColumnFor2Tree()
    {
        _catalog.GetReferenceSize("CC", secondTreeSet: true)
            .Should().BeApproximately(473.183193210402, 0.000001);
    }

    [Theory]
    [InlineData("MS", 439.489958643413)]
    [InlineData("PG", 496.873764541968)]
    [InlineData("EG", 405.46550540893)]
    [InlineData("SD", 490.949366617362)]
    [InlineData("JU", 556.323458536752)]
    public void GetReferenceSize_PrefersCustomMainVillage(string code, double expected)
    {
        _catalog.IsCustomMainVillage(code).Should().BeTrue();
        _catalog.GetReferenceSize(code, secondTreeSet: false).Should().BeApproximately(expected, 0.000001);
    }

    [Fact]
    public void GetReferenceSize_UnknownCodeIsZero()
    {
        _catalog.GetReferenceSize("ZZ", secondTreeSet: false).Should().Be(0);
    }
}

/// <summary>Kiểm tra <see cref="VillageMeasurer"/> với ImgLoc giả lập.</summary>
public class VillageMeasurerTests : IDisposable
{
    private readonly string _root;

    public VillageMeasurerTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "mybot-village-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(_root, "NormalVillage"));
        Directory.CreateDirectory(Path.Combine(_root, "Custom"));
        Directory.CreateDirectory(Path.Combine(_root, "BuilderBase"));
        Directory.CreateDirectory(Path.Combine(_root, "VillageScenery"));
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
    public void Measure_UsesStoneAndTreePythagoras()
    {
        File.WriteAllText(Path.Combine(_root, "NormalVillage", "stoneCC-129-490-75,76_0_93.xml"), "x");
        File.WriteAllText(Path.Combine(_root, "NormalVillage", "treeCC-770-260-60,62_0_92.xml"), "x");

        var searcher = new FakeTileSearcher
        {
            OnFind = name => name.StartsWith("stoneCC", StringComparison.Ordinal) ? new TilePoint(200, 500)
                : name.StartsWith("treeCC", StringComparison.Ordinal) ? new TilePoint(600, 200)
                : null,
        };

        var measurer = new VillageMeasurer(
            searcher,
            new IdentityPositionConverter(),
            new VillageReferenceCatalog(),
            _root);

        VillageMeasurement result = measurer.Measure(
            Frame(),
            new VillageMeasureRequest("stone", "tree", OnMainBase: false, OnBuilderBase: false, OnBuilderBaseEnemy: false));

        result.Found.Should().BeTrue();
        result.Size.Should().BeApproximately(363.62, 0.001);
        result.ReferenceSize.Should().BeApproximately(463.064874687304, 0.000001);
        result.Zoom.Should().BeApproximately(363.62 / 463.064874687304, 0.000001);
        result.OffsetX.Should().Be(200 - 129);
        result.OffsetY.Should().Be(500 - 490);
        result.TreeCode.Should().Be("CC");
        result.SecondTreeSet.Should().BeFalse();
    }

    [Fact]
    public void Measure_OnlyStone_ReturnsOffsetWithoutSize()
    {
        File.WriteAllText(Path.Combine(_root, "NormalVillage", "stoneCC-129-490-75,76_0_93.xml"), "x");

        var searcher = new FakeTileSearcher
        {
            OnFind = name => name.StartsWith("stoneCC", StringComparison.Ordinal) ? new TilePoint(150, 470) : null,
        };

        var measurer = new VillageMeasurer(
            searcher,
            new IdentityPositionConverter(),
            new VillageReferenceCatalog(),
            _root);

        VillageMeasurement result = measurer.Measure(
            Frame(),
            new VillageMeasureRequest("stone", "tree", OnMainBase: false, OnBuilderBase: false, OnBuilderBaseEnemy: false));

        result.Found.Should().BeTrue();
        result.Size.Should().Be(0);
        result.ReferenceSize.Should().Be(0);
        result.OffsetX.Should().Be(21);
        result.OffsetY.Should().Be(-20);
    }

    [Fact]
    public void Measure_CustomScenery_ReadsTemplatesFromCustomDirectory()
    {
        File.WriteAllText(Path.Combine(_root, "Custom", "stoneMS-126-448-40,67_0_88.xml"), "x");
        File.WriteAllText(Path.Combine(_root, "Custom", "treeMS-576-136-67,42_0_92.xml"), "x");

        var searcher = new FakeTileSearcher
        {
            Scenery = new[] { new NamedTilePoint("villageMS-501-666_0_92", 501, 666) },
            OnFind = name => name.StartsWith("stoneMS", StringComparison.Ordinal) ? new TilePoint(300, 450)
                : name.StartsWith("treeMS", StringComparison.Ordinal) ? new TilePoint(700, 200)
                : null,
        };

        var measurer = new VillageMeasurer(
            searcher,
            new IdentityPositionConverter(),
            new VillageReferenceCatalog(),
            _root);

        VillageMeasurement result = measurer.Measure(
            Frame(),
            new VillageMeasureRequest("stone", "tree", OnMainBase: true, OnBuilderBase: false, OnBuilderBaseEnemy: false));

        result.Found.Should().BeTrue();
        result.TreeCode.Should().Be("MS");
        result.ReferenceSize.Should().BeApproximately(439.489958643413, 0.000001);
        searcher.SearchedFiles.Should().Contain(path =>
            path.Contains(
                Path.DirectorySeparatorChar + "Custom" + Path.DirectorySeparatorChar,
                StringComparison.Ordinal));
    }

    [Fact]
    public void Measure_NoSceneryOnFirstZoom_SkipsStoneAndTree()
    {
        File.WriteAllText(Path.Combine(_root, "NormalVillage", "stoneCC-129-490-75,76_0_93.xml"), "x");

        var searcher = new FakeTileSearcher
        {
            OnFind = _ => new TilePoint(200, 500),
        };

        var measurer = new VillageMeasurer(
            searcher,
            new IdentityPositionConverter(),
            new VillageReferenceCatalog(),
            _root);

        VillageMeasurement result = measurer.Measure(
            Frame(),
            new VillageMeasureRequest(
                "stone", "tree", OnMainBase: true, OnBuilderBase: false, OnBuilderBaseEnemy: false,
                SearchZoomOutCount: 1));

        result.Found.Should().BeFalse();
        searcher.SearchedFiles.Should().BeEmpty("GetVillageSize ép zoom out ở hai lần đo đầu");
    }

    [Fact]
    public void Measure_NoSceneryAfterTwoZoomOuts_SearchesAllStones()
    {
        File.WriteAllText(Path.Combine(_root, "NormalVillage", "stoneCC-129-490-75,76_0_93.xml"), "x");

        var searcher = new FakeTileSearcher
        {
            OnFind = name => name.StartsWith("stone", StringComparison.Ordinal)
                ? new TilePoint(200, 500)
                : null,
        };

        var measurer = new VillageMeasurer(
            searcher,
            new IdentityPositionConverter(),
            new VillageReferenceCatalog(),
            _root);

        VillageMeasurement result = measurer.Measure(
            Frame(),
            new VillageMeasureRequest(
                "stone", "tree", OnMainBase: true, OnBuilderBase: false, OnBuilderBaseEnemy: false,
                SearchZoomOutCount: 2));

        result.Found.Should().BeTrue();
        result.OffsetX.Should().Be(200 - 129);
    }

    [Fact]
    public void Measure_SceneryFarFromReference_RequestsRecaptureAndCenters()
    {
        var searcher = new FakeTileSearcher
        {
            Scenery = new[] { new NamedTilePoint("villageCC-56-68_0_92", 200, 700) },
        };

        var measurer = new VillageMeasurer(
            searcher,
            new IdentityPositionConverter(),
            new VillageReferenceCatalog(),
            _root);

        (int X, int Y, int OffsetX, int OffsetY)? centered = null;
        VillageMeasurement result = measurer.Measure(
            Frame(),
            new VillageMeasureRequest(
                "stone",
                "tree",
                OnMainBase: true,
                OnBuilderBase: false,
                OnBuilderBaseEnemy: false,
                CenterVillage: (x, y, dx, dy) => centered = (x, y, dx, dy)));

        result.NeedsRecapture.Should().BeTrue();
        result.Found.Should().BeFalse();
        centered.Should().Be((200, 700, 144, 632));
    }

    [Fact]
    public void Measure_SecondaryTreeSet_UsesAlternateReferenceSize()
    {
        File.WriteAllText(Path.Combine(_root, "NormalVillage", "2stoneCC-294-611-75,82_0_93.xml"), "x");
        File.WriteAllText(Path.Combine(_root, "NormalVillage", "2treeCC-804-277-60,62_0_92.xml"), "x");

        var searcher = new FakeTileSearcher
        {
            OnFind = name => name.StartsWith("2stoneCC", StringComparison.Ordinal) ? new TilePoint(300, 600)
                : name.StartsWith("2treeCC", StringComparison.Ordinal) ? new TilePoint(700, 300)
                : null,
        };

        var measurer = new VillageMeasurer(
            searcher,
            new IdentityPositionConverter(),
            new VillageReferenceCatalog(),
            _root);

        VillageMeasurement result = measurer.Measure(
            Frame(),
            new VillageMeasureRequest("2stone", "2tree", OnMainBase: false, OnBuilderBase: false, OnBuilderBaseEnemy: false));

        result.Found.Should().BeTrue();
        result.SecondTreeSet.Should().BeTrue();
        result.ReferenceSize.Should().BeApproximately(473.183193210402, 0.000001);
    }

    [Theory]
    [InlineData("village9C-468-678_0_91.xml", "9C")]
    [InlineData("villageDS-350-656_0_92.xml", "DS")]
    [InlineData("treeDSS-1-2-3_0_92.xml", "DSS")]
    [InlineData("2treeCC-1-2-3_0_92.xml", "CC")]
    public void NameParsing_MatchesAutoIt(string fileName, string expectedCode)
    {
        if (fileName.StartsWith("tree", StringComparison.Ordinal)
            || fileName.StartsWith("2tree", StringComparison.Ordinal))
        {
            VillageMeasurer.TreeCodeFromName(fileName).Should().Be(expectedCode);
        }
        else
        {
            VillageMeasurer.SceneryCodeFromName(fileName).Should().Be(expectedCode);
        }
    }

    private static Mat Frame() => new(732, 860, MatType.CV_8UC3, Scalar.All(0));

    private sealed class FakeTileSearcher : ITileSearcher
    {
        public bool IsAvailable => true;

        public Func<string, TilePoint?> OnFind { get; set; } = _ => null;

        public IReadOnlyList<NamedTilePoint> Scenery { get; set; } = Array.Empty<NamedTilePoint>();

        public List<string> SearchedDirectories { get; } = new();

        public List<string> SearchedFiles { get; } = new();

        public TilePoint? Find(Mat frame, string templatePath, Rect area, int maxPoints = 1)
        {
            SearchedFiles.Add(templatePath);
            return OnFind(Path.GetFileName(templatePath));
        }

        public IReadOnlyList<NamedTilePoint> SearchDirectory(Mat frame, string directory, Rect area, int maxPoints = 0)
        {
            SearchedDirectories.Add(directory);
            return Scenery;
        }
    }

    private sealed class IdentityPositionConverter : IVillagePositionConverter
    {
        public bool IsAvailable => true;

        public void SetOffset(int x, int y, double zoom)
        {
        }

        public (int X, int Y) Convert(int x, int y, double zoom) => (x, y);
    }
}

/// <summary>Kiểm tra luồng <see cref="ZoomOutEngine"/>.</summary>
public class ZoomOutEngineTests
{
    private readonly Mock<IInputBackend> _input = new();
    private readonly Mock<IZoomCommand> _adb = new();
    private readonly Mock<IZoomCommand> _wheel = new();
    private readonly Mock<IProblemDetector> _problem = new();
    private readonly ScriptedMeasurer _measurer = new();

    public ZoomOutEngineTests()
    {
        _adb.Setup(c => c.TryZoomStep(It.IsAny<int>(), It.IsAny<bool>())).Returns(true);
        _wheel.Setup(c => c.TryWheelStep(It.IsAny<bool>())).Returns(true);
        _problem.Setup(d => d.IsProblemAffect(It.IsAny<Mat>())).Returns(false);
    }

    [Fact]
    public async Task ZoomOut_AlreadyZoomed_SendsNoCommand()
    {
        _measurer.Fallback = Accepted(offsetX: 0, offsetY: 0);
        ZoomOutEngine engine = CreateEngine();

        bool result = await engine.ZoomOutAsync(new ZoomContext(OnMainBase: true));

        result.Should().BeTrue();
        _adb.Verify(c => c.TryZoomStep(It.IsAny<int>(), It.IsAny<bool>()), Times.Never);
        _wheel.Verify(c => c.TryWheelStep(It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ZoomOut_NotMeasured_ZoomsUntilAccepted()
    {
        _measurer.Results.Enqueue(new VillageMeasurement());
        _measurer.Results.Enqueue(new VillageMeasurement());
        _measurer.Fallback = Accepted(offsetX: 0, offsetY: 0);

        ZoomOutEngine engine = CreateEngine();
        bool result = await engine.ZoomOutAsync(new ZoomContext(OnMainBase: true));

        result.Should().BeTrue();
        _adb.Verify(c => c.TryZoomStep(It.IsAny<int>(), It.IsAny<bool>()), Times.AtLeast(2));
        _measurer.Calls.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task Search_ZoomedButTooLarge_ClearsHintWhenFirstZoomNotSkipped()
    {
        _measurer.Fallback = Accepted(
            offsetX: 0, offsetY: 0, size: 500, referenceSize: 463.064874687304);
        ZoomOutEngine engine = CreateEngine();

        ZoomSearchResult first = await engine.SearchZoomOutAsync(
            new ZoomContext(OnMainBase: true), centerVillage: true, update: true);

        first.IsZoomedOut.Should().BeFalse();
        engine.SkipFirstZoomOut.Should().BeTrue();

        ZoomSearchResult second = await engine.SearchZoomOutAsync(
            new ZoomContext(OnMainBase: true), centerVillage: true, update: true);

        second.IsZoomedOut.Should().BeTrue();
    }

    [Fact]
    public async Task Search_OffsetNonZero_DragsVillageToCenter()
    {
        _measurer.Results.Enqueue(Accepted(offsetX: 10, offsetY: 5));
        _measurer.Fallback = Accepted(offsetX: 0, offsetY: 0);

        ZoomOutEngine engine = CreateEngine();
        ZoomSearchResult result = await engine.SearchZoomOutAsync(
            new ZoomContext(OnMainBase: true), centerVillage: true, update: true);

        result.IsZoomedOut.Should().BeTrue();
        result.ChangeX.Should().Be(-10);
        result.ChangeY.Should().Be(-5);
        _input.Verify(i => i.Swipe(300, 400, 290, 395, It.IsAny<int>()), Times.Once);
    }

    [Fact]
    public async Task Search_TooManyMeasureFailures_RestartsCoC()
    {
        _measurer.Fallback = new VillageMeasurement();
        ZoomOutEngine engine = CreateEngine();

        int restarts = 0;
        engine.RestartCoCAsync = _ =>
        {
            restarts++;
            return Task.CompletedTask;
        };

        for (int i = 0; i < 12; i++)
        {
            _ = await engine.SearchZoomOutAsync(
                new ZoomContext(OnMainBase: true), centerVillage: true, update: true);
        }

        restarts.Should().Be(1);
    }

    [Fact]
    public async Task ZoomOut_AfterFortyAdbAttempts_SwitchesToCtrlWheel()
    {
        _measurer.Fallback = Accepted(offsetX: 0, offsetY: 0, size: 100, referenceSize: 440);

        ZoomOutEngine engine = CreateEngine();
        bool result = await engine.ZoomOutAsync(new ZoomContext(OnMainBase: true));

        result.Should().BeFalse("làng luôn quá nhỏ nên không bao giờ đạt mức zoom chuẩn");
        _adb.Verify(c => c.TryZoomStep(It.IsAny<int>(), It.IsAny<bool>()), Times.AtMost(45));
        _wheel.Verify(c => c.TryWheelStep(true), Times.AtLeastOnce);
    }

    [Fact]
    public void IsCoordSafe_RejectsHudCorners()
    {
        ZoomOutEngine engine = CreateEngine();
        var main = new ZoomContext(OnMainBase: true);

        engine.IsCoordSafe(300, 400, main).Should().BeTrue();
        engine.IsCoordSafe(50, 700, main).Should().BeFalse("quá gần nút War");
        engine.IsCoordSafe(40, 320, main).Should().BeFalse("quá gần tab CHAT");
        engine.IsCoordSafe(300, 40, main).Should().BeFalse("quá gần nút Builder/Shield");
        engine.IsCoordSafe(750, 160, main).Should().BeFalse("quá gần nút Gems");
    }

    private ZoomOutEngine CreateEngine()
    {
        return new ZoomOutEngine(
            new ZoomOptions(),
            _measurer,
            positionConverter: null,
            () => new Mat(732, 860, MatType.CV_8UC3, Scalar.All(0)),
            _input.Object,
            _adb.Object,
            _wheel.Object,
            _problem.Object,
            new ImmediateTime());
    }

    private static VillageMeasurement Accepted(
        int offsetX, int offsetY, double size = 440, double referenceSize = 440)
    {
        return new VillageMeasurement
        {
            Found = true,
            Size = size,
            ReferenceSize = referenceSize,
            Zoom = size / referenceSize,
            OffsetX = offsetX,
            OffsetY = offsetY,
            StoneX = 300,
            StoneY = 400,
            StoneFile = "stoneCC-129-490-75,76_0_93.xml",
            TreeX = 700,
            TreeY = 200,
            TreeFile = "treeCC-770-260-60,62_0_92.xml",
        };
    }

    private sealed class ScriptedMeasurer : IVillageMeasurer
    {
        public bool IsAvailable => true;

        public Queue<VillageMeasurement> Results { get; } = new();

        public VillageMeasurement Fallback { get; set; } = new();

        public int Calls { get; private set; }

        public VillageMeasurement Measure(Mat frame, VillageMeasureRequest request)
        {
            Calls++;
            return Results.Count > 0 ? Results.Dequeue() : Fallback;
        }
    }

    private sealed class ImmediateTime : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

/// <summary>
/// Kiểm tra <see cref="VillageCenterer"/> (port <c>ZoomOutHelperQuick</c>):
/// tìm tree/stone theo vùng cố định, bỏ qua khi lệch ≤ 15px và kéo bằng
/// đúng độ lệch từ điểm kéo mặc định.
/// </summary>
public class VillageCentererTests
{
    private readonly AnchorSearcher _searcher = new();
    private readonly Mock<IInputBackend> _input = new();
    private readonly ZoomOptions _options = new();

    [Fact]
    public async Task TryCenter_TreeFound_DragsFromDefaultPoint()
    {
        _searcher.TreeResults.Add(new NamedTilePoint("treeCC-770-260-60,62_0_92", 800, 300));

        VillageCenterResult result = await CenterAsync(new ZoomContext(OnMainBase: true));

        result.Found.Should().BeTrue();
        result.Dragged.Should().BeTrue();
        result.OffsetX.Should().Be(30);
        result.OffsetY.Should().Be(40);
        result.AnchorName.Should().Be("treeCC-770-260-60,62_0_92");
        _input.Verify(i => i.Swipe(430, 650, 400, 610, 500), Times.Once);
    }

    [Fact]
    public async Task TryCenter_WithinFifteenPixels_DoesNotDrag()
    {
        _searcher.TreeResults.Add(new NamedTilePoint("treeCC-770-260-60,62_0_92", 780, 250));

        VillageCenterResult result = await CenterAsync(new ZoomContext(OnMainBase: true));

        result.Found.Should().BeTrue();
        result.Dragged.Should().BeFalse("AutoIt coi lệch ≤ 15px là đã ở giữa");
        result.OffsetX.Should().Be(10);
        result.OffsetY.Should().Be(-10);
        _input.Verify(
            i => i.Swipe(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task TryCenter_NoTree_SearchesStoneRegion()
    {
        _searcher.StoneResults.Add(new NamedTilePoint("stoneCC-129-490-75,76_0_93", 100, 480));

        VillageCenterResult result = await CenterAsync(new ZoomContext(OnMainBase: true));

        result.Found.Should().BeTrue();
        result.Dragged.Should().BeTrue();
        result.OffsetX.Should().Be(-29);
        result.OffsetY.Should().Be(-10);
        _input.Verify(i => i.Swipe(430, 650, 459, 660, 500), Times.Once);
        _searcher.Searches.Should().HaveCount(2, "thử tree trước rồi mới tới stone");
        _searcher.Searches[0].Area.X.Should().Be(430);
        _searcher.Searches[0].Area.Y.Should().Be(20);
        _searcher.Searches[0].Area.Width.Should().Be(320);
        _searcher.Searches[0].Area.Height.Should().Be(180);
        _searcher.Searches[1].Area.X.Should().Be(60);
        _searcher.Searches[1].Area.Y.Should().Be(330);
        _searcher.Searches[1].Area.Width.Should().Be(370);
        _searcher.Searches[1].Area.Height.Should().Be(230);
    }

    [Fact]
    public async Task TryCenter_NotOnMainBase_SkipsSearchAndDrag()
    {
        VillageCenterResult result = await CenterAsync(new ZoomContext(OnMainBase: false));

        result.Found.Should().BeFalse();
        _searcher.Searches.Should().BeEmpty();
        _input.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CenterVillage_RepeatsDragUntilWithinTolerance()
    {
        var centerer = new Mock<IVillageCenterer>();
        centerer
            .SetupSequence(c => c.TryCenterAsync(
                It.IsAny<Mat>(), It.IsAny<ZoomContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VillageCenterResult { Found = true, Dragged = true })
            .ReturnsAsync(new VillageCenterResult { Found = true, Dragged = false });

        ZoomOutEngine engine = CreateEngine(centerer.Object);
        bool centered = await engine.CenterVillageAsync(new ZoomContext(OnMainBase: true));

        centered.Should().BeTrue();
        centerer.Verify(
            c => c.TryCenterAsync(
                It.IsAny<Mat>(), It.IsAny<ZoomContext>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task SearchQuickCentersBeforeMeasuring()
    {
        var centerer = new Mock<IVillageCenterer>();
        centerer
            .Setup(c => c.TryCenterAsync(
                It.IsAny<Mat>(), It.IsAny<ZoomContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VillageCenterResult { Found = true, Dragged = true, OffsetX = 40 });

        var measurer = new SingleResultMeasurer(new VillageMeasurement
        {
            Found = true,
            Size = 440,
            ReferenceSize = 440,
            Zoom = 1,
            OffsetX = 0,
            OffsetY = 0,
            StoneFile = "stoneCC-129-490-75,76_0_93.xml",
            StoneX = 300,
            StoneY = 400,
        });

        ZoomOutEngine engine = CreateEngine(centerer.Object, measurer);
        ZoomSearchResult result = await engine.SearchZoomOutAsync(
            new ZoomContext(OnMainBase: true), centerVillage: true, update: true);

        result.IsZoomedOut.Should().BeTrue();
        centerer.Verify(
            c => c.TryCenterAsync(
                It.IsAny<Mat>(), It.IsAny<ZoomContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task<VillageCenterResult> CenterAsync(ZoomContext context)
    {
        using var frame = new Mat(732, 860, MatType.CV_8UC3, Scalar.All(0));
        var centerer = new VillageCenterer(
            _options,
            _searcher,
            _input.Object,
            anchorDirectory: "imgxml/village/NormalVillage",
            new ImmediateTime());

        return await centerer.TryCenterAsync(frame, context);
    }

    private static ZoomOutEngine CreateEngine(IVillageCenterer centerer, IVillageMeasurer? measurer = null)
    {
        var problem = new Mock<IProblemDetector>();
        problem.Setup(d => d.IsProblemAffect(It.IsAny<Mat>())).Returns(false);

        return new ZoomOutEngine(
            new ZoomOptions(),
            measurer ?? new SingleResultMeasurer(new VillageMeasurement()),
            positionConverter: null,
            () => new Mat(732, 860, MatType.CV_8UC3, Scalar.All(0)),
            Mock.Of<IInputBackend>(),
            adbCommand: null,
            wheelCommand: null,
            problem.Object,
            new ImmediateTime(),
            logger: null,
            centerer);
    }

    private sealed class AnchorSearcher : ITileSearcher
    {
        public bool IsAvailable => true;

        public List<NamedTilePoint> TreeResults { get; } = new();

        public List<NamedTilePoint> StoneResults { get; } = new();

        public List<(string Directory, Rect Area)> Searches { get; } = new();

        public TilePoint? Find(Mat frame, string templatePath, Rect area, int maxPoints = 1) => null;

        public IReadOnlyList<NamedTilePoint> SearchDirectory(
            Mat frame,
            string directory,
            Rect area,
            int maxPoints = 0)
        {
            Searches.Add((directory, area));
            return area.Y == 20 ? TreeResults : StoneResults;
        }
    }

    private sealed class SingleResultMeasurer : IVillageMeasurer
    {
        private readonly VillageMeasurement _result;

        public SingleResultMeasurer(VillageMeasurement result)
        {
            _result = result;
        }

        public bool IsAvailable => true;

        public VillageMeasurement Measure(Mat frame, VillageMeasureRequest request) => _result;
    }

    private sealed class ImmediateTime : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;

        public Task DelayAsync(int milliseconds, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}

/// <summary>Kiểm tra các thành phần ADB/minitouch và phát hiện popup.</summary>
public class ZoomCommandTests
{
    [Fact]
    public void AdbShell_ParseMouseDevice_PrefersNameMatch()
    {
        const string output = """
            add device 1: /dev/input/event0
              name: "gpio-keys"
            add device 2: /dev/input/event2
              name: "Microvirt Virtual Input"
              events:
                ABS (0003): 0035  : value 0, min 0, max 860, fuzz 0, flat 0, resolution 0
                ABS (0003): 0036  : value 0, min 0, max 732, fuzz 0, flat 0, resolution 0
            """;

        AdbShell.ParseMouseDevice(output).Should().Be("/dev/input/event2");
    }

    [Fact]
    public void AdbShell_ParseMouseDevice_FallsBackToScreenSize()
    {
        const string output = """
            add device 1: /dev/input/event4
              name: "unknown touch"
              events:
                ABS (0003): 0035  : value 0, min 0, max 860, fuzz 0, flat 0, resolution 0
                ABS (0003): 0036  : value 0, min 0, max 732, fuzz 0, flat 0, resolution 0
            """;

        AdbShell.ParseMouseDevice(output).Should().Be("/dev/input/event4");
    }

    [Fact]
    public void AdbShell_ParseMouseDevice_FallsBackToScreenSize_MEmuFormat()
    {
        // MEmu API 28: "getevent -p" chỉ in tiền tố "ABS (0003):" ở dòng đầu,
        // các trục 0035/0036 nằm trên dòng nối tiếp.
        const string output = """
            add device 1: /dev/input/event5
              name: "MemuHyperv mouse integration"
              events:
                ABS (0003): 002f  : value 1, min 0, max 15, fuzz 0, flat 0, resolution 0
                            0035  : value 0, min 0, max 65535, fuzz 0, flat 0, resolution 0
                            0036  : value 0, min 0, max 65535, fuzz 0, flat 0, resolution 0
            add device 2: /dev/input/event6
              name: "User Input"
              events:
                ABS (0003): 002f  : value 1, min 0, max 15, fuzz 0, flat 0, resolution 0
                            0035  : value 0, min 0, max 860, fuzz 0, flat 0, resolution 0
                            0036  : value 0, min 0, max 732, fuzz 0, flat 0, resolution 0
            """;

        AdbShell.ParseMouseDevice(output, namePattern: "does-not-exist")
            .Should().Be("/dev/input/event6");
    }

    [Fact]
    public void AdbShell_ParseMouseDevice_FallsBackToScreenSize_NamedAxes()
    {
        // "getevent -pl" in tên trục thay cho mã hex.
        const string output = """
            add device 1: /dev/input/event6
              name: "User Input"
              events:
                ABS (0003): ABS_MT_POSITION_X     : value 0, min 0, max 860, fuzz 0, flat 0, resolution 0
                            ABS_MT_POSITION_Y     : value 0, min 0, max 732, fuzz 0, flat 0, resolution 0
            """;

        AdbShell.ParseMouseDevice(output, namePattern: "does-not-exist")
            .Should().Be("/dev/input/event6");
    }

    [Theory]
    [InlineData(false, "Normal")]
    [InlineData(true, "Small")]
    public void AndroidZoomOut_SelectScript_UsesAutoItRanges(bool builderBaseEnemy, string prefix)
    {
        var random = new Random(1234);
        for (int i = 0; i < 100; i++)
        {
            string script = AndroidZoomOut.SelectScript(builderBaseEnemy, random);
            script.Should().StartWith(prefix);

            int index = int.Parse(script[prefix.Length..]);
            if (builderBaseEnemy)
            {
                index.Should().BeInRange(0, 2);
            }
            else
            {
                index.Should().BeInRange(0, 6);
            }
        }
    }

    [Fact]
    public void MinitouchRunner_FindScript_UsesEmulatorSpecificFileFirst()
    {
        string directory = Path.Combine(Path.GetTempPath(), "mybot-scripts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "Normal0.minitouch"), "x");
            File.WriteAllText(Path.Combine(directory, "Normal0.MEmu.script"), "x");

            var runner = new MinitouchRunner(Mock.Of<IAdbShell>(), directory);
            runner.FindScript("Normal0", "MEmu").Should().EndWith("Normal0.MEmu.script");

            File.Delete(Path.Combine(directory, "Normal0.MEmu.script"));
            runner.FindScript("Normal0", "MEmu").Should().EndWith("Normal0.minitouch");
            runner.FindScript("Normal6", "MEmu").Should().BeNull();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ProblemDetector_MatchesAutoItGrayStrip()
    {
        using var frame = new Mat(732, 860, MatType.CV_8UC3, Scalar.All(0x20));
        foreach (int x in new[] { 253, 373, 473, 283, 320, 594 })
        {
            frame.Set(425, x, new Vec3b(0x42, 0x42, 0x42));
        }

        frame.Set(32, 823, new Vec3b(0x20, 0x20, 0x20));

        var detector = new ProblemDetector(androidApiLevel: 30, midOffsetY: 30);
        detector.IsProblemAffect(frame).Should().BeTrue();

        frame.Set(425, 253, new Vec3b(0x10, 0x10, 0x10));
        detector.IsProblemAffect(frame).Should().BeFalse();
    }
}
