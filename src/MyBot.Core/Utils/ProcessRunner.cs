using System;
using System.Diagnostics;
using System.IO;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Utils;

public class ProcessRunner : IProcessRunner
{
    public ProcessResult Run(string fileName, string arguments) => Run(fileName, arguments, 5000);

    public ProcessResult Run(string fileName, string arguments, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi);
        if (process == null)
        {
            throw new InvalidOperationException($"Không thể khởi tạo tiến trình: {fileName} {arguments}");
        }

        using var ms = new MemoryStream();
        // Đọc stdout và stderr
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(ms);
        var errorTask = process.StandardError.ReadToEndAsync();

        bool exited = process.WaitForExit(timeoutMs);
        if (!exited)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Process có thể đã thoát ngay lúc chuẩn bị kill
            }

            return new ProcessResult(-1, Array.Empty<byte>(), $"timeout after {timeoutMs}ms");
        }

        outputTask.GetAwaiter().GetResult();
        string stdErr = errorTask.GetAwaiter().GetResult();

        return new ProcessResult(process.ExitCode, ms.ToArray(), stdErr);
    }
}
