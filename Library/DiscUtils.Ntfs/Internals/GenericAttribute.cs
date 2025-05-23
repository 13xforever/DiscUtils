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

using DiscUtils.Streams;

namespace DiscUtils.Ntfs.Internals;

/// <summary>
/// Base class for all attributes within Master File Table entries.
/// </summary>
/// <remarks>
/// More specialized base classes are provided for known attribute types.
/// </remarks>
public abstract class GenericAttribute : IAttributeLocator
{
    private readonly INtfsContext _context;
    private readonly AttributeRecord _record;

    internal GenericAttribute(INtfsContext context, AttributeRecord record)
    {
        _context = context;
        _record = record;
    }

    /// <summary>
    /// Gets the type of the attribute.
    /// </summary>
    public AttributeType AttributeType => _record.AttributeType;

    /// <summary>
    /// Gets a buffer that can access the content of the attribute.
    /// </summary>
    public IBuffer Content
    {
        get
        {
            var rawBuffer = _record.GetReadOnlyDataBuffer(_context);
            return new SubBuffer(rawBuffer, 0, _record.DataLength);
        }
    }

    /// <summary>
    /// Gets the amount of valid data in the attribute's content.
    /// </summary>
    public long ContentLength => _record.DataLength;

    /// <summary>
    /// Gets the flags indicating how the content of the attribute is stored.
    /// </summary>
    public AttributeFlags Flags => _record.Flags;

    /// <summary>
    /// Gets the unique id of the attribute.
    /// </summary>
    public ushort Identifier => _record.AttributeId;

    /// <summary>
    /// Gets a value indicating whether the attribute content is stored in the MFT record itself.
    /// </summary>
    public bool IsResident => !_record.IsNonResident;

    /// <summary>
    /// Gets the name of the attribute (if any).
    /// </summary>
    public string Name => _record.Name;

    public long FirstFileCluster => _record.StartVcn;

    internal static GenericAttribute FromAttributeRecord(INtfsContext context, AttributeRecord record)
    {
        return record.AttributeType switch
        {
            AttributeType.AttributeList => new AttributeListAttribute(context, record),
            AttributeType.FileName => new FileNameAttribute(context, record),
            AttributeType.StandardInformation => new StandardInformationAttribute(context, record),
            _ => new UnknownAttribute(context, record),
        };
    }
}