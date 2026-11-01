using System.Text;

namespace DotNetFM;

public sealed class TypeAheadBuffer
{
    private const int TimeoutMs = 800;

    private readonly StringBuilder _buffer = new();
    private int _lastKeyTick;

    public void Append(char c)
    {
        int now = Environment.TickCount;

        if (_buffer.Length > 0 && (now - _lastKeyTick) > TimeoutMs)
            _buffer.Clear();

        _buffer.Append(c);
        _lastKeyTick = now;
    }

    public string Prefix => _buffer.ToString();

    public void Reset() => _buffer.Clear();
}
