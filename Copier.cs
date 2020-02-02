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
    class Copier : IDisposable
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
        private bool isRunning = true;
        private Thread workThread = null;
        private bool aborted = false;
        private DirectoryInfo dtop = null;
        private const int quickCheckSize = 100000;
        private const int timeBufferDiff = 120;
        private int timeDiff = 0;
        public bool IsRunning { get => isRunning; set => isRunning = value; }
        public List<String> messages = new List<string>();
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
                "clock$"
        };

        public Copier(string src, string dst, 
                      bool checkTime, bool applyTimeBuffer, 
                      bool checkContent, bool useQuickContentCheck,
                      bool checkSize, bool onlyCopyIfBigger, 
                      bool deleteMissing, bool dryRun)
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
            if (checkContent )
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
                l.Debug("  content crc src="+scrc+", dcrc="+dcrc);
                if ( scrc != dcrc )
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

            // see if the file needs to be copied
            bool copy = testCopy(f, dfi);
            l.Debug("Final decision is " + copy );

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
            lock(messages)
            {
                rv = new List<String>(messages);
                messages.Clear();
            }
            return rv;
        }

        private void addMessage(String s)
        {
            lock(messages)
            {
                messages.Add(s);
            }
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
            if (!isReserved(top))
            {
                DirectoryInfo dd = processDirectory(sd);

                if ( deleteMissing )
                {
                    files = dd.GetFiles("*.*");
                    if (files != null)
                    {
                        foreach (System.IO.FileInfo fi in files)
                        {
                            if (!isRunning)
                                return;
                            string src = sd.FullName + System.IO.Path.DirectorySeparatorChar + fi.Name;
                            if (File.Exists(src))
                                continue;
                            delCount++;
                            l.Info("Deleting " + fi.FullName);
                            if (dryRun)
                                continue;
                            fi.Delete();
                        }
                    }
                }
                

                try
                {
  

                    // First, process all the files directly under this folder
                    files = sd.GetFiles("*.*");
                    if (!isRunning)
                        return;

                    if (files != null)
                    {
                        foreach (System.IO.FileInfo fi in files)
                        {
                            processFile(dd, fi);
                            if (!isRunning)
                                return;
                        }
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
            else 
            { 
                l.Error("Can't process directory " + top + " in path " + sd.FullName);
                addMessage(String.Format("Can't process {0} in {1} - it is reserved name" , top , sd.FullName));

            }



        }


        void IDisposable.Dispose()
        {
            if (workThread != null)
                workThread.Join();
            workThread = null;
        }
    }
}
