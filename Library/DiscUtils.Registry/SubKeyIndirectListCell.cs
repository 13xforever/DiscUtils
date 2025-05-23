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
using System.Collections.Generic;
using DiscUtils.Streams;

namespace DiscUtils.Registry;

internal sealed class SubKeyIndirectListCell : ListCell
{
    private readonly RegistryHive _hive;

    public SubKeyIndirectListCell(RegistryHive hive, int index)
        : base(index)
    {
        _hive = hive;
    }

    public List<int> CellIndexes { get; private set; }

    internal override int Count
    {
        get
        {
            var total = 0;
            foreach (var cellIndex in CellIndexes)
            {
                var cell = _hive.GetCell<Cell>(cellIndex);
                if (cell is ListCell listCell)
                {
                    total += listCell.Count;
                }
                else
                {
                    total++;
                }
            }

            return total;
        }
    }

    public string ListType { get; private set; }

    public override int Size => 4 + CellIndexes.Count * 4;

    public override int ReadFrom(ReadOnlySpan<byte> buffer)
    {
        var latin1Encoding = EncodingUtilities.GetLatin1Encoding();

        ListType = latin1Encoding.GetString(buffer.Slice(0, 2));
        int numElements = EndianUtilities.ToInt16LittleEndian(buffer.Slice(2));
        CellIndexes = new List<int>(numElements);

        for (var i = 0; i < numElements; ++i)
        {
            CellIndexes.Add(EndianUtilities.ToInt32LittleEndian(buffer.Slice(0x4 + i * 0x4)));
        }

        return 4 + CellIndexes.Count * 4;
    }

    public override void WriteTo(Span<byte> buffer)
    {
        EncodingUtilities
            .GetLatin1Encoding()
            .GetBytes(ListType, buffer.Slice(0, 2));

        EndianUtilities.WriteBytesLittleEndian((ushort)CellIndexes.Count, buffer.Slice(2));
        for (var i = 0; i < CellIndexes.Count; ++i)
        {
            EndianUtilities.WriteBytesLittleEndian(CellIndexes[i], buffer.Slice(4 + i * 4));
        }
    }

    internal override int FindKey(string name, out int cellIndex)
    {
        if (CellIndexes.Count <= 0)
        {
            cellIndex = 0;
            return -1;
        }

        // Check first and last, to early abort if the name is outside the range of this list
        var result = DoFindKey(name, 0, out cellIndex);
        if (result <= 0)
        {
            return result;
        }

        result = DoFindKey(name, CellIndexes.Count - 1, out cellIndex);
        if (result >= 0)
        {
            return result;
        }

        var finder = new KeyFinder(_hive, name);
        var idx = CellIndexes.BinarySearch(-1, finder);
        cellIndex = finder.CellIndex;
        return idx < 0 ? -1 : 0;
    }

    internal override IEnumerable<string> EnumerateKeyNames()
    {
        for (var i = 0; i < CellIndexes.Count; ++i)
        {
            var cell = _hive.GetCell<Cell>(CellIndexes[i]);
            if (cell is ListCell listCell)
            {
                foreach (var name in listCell.EnumerateKeyNames())
                {
                    yield return name;
                }
            }
            else
            {
                yield return ((KeyNodeCell)cell).Name;
            }
        }
    }

    internal override IEnumerable<KeyNodeCell> EnumerateKeys()
    {
        for (var i = 0; i < CellIndexes.Count; ++i)
        {
            var cell = _hive.GetCell<Cell>(CellIndexes[i]);
            if (cell is ListCell listCell)
            {
                foreach (var keyNodeCell in listCell.EnumerateKeys())
                {
                    yield return keyNodeCell;
                }
            }
            else
            {
                yield return (KeyNodeCell)cell;
            }
        }
    }

    internal override int LinkSubKey(string name, int cellIndex)
    {
        // Look for the first sublist that has a subkey name greater than name
        if (ListType == "ri")
        {
            if (CellIndexes.Count == 0)
            {
                throw new NotImplementedException("Empty indirect list");
            }

            for (var i = 0; i < CellIndexes.Count - 1; ++i)
            {
                var cell = _hive.GetCell<ListCell>(CellIndexes[i]);
                if (cell.FindKey(name, out var tempIndex) <= 0)
                {
                    CellIndexes[i] = cell.LinkSubKey(name, cellIndex);
                    return _hive.UpdateCell(this, false);
                }
            }

            var lastCell = _hive.GetCell<ListCell>(CellIndexes[CellIndexes.Count - 1]);
            CellIndexes[CellIndexes.Count - 1] = lastCell.LinkSubKey(name, cellIndex);
            return _hive.UpdateCell(this, false);
        }

        for (var i = 0; i < CellIndexes.Count; ++i)
        {
            var cell = _hive.GetCell<KeyNodeCell>(CellIndexes[i]);
            if (string.Compare(name, cell.Name, StringComparison.OrdinalIgnoreCase) < 0)
            {
                CellIndexes.Insert(i, cellIndex);
                return _hive.UpdateCell(this, true);
            }
        }

        CellIndexes.Add(cellIndex);
        return _hive.UpdateCell(this, true);
    }

    internal override int UnlinkSubKey(string name)
    {
        if (ListType == "ri")
        {
            if (CellIndexes.Count == 0)
            {
                throw new NotImplementedException("Empty indirect list");
            }

            for (var i = 0; i < CellIndexes.Count; ++i)
            {
                var cell = _hive.GetCell<ListCell>(CellIndexes[i]);
                if (cell.FindKey(name, out var tempIndex) <= 0)
                {
                    CellIndexes[i] = cell.UnlinkSubKey(name);
                    if (cell.Count == 0)
                    {
                        _hive.FreeCell(CellIndexes[i]);
                        CellIndexes.RemoveAt(i);
                    }

                    return _hive.UpdateCell(this, false);
                }
            }
        }
        else
        {
            for (var i = 0; i < CellIndexes.Count; ++i)
            {
                var cell = _hive.GetCell<KeyNodeCell>(CellIndexes[i]);
                if (string.Equals(name, cell.Name, StringComparison.OrdinalIgnoreCase))
                {
                    CellIndexes.RemoveAt(i);
                    return _hive.UpdateCell(this, true);
                }
            }
        }

        return Index;
    }

    private int DoFindKey(string name, int listIndex, out int cellIndex)
    {
        var cell = _hive.GetCell<Cell>(CellIndexes[listIndex]);
        if (cell is ListCell listCell)
        {
            return listCell.FindKey(name, out cellIndex);
        }

        cellIndex = CellIndexes[listIndex];
        return string.Compare(name, ((KeyNodeCell)cell).Name, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class KeyFinder : IComparer<int>
    {
        private readonly RegistryHive _hive;
        private readonly string _searchName;

        public KeyFinder(RegistryHive hive, string searchName)
        {
            _hive = hive;
            _searchName = searchName;
        }

        public int CellIndex { get; set; }

        #region IComparer<int> Members

        public int Compare(int x, int y)
        {
            var cell = _hive.GetCell<Cell>(x);

            int result;
            if (cell is ListCell listCell)
            {
                result = listCell.FindKey(_searchName, out var cellIndex);
                if (result == 0)
                {
                    CellIndex = cellIndex;
                }

                return -result;
            }

            result = string.Compare(((KeyNodeCell)cell).Name, _searchName, StringComparison.OrdinalIgnoreCase);
            if (result == 0)
            {
                CellIndex = x;
            }

            return result;
        }

        #endregion
    }
}