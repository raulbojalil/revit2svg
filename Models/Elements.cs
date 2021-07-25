using System.Collections.Generic;

namespace Revit2Svg.Models
{
    public class Element
    {
        public string Name { get; set; }
        public Rectangle BoundingBox { get; set; }
    }

    public class Level : Element
    {
        /// <summary>
        /// Elevation of the level
        /// </summary>
        public double Elevation { get; set; }

        /// <summary>
        /// Floor height in internal units
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// Floor height in meters
        /// </summary>
        public double HeightM { get; set; }

        /// <summary>
        /// Floor height in feet
        /// </summary>
        public double HeightFt { get; set; }

        /// <summary>
        /// Width in meters
        /// </summary>
        public double WidthM { get; set; }

        /// <summary>
        /// Width in feet
        /// </summary>
        public double WidthFt { get; set; }

        /// <summary>
        /// Children elements like walls, doors, windows, etc.
        /// </summary>
        public IList<Element> Elements { get; set; }

        /// <summary>
        /// Bounding box including walls only
        /// </summary>
        public Rectangle InnerBoundingBox { get; set; }
    }

    public class Wall : Element
    {
        /// <summary>
        /// The width of the wall
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// Location line
        /// </summary>
        public Line Line { get; set; }
    }

    public class DoorOrWindow : Element
    {
        /// <summary>
        /// Whether this element is a window or a door
        /// </summary>
        public bool IsWindow { get; set; }

        /// <summary>
        /// Location point
        /// </summary>
        public Point Point { get; set; }
    }
}
