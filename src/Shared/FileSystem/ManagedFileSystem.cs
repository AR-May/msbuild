// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Microsoft.Build.Shared.FileSystem
{
    /// <summary>
    /// Implementation of file system operations directly over the dot net managed layer
    /// </summary>
    internal class ManagedFileSystem : IFileSystem
    {
        private static readonly ManagedFileSystem Instance = new ManagedFileSystem();

        private static object locker = new object();
        public static ManagedFileSystem Singleton() => ManagedFileSystem.Instance;

        protected ManagedFileSystem() { }

        public TextReader ReadFile(string path)
        {
            return new StreamReader(path);
        }

        public Stream GetFileStream(string path, FileMode mode, FileAccess access, FileShare share)
        {
            return new FileStream(path, mode, access, share);
        }

        public string ReadFileAllText(string path)
        {
            return File.ReadAllText(path);
        }

        public byte[] ReadFileAllBytes(string path)
        {
            return File.ReadAllBytes(path);
        }

        private bool ResultsTheSame(IEnumerable<string> result0, IEnumerable<string> result1)
        {
            return result0.SequenceEqual(result1);
        }

        private static StreamWriter GetDebugFile()
        {
            Stream fileStream = new FileStream(@"C:\Users\alinama\work\MSBUILD\issues\Microsoft-IO-Redist#6075\Errors.txt", FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.SequentialScan);
            return new StreamWriter(fileStream);
        }

        public virtual IEnumerable<string> EnumerateFiles(string path, string searchPattern, SearchOption searchOption)
        {
#if FEATURE_MSIOREDIST
            string ex0 = "", ex1 = "";
            IEnumerable<string> result0 = null, result1 = null;

            try
            {
                result0 = Directory.EnumerateFiles(path, searchPattern, searchOption);
            }
            catch (Exception ex)
            {
                ex0 = ex.Message;
            }

            try
            {
                result1 = Microsoft.IO.Directory.EnumerateFiles(path, searchPattern, (Microsoft.IO.SearchOption)searchOption);
            }
            catch (Exception ex)
            {
                ex1 = ex.Message;
            }
            lock (locker)
                using (StreamWriter file = GetDebugFile())
                {
                    string res0 = result0 == null ? result0.Aggregate((x, y) => x + ", " + y) : "-";
                    string res1 = result1 == null ? result1.Aggregate((x, y) => x + ", " + y) : "-";

                    if (ex0 != ex1 || (ex0 == "" && !ResultsTheSame(result0, result1)))
                    {
                        file.WriteLine($"Error in EnumerateFiles: path: {path}, searchPattern: {searchPattern}, searchOption: {searchOption}.");
                        file.WriteLine($"ex0: {ex0}\nresult0: {res0}\nex1: {ex1}\nresult1: {res1}");
                    }
                    else
                    {
                        file.WriteLine($"OK in EnumerateFiles: path: {path}, searchPattern: {searchPattern}, searchOption: {searchOption}.");
                        file.WriteLine($"ex0: {ex0}\nresult0: {res0}\nex1: {ex1}\nresult1: {res1}");
                    }
                }

            return ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_0)
                    ? Microsoft.IO.Directory.EnumerateFiles(path, searchPattern, (Microsoft.IO.SearchOption)searchOption)
                    : Directory.EnumerateFiles(path, searchPattern, searchOption);
#else
            return Directory.EnumerateFiles(path, searchPattern, searchOption);
#endif
        }

        public virtual IEnumerable<string> EnumerateDirectories(string path, string searchPattern, SearchOption searchOption)
        {
#if FEATURE_MSIOREDIST
            string ex0 = "", ex1 = "";
            IEnumerable<string> result0 = null, result1 = null;

            try
            {
                result0 = Directory.EnumerateDirectories(path, searchPattern, searchOption);
            }
            catch (Exception ex)
            {
                ex0 = ex.Message;
            }

            try
            {
                result1 = Microsoft.IO.Directory.EnumerateDirectories(path, searchPattern, (Microsoft.IO.SearchOption)searchOption);
            }
            catch (Exception ex)
            {
                ex1 = ex.Message;
            }

            lock (locker)
                using (StreamWriter file = GetDebugFile())
                {
                    string res0 = result0 == null ? result0.Aggregate((x, y) => x + ", " + y) : "-";
                    string res1 = result1 == null ? result1.Aggregate((x, y) => x + ", " + y) : "-";

                    if (ex0 != ex1 || (ex0 == "" && !ResultsTheSame(result0, result1)))
                    {
                        file.WriteLine("$Error in EnumerateDirectories: path: {path}, searchPattern: {searchPattern}, searchOption: {searchOption}.");
                        file.WriteLine("$ex0: {ex0}\nresult0: {res0}\nex1: {ex1}\nresult1: {res1}");
                    }
                    else
                    {
                        file.WriteLine($"OK in EnumerateDirectories: path: {path}, searchPattern: {searchPattern}, searchOption: {searchOption}.");
                        file.WriteLine($"ex0: {ex0}\nresult0: {res0}\nex1: {ex1}\nresult1: {res1}");
                    }
                }

            return ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_0)
                    ? Microsoft.IO.Directory.EnumerateDirectories(path, searchPattern, (Microsoft.IO.SearchOption)searchOption)
                    : Directory.EnumerateDirectories(path, searchPattern, searchOption);
#else
            return Directory.EnumerateDirectories(path, searchPattern, searchOption);
#endif
        }

        public virtual IEnumerable<string> EnumerateFileSystemEntries(string path, string searchPattern, SearchOption searchOption)
        {
#if FEATURE_MSIOREDIST
            string ex0 = "", ex1 = "";
            IEnumerable<string> result0 = null, result1 = null;

            try
            {
                result0 = Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
            }
            catch (Exception ex)
            {
                ex0 = ex.Message;
            }

            try
            {
                result1 = Microsoft.IO.Directory.EnumerateFileSystemEntries(path, searchPattern, (Microsoft.IO.SearchOption)searchOption);
            }
            catch (Exception ex)
            {
                ex1 = ex.Message;
            }
            lock (locker)
                using (StreamWriter file = GetDebugFile())
                {
                    string res0 = result0 == null ? result0.Aggregate((x, y) => x + ", " + y) : "-";
                    string res1 = result1 == null ? result1.Aggregate((x, y) => x + ", " + y) : "-";

                    if (ex0 != ex1 || (ex0 == "" && !ResultsTheSame(result0, result1)))
                    {
                        file.WriteLine($"Error in EnumerateFileSystemEntries: path: {path}, searchPattern: {searchPattern}, searchOption: {searchOption}.");
                        file.WriteLine($"ex0: {ex0}\nresult0: {res0}\nex1: {ex1}\nresult1: {res1}");
                    }
                    else
                    {
                        file.WriteLine($"OK in EnumerateFileSystemEntries: path: {path}, searchPattern: {searchPattern}, searchOption: {searchOption}.");
                        file.WriteLine($"ex0: {ex0}\nresult0: {res0}\nex1: {ex1}\nresult1: {res1}");
                    }
                }

            return ChangeWaves.AreFeaturesEnabled(ChangeWaves.Wave17_0)
                    ? Microsoft.IO.Directory.EnumerateFileSystemEntries(path, searchPattern, (Microsoft.IO.SearchOption)searchOption)
                    : Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
#else
            return Directory.EnumerateFileSystemEntries(path, searchPattern, searchOption);
#endif
        }

        public FileAttributes GetAttributes(string path)
        {
            return File.GetAttributes(path);
        }

        public virtual DateTime GetLastWriteTimeUtc(string path)
        {
            return File.GetLastWriteTimeUtc(path);
        }

        public virtual bool DirectoryExists(string path)
        {
            return Directory.Exists(path);
        }

        public virtual bool FileExists(string path)
        {
            return File.Exists(path);
        }

        public virtual bool FileOrDirectoryExists(string path)
        {
            return FileExists(path) || DirectoryExists(path);
        }
    }
}
