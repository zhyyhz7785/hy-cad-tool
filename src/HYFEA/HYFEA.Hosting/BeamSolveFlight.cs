using System.Threading;

namespace HYFEA.Hosting;

/// <summary>单飞求解票：取消上一趟，Present 只接受当前序号。</summary>
public sealed class BeamSolveFlight
{
    private int _serial;
    private CancellationTokenSource? _cts;

    public BeamSolveTicket Begin()
    {
        var next = new CancellationTokenSource();
        var prev = Interlocked.Exchange(ref _cts, next);
        if (prev != null)
        {
            try
            {
                prev.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            prev.Dispose();
        }

        return new BeamSolveTicket(Interlocked.Increment(ref _serial), next.Token);
    }

    public bool IsCurrent(int serial) => serial == Volatile.Read(ref _serial);

    /// <summary>序号过期或活动文档已换则丢弃，避免画到错误图。</summary>
    public bool ShouldPresent(int serial, object? expectedDocument, object? activeDocument)
    {
        if (!IsCurrent(serial))
            return false;
        if (expectedDocument == null || activeDocument == null)
            return false;
        return ReferenceEquals(expectedDocument, activeDocument);
    }
}

public readonly struct BeamSolveTicket
{
    public BeamSolveTicket(int serial, CancellationToken token)
    {
        Serial = serial;
        Token = token;
    }

    public int Serial { get; }

    public CancellationToken Token { get; }
}
