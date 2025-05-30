//
// Copyright (c) 2008-2011, Kenneth Bell
//
// Permission is hereby granted, free of charge, to any person obtaining a
// copy of this software and associated documentation files (the "Software"),
// to deal in the Software without restriction, including without limitation
// the rights to use, copy, modify, merge, publish, distribute, sublicense,
// and/or sell copies of the Software, and to permit persons to whom the
// Software is furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
// DEALINGS IN THE SOFTWARE.
//

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Management.Automation;
using System.Management.Automation.Provider;
using System.Text;

namespace DiscUtils.PowerShell.VirtualDiskProvider;

#pragma warning disable IDE0079 // Remove unnecessary suppression

internal sealed class FileContentReaderWriter : IContentWriter, IContentReader
{
    private Provider _provider;
    private Stream _contentStream;
    private ContentEncoding _encoding;
    private StreamReader _reader;
    private StreamWriter _writer;

    public FileContentReaderWriter(Provider provider, Stream contentStream, ContentParameters dynParams)
    {
        _provider = provider;
        _contentStream = contentStream;
        _contentStream.Position = 0;
        if (dynParams != null)
        {
            _encoding = dynParams.Encoding;
        }
    }

    public void Close()
    {
        _writer?.Flush();

        try
        {
            _contentStream.Close();
        }
        catch (Exception e)
        {
            _provider.WriteError(
                new ErrorRecord(
                    new IOException("Failure using virtual disk", e),
                    "CloseFailed",
                    ErrorCategory.WriteError,
                    null));
        }
    }

    public void Seek(long offset, SeekOrigin origin)
    {
        _contentStream.Seek(offset, origin);
    }

    public IList Read(long readCount)
    {
        try
        {
            if (_encoding == ContentEncoding.Byte)
            {
                if (readCount <= 0)
                {
                    readCount = long.MaxValue;
                }

                var maxToRead = (int)Math.Min(Math.Min(readCount, _contentStream.Length - _contentStream.Position), int.MaxValue);

                var fileContent = ArrayPool<byte>.Shared.Rent(maxToRead);
                try
                {
                    var numRead = _contentStream.Read(fileContent, 0, maxToRead);

                    var result = new object[numRead];
                    for (var i = 0; i < numRead; ++i)
                    {
                        result[i] = fileContent[i];
                    }

                    return result;
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(fileContent);
                }
            }
            else
            {
                var result = new List<object>();

                if (_reader == null)
                {
                    if (_encoding == ContentEncoding.Unknown)
                    {
                        _reader = new StreamReader(_contentStream, Encoding.ASCII, true);
                    }
                    else
                    {
                        _reader = new StreamReader(_contentStream, GetEncoding(Encoding.ASCII));
                    }
                }

                while ((result.Count < readCount || readCount <= 0) && !_reader.EndOfStream)
                {
                    result.Add(_reader.ReadLine());
                }

                return result;
            }
        }
        catch (Exception e)
        {
            _provider.WriteError(
                new ErrorRecord(
                    new IOException($"Failure reading from virtual disk{e}", e),
                    "ReadFailed",
                    ErrorCategory.ReadError,
                    null));
            return null;
        }
    }

    public IList Write(IList content)
    {
        try
        {
            if (content == null || content.Count == 0)
            {
                return content;
            }

            if (content is byte[] bytes)
            {
                _contentStream.Write(bytes, 0, bytes.Length);

                return content;
            }
            else if (content[0].GetType() == typeof(byte))
            {
                var buffer = ArrayPool<byte>.Shared.Rent(content.Count);
                try
                {
                    for (var i = 0; i < content.Count; ++i)
                    {
                        buffer[i] = (byte)content[i];
                    }

                    _contentStream.Write(buffer, 0, content.Count);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
                return content;
            }
            else if ((content[0] as string) != null)
            {
                if (_writer == null)
                {
                    var initialContent = (string)content[0];
                    var foundExtended = false;
                    var toInspect = Math.Min(20, initialContent.Length);
                    for (var i = 0; i < toInspect; ++i)
                    {
                        if (initialContent[i] > 127)
                        {
                            foundExtended = true;
                        }
                    }

                    _writer = new StreamWriter(_contentStream, GetEncoding(foundExtended ? Encoding.Unicode : Encoding.ASCII));
                }

                string lastLine = null;
                foreach (string s in content)
                {
                    _writer.WriteLine(s);
                    lastLine = s;
                }

                _writer.Flush();

                return content;
            }
            else
            {
                return null;
            }
        }
        catch (Exception e)
        {
            _provider.WriteError(
                new ErrorRecord(
                    new IOException("Failure writing to virtual disk", e),
                    "WriteFailed",
                    ErrorCategory.WriteError,
                    null));
            return null;
        }
    }

    public void Dispose()
    {
        _writer?.Dispose();

        _reader?.Dispose();

        _contentStream?.Dispose();
    }

    private Encoding GetEncoding(Encoding defEncoding)
    {
        return _encoding switch
        {
            ContentEncoding.BigEndianUnicode => Encoding.BigEndianUnicode,
            ContentEncoding.UTF8 => Encoding.UTF8,
#pragma warning disable SYSLIB0001 // Type or member is obsolete
            ContentEncoding.UTF7 => Encoding.UTF7,
#pragma warning restore SYSLIB0001 // Type or member is obsolete
            ContentEncoding.Unicode => Encoding.Unicode,
            ContentEncoding.Ascii => Encoding.ASCII,
            _ => defEncoding,
        };
    }

}
