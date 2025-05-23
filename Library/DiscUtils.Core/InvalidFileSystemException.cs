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
using System.IO;
using System.Runtime.Serialization;
using System.Security.Permissions;

namespace DiscUtils;

/// <summary>
/// Exception thrown when some invalid file system data is found, indicating probably corruption.
/// </summary>
[Serializable]
public class InvalidFileSystemException : IOException
{
    /// <summary>
    /// Initializes a new instance of the InvalidFileSystemException class.
    /// </summary>
    public InvalidFileSystemException() {}

    /// <summary>
    /// Initializes a new instance of the InvalidFileSystemException class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    public InvalidFileSystemException(string message)
        : base(message) {}

    /// <summary>
    /// Initializes a new instance of the InvalidFileSystemException class.
    /// </summary>
    /// <param name="message">The exception message.</param>
    /// <param name="innerException">The inner exception.</param>
    public InvalidFileSystemException(string message, Exception innerException)
        : base(message, innerException) {}

    /// <summary>
    /// Initializes a new instance of the InvalidFileSystemException class.
    /// </summary>
    /// <param name="info">The serialization info.</param>
    /// <param name="context">The streaming context.</param>
#if !NETCOREAPP
    [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.SerializationFormatter)]
#elif NET8_0_OR_GREATER
    [Obsolete("Binary serialization deprecated")]
#endif
    protected InvalidFileSystemException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}