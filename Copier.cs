/*
 * Copyright (C) 2020 Russell Brown
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using CSLib;
using DamienG.Security.Cryptography;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace DirectoryMirror
{
    //
    // class Copier
    //
    // Copier does the actual testing and copying of files from src to dest.
    // A new instance is created each time the 'Start' button is pressed. It
    // reports its status via 'GetStatus' and can be terminated before completipon
    // by calling 'Stop'. When finished 'IsRunning' returns false. The process is
    // run in a background thread so the GUI dosen't freeze up.
    //
    // Note1 - Built with NetCore - this allows for long paths. Same methods in Net Framework will fail
    // on some paths.
    //
    // Note2 - Come paths may contain reserver words ( COM etc ) which windows cant handle. In that case
    // show a warning and continue - see isReserved
    //
    class Copier 
    {

        private int sourceDirCount = 0;
        private int sourceFileCount = 0;
        private int missingDirCount = 0;
        private int copiedCount = 0;
        private int foundCount = 0;
        private int delCount = 0;
        private bool dryRun = false;
        private string status = "";
        private string src = "";
        private string dst = "";
        private bool checkTime = false;
        private bool checkContent = false;
        private bool checkSize = false;
        private bool deleteMissing = false;
        private bool useQuickContentCheck = false;
        private bool onlyCopyIfBigger = false;
        private bool useFilter = false;
        private bool isRunning = true;
        private Thread workThread = null;
        private bool aborted = false;
        private DirectoryInfo dtop = null;
        private const int quickCheckSize = 100000;
        private const int timeBufferDiff = 120;
        private int timeDiff = 0;
        private int excludedDirs = 0;
        private int excludedFiles = 0;

        // isRunning can be used to abort the copier thread
        public bool IsRunning { get => isRunning; set => isRunning = value; }

        // strings to be displayed on GUI in GUI thread ( GetMessage )
        private List<String> messages = new List<string>();

        // list of MS reserved directory names
        static string[] _reserved = new string[]
        {
                "con",
                "prn",
                "aux",
                "nul",
                "com1",
                "com2",
                "com3",
                "com4",
                "com5",
                "com6",
                "com7",
                "com8",
                "com9",
                "lpt1",
                "lpt2",
                "lpt3",
                "lpt4",
                "lpt5",
                "lpt6",
                "lpt7",
                "lpt8",
                "lpt9",
                "clock$",
                "crap"
        };

        public Copier(string src, string dst,
                      bool checkTime, bool applyTimeBuffer,
                      bool checkContent, bool useQuickContentCheck,
                      bool checkSize, bool onlyCopyIfBigger,
                      bool deleteMissing, bool useFilter, bool dryRun)
        {
            this.src = src;
            this.dst = dst;
            this.dryRun = dryRun;
            this.checkSize = checkSize;
            this.checkContent = checkContent;
            this.checkTime = checkTime;
            this.deleteMissing = deleteMissing;
            this.onlyCopyIfBigger = onlyCopyIfBigger;
            this.useQuickContentCheck = useQuickContentCheck;
            this.useFilter = useFilter;
            sourceDirCount = 0;
            missingDirCount = 0;
            copiedCount = 0;
            foundCount = 0;
            // if applyTimeBuffer is set then add timeBufferDist to the timeDiff
            if (applyTimeBuffer)
                timeDiff = timeBufferDiff;
        }

        //
        // Start
        //
        // Called after instansiation to start the copy process. Starts the background
        // thread
        //
        public void Start()
        {
            l.Info("Start scan " + DateTime.Now.ToString());
            addMessage("Start scan " + DateTime.Now.ToString());
            dtop = new DirectoryInfo(src);
            workThread = new Thread(Run);
            workThread.Start();
        }

        //
        // Run
        //
        // Forms the background thread of the copy process.
        //
        public void Run()
        {
            walk(dtop);
            isRunning = false;
            l.Info("End scan " + DateTime.Now.ToString());
            addMessage("End scan " + DateTime.Now.ToString());
        }

        //
        // GetStatus
        //
        // Returns a status string for display on the GUI
        //
        public string GetStatus()
        {
            makeStatus();
            return status;
        }

        //
        // Stop
        //
        // Stops the background thread before normal finish
        //
        public void Stop()
        {
            if (workThread == null)
                return;
            aborted = true;
            isRunning = false;
            workThread.Join();
            return;
        }

        //
        // processDirectory
        //
        // This processes a single directory in the source directory tree
        //
        DirectoryInfo processDirectory(DirectoryInfo d)
        {
            sourceDirCount++;

            // form the destination directory name by substituting
            // destination root for source root

            string path = d.FullName;
            path = path.Substring(src.Length);
            path = dst + path;
            DirectoryInfo dd = new DirectoryInfo(path);

            if (dd.Exists)
                foundCount++;
            else
            {
                missingDirCount++;
                // create destination directory if it dosnt exist
                l.Info("Create destination dir " + dd.FullName);
                if (!dryRun)
                    dd.Create();
            }
            return dd;
        }

        //
        // testCopy
        //
        // Given a source and destination file, this will go through all the copy 
        // parameters to determin if it needs to be copied
        //
        bool testCopy(FileInfo s, FileInfo d)
        {
            // Simple case - destination dosn't exist
            if (!d.Exists)
            {
                l.Debug("  no destination exists, simple copy");
                return true;
            }

            // Check modification time (timeDiff includes any buffering )
            if (checkTime)
            {
                var ts = s.LastWriteTimeUtc - d.LastWriteTimeUtc;
                l.Debug("  check times Src=" + s.LastAccessTimeUtc + ", Dst=" + d.LastAccessTimeUtc + ", secs=" + ts.TotalSeconds + ", diff=" + timeDiff);
                if (ts.TotalSeconds > timeDiff)
                {
                    l.Debug("  check times secs=" + ts.TotalSeconds + " greater than " + timeDiff);
                    return true;
                }
            }

            // Check size or simple content changed
            if (checkSize)
            {
                l.Debug("  check size Src=" + s.Length + ", Dst=" + d.Length);
                if (onlyCopyIfBigger)
                {
                    if (s.Length > d.Length)
                    {
                        l.Debug("  is bigger check pass ");
                        return true;
                    }
                    else
                        l.Debug("  is bigger fail ");
                }
                else
                {
                    if (s.Length != d.Length)
                    {
                        l.Debug("  length different check pass ");
                        return true;
                    }
                    l.Debug("  length different check fail ");
                }
            }

            // Check content
            if (checkContent)
            {
                // simple case, if sizes are different then the content is different
                if (s.Length > d.Length)
                {
                    l.Debug("  content check simple pass");
                    return true;
                }

                // get and compare the crc of the files
                UInt32 scrc = getCrc(s);
                UInt32 dcrc = getCrc(d);
                l.Debug("  content crc src=" + scrc + ", dcrc=" + dcrc);
                if (scrc != dcrc)
                {
                    l.Debug("  content pass");
                    return true;
                }
                l.Debug("  content fail");
            }
            l.Debug("  all fail");
            // nothing to copy
            return false;
        }

        //
        // getCrc
        //
        // we read the file (or a part of it ) into memory and 
        // calculate a crc
        //
        uint getCrc(FileInfo s)
        {

            uint crc = 0;
            int maxSizeCheck = Int32.MaxValue;

            // open the file
            using (BinaryReader ss = new BinaryReader(File.Open(s.FullName, FileMode.Open)))
            {
                // read testSize bytes. limit to maxSizeCheck if greater than maxint in size.
                int testSize = 0;
                if (s.Length > (long)maxSizeCheck && !useQuickContentCheck)
                {
                    testSize = maxSizeCheck;
                    l.Warn("Content check of " + s.FullName + " restricted to " + maxSizeCheck + " bytes");
                }
                else
                    testSize = (int)s.Length;

                // if quick check is enabled and testSize is bigger than
                // quickCheckSize set it to quickCheckSize
                if (useQuickContentCheck && quickCheckSize < s.Length)
                    testSize = (int)quickCheckSize;

                // read testSize bytes. we may need to adjust start if realing less than
                // the full file size
                long start = s.Length - (long)testSize;
                ss.BaseStream.Position = start;
                byte[] content = ss.ReadBytes((int)testSize);

                // calc crc
                crc = Crc32.Compute(content);
            }
            return crc;
        }


        //
        // processFile
        //
        // this processes a single file in the source tree
        // 
        void processFile(DirectoryInfo dd, FileInfo f)
        {
            sourceFileCount++;

            l.Debug("Considering " + f.FullName);

            // work out destination name from dest dir and file name
            string dest = dd.FullName + System.IO.Path.DirectorySeparatorChar + f.Name;
            FileInfo dfi = new FileInfo(dest);

            if (!passExcludeIncludeFile(f.Name))
            {
                excludedFiles++;
                l.Info("File " + f.FullName + " failed wildcard test");
                return;
            }

            // see if the file needs to be copied
            bool copy = testCopy(f, dfi);
            l.Debug("Final decision is " + copy);

            // copy file if tests passed 
            if (copy)
            {
                copiedCount++;
                l.Info("Copy " + f.FullName + " to " + dest);
                // ignore if dryRun is set
                if (dryRun)
                    return;
                f.CopyTo(dest, true);
            }

        }

        //
        // makeStatus
        //
        // create a status string to display on GUI
        //
        private void makeStatus()
        {
            StringBuilder s = new StringBuilder();
            if (aborted)
                s.Append("Aborted:");
            else if (isRunning)
                s.Append("Running:");
            else
                s.Append("Finished:");

            s.Append(" Scanned ");
            s.Append(sourceDirCount.ToString());
            s.Append(" dirs, ");
            s.Append(sourceFileCount.ToString());
            s.Append(" files.  Missing dirs:");
            s.Append(missingDirCount.ToString());
            s.Append(", Copied:");
            s.Append(copiedCount.ToString());
            s.Append(", Deleted:");
            s.Append(delCount.ToString());
            s.Append(", Excluded Dirs:");
            s.Append(excludedDirs.ToString());
            s.Append(", Files:");
            s.Append(excludedFiles.ToString());
            status = s.ToString();
        }

        //
        // isReserved
        //
        // return true if directory name is not valid in windows ( if we are copying
        // from a linux filesystem for instance ). windows can;t handle it
        //
        public static bool isReserved(string fn)
        {
            string lc = fn.ToLower(); // for comparison purposes
            char c1 = lc[0];
            // quick test can eliminate most names
            if (!(c1 == 'c' || c1 == 'l' || c1 == 'n' || c1 == 'a' || c1 == 'p'))
                return false;
            // otherwize chack all names
            foreach (string s in _reserved)
                if (lc == s)
                    return true;
            return false;
        }
        //
        // GetMessages/addMessage
        //
        // used to send/receive messages to the GUI as we cant display them directly form a 
        // background thread. A timer process in the main GUI calls GetMessages periodically 
        // to get and clear messages. more sophisticated mechanisms exist, but why complicate
        // things?
        public List<String> GetMessages()
        {
            List<String> rv;
            lock (messages)
            {
                rv = new List<String>(messages);
                messages.Clear();
            }
            return rv;
        }

        private void addMessage(String s)
        {
            lock (messages)
            {
                messages.Add(s);
            }
        }

        //
        // passExcludeDir
        //
        // see if a directory is in the excluded directories list
        //
        bool passExcludeDir(string s)
        {
            // dont perform test if use filter is not set
            if (!useFilter)
                return true;

            // see if directory matches something in the list (case insensitive)
            s = s.ToLower();
            foreach (var di in MainWindow.Get.excludedirs)
            {
                if (WildcardMatch.EqualsWildcard(s, di.pattern.ToLower()))
                {
                    l.Info("Dir {0} faild wildcard test with {1}", s, di.pattern);
                    return false;
                }
            }
            l.Info("Dir {0} pass wildcard test ", s);
            return true;
        }
        //
        // passExcludeIncludeFile
        //
        // see if the file name appears in the include or exclude list
        //
        bool passExcludeIncludeFile(string s)
        {
            // dont test if use filter not set
            if (!useFilter)
                return true;

            // ignore case in tests
            s = s.ToLower();

            // if includes are set, only check against them
            if (MainWindow.Get.includes.Count > 0)
            {
                foreach (var di in MainWindow.Get.includes)
                {
                    if (WildcardMatch.EqualsWildcard(s, di.pattern.ToLower()))
                    {
                        l.Info("File {0} pass wildcard include test with {1}", s, di.pattern);
                        return true;
                    }
                }
                return false;
            }

            // otherwise see if it is in excludes
            foreach (var di in MainWindow.Get.excludes)
            {
                if (WildcardMatch.EqualsWildcard(s, di.pattern.ToLower()))
                {
                    l.Info("File {0} failed wildcard test with {1}", s, di.pattern);
                    return false;
                }
            }
            l.Info("File {0} pass wildcard tests ", s);
            return true;
        }

        //
        // walk
        //
        // used to process source tree. calls processDirecrory, processFile and
        // itself recursivly
        //
        private void walk(DirectoryInfo sd)
        {
            FileInfo[] files = null;
            DirectoryInfo[] subDirs = null;

            string top = sd.Name;

            // see if directory in excluded dirs, quit if it is
            if (!passExcludeDir(top))
            {
                l.Info("Excluded dir {0}", top);
                addMessage(String.Format("Can't process {0} in {1} - it is excluded ", top, sd.FullName));
                excludedDirs++;
                return;
            }

            // see if directory is a reserved word, quit if it is
            if (isReserved(top))
            {
                excludedDirs++;
                l.Error("Can't process directory " + top + " in path " + sd.FullName);
                addMessage(String.Format("Can't process {0} in {1} - it is reserved name", top, sd.FullName));
                return;
            }

            // all passed, process it
            DirectoryInfo dd = processDirectory(sd);

            // if delete missing is set, get list of fines in destination and see if the equivalent 
            // exists in the source, if not dtelte the one in the destination
            if (deleteMissing)
            {
                files = dd.GetFiles("*.*");
                if (files != null)
                {
                    foreach (System.IO.FileInfo fi in files)
                    {
                        if (!isRunning)// exit if abort signalled
                            return;

                        // continue if it exists
                        string src = sd.FullName + System.IO.Path.DirectorySeparatorChar + fi.Name;
                        if (File.Exists(src))
                            continue;

                        // else delete it
                        delCount++;
                        l.Info("Deleting " + fi.FullName);
                        if (dryRun)
                            continue;
                        fi.Delete();
                    }
                }
            }

            // now process files in the source
            try
            {

                files = sd.GetFiles("*.*");
                if (!isRunning)// exit if abort signalled
                    return;

                if (files != null)
                {
                    foreach (System.IO.FileInfo fi in files)
                    {
                        processFile(dd, fi);
                        if (!isRunning)
                            return;
                    }

                    // then any directories, call walk recursivly
                    subDirs = sd.GetDirectories();
                    foreach (DirectoryInfo dirInfo in subDirs)
                        walk(dirInfo);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                l.Error(e.Message);
                addMessage(String.Format("Can't process {0} - access error.", top));

            }
            catch (DirectoryNotFoundException e)
            {
                l.Fatal("Directory not found: " + sd);
            }
        }
    }
  
}
