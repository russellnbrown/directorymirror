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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DirectoryMirror
{
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
        private bool contentQuick = false;
        private bool isBigger = false;
        private bool isRunning = true;
        private Thread workThread = null;
        private bool aborted = false;
        private DirectoryInfo dtop = null;
        private const int quickCheckSize = 100000;
        private const int timeBufferDiff = 120;
        private int timeDiff = 0;
        public bool IsRunning { get => isRunning; set => isRunning = value; }


        public string GetStatus()
        {
            makeStatus();
            return status;
        }
  
        public Copier(string src, string dst, 
                      bool checkTime, bool timeBuffer, 
                      bool checkContent, bool contentQuick,
                      bool checkSize, bool isBigger, 
                      bool deleteMissing, bool dryRun)
        {
            this.src = src;
            this.dst = dst;
            this.dryRun = dryRun;
            this.checkSize = checkSize;
            this.checkContent = checkContent;
            this.checkTime = checkTime;
            this.deleteMissing = deleteMissing;
            this.isBigger = isBigger;
            sourceDirCount = 0;
            missingDirCount = 0;
            copiedCount = 0;
            foundCount = 0;
            if (timeBuffer)
                timeDiff = timeBufferDiff;
        }

 
        public void Start()
        {
            l.Info("Start scan " + DateTime.Now.ToString());
            addMessage("Start scan " + DateTime.Now.ToString());
            dtop = new DirectoryInfo(src);
            workThread = new Thread(Run);
            workThread.Start();
        }

        public void Run()
        {
            walk(dtop);
            isRunning = false;
            l.Info("End scan " + DateTime.Now.ToString()); 
             addMessage("End scan " + DateTime.Now.ToString());
        }

        public void Stop()
        {
            if (workThread == null)
                return;
            aborted = true;
            isRunning = false;
            workThread.Join();
            return;
        }

        DirectoryInfo processDirectory(DirectoryInfo d)
        {
            sourceDirCount++;
            string path = d.FullName;
            path = path.Substring(src.Length);
            path = dst + path;
            DirectoryInfo dd = new DirectoryInfo(path);
            if (dd.Exists)
            {
                foundCount++;
            }
            else
            {
                missingDirCount++;
                l.Info("Create destination dir " + dd.FullName);
                if (!dryRun)
                    dd.Create();
            }
            return dd;
        }

        bool testCopy(FileInfo s, FileInfo d)
        {
            // Simple case - destination dosn't exist
            if (!d.Exists)
                return true;

            // Check modification time (timeDiff includes any buffering )
            if (checkTime)
            {
                var ts = s.LastWriteTimeUtc - d.LastWriteTimeUtc;
                if (ts.TotalSeconds > timeDiff)
                    return true;
            }

            // Check size or simple content changed
            if ( checkSize )
            {
                if (isBigger)
                {
                    if (s.Length > d.Length)
                        return true;
                }
                else
                {
                    if (s.Length != d.Length)
                        return true;
                }
            }

            // Check content
            if (checkContent )
            {
                if (s.Length > d.Length)
                    return true;
                UInt32 scrc = getCrc(s);
                UInt32 dcrc = getCrc(d);
                return scrc != dcrc;
            }

            // nothing to copy
            return false;
        }

        uint getCrc(FileInfo s)
        {
            uint crc = 0;
            using (BinaryReader ss = new BinaryReader(File.Open(s.FullName, FileMode.Open)))
            {
                int testSize = 0;
                if (s.Length > Int32.MaxValue && !contentQuick)
                {
                    testSize = Int32.MaxValue;
                    l.Warn("Content check of " + s.FullName + " restricted to " + Int32.MaxValue + " bytes");
                }

                if (contentQuick && quickCheckSize < s.Length)
                    testSize = (int)quickCheckSize;

                long start = s.Length - (long)testSize;
                ss.BaseStream.Position = start;
                byte[] content = ss.ReadBytes((int)testSize);
                 crc = Crc32.Compute(content);
            }
            return crc;
        }


        void processFile(DirectoryInfo dd, FileInfo f)
        {
            
            string dest = dd.FullName + System.IO.Path.DirectorySeparatorChar + f.Name;
            FileInfo dfi = new FileInfo(dest);
            bool copy = testCopy(f, dfi);
            sourceFileCount++;
            if (copy)
            {
                copiedCount++;
                l.Info("Copy " + f.FullName + " to " + dest);
                if (dryRun)
                    return;
                f.CopyTo(dest, true);
            }

        }

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

        public static bool isReserved(string fn)
        {
            string lc = fn.ToLower();
            char c1 = lc[0];
            if (!(c1 == 'c' || c1 == 'l' || c1 == 'n' || c1 == 'a' || c1 == 'p'))
                return false;
            foreach (string s in _reserved)
                if (lc == s)
                    return true;
            return false;
        }

        public List<String> messages = new List<string>();
        public List<String> getMessages()
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
