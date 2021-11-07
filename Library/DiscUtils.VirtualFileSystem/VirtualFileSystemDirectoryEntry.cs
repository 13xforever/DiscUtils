﻿using System;
using System.IO;
using System.Linq;
using DiscUtils.Core.WindowsSecurity.AccessControl;

namespace DiscUtils.VirtualFileSystem
{
    public abstract class VirtualFileSystemDirectoryEntry
    {
        public VirtualFileSystem FileSystem { get; }

        public VirtualFileSystemDirectory Parent { get; }

        public virtual FileAttributes Attributes { get; set; } = FileAttributes.Archive;

        public DateTime LastWriteTimeUtc { get; set; } = DateTime.UtcNow;

        public DateTime LastAccessTimeUtc { get; set; } = DateTime.UtcNow;

        public DateTime CreationTimeUtc { get; set; } = DateTime.UtcNow;

        public RawSecurityDescriptor SecurityDescriptor { get; set; }

        public ReparsePoint ReparsePoint { get; set; }

        public string ShortName { get; set; }

        public abstract long FileId { get; }

        internal VirtualFileSystemDirectoryEntry(VirtualFileSystem fileSystem) =>
            FileSystem = fileSystem ?? throw new ArgumentNullException(nameof(FileSystem));

        internal VirtualFileSystemDirectoryEntry(VirtualFileSystemDirectory parent, string name)
        {
            Parent = parent ?? throw new ArgumentNullException(nameof(parent));
            FileSystem = parent.FileSystem ?? throw new ArgumentException(nameof(parent), "FileSystem property is null");

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException(nameof(name), "File names cannot be null or empty");
            }

            parent.AddEntry(name, this);
        }

        public WindowsFileInformation GetStandardInformation() => new()
        {
            ChangeTime = LastWriteTimeUtc.ToLocalTime(),
            CreationTime = CreationTimeUtc.ToLocalTime(),
            LastAccessTime = LastAccessTimeUtc.ToLocalTime(),
            LastWriteTime = LastWriteTimeUtc.ToLocalTime(),
            FileAttributes = Attributes
        };

        public void SetStandardInformation(WindowsFileInformation info)
        {
            CreationTimeUtc = info.CreationTime.ToUniversalTime();
            LastAccessTimeUtc = info.LastAccessTime.ToUniversalTime();
            LastWriteTimeUtc = info.LastWriteTime.ToUniversalTime();
            Attributes = info.FileAttributes;
        }

        public virtual void Delete()
        {
            if (Parent == null)
            {
                throw new IOException("Root directory cannot be removed");
            }

            Parent.RemoveEntry(this);
        }

        public virtual string Name => Parent?.EnumerateNamesForEntry(this).FirstOrDefault() ?? string.Empty;

        public string FullPath => Parent != null ? Path.Combine(Parent.FullPath, Name) : string.Empty;

        public override string ToString() => FullPath;

        public VirtualFileSystemDirectoryEntry Move(VirtualFileSystemDirectory new_parent, string new_name, bool replace)
        {
            var existing = new_parent.GetEntry(new_name);

            if (existing != null)
            {
                if (replace)
                {
                    existing.Parent.RemoveEntry(existing);
                }
                else
                {
                    throw new IOException($"File or directory '{new_name}' already exists in directory '{new_parent}'");
                }
            }

            var new_entry = AddLink(new_parent, new_name);
            
            Parent.RemoveEntry(this);
            
            return new_entry;
        }

        public abstract VirtualFileSystemDirectoryEntry AddLink(VirtualFileSystemDirectory new_parent, string new_name);
    }
}
