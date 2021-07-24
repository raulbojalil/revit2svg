using System.Collections.Generic;

namespace Revit2Svg.Models
{
    public class Level
    {
        /// <summary>
        /// The name of the level
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Elevation of the level
        /// </summary>
        public double Elevation { get; set; }

        /// <summary>
        /// Floor height in internal units
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// The bounding box of the level
        /// </summary>
        public Rectangle BoundingBox { get; set; }

        /// <summary>
        /// Children elements like doors, windows, etc.
        /// </summary>
        public IList<Element> Elements { get; set; }
    }
}
