#define _WINDOWS
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
        { // Build an absolute path using elements from -pathels-, in reverse order
          // Build up to and including pathels[level]
            ostringstream s;

            for (int i = pathels.size() - 1; i >= level; --i)
            {
                s << separator;
                s << pathels[i];
            }
            return s.str();
        }

        private static bool testDevelopmentPath(List<string> pathels,int level,string root)
        { // Given pathels[level] is "Ghidra", determine if this is a Ghidra development layout
            if (level + 2 >= pathels.size()) return false;
            string parent = pathels[level + 1];
            if (parent.size() < 11) return false;
            string piecestr = parent.substr(0, 7);
            if (piecestr != "ghidra.") return false;
            piecestr = parent.substr(parent.size() - 4);
            if (piecestr != ".git") return false;
            root = buildPath(pathels, level + 2);
            List<string> testpaths1;
            List<string> testpaths2;
            scanDirectoryRecursive(testpaths1, "ghidra.git", root, 1);
            if (testpaths1.size() != 1) return false;
            scanDirectoryRecursive(testpaths2, "Ghidra", testpaths1[0], 1);
            return (testpaths2.size() == 1);
        }

        private static bool testInstallPath(List<string> pathels,int level,string root)
        {
            if (level + 1 >= pathels.size()) return false;
            root = buildPath(pathels, level + 1);
            List<string> testpaths1;
            List<string> testpaths2;
            scanDirectoryRecursive(testpaths1, "server", root, 1);
            if (testpaths1.size() != 1) return false;
            scanDirectoryRecursive(testpaths2, "server.conf", testpaths1[0], 1);
            return (testpaths2.size() == 1);
        }

        public void addDir2Path(string path)
        {
            if (path.size() > 0)
            {
                pathlist.Add(path);
                if (path[path.size() - 1] != separator)
                    pathlist.back() += separator;
            }
        }

        public void addCurrentDir()
#if _WINDOWS
        {
            char dirname[256];

            if (0 != GetCurrentDirectoryA(256, dirname))
            {
                string filename(dirname);
                addDir2Path(filename);
            }
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
        public void findFile(string res, string name)
        {               // Search through paths to find file with given name
            List<string>::const_iterator iter;

            if (name[0] == separator)
            {
                res = name;
                ifstream s(res.c_str());
                if (s)
                {
                    s.close();
                    return;
                }
            }
            else
            {
                for (iter = pathlist.begin(); iter != pathlist.end(); ++iter)
                {
                    res = *iter + name;
                    ifstream s(res.c_str());
                    if (s)
                    {
                        s.close();
                        return;
                    }
                }
            }
            res.clear();            // Can't find it, return empty string
        }


        // List of files with suffix
        public void matchList(List<string> res, string match,bool isSuffix)
        {
            List<string>::const_iterator iter;

            for (iter = pathlist.begin(); iter != pathlist.end(); ++iter)
                matchListDir(res, match, isSuffix, *iter, false);
        }

        public static bool isDirectory(string path)
#if _WINDOWS
        {
          DWORD attribs = GetFileAttributes(path.c_str());
          if (attribs == INVALID_FILE_ATTRIBUTES) return false;
          return ((attribs & FILE_ATTRIBUTE_DIRECTORY)!=0);
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

        public static void matchListDir(List<string> res, string match, bool isSuffix, string dir, bool allowdot)
#if _WINDOWS
        {
          WIN32_FIND_DATAA FindFileData;
                HANDLE hFind;
                string dirfinal;

                dirfinal = dirname;
          if (dirfinal[dirfinal.size() - 1] != separator)
            dirfinal += separator;
          string regex = dirfinal + '*';

                hFind = FindFirstFileA(regex.c_str(),&FindFileData);
          if (hFind == INVALID_HANDLE_VALUE) return;
          do {
            string fullname(FindFileData.cFileName);
            if (match.size() <= fullname.size()) {
              if (allowdot||(fullname[0] != '.')) {
	        if (isSuffix) {
	          if (0==fullname.compare(fullname.size()-match.size(),match.size(),match))
	            res.Add(dirfinal + fullname);
	        }
	        else {
	          if (0==fullname.compare(0,match.size(),match))
	            res.Add(dirfinal + fullname);
	        }
              }
            }
          } while (0 != FindNextFileA(hFind, &FindFileData)) ;
        FindClose(hFind);
        }
#else
        {               // Look through files in a directory for those matching -match-
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
          WIN32_FIND_DATAA FindFileData;
                HANDLE hFind;
                string dirfinal = dirname;
          if (dirfinal[dirfinal.size() - 1] != separator)
            dirfinal += separator;
          string regex = dirfinal + "*";
                char* s = regex.c_str();


                hFind = FindFirstFileA(s,&FindFileData);
          if (hFind == INVALID_HANDLE_VALUE) return;
          do {
            if ((FindFileData.dwFileAttributes & FILE_ATTRIBUTE_DIRECTORY) == FILE_ATTRIBUTE_DIRECTORY ) {
              string fullname(FindFileData.cFileName);
              if (allowdot || (fullname[0] != '.'))
	        res.Add(dirfinal + fullname);
            }
        } while (0 != FindNextFileA(hFind, &FindFileData)) ;
        FindClose(hFind);
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

        public static void scanDirectoryRecursive(List<string> res, string matchname, string rootpath,int maxdepth)
        {
            if (maxdepth == 0) return;
            List<string> subdir;
            directoryList(subdir, rootpath);
            List<string>::const_iterator iter;
            for (iter = subdir.begin(); iter != subdir.end(); ++iter)
            {
                string curpath = *iter;
                string::size_type pos = curpath.rfind(separator);
                if (pos == string::npos)
                    pos = 0;
                else
                    pos = pos + 1;
                if (curpath.compare(pos, string::npos, matchname) == 0)
                    res.Add(curpath);
                else
                    scanDirectoryRecursive(res, matchname, curpath, maxdepth - 1); // Recurse
            }
        }

        public static void splitPath(string full,string path,string @base)
        { // Split path string -full- into its -base-name and -path- (relative or absolute)
          // If there is no path, i.e. only a basename in full, then -path- will return as an empty string
          // otherwise -path- will be non-empty and end in a separator character
            string::size_type end = full.size() - 1;
            if (full[full.size() - 1] == separator) // Take into account terminating separator
                end = full.size() - 2;
            string::size_type pos = full.rfind(separator, end);
            if (pos == string::npos)
            {   // Didn't find any separator
                base = full;
                path.clear();
            }
            else
            {
                string::size_type sz = (end - pos);
                base = full.substr(pos + 1, sz);
                path = full.substr(0, pos + 1);
            }
        }

        public static bool isAbsolutePath(string full)
        {
            if (full.empty()) return false; return (full[0] == separator);
        }

        public static string discoverGhidraRoot(string argv0)
        { // Find the root of the ghidra distribution based on current working directory and passed in path
            List<string> pathels;
            string cur(argv0);
            string base;
            int skiplevel = 0;
            bool isAbs = isAbsolutePath(cur);

            for (; ; )
            {
                int sizebefore = cur.size();
                splitPath(cur, cur, base);
                if (cur.size() == sizebefore) break;
                if (base == ".")
                    skiplevel += 1;
                else if (base == "..")
                    skiplevel += 2;
                if (skiplevel > 0)
                    skiplevel -= 1;
                else
                    pathels.Add(base);
            }
            if (!isAbs)
            {
                FileManage curdir;
                curdir.addCurrentDir();
                cur = curdir.pathlist[0];
                for (; ; )
                {
                    int sizebefore = cur.size();
                    splitPath(cur, cur, base);
                    if (cur.size() == sizebefore) break;
                    pathels.Add(base);
                }
            }

            for (int i = 0; i < pathels.size(); ++i)
            {
                if (pathels[i] != "Ghidra") continue;
                string root;
                if (testDevelopmentPath(pathels, i, root))
                    return root;
                if (testInstallPath(pathels, i, root))
                    return root;
            }
            return "";
        }
    }
}
