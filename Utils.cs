using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revit2Svg
{
    public class Utils
    {
        public static string NormalizeDouble(double d)
        {
            if (d.ToString().Contains("E")) return "0";
            return d.ToString();
        }
    }
}
