using System;

namespace Revit2Svg.Models
{
    public class Rectangle
    {
        public double MinX { get; set; }
        public double MinY { get; set; }
        public double MaxX { get; set; }
        public double MaxY { get; set; }

        public double Height { 
            get
            {
                return MaxY - MinY;
            }
        }
        public double Width
        {
            get
            {
                return MaxX - MinX;
            }
        }

        public void EnsureContainsLine(Line line)
        {
            if (line.X1 > line.X2)
                throw new Exception("Line should go from left to right");

            MinX = Math.Min(MinX, line.X1);
            MinY = Math.Min(MinY, line.Y1);

            MaxX = Math.Max(MaxX, line.X2);
            MaxY = Math.Max(MaxY, line.Y2);
        }

        public void EnsureContainsRectangle(Rectangle boundingBox)
        {
            if (boundingBox.MinX > boundingBox.MaxX || boundingBox.MinY > boundingBox.MaxY) 
                throw new Exception("Malformed rectangle");

            MinX = Math.Min(MinX, boundingBox.MinX);
            MinY = Math.Min(MinY, boundingBox.MinY);

            MaxX = Math.Max(MaxX, boundingBox.MaxX);
            MaxY = Math.Max(MaxY, boundingBox.MaxY);
        }
    }
}
