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
using System.Linq;

namespace DiscUtils.Common;

public class CommandLineSwitch
{
    private string[] _shortSwitches;
    private string _fullSwitch;
    private string _paramName;
    private string _description;
    private bool _isPresent;
    private string _paramValue;

    public CommandLineSwitch(string fullSwitch, string paramName, string description)
    {
        _shortSwitches = [];
        _fullSwitch = fullSwitch;
        _paramName = paramName;
        _description = description;
    }

    public CommandLineSwitch(string shortSwitch, string fullSwitch, string paramName, string description)
    {
        _shortSwitches = [shortSwitch];
        _fullSwitch = fullSwitch;
        _paramName = paramName;
        _description = description;
    }

    public CommandLineSwitch(string[] shortSwitches, string fullSwitch, string paramName, string description)
    {
        _shortSwitches = shortSwitches;
        _fullSwitch = fullSwitch;
        _paramName = paramName;
        _description = description;
    }

    public string ParameterName => _paramName;

    public string FullSwitchName => _fullSwitch;

    public virtual string FullDescription => _description;

    public bool IsPresent => _isPresent;

    public string Value => _paramValue;

    internal void WriteDescription(TextWriter writer, string lineTemplate, int perLineDescWidth)
    {
        string[] switches;
        switches = BuildSwitchInfo(_shortSwitches, _fullSwitch, _paramName, out var ignore);

        var text = Utilities.WordWrap(FullDescription, perLineDescWidth).ToArray();

        for (var i = 0; i < Math.Max(switches.Length, text.Length); ++i)
        {
            writer.WriteLine(lineTemplate, ((i < switches.Length) ? switches[i] : ""), ((i < text.Length) ? text[i] : ""));
        }
    }

    internal int SwitchDisplayLength
    {
        get
        {
            BuildSwitchInfo(_shortSwitches, _fullSwitch, _paramName, out var maxLen);
            return maxLen;
        }
    }

    private static string[] BuildSwitchInfo(string[] shortSwitches, string fullSwitch, string param, out int maxLen)
    {
        maxLen = 0;

        var result = new string[shortSwitches.Length + 1];

        for (var i = 0; i < shortSwitches.Length; ++i)
        {
            result[i] = $"-{shortSwitches[i]}";
            if (param != null)
            {
                result[i] += $" <{param}>";
            }

            maxLen = Math.Max(result[i].Length, maxLen);
        }

        result[result.Length - 1] = $"-{fullSwitch}";
        if (param != null)
        {
            result[result.Length - 1] += $" <{param}>";
        }

        maxLen = Math.Max(result[result.Length - 1].Length, maxLen);

        return result;
    }

    internal bool Matches(string switchName)
    {
        if (switchName == _fullSwitch)
        {
            return true;
        }

        foreach (var sw in _shortSwitches)
        {
            if (sw == switchName)
            {
                return true;
            }
        }

        return false;
    }

    internal virtual int Process(string[] args, int pos)
    {
        _isPresent = true;

        if (!string.IsNullOrEmpty(_paramName))
        {
            if (pos >= args.Length)
            {
                throw new Exception($"Command-line switch {_fullSwitch} is missing value");
            }

            _paramValue = args[pos];
            ++pos;
        }

        return pos;
    }
}
