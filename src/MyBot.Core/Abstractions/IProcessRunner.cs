using System.Text;

namespace MyBot.Core.Abstractions;

public record ProcessResult(int ExitCode, byte[] RawStdOut, string StdErr = "")
{
    public ProcessResult(int exitCode, string stdOut, string stdErr = "")
        : this(exitCode, Encoding.UTF8.GetBytes(stdOut), stdErr)
    {
    }

    public string StdOut => Encoding.UTF8.GetString(RawStdOut);
    public bool IsSuccess => ExitCode == 0;
}

public interface IProcessRunner
{
    ProcessResult Run(string fileName, string arguments);
    ProcessResult Run(string fileName, string arguments, int timeoutMs);
}
