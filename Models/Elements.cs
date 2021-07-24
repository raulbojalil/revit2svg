namespace Revit2Svg.Models
{
    public class Element
    {
        public int LevelIndex { get; set; }
        public string LevelName { get; set; }
        public string Name { get; set; }
        public Rectangle BoundingBox { get; set; }
    }

    public class Wall : Element
    {
        public double Width { get; set; }
        public Line Line { get; set; }
    }

    public class DoorOrWindow : Element
    {
        public bool IsWindow { get; set; }
        public Point Point { get; set; }
    }
}
