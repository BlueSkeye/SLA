﻿#define _WINDOWS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection.Metadata;
using System.Runtime.Intrinsics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static Sla.DECCORE.ScoreProtoModel;
using System.Xml.Linq;

namespace Sla.SLEIGH
{
    internal class FileManage
    {
        private List<string> pathlist;    // List of paths to search for files
        private const char separator =
#if _WINDOWS
            '\\'
#else
            '/'
#endif
            ;

        private static string buildPath(List<string> pathels,int level)
        {
            // Build an absolute path using elements from -pathels-, in reverse order
            // Build up to and including pathels[level]
            TextWriter s = new StringWriter();

            for (int i = pathels.size() - 1; i >= level; --i) {
                s.Write(separator);
                s.Write(pathels[i]);
            }
            return s.ToString();
        }

        private static bool testDevelopmentPath(List<string> pathels, int level, out string root)
        {
            // Given pathels[level] is "Ghidra", determine if this is a Ghidra development layout
            root = string.Empty;
            if (level + 2 >= pathels.size()) return false;
            string parent = pathels[level + 1];
            if (parent.Length < 11) return false;
            string piecestr = parent.Substring(0, 7);
            if (piecestr != "ghidra.") return false;
            piecestr = parent.Substring(parent.Length - 4);
            if (piecestr != ".git") return false;
            root = buildPath(pathels, level + 2);
            List<string> testpaths1 = new List<string>();
            List<string> testpaths2 = new List<string>();
            scanDirectoryRecursive(testpaths1, "ghidra.git", root, 1);
            if (testpaths1.size() != 1) return false;
            scanDirectoryRecursive(testpaths2, "Ghidra", testpaths1[0], 1);
            return (testpaths2.size() == 1);
        }

        private static bool testInstallPath(List<string> pathels, int level, out string root)
        {
            if (level + 1 >= pathels.size()) {
                root = string.Empty;
                return false;
            }
            root = buildPath(pathels, level + 1);
            List<string> testpaths1 = new List<string>();
            List<string> testpaths2 = new List<string>();
            scanDirectoryRecursive(testpaths1, "server", root, 1);
            if (testpaths1.size() != 1) return false;
            scanDirectoryRecursive(testpaths2, "server.conf", testpaths1[0], 1);
            return (testpaths2.size() == 1);
        }

        public void addDir2Path(string path)
        {
            if (path.Length > 0) {
                pathlist.Add(path);
                if (path[path.Length - 1] != separator)
                    pathlist.SetLastItem(pathlist.GetLastItem() + separator);
            }
        }

        public void addCurrentDir()
#if _WINDOWS
        {
            addDir2Path(Environment.CurrentDirectory);
        }
#else
        {
            // Add current working directory to path
            char dirname[256];
            char* buf;

            buf = getcwd(dirname, 256);
            if ((char*)0 == buf) return;
            string filename(buf);
            addDir2Path(filename);
        }
#endif

        // Resolve full pathname
        public void findFile(out string res, string name)
        {
            if (name[0] == separator) {
                res = name;
                try {
                    FileStream s = File.OpenRead(res);
                    s.Close();
                }
                catch { }
            }
            else {
                // Search through paths to find file with given name
                foreach (string path in pathlist) {
                    res = path + name;
                    try {
                        FileStream s = File.OpenRead(res);
                        s.Close();
                    }
                    catch { }
                }
            }
            // Can't find it, return empty string
            res = string.Empty;
        }


        // List of files with suffix
        public void matchList(List<string> res, string match,bool isSuffix)
        {
            foreach (string path in pathlist)
                matchListDir(res, match, isSuffix, path, false);
        }

        public static bool isDirectory(string path)
#if _WINDOWS
        {
            return (0 != (new DirectoryInfo(path).Attributes & FileAttributes.Directory));
        }
#else
        {
            stat buf;
            if (stat(path.c_str(), &buf) < 0) {
                return false;
            }
            return S_ISDIR(buf.st_mode);
        }
#endif

        public static void matchListDir(List<string> res, string match, bool isSuffix, string dirname,
            bool allowdot)
#if _WINDOWS
        {
            string dirfinal = dirname;
            if (dirfinal[dirfinal.Length - 1] != separator) {
                dirfinal += separator;
            }
            string regex = dirfinal + '*';

            DirectoryInfo rootDirectory = new DirectoryInfo(dirname);
            if (!rootDirectory.Exists) {
                return;
            }
            foreach(FileInfo candidate in rootDirectory.GetFiles()) {
                string fullname = candidate.Name;
                if (match.Length > fullname.Length) {
                    continue;
                }
                if (allowdot || (fullname[0] != '.')) {
                    if (isSuffix) {
                        if (0 == string.Compare(fullname.Substring(fullname.Length - match.Length), match, true)) {
                            res.Add(candidate.FullName);
                        }
                    }
                    else {
                        if (0 == string.Compare(fullname, match, true)) {
                            res.Add(candidate.FullName);
                        }
                    }
                }
            }
        }
#else
        {
        // Look through files in a directory for those matching -match-
            DIR* dir;
            dirent* entry;
            string dirfinal = dirname;
            if (dirfinal[dirfinal.size() - 1] != separator)
                dirfinal += separator;

            dir = opendir(dirfinal.c_str());
            if (dir == (DIR*)0) return;
            entry = readdir(dir);
            while (entry != (dirent*)0) {
                string fullname(entry.d_name);
                if (match.size() <= fullname.size())
                {
                    if (allowdot || (fullname[0] != '.'))
                    {
                        if (isSuffix)
                        {
                            if (0 == fullname.compare(fullname.size() - match.size(), match.size(), match))
                                res.Add(dirfinal + fullname);
                        }
                        else
                        {
                            if (0 == fullname.compare(0, match.size(), match))
                                res.Add(dirfinal + fullname);
                        }
                    }
                }
                entry = readdir(dir);
            }
            closedir(dir);
        }
#endif

        public static void directoryList(List<string> res, string dirname, bool allowdot = false)
#if _WINDOWS
        {
            string dirfinal = dirname;
            if (dirfinal[dirfinal.Length - 1] != separator) {
                dirfinal += separator;
            }
            string regex = dirfinal + '*';

            DirectoryInfo rootDirectory = new DirectoryInfo(dirname);
            if (!rootDirectory.Exists) {
                return;
            }
            foreach (DirectoryInfo candidate in rootDirectory.GetDirectories()) {
                if (allowdot || (candidate.Name[0] != '.'))
                    res.Add(candidate.FullName);
            }
        }
#else
        { // List full pathnames of all directories under the directory -dir-
            DIR* dir;
            dirent* entry;
            string dirfinal;

            dirfinal = dirname;
            if (dirfinal[dirfinal.size() - 1] != separator)
                dirfinal += separator;

            dir = opendir(dirfinal.c_str());
            if (dir == (DIR*)0) return;
            entry = readdir(dir);
            while (entry != (dirent*)0) {
                if (entry.d_type == DT_DIR)
                {
                    string fullname(entry.d_name);
                    if ((fullname != ".") && (fullname != ".."))
                    {
                        if (allowdot || (fullname[0] != '.'))
                            res.Add(dirfinal + fullname);
                    }
                }
                entry = readdir(dir);
            }
            closedir(dir);
        }
#endif

        public static void scanDirectoryRecursive(List<string> res, string matchname, string rootpath,
            int maxdepth)
        {
            if (maxdepth == 0) return;
            List<string> subdir = new List<string>();
            directoryList(subdir, rootpath);
            foreach (string curpath in subdir) {
                int pos = curpath.LastIndexOf(separator);
                if (-1 == pos)
                    pos = 0;
                else
                    pos = pos + 1;
                if (curpath.compare(pos, -1, matchname) == 0)
                    res.Add(curpath);
                else
                    scanDirectoryRecursive(res, matchname, curpath, maxdepth - 1); // Recurse
            }
        }

        public static void splitPath(string full,out string path,out string @base)
        {
            // Split path string -full- into its -base-name and -path- (relative or absolute)
            // If there is no path, i.e. only a basename in full, then -path- will return as an empty string
            // otherwise -path- will be non-empty and end in a separator character
            int end = full.Length - 1;
            if (full[full.Length - 1] == separator)
                // Take into account terminating separator
                end = full.Length - 2;
            int pos = full.LastIndexOf(separator);
            if (-1 == pos) {
                // Didn't find any separator
                @base = full;
                path = string.Empty;
            }
            else {
                int sz = (end - pos);
                @base = full.Substring(pos + 1, sz);
                path = full.Substring(0, pos + 1);
            }
        }

        public static bool isAbsolutePath(string full)
        {
            if (full.empty()) return false; return (full[0] == separator);
        }

        public static string discoverGhidraRoot(string argv0)
        {
            // Find the root of the ghidra distribution based on current working directory and passed in path
            List<string> pathels = new List<string>();
            string cur = argv0;
            string @base;
            int skiplevel = 0;
            bool isAbs = isAbsolutePath(cur);

            while(true) {
                int sizebefore = cur.Length;
                splitPath(cur, out cur, out @base);
                if (cur.Length == sizebefore) break;
                if (@base == ".")
                    skiplevel += 1;
                else if (@base == "..")
                    skiplevel += 2;
                if (skiplevel > 0)
                    skiplevel -= 1;
                else
                    pathels.Add(@base);
            }
            if (!isAbs) {
                FileManage curdir = new FileManage();
                curdir.addCurrentDir();
                cur = curdir.pathlist[0];
                while(true) {
                    int sizebefore = cur.Length;
                    splitPath(cur, out cur, out @base);
                    if (cur.Length == sizebefore) break;
                    pathels.Add(@base);
                }
            }

            for (int i = 0; i < pathels.size(); ++i) {
                if (pathels[i] != "Ghidra") continue;
                string root;
                if (testDevelopmentPath(pathels, i, out root))
                    return root;
                if (testInstallPath(pathels, i, out root))
                    return root;
            }
            return "";
        }
    }
}
