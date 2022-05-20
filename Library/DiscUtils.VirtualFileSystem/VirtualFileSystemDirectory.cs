﻿using DiscUtils.Internal;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiscUtils.VirtualFileSystem;

public sealed class VirtualFileSystemDirectory : VirtualFileSystemDirectoryEntry
{
    private readonly Dictionary<string, VirtualFileSystemDirectoryEntry> _entries;

    public override FileAttributes Attributes
    {
        get => base.Attributes | FileAttributes.Directory;
        set => base.Attributes = value | FileAttributes.Directory;
    }

    internal VirtualFileSystemDirectory(VirtualFileSystem fileSystem)
        : base(fileSystem)
    {
        StringComparer nameComparer;
        if (!fileSystem.Options.CaseSensitive)
        {
            nameComparer = StringComparer.OrdinalIgnoreCase;
        }
        else
        {
            nameComparer = StringComparer.Ordinal;
        }
        _entries = new Dictionary<string, VirtualFileSystemDirectoryEntry>(nameComparer);
    }

    internal VirtualFileSystemDirectory(VirtualFileSystemDirectory parent, string name)
        : base(parent, name)
    {
        _entries = new Dictionary<string, VirtualFileSystemDirectoryEntry>(parent._entries.Comparer);
    }

    internal VirtualFileSystemDirectory(VirtualFileSystemDirectory parent, string name, VirtualFileSystemDirectory existing_link)
        : base(parent, name)
    {
        _entries = existing_link._entries;
    }

    public VirtualFileSystemDirectoryEntry GetEntry(string name)
    {
        if (_entries.TryGetValue(name, out var found))
        {
            return found;
        }

        return null;
    }

    internal void AddEntry(string name, VirtualFileSystemDirectoryEntry entry) => _entries.Add(name, entry);

    internal void RemoveEntry(VirtualFileSystemDirectoryEntry entry)
    {
        var entries = EnumerateNamesForEntry(entry).ToArray();

        if (!Array.ConvertAll(entries, _entries.Remove).Any(true.Equals))
        {
            throw new FileNotFoundException("File or directory not found");
        }
    }

    public IEnumerable<string> EnumerateNamesForEntry(VirtualFileSystemDirectoryEntry entry) => _entries
        .Where(e => ReferenceEquals(e.Value, entry))
        .Select(e => e.Key);

    public VirtualFileSystemDirectory CreateSubDirectoryTree(string path)
    {
        var path_elements = path.Split(Utilities.PathSeparators, StringSplitOptions.RemoveEmptyEntries);

        var directory = this;

        for (var i = 0; i < path_elements.Length; i++)
        {
            var found = directory.GetEntry(path_elements[i]);

            if (found == null)
            {
                directory = new VirtualFileSystemDirectory(directory, path_elements[i]);
            }
            else if (found is VirtualFileSystemDirectory subdir)
            {
                directory = subdir;
            }
            else
            {
                throw new IOException($"A file with that name already exists: '{path}'");
            }
        }

        return directory;
    }

    public ICollection<string> GetNames() => _entries.Keys;

    public ICollection<VirtualFileSystemDirectoryEntry> GetEntries() => _entries.Values;

    public IEnumerable<KeyValuePair<string, VirtualFileSystemDirectoryEntry>> EnumerateTree()
    {
        foreach (var entry in _entries)
        {
            yield return entry;

            if (entry.Value is VirtualFileSystemDirectory subdir)
            {
                foreach (var subdir_entry in subdir.EnumerateTree())
                {
                    yield return new KeyValuePair<string, VirtualFileSystemDirectoryEntry>(Path.Combine(entry.Key, subdir_entry.Key), subdir_entry.Value);
                }
            }
        }
    }

    public IEnumerable<VirtualFileSystemDirectoryEntry> EnumerateTreeEntries()
    {
        foreach (var entry in _entries.Values)
        {
            yield return entry;

            if (entry is VirtualFileSystemDirectory subdir)
            {
                foreach (var subdir_entry in subdir.EnumerateTreeEntries())
                {
                    yield return subdir_entry;
                }
            }
        }
    }

    public IEnumerable<KeyValuePair<string, VirtualFileSystemDirectory>> EnumerateDirectories() => _entries
        .Where(entry => entry.Value is VirtualFileSystemDirectory)
        .Select(entry => new KeyValuePair<string, VirtualFileSystemDirectory>(entry.Key, entry.Value as VirtualFileSystemDirectory));

    public IEnumerable<KeyValuePair<string, VirtualFileSystemFile>> EnumerateFiles() => _entries
        .Where(entry => entry.Value is VirtualFileSystemFile)
        .Select(entry => new KeyValuePair<string, VirtualFileSystemFile>(entry.Key, entry.Value as VirtualFileSystemFile));

    public override void Delete() => Delete(recursive: false);

    public void Delete(bool recursive)
    {
        if (!recursive && _entries.Count != 0)
        {
            throw new IOException("Directory not empty");
        }

        base.Delete();
    }

    public VirtualFileSystemDirectoryEntry ResolvePathToEntry(string path) =>
        ResolvePath(path.Split(Utilities.PathSeparators, StringSplitOptions.RemoveEmptyEntries), 0);

    private VirtualFileSystemDirectoryEntry ResolvePath(string[] path, int pathindex)
    {
        if (pathindex == path.Length)
        {
            return this;
        }

        if (!_entries.TryGetValue(path[pathindex], out var found))
        {
            return null;
        }

        pathindex++;

        if (pathindex >= path.Length)
        {
            return found;
        }

        if (found is VirtualFileSystemDirectory subdir)
        {
            return subdir.ResolvePath(path, pathindex);
        }

        return null;
    }

    public override long FileId => _entries.GetHashCode();

    public override VirtualFileSystemDirectoryEntry AddLink(VirtualFileSystemDirectory new_parent, string new_name)
        => new VirtualFileSystemDirectory(new_parent, new_name, this);

    public override string Name => $@"{base.Name}\";
}
