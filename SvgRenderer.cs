using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Rectangle = Revit2Svg.Models.Rectangle;
using Wall = Revit2Svg.Models.Wall;
using Line = Revit2Svg.Models.Line;
using System.IO;

namespace Revit2Svg
{
    public class SvgRenderer
    {
        public static void DrawWalls(Document doc, double scale = 10, bool renderLines = true, 
            bool renderRects = true, bool renderFloorNames = true, double paddingBetweenLevels = 10)
        {
            var walls = new List<Wall>();

            var offsetX = double.MaxValue;
            var offsetY = double.MaxValue;

            var svgWidth = 0;
            var svgHeight = 0;

            var currentLevelIndex = 0;
            double currentLevelOffset = 0;

            var svgBuilder = new StringBuilder();
            var levelHeights = new Dictionary<int, double>();

            ElementId currentLevelId = null;

            var levels = (new FilteredElementCollector(doc).OfClass(typeof(Level))).ToElements();

            var wallsCollector = (new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Wall)));

            foreach (Autodesk.Revit.DB.Wall wall in wallsCollector.OrderBy(x => (x as Autodesk.Revit.DB.Wall).LevelId.IntegerValue))
            {
                if (currentLevelId == null)
                    currentLevelId = wall.LevelId;

                if (currentLevelId.IntegerValue != wall.LevelId.IntegerValue)
                    currentLevelIndex++;

                currentLevelId = wall.LevelId;

                //var geometry = wall.get_Geometry(new Options());
                //foreach(var item in geometry)
                //{

                //}

                var line = ((wall.Location as LocationCurve).Curve as Autodesk.Revit.DB.Line);
                var boundingBox = wall.get_BoundingBox(null);

                var units = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).GetUnitTypeId();
                var length = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
                var actualLength = UnitUtils.ConvertFromInternalUnits(length, units);

                walls.Add(new Wall()
                {
                    LevelIndex = currentLevelIndex,
                    LevelName = levels.FirstOrDefault(x => x.Id.IntegerValue == wall.LevelId.IntegerValue)?.Name ?? "Unknown",
                    Description = $"{wall.Name} ({actualLength} {units.TypeId})",
                    Line = FixLine(scale, line, boundingBox),
                    BoundingBox = GetRectangleFromBoundingBox(scale, boundingBox),
                    Width = wall.Width,
                    
                });
            }

            levelHeights.Add(0, 0);

            foreach (var wall in walls.OrderBy(x => x.LevelIndex))
            {
                if (!levelHeights.ContainsKey(wall.LevelIndex + 1))
                    levelHeights.Add(wall.LevelIndex + 1, double.MinValue);

                if (wall.Line.Y1 > levelHeights[wall.LevelIndex + 1]) 
                    levelHeights[wall.LevelIndex + 1] = wall.Line.Y1;
                if (wall.Line.Y2 > levelHeights[wall.LevelIndex + 1]) 
                    levelHeights[wall.LevelIndex + 1] = wall.Line.Y2;
                if (wall.BoundingBox.Y + wall.BoundingBox.Height > levelHeights[wall.LevelIndex + 1])
                    levelHeights[wall.LevelIndex + 1] = wall.BoundingBox.Y + wall.BoundingBox.Height;

                if (wall.Line.X1 < offsetX) offsetX = wall.Line.X1;
                if (wall.Line.Y1 < offsetY) offsetY = wall.Line.Y1;
                if (wall.Line.X2 < offsetX) offsetX = wall.Line.X2;
                if (wall.Line.Y2 < offsetY) offsetY = wall.Line.Y2;

                if (wall.BoundingBox.X < offsetX) 
                    offsetX = wall.BoundingBox.X;
                if (wall.BoundingBox.Y < offsetY) 
                    offsetY = wall.BoundingBox.Y;
            }

            currentLevelIndex = -1;

            foreach (var wall in walls.OrderBy(x => x.LevelIndex))
            {
                if(currentLevelIndex != wall.LevelIndex)
                {
                    currentLevelOffset += levelHeights[wall.LevelIndex]
                    + (Math.Abs(offsetY) * wall.LevelIndex)
                    + (paddingBetweenLevels * wall.LevelIndex);

                    if (renderFloorNames)
                    {
                        currentLevelOffset += 10; //Padding around the text

                        svgBuilder.AppendLine(
                            $"<text x=\"0\" y=\"{currentLevelOffset}\" fill=\"black\">{wall.LevelName}</text>");

                        currentLevelOffset += 10; //Padding around the text
                    }
                }

                currentLevelIndex = wall.LevelIndex;

                var x1 = wall.Line.X1 + Math.Abs(offsetX);
                var y1 = wall.Line.Y1 + Math.Abs(offsetY) + currentLevelOffset;
                var x2 = wall.Line.X2 + Math.Abs(offsetX);
                var y2 = wall.Line.Y2 + Math.Abs(offsetY) + currentLevelOffset;
                var strokeWidth = wall.Width * scale;

                var rectX = wall.BoundingBox.X + Math.Abs(offsetX);
                var rectY = wall.BoundingBox.Y + Math.Abs(offsetY) + currentLevelOffset;
                var rectWidth = wall.BoundingBox.Width;
                var rectHeight = wall.BoundingBox.Height;

                if (svgWidth < rectX + rectWidth) svgWidth = Convert.ToInt32(rectX + rectWidth);
                if (svgHeight < rectY + rectHeight) svgHeight = Convert.ToInt32(rectY + rectHeight);

                svgBuilder.AppendLine(
                    $"<!-- {wall.Description} -->");

                if (renderLines)
                {
                    svgBuilder.AppendLine(
                        $"<line x1=\"{x1.Normalize()}\" y1=\"{y1.Normalize()}\" x2=\"{x2.Normalize()}\" y2=\"{y2.Normalize()}\" style=\"stroke:rgb(0,0,0);stroke-width:{strokeWidth}\" />");
                }

                if (renderRects)
                {
                    svgBuilder.AppendLine(
                        $"<rect x=\"{rectX.Normalize()}\" y=\"{rectY.Normalize()}\" width=\"{rectWidth.Normalize()}\" height=\"{rectHeight.Normalize()}\" style=\"stroke:rgb(255,0,0);fill: none;stroke-width:1\" />");
                }

            }

            var contents = $"<!DOCTYPE html><html><body><svg height=\"{svgHeight}\" width=\"{svgWidth}\">\r\n{svgBuilder}</svg></body></html>";
            File.WriteAllText(@".\svg_data.html", contents);
        }

        private static Line FixLine(double scale, Autodesk.Revit.DB.Line line, BoundingBoxXYZ boundingBox)
        {
            //Fit the lines inside the bounding boxes
            var x1 = line.Direction.X >= 0 ? boundingBox.Min.X : boundingBox.Max.X;
            var y1 = line.Direction.Y >= 0 ? boundingBox.Min.Y : boundingBox.Max.Y;

            var offsetX = ((boundingBox.Max.X - boundingBox.Min.X) / 2)
                * (Math.Abs(line.Direction.Y) * (line.Direction.X >= 0 ? 1 : -1));
            var offsetY = ((boundingBox.Max.Y - boundingBox.Min.Y) / 2)
                * (Math.Abs(line.Direction.X) * (line.Direction.Y >= 0 ? 1 : -1));

            if (Math.Abs(line.Direction.X.Normalize()) > 0 && Math.Abs(line.Direction.Y.Normalize()) > 0)
            {
                offsetX = 0;
                offsetY = 0;
            }


            return new Line()
            {
                X1 = (x1 + offsetX) * scale,
                Y1 = (y1 + offsetY) * scale,
                X2 = ((x1 + (line.Direction.X * line.Length)) + offsetX) * scale,
                Y2 = ((y1 + (line.Direction.Y * line.Length)) + offsetY) * scale,
            };

        }
        private static Rectangle GetRectangleFromBoundingBox(double scale, BoundingBoxXYZ box)
        {
            return new Rectangle()
            {
                X = box.Min.X * scale,
                Y = box.Min.Y * scale,
                Width = (box.Max.X * scale) - (box.Min.X * scale),
                Height = (box.Max.Y * scale) - (box.Min.Y * scale)
            };
        }
    }
}
