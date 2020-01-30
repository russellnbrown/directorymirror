/*
 * Copyright (C) 2020 russell brown
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;

namespace CSLib
{
    // l - provide a basic console/file logging 
    public class Settings
    {
        
        private static SortedDictionary<string, String> settings = new SortedDictionary<string, String>();
        private static string Name = "settings.dat";

  
        public static String Get(string name, String dval)
        {
            if (settings.ContainsKey(name))
                return settings[name];
            return dval;
        }

        public static bool Get(string name, bool dval)
        {
            if (settings.ContainsKey(name))
                return Boolean.Parse(settings[name]);
            return dval;
        }
        public static int Get(string name, int dval)
        {
            if (settings.ContainsKey(name))
                return Int32.Parse(settings[name]);
            return dval;
        }

        public static void Set(string name, String val)
        {
            settings[name] = val;
        }
        public static void Set(string name, int val)
        {
            settings[name] = val.ToString();
        }
        public static void Set(string name, bool val)
        {
            settings[name] = val.ToString();
        }

        public static void Load(string name)
        {
            Name = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            Name += Path.DirectorySeparatorChar;
            Name += name;
            Name += ".dat";

           

            try
            {
                using (StreamReader sr = new StreamReader(Name))
                {
                    string line = sr.ReadLine();
                    char[] seps = { '=' };
                    while (line != null)
                    {
                        string[] parts = line.Split(seps);
                        if (parts.Length == 2 && parts[0].Length > 0 && parts[1].Length > 0)
                            settings.Add(parts[0], parts[1]);
                        line = sr.ReadLine();
                    }
                    sr.Close();
                }
            }
            catch
            {

            }
        }

        public static void Save()
        {
            try
            {
                using (StreamWriter sw = new StreamWriter(Name))
                {
                    foreach (var kvp in settings)
                    {
                        sw.WriteLine(kvp.Key + "=" + kvp.Value);
                    }
                    sw.Close();
                }
            }
            catch
            {

            }
        }


    }
}


