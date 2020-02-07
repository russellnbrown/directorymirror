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
