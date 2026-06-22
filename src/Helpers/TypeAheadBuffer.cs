using System.Text;

namespace DotNetFM;

/// <summary>
/// Accumulates typed characters into a soft search buffer with a sliding
/// timeout. Feeding a character after the timeout clears the buffer first,
/// so the next keystroke starts a fresh search. Pure state machine — no UI
/// dependencies. Callers own prefix matching against their own data.
/// </summary>
public sealed class TypeAheadBuffer
{
    /// <summary>How long the buffer stays live between keystrokes (ms).</summary>
    private const int TimeoutMs = 800;

    private readonly StringBuilder _buffer = new();
    private int _lastKeyTick;

    /// <summary>
    /// Appends a character to the buffer, resetting it first when more than
    /// <see cref="TimeoutMs"/> has elapsed since the previous keystroke.
    /// </summary>
    public void Append(char c)
    {
        int now = Environment.TickCount;

        if (_buffer.Length > 0 && (now - _lastKeyTick) > TimeoutMs)
            _buffer.Clear();

        _buffer.Append(c);
        _lastKeyTick = now;
    }

    /// <summary>The current search prefix, or empty when nothing is buffered.</summary>
    public string Prefix => _buffer.ToString();

    /// <summary>Clears the buffer so the next keystroke starts a fresh search.</summary>
    public void Reset() => _buffer.Clear();
}
