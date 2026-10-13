/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2013 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

namespace OpenTyrian.Core;

public sealed class TextTokenReader(ReadOnlyMemory<byte> text)
{
    public ReadOnlyMemory<byte> Remaining { get; private set; } = text;

    private ReadOnlyMemory<byte> CString()
    {
        var end = Remaining.Span.IndexOf((byte)0);
        return end < 0 ? Remaining : Remaining[..end];
    }

    public bool TryReadInt32(out int value)
    {
        value = 0;
        var source = CString(); var offset = 0;
        while (offset < source.Length && ItemDefinitions.IsSpace((char)source.Span[offset])) offset++;
        var negative = offset < source.Length && source.Span[offset] == (byte)'-';
        if (offset < source.Length && source.Span[offset] is (byte)'+' or (byte)'-') offset++;
        var start = offset;
        var limit = OperatingSystem.IsWindows() ? negative ? 2147483648UL : 2147483647UL :
            negative ? 9223372036854775808UL : 9223372036854775807UL;
        ulong magnitude = 0;
        while (offset < source.Length && source.Span[offset] is >= (byte)'0' and <= (byte)'9')
        {
            var digit = (ulong)(source.Span[offset++] - (byte)'0');
            magnitude = magnitude > (limit - digit) / 10 ? limit : magnitude * 10 + digit;
        }
        if (offset == start) return false;
        value = unchecked((int)(negative ? 0UL - magnitude : magnitude));
        Remaining = source[offset..];
        return true;
    }

    public bool TryReadToken(int capacity, out ReadOnlyMemory<byte> value)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);
        value = default;
        var source = CString(); var start = 0;
        while (start < source.Length && source.Span[start] is (byte)' ' or (byte)'\t') start++;
        var end = start;
        while (end < source.Length && source.Span[end] is not ((byte)' ' or (byte)'\t')) end++;
        if (end == start) return false;
        if (end - start >= capacity) throw new InvalidDataException("error: item reference exceeds its maximum length");
        value = source[start..end];
        while (end < source.Length && source.Span[end] is (byte)' ' or (byte)'\t') end++;
        Remaining = source[end..];
        return true;
    }
}
