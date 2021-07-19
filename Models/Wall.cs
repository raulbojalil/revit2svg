using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revit2Svg.Models
{
    public class Wall
    {
        public int LevelIndex { get; set; }
        public string LevelName { get; set; }
        public double LevelElevation { get; set; }
        public string Description { get; set; }
        public double Width { get; set; }
        public Line Line { get; set; }
        public Rectangle BoundingBox { get; set; }
    }
}
