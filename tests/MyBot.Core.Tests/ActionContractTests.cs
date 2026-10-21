using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using MyBot.Core.Abstractions;
using MyBot.Core.ActionContract;
using OpenCvSharp;

namespace MyBot.Core.Tests;

public class ActionContractTests
{
    [Fact]
    public void Live_VerifyPass()
    {
        // Arrange
        var captureMock = new Mock<ICaptureBackend>();
        var inputMock = new Mock<IInputBackend>();
        var loggerMock = new Mock<IBotLogger>();
        var timeMock = new Mock<ITimeProvider>();

        // SetupSequence: preFrame đen, postFrame trắng
        captureMock.SetupSequence(c => c.GrabFrame())
            .Returns(new Mat(100, 100, MatType.CV_8UC3, Scalar.Black))
            .Returns(new Mat(100, 100, MatType.CV_8UC3, Scalar.White));

        var assertion = new VisualAssertion(
            Region: new Rect(0, 0, 100, 100),
            VerifyFn: (pre, post) => Cv2.Norm(pre, post) > 10,
            TimeoutMs: 200,
            Description: "Test button change"
        );

        var executor = new ActionContractExecutor(
            captureMock.Object,
            inputMock.Object,
            loggerMock.Object,
            timeMock.Object,
            ExecutionMode.Live
        );

        // Act
        var sw = Stopwatch.StartNew();
        bool result = executor.Execute((50, 50), assertion);
        sw.Stop();

        // Assert
        result.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(300);
        inputMock.Verify(i => i.Tap(50, 50), Times.Once);
        captureMock.Verify(c => c.GrabFrame(), Times.Exactly(2));
    }

    [Fact]
    public void Live_VerifyFail()
    {
        // Arrange
        var captureMock = new Mock<ICaptureBackend>();
        var inputMock = new Mock<IInputBackend>();
        var loggerMock = new Mock<IBotLogger>();
        var timeMock = new Mock<ITimeProvider>();
        timeMock.Setup(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Luôn trả ảnh đen giống nhau
        captureMock.Setup(c => c.GrabFrame())
            .Returns(() => new Mat(100, 100, MatType.CV_8UC3, Scalar.Black));

        var assertion = new VisualAssertion(
            Region: new Rect(0, 0, 100, 100),
            VerifyFn: (pre, post) => false, // Luôn thất bại
            TimeoutMs: 150,
            Description: "Fail button"
        );

        var executor = new ActionContractExecutor(
            captureMock.Object,
            inputMock.Object,
            loggerMock.Object,
            timeMock.Object,
            ExecutionMode.Live,
            defaultMaxRetries: 0 // Thử 1 lần để test nhanh
        );

        // Act
        var sw = Stopwatch.StartNew();
        bool result = executor.Execute((50, 50), assertion, maxRetries: 0);
        sw.Stop();

        // Assert
        result.Should().BeFalse();
        sw.ElapsedMilliseconds.Should().BeLessThan(50);
        loggerMock.Verify(l => l.Warning(It.Is<string>(s => s.Contains("[ACTION_WARN]"))), Times.Once);
        loggerMock.Verify(l => l.Error(It.Is<string>(s => s.Contains("[ACTION_FAIL]")), null), Times.Once);
    }

    [Fact]
    public void Live_Retry()
    {
        // Arrange
        var captureMock = new Mock<ICaptureBackend>();
        var inputMock = new Mock<IInputBackend>();
        var loggerMock = new Mock<IBotLogger>();
        var timeMock = new Mock<ITimeProvider>();
        timeMock.Setup(t => t.DelayAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // Lần 1: đen - đen (fail) -> retry
        // Lần 2: đen - trắng (pass)
        captureMock.SetupSequence(c => c.GrabFrame())
            .Returns(new Mat(100, 100, MatType.CV_8UC3, Scalar.Black)) // attempt 0 pre
            .Returns(new Mat(100, 100, MatType.CV_8UC3, Scalar.Black)) // attempt 0 post
            .Returns(new Mat(100, 100, MatType.CV_8UC3, Scalar.Black)) // attempt 1 pre
            .Returns(new Mat(100, 100, MatType.CV_8UC3, Scalar.White)); // attempt 1 post

        var assertion = new VisualAssertion(
            Region: new Rect(0, 0, 100, 100),
            VerifyFn: (pre, post) => Cv2.Norm(pre, post) > 10,
            TimeoutMs: 30,
            Description: "Retry button"
        );

        var executor = new ActionContractExecutor(
            captureMock.Object,
            inputMock.Object,
            loggerMock.Object,
            timeMock.Object,
            ExecutionMode.Live,
            defaultMaxRetries: 1
        );

        // Act
        var sw = Stopwatch.StartNew();
        bool result = executor.Execute((30, 40), assertion, maxRetries: 1);
        sw.Stop();

        // Assert
        result.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(50);
        inputMock.Verify(i => i.Tap(30, 40), Times.Exactly(2));
        captureMock.Verify(c => c.GrabFrame(), Times.Exactly(4));
    }

    [Fact]
    public void Live_NullAssertion()
    {
        // Arrange
        var captureMock = new Mock<ICaptureBackend>();
        var inputMock = new Mock<IInputBackend>();
        var loggerMock = new Mock<IBotLogger>();
        var timeMock = new Mock<ITimeProvider>();

        var executor = new ActionContractExecutor(
            captureMock.Object,
            inputMock.Object,
            loggerMock.Object,
            timeMock.Object,
            ExecutionMode.Live
        );

        // Act
        bool result = executor.Execute((100, 200), assertion: null);

        // Assert
        result.Should().BeTrue();
        inputMock.Verify(i => i.Tap(100, 200), Times.Once);
        captureMock.Verify(c => c.GrabFrame(), Times.Never);
    }

    [Fact]
    public void Replay_Bypass()
    {
        // Arrange
        var captureMock = new Mock<ICaptureBackend>();
        var inputMock = new Mock<IInputBackend>();
        var loggerMock = new Mock<IBotLogger>();
        var timeMock = new Mock<ITimeProvider>();

        var assertion = new VisualAssertion(
            Region: new Rect(0, 0, 100, 100),
            VerifyFn: (pre, post) => false,
            TimeoutMs: 1000
        );

        var executor = new ActionContractExecutor(
            captureMock.Object,
            inputMock.Object,
            loggerMock.Object,
            timeMock.Object,
            ExecutionMode.Replay
        );

        // Act
        var sw = Stopwatch.StartNew();
        bool result = executor.Execute((10, 20), assertion);
        sw.Stop();

        // Assert
        result.Should().BeTrue();
        sw.ElapsedMilliseconds.Should().BeLessThan(50);
        inputMock.Verify(i => i.Tap(10, 20), Times.Once);
        captureMock.Verify(c => c.GrabFrame(), Times.Never);
    }

    [Fact]
    public void InvalidClick_Negative()
    {
        // Arrange
        var captureMock = new Mock<ICaptureBackend>();
        var inputMock = new Mock<IInputBackend>();
        var loggerMock = new Mock<IBotLogger>();
        var timeMock = new Mock<ITimeProvider>();

        var executor = new ActionContractExecutor(
            captureMock.Object,
            inputMock.Object,
            loggerMock.Object,
            timeMock.Object,
            ExecutionMode.Live
        );

        // Act & Assert
        Action act = () => executor.Execute((-1, 50));
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void RegionClamp()
    {
        // Arrange
        var captureMock = new Mock<ICaptureBackend>();
        var inputMock = new Mock<IInputBackend>();
        var loggerMock = new Mock<IBotLogger>();
        var timeMock = new Mock<ITimeProvider>();

        // Frame 860x732
        captureMock.Setup(c => c.GrabFrame())
            .Returns(() => new Mat(732, 860, MatType.CV_8UC3, Scalar.Black));

        // Region vượt khỏi biên (900, 700, 100, 100)
        var assertion = new VisualAssertion(
            Region: new Rect(900, 700, 100, 100),
            VerifyFn: (pre, post) => true,
            TimeoutMs: 100
        );

        var executor = new ActionContractExecutor(
            captureMock.Object,
            inputMock.Object,
            loggerMock.Object,
            timeMock.Object,
            ExecutionMode.Live
        );

        // Act & Assert
        Action act = () => executor.Execute((100, 100), assertion);
        act.Should().NotThrow();
    }
}
