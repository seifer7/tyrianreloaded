/*
 * OpenTyrian: A modern cross-platform port of Tyrian
 * Copyright (C) 2007-2009 The OpenTyrian Development Team
 * Distributed under the GNU General Public License, version 2 or later.
 * See COPYING for details.
 */

using System.Buffers.Binary;

namespace OpenTyrian.Core;

public readonly record struct PaletteColor(byte Red, byte Green, byte Blue);

public sealed class AnimationFile : IDisposable
{
    private const int PaletteOffset = 0x100;
    private const int PageHeaderOffset = 0x500;
    private const int AnimationOffset = 0xB00;
    private const int PageSize = 0x10000;
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private readonly PageHeader[] _pages;
    private readonly PaletteColor[] _palette;
    private byte[] _currentPage = [];
    private ushort[] _recordSizes = [];
    private PageHeader _currentHeader;
    private int _currentPageNumber = -1;
    public uint RecordCount { get; }
    public IReadOnlyList<PaletteColor> Palette => Array.AsReadOnly(_palette);

    public AnimationFile(Stream stream, bool leaveOpen = false)
    {
        ArgumentNullException.ThrowIfNull(stream);
        _stream = stream;
        _leaveOpen = leaveOpen;
        try
        {
            if (!stream.CanRead || !stream.CanSeek || stream.Length < AnimationOffset)
            {
                throw new InvalidDataException("Animation file is truncated or not seekable.");
            }

            stream.Position = 0;
            Span<byte> header = stackalloc byte[12];
            BinaryFile.ReadExactly(stream, header);
            var pageCount = BinaryPrimitives.ReadUInt16LittleEndian(header[6..]);
            RecordCount = BinaryPrimitives.ReadUInt32LittleEndian(header[8..]);
            if (!header[..4].SequenceEqual("LPF "u8) || pageCount is 0 or > 256 || RecordCount is 0 or > 65535)
            {
                throw new InvalidDataException("Invalid animation file header.");
            }

            stream.Position = PageHeaderOffset;
            _pages = new PageHeader[pageCount];
            for (var index = 0; index < pageCount; index++)
            {
                _pages[index] = ReadPageHeader();
            }

            var final = _pages[^1];
            var minimumSize = (long)(pageCount - 1) * PageSize + AnimationOffset + final.Bytes + final.Records * 2 + 8;
            if (stream.Length < minimumSize)
            {
                throw new InvalidDataException("Animation page data is truncated.");
            }

            stream.Position = PaletteOffset;
            _palette = new PaletteColor[256];
            Span<byte> color = stackalloc byte[4];
            for (var index = 0; index < _palette.Length; index++)
            {
                BinaryFile.ReadExactly(stream, color);
                _palette[index] = new PaletteColor(color[2], color[1], color[0]);
            }
        }
        catch
        {
            if (!leaveOpen)
            {
                stream.Dispose();
            }

            throw;
        }
    }

    public int FindPage(uint frame)
    {
        for (var index = 0; index < _pages.Length; index++)
        {
            var page = _pages[index];
            if (page.BaseRecord <= frame && page.BaseRecord + page.Records > frame)
            {
                return index;
            }
        }

        return -1;
    }

    public void LoadPage(int pageNumber)
    {
        if ((uint)pageNumber >= _pages.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(pageNumber));
        }

        if (_currentPageNumber == pageNumber)
        {
            return;
        }

        _stream.Position = AnimationOffset + (long)pageNumber * PageSize;
        var page = ReadPageHeader();
        if (page.Records > 256)
        {
            throw new InvalidDataException("Animation page exceeds the native 256-record limit.");
        }

        _stream.Seek(2, SeekOrigin.Current);
        var sizes = new ushort[page.Records];
        BinaryFile.ReadUInt16(_stream, sizes);
        var data = new byte[page.Bytes];
        BinaryFile.ReadExactly(_stream, data);
        if (sizes.Sum(size => (int)size) != page.Bytes)
        {
            throw new InvalidDataException("Animation record lengths do not match the page length.");
        }

        _currentHeader = page;
        _recordSizes = sizes;
        _currentPage = data;
        _currentPageNumber = pageNumber;
    }

    public int RenderFrame(uint frameNumber, Memory<byte> frame)
    {
        var index = (long)frameNumber - _currentHeader.BaseRecord;
        if (_currentPageNumber < 0 || index < 0 || index >= _recordSizes.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(frameNumber), "Frame is not on the loaded page.");
        }

        var offset = 0;
        for (var record = 0; record < index; record++)
        {
            offset += _recordSizes[record];
        }

        var length = _recordSizes[index];
        if (length < 4)
        {
            throw new InvalidDataException("Animation record is missing its four-byte prefix.");
        }

        return AnimationDecoder.Decode(_currentPage.AsMemory(offset + 4, length - 4), frame);
    }

    private PageHeader ReadPageHeader()
    {
        Span<byte> header = stackalloc byte[6];
        BinaryFile.ReadExactly(_stream, header);
        return new PageHeader(
            BinaryPrimitives.ReadUInt16LittleEndian(header),
            BinaryPrimitives.ReadUInt16LittleEndian(header[2..]),
            BinaryPrimitives.ReadUInt16LittleEndian(header[4..]));
    }

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _stream.Dispose();
        }
    }

    private readonly record struct PageHeader(ushort BaseRecord, ushort Records, ushort Bytes);
}
