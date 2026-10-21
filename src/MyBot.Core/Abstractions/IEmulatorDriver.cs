namespace MyBot.Core.Abstractions;

public record EmulatorInstance(string Id, string Name, int AdbPort, IntPtr WindowHandle);

public interface IEmulatorDriver
{
    string DriverName { get; }
    IEnumerable<EmulatorInstance> DetectInstances();
    bool Launch(string instanceId);
    ICaptureBackend CreateCaptureBackend();
    IInputBackend CreateInputBackend();
}
