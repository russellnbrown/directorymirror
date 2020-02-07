using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DirectoryMirror
{
    public class IgnoreItem
    {
        public IgnoreItem(string pattern, bool isDir)
        {
            this.pattern = pattern;
            this.isDir = isDir;
        }

        public IgnoreItem()
        {

        }

        public String pattern { get; set; }
        public Boolean isDir { get; set; }

        public static String toCSVString(List<IgnoreItem> i)
        {
            StringBuilder sb = new StringBuilder();
            foreach(var itm in i)
            {
                sb.Append(itm.pattern);
                sb.Append(",");
                sb.Append(itm.isDir);
                sb.Append(",");
            }
            return sb.ToString();
        }
        public static List<IgnoreItem> fromCSVString(String s)
        {
            char[] seps = { ',' };
            String[] parts = s.Split(seps);
            List<IgnoreItem> ii = new List<IgnoreItem>();
            for(int sx=0; sx<parts.Length-1; sx+=2)
            {
                ii.Add(new IgnoreItem(parts[sx], Boolean.Parse(parts[sx + 1])));
            }
            return ii;
        }

    }
}
