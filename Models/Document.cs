using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revit2Svg.Models
{
    public class Document
    {
        public string Title { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double TimeZone { get; set; }

        /// <summary>
        /// Project units
        /// </summary>
        public string Units { get; set; }
        /// <summary>
        /// Angle from true north in radians
        /// </summary>
        public double AngleFromTrueNorth { get; set; }

        public IList<Level> Levels { get; set; }

    }
}
