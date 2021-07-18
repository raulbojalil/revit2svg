using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revit2Svg
{
    public static class Utils
    {
        public static double Normalize(this double d)
        {
            if (d.ToString().Contains("E")) return 0;
            return d;
        }
    }
}
