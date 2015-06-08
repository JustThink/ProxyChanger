using System;
using System.IO;
using System.Threading;

namespace ProxyChanger
{
    public static class FileLocker
    {
        #region Дожидаемся доступа к файлу

        public static void Lock(string path, Action<FileStream> action) { Lock(path, -1, action); }

        public static void Lock(string path, int millisecondsTimeout, Action<FileStream> action)
        {
            Lock(path, millisecondsTimeout, action,
                FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public static void Lock(string path, int millisecondsTimeout, Action<FileStream> action,
            FileMode mode, FileAccess access, FileShare share)
        {
            var log = Program.Log;
            var autoResetEvent = new AutoResetEvent(false);

            const string pattern = "Lock('{0}', {1}, {2}, {3}, {4})";
            string msg = string.Format(pattern, path, millisecondsTimeout, mode, access, share);
            log.Trace(msg);

            while (true)
            {
                try
                {
                    using (var stream = File.Open(path, mode, access, share))
                    {
                        const string pattern2 = "Access to file: {0}, {1}, {2}, {3}";
                        string msg2 = string.Format(pattern2, path, mode, access, share);
                        log.Trace(msg2);

                        action(stream);
                        break;
                    }
                }
                catch (IOException exc)
                {
                    const string pattern3 = "Cannot access to file: {0}";
                    string msg3 = string.Format(pattern3, path);
                    log.TraceException(msg3, exc);

                    AutoResetFileSystemWatcher(path, millisecondsTimeout, autoResetEvent);
                }
            }
        }

        #endregion

        private static void AutoResetFileSystemWatcher(string path, int millisecondsTimeout, AutoResetEvent autoResetEvent)
        {
            var log = Program.Log;

            string directoryName;
            try
            {
                directoryName = Path.GetDirectoryName(path);
            }
            catch (Exception ex)
            {
                log.ErrorException("Failed to get directory name: " + path, ex);
                return;
            }

            if (string.IsNullOrEmpty(directoryName))
            {
                log.Error("Directory name is empty");
                return;
            }

            using (var fileSystemWatcher = new FileSystemWatcher(directoryName))
            {
                fileSystemWatcher.EnableRaisingEvents = true;

                fileSystemWatcher.Changed +=
                (o, e) =>
                {
                    if (Path.GetFullPath(e.FullPath) == Path.GetFullPath(path))
                    {
                        const string pattern = "Changed file system watcher for file: {0}";
                        string msg2 = string.Format(pattern, path);
                        log.Trace(msg2);

                        autoResetEvent.Set();
                    }
                };

                autoResetEvent.WaitOne(millisecondsTimeout);
            }
        }
    }
}
