using System;
using System.Collections.Generic;
using System.Text;
using MyBot.Core.Abstractions;

namespace MyBot.Core.Tests.Mocks;

public class MockProcessRunner : IProcessRunner
{
    private readonly Queue<Func<string, string, int, ProcessResult>> _queue = new();
    private readonly List<(string FileName, string Arguments, int TimeoutMs)> _callHistory = new();

    public int RunCount => _callHistory.Count;
    public IReadOnlyList<(string FileName, string Arguments, int TimeoutMs)> LastArguments => _callHistory;

    public void QueueResult(ProcessResult result)
    {
        _queue.Enqueue((file, args, timeout) => result);
    }

    public void QueueResult(int exitCode, byte[] rawStdOut, string stdErr = "")
    {
        _queue.Enqueue((file, args, timeout) => new ProcessResult(exitCode, rawStdOut, stdErr));
    }

    public void QueueResult(int exitCode, string stdOut, string stdErr = "")
    {
        _queue.Enqueue((file, args, timeout) => new ProcessResult(exitCode, Encoding.UTF8.GetBytes(stdOut), stdErr));
    }

    public void QueueResponder(Func<string, string, int, ProcessResult> responder)
    {
        _queue.Enqueue(responder);
    }

    public ProcessResult Run(string fileName, string arguments) => Run(fileName, arguments, 5000);

    public ProcessResult Run(string fileName, string arguments, int timeoutMs)
    {
        _callHistory.Add((fileName, arguments, timeoutMs));

        if (_queue.Count == 0)
        {
            throw new InvalidOperationException($"MockProcessRunner queue empty — expected {_callHistory.Count - 1} calls, got {_callHistory.Count}");
        }

        var responder = _queue.Dequeue();
        return responder(fileName, arguments, timeoutMs);
    }
}
