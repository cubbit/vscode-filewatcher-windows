/*---------------------------------------------------------
 * Copyright (C) Microsoft Corporation. All rights reserved.
 *--------------------------------------------------------*/

using System;
using System.IO;

namespace VSCode.FileSystem
{

    public class FileEvent
    {
        public int changeType { get; set; }
        public string path { get; set; }
		public string oldPath { get; set; }
	}

    public enum ChangeType : int
    {
        CHANGED = 0,
        CREATED = 1,
        DELETED = 2,
        RENAMED = 3,
        LOG = 4,
    }

    public class FileWatcher
    {
        private string watchPath;
        private int prefixLength = 0;
        private Action<FileEvent> eventCallback = null;

        public FileSystemWatcher Create(string path, Action<FileEvent> onEvent, Action<ErrorEventArgs> onError)
        {

            watchPath = path;
            prefixLength = watchPath.Length + 1;
            eventCallback = onEvent;

            FileSystemWatcher watcher = new FileSystemWatcher();
            watcher.Path = watchPath;
            watcher.IncludeSubdirectories = true;

            watcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName | NotifyFilters.DirectoryName;

            watcher.Changed += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.CHANGED); });
            watcher.Created += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.CREATED); });
            watcher.Deleted += new FileSystemEventHandler((object source, FileSystemEventArgs e) => { ProcessEvent(e, ChangeType.DELETED); });
            watcher.Renamed += new RenamedEventHandler((object source, RenamedEventArgs e) => { ProcessRenameEvent(e); });
            watcher.Error += new ErrorEventHandler((object source, ErrorEventArgs e) => { onError(e); });

            watcher.InternalBufferSize = 32768; // changing this to a higher value can lead into issues when watching UNC drives

            return watcher;
        }

        private void ProcessEvent(FileSystemEventArgs e, ChangeType changeType)
        {
            this.eventCallback(new FileEvent
            {
                changeType = (int)changeType,
                path = e.FullPath,
            });

            try
			{
                if (changeType == ChangeType.CREATED && (File.Exists(e.FullPath) || Directory.Exists(e.FullPath)))
                {
                    var attributes = File.GetAttributes(e.FullPath);

                    if (attributes.HasFlag(FileAttributes.Directory))
                    {
                        var directories = Directory.GetDirectories(e.FullPath);

                        foreach (var directory in directories)
                        {
                            var eventArg = new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(directory), Path.GetFileName(directory));
                            ProcessEvent(eventArg, changeType);
                        }

                        var files = Directory.GetFiles(e.FullPath);

                        foreach (var file in files)
                        {
                            var eventArg = new FileSystemEventArgs(WatcherChangeTypes.Created, Path.GetDirectoryName(file), Path.GetFileName(file));
                            ProcessEvent(eventArg, changeType);
                        }
                    }
                }
            }
            catch (Exception)
			{
                // Swallowing exception because we dispatched the first event anyway
			}

            
        }

        private void ProcessRenameEvent(RenamedEventArgs e)
        {
            var newInPath = e.FullPath.StartsWith(watchPath);
            var oldInPath = e.OldFullPath.StartsWith(watchPath);

            if (newInPath && oldInPath)
			{
                this.eventCallback(new FileEvent
                {
                    changeType = (int)ChangeType.RENAMED,
                    path = e.FullPath,
                    oldPath = e.OldFullPath,
                });
			}
            else
			{
                if (newInPath)
                {
                    this.eventCallback(new FileEvent
                    {
                        changeType = (int)ChangeType.CREATED,
                        path = e.FullPath
                    });
                }

                if (oldInPath)
                {
                    this.eventCallback(new FileEvent
                    {
                        changeType = (int)ChangeType.DELETED,
                        path = e.OldFullPath
                    });
                }
            }
        }
    }
}