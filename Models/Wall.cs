using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Revit2Svg.Models
{
    public class Wall
    {
        public int Level { get; set; }
        public string Description { get; set; }
        public double Width { get; set; }
        public Line Line { get; set; }
        public Rectangle BoundingBox { get; set; }
    }
}
