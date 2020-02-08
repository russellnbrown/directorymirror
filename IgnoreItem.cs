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
using System.Linq;
using System.Text;

namespace DirectoryMirror
{
    public class IgnoreItem
    {
        public IgnoreItem(string pattern)
        {
            this.pattern = pattern;
        }

        public IgnoreItem()
        {

        }

        public String pattern { get; set; }

        public static String toCSVString(List<IgnoreItem> i)
        {
            StringBuilder sb = new StringBuilder();
            foreach(var itm in i)
            {
                if (itm.pattern.Length > 0)
                {
                    sb.Append(itm.pattern);
                    sb.Append(",");
                }
            }
            return sb.ToString();
        }
        public static List<IgnoreItem> fromCSVString(String s)
        {
            char[] seps = { ',' };
            String[] parts = s.Split(seps);
            List<IgnoreItem> ii = new List<IgnoreItem>();
            for(int sx=0; sx<parts.Length-1; sx++)
            {
                if ( parts[sx].Length > 0 ) 
                    ii.Add(new IgnoreItem(parts[sx]));
            }
            return ii;
        }

    }
}
