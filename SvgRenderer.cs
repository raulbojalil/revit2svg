using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rectangle = Revit2Svg.Models.Rectangle;
using Line = Revit2Svg.Models.Line;
using Point = Revit2Svg.Models.Point;
using Element = Revit2Svg.Models.Element;
using Wall = Revit2Svg.Models.Wall;
using DoorOrWindow = Revit2Svg.Models.DoorOrWindow;
using System.IO;

namespace Revit2Svg
{
    public class SvgRenderer
    {
        public static void DrawWalls(Document doc, double scale = 10, bool renderLines = true, 
            bool renderRects = true, bool renderFloorNames = true, double paddingBetweenLevels = 10)
        {
            var walls = new List<Element>();

            var offsetX = double.MaxValue;
            var offsetY = double.MaxValue;

            var svgWidth = 0;
            var svgHeight = 0;

            ForgeTypeId units = null;

            var currentLevelIndex = -1;
            double currentLevelOffset = 0;

            var svgBuilder = new StringBuilder();
            var levelBoundingBoxes = new Dictionary<int, Rectangle>();
            
            var levels = (new FilteredElementCollector(doc).OfClass(typeof(Level)))
                .ToElements()
                .Select(x => x as Level)
                .OrderBy(x => x.LevelId.IntegerValue)
                .ToList();

            var wallsCollector = (new FilteredElementCollector(doc)
                .OfClass(typeof(Autodesk.Revit.DB.Wall)))
                .ToElements();
            
            var doorsCollector = (new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Doors)
                    .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance)))
                    .ToElements();

            var windowsCollector = (new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Windows)
                   .OfClass(typeof(Autodesk.Revit.DB.FamilyInstance)))
                   .ToElements();

            var elements = wallsCollector.Concat(doorsCollector).Concat(windowsCollector);

            foreach (var element in elements)
            {
                switch (element)
                {
                    case Autodesk.Revit.DB.Wall wall when wall is Autodesk.Revit.DB.Wall:
                        {
                            var line = ((wall.Location as LocationCurve).Curve as Autodesk.Revit.DB.Line);
                            var boundingBox = element.get_BoundingBox(null);
                            units = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).GetUnitTypeId();
                            var length = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
                            var actualLength = UnitUtils.ConvertFromInternalUnits(length, units);
                            var level = levels.First(x => x.Id.IntegerValue == wall.LevelId.IntegerValue);

                            walls.Add(new Wall()
                            {
                                LevelIndex = levels.IndexOf(level),
                                LevelName = level.Name,
                                Description = $"{wall.Name} ({actualLength} {units.TypeId})",
                                Line = FixLine(scale, line, boundingBox),
                                BoundingBox = GetRectangleFromBoundingBox(scale, boundingBox),
                                Width = wall.Width,
                            });

                            break;
                        }

                    case Autodesk.Revit.DB.FamilyInstance doorOrWindow when doorOrWindow is Autodesk.Revit.DB.FamilyInstance:
                        {
                            var boundingBox = element.get_BoundingBox(null);
                            var location = doorOrWindow.Location as LocationPoint;
                            var rectangle = GetRectangleFromBoundingBox(scale, boundingBox);
                            var level = levels.First(x => x.Id.IntegerValue == doorOrWindow.LevelId.IntegerValue);

                            walls.Add(new DoorOrWindow()
                            {
                                IsWindow = doorOrWindow.Category.Id.IntegerValue == -2000014,
                                LevelIndex = levels.IndexOf(level),
                                LevelName = level.Name,
                                Description = $"{doorOrWindow.Name}",
                                Point = new Point() { X = location.Point.X * scale, Y = location.Point.Y * scale },
                                BoundingBox = GetRectangleFromBoundingBox(scale, boundingBox)
                            });

                            break;
                        }
                }
            }

            foreach (var wall in walls.OrderBy(x => x.LevelIndex))
            {
                if (!levelBoundingBoxes.ContainsKey(wall.LevelIndex))
                    levelBoundingBoxes.Add(wall.LevelIndex, new Rectangle()
                    {
                        MinX = double.MaxValue,
                        MinY = double.MaxValue,
                        MaxX = double.MinValue,
                        MaxY = double.MinValue
                    });

                if(renderLines && wall is Wall)
                    levelBoundingBoxes[wall.LevelIndex].EnsureContainsLine((wall as Wall).Line);

                if(renderRects)
                    levelBoundingBoxes[wall.LevelIndex].EnsureContainsRectangle(wall.BoundingBox);

                if (renderLines && wall is Wall)
                {
                    if ((wall as Wall).Line.X1 < offsetX) offsetX = (wall as Wall).Line.X1;
                    if ((wall as Wall).Line.Y1 < offsetY) offsetY = (wall as Wall).Line.Y1;
                    if ((wall as Wall).Line.X2 < offsetX) offsetX = (wall as Wall).Line.X2;
                    if ((wall as Wall).Line.Y2 < offsetY) offsetY = (wall as Wall).Line.Y2;
                }

                if (renderRects)
                {
                    if (wall.BoundingBox.MinX < offsetX) offsetX = wall.BoundingBox.MinX;
                    if (wall.BoundingBox.MinY < offsetY) offsetY = wall.BoundingBox.MinY;
                }
            }

            foreach (var wall in walls.OrderBy(x => x.LevelIndex))
            {
                if(currentLevelIndex != wall.LevelIndex)
                {
                    currentLevelOffset += wall.LevelIndex == 0 
                        ? 0 
                        : levelBoundingBoxes[wall.LevelIndex-1].Height + paddingBetweenLevels;

                    var levelBoundingBox = levelBoundingBoxes[wall.LevelIndex];
                    
                    if (renderFloorNames)
                    {
                        currentLevelOffset += 10; //Padding around the text

                        var floorHeight = UnitUtils.ConvertFromInternalUnits(
                            levels[wall.LevelIndex + 1].Elevation - levels[wall.LevelIndex].Elevation, 
                            units);

                        var floorWidth = UnitUtils.ConvertFromInternalUnits(
                            levelBoundingBox.Width / scale,
                            units);

                        svgBuilder.AppendLine(
                            $"<text x=\"0\" y=\"{currentLevelOffset}\" fill=\"black\">{wall.LevelName} (Width: {Math.Round(floorWidth, 2)}, Height: {Math.Round(floorHeight, 2)} {units.TypeId})</text>");

                        currentLevelOffset += 10; //Padding around the text
                    }

                    var lrectX = levelBoundingBox.MinX + Math.Abs(offsetX);
                    var lrectY = levelBoundingBox.MinY + Math.Abs(offsetY) + currentLevelOffset;
                    var lrectWidth = levelBoundingBox.Width;
                    var lrectHeight = levelBoundingBox.Height;

                    svgBuilder.AppendLine(
                       $"<rect x=\"{lrectX.Normalize()}\" y=\"{lrectY.Normalize()}\" width=\"{lrectWidth.Normalize()}\" height=\"{lrectHeight.Normalize()}\" style=\"stroke:rgb(0,0,255);fill: none;stroke-width:2\" />");

                }

                currentLevelIndex = wall.LevelIndex;

                if (renderLines && wall is Wall)
                {
                    var x1 = (wall as Wall).Line.X1 + Math.Abs(offsetX);
                    var y1 = (wall as Wall).Line.Y1 + Math.Abs(offsetY) + currentLevelOffset;
                    var x2 = (wall as Wall).Line.X2 + Math.Abs(offsetX);
                    var y2 = (wall as Wall).Line.Y2 + Math.Abs(offsetY) + currentLevelOffset;
                    var strokeWidth = (wall as Wall).Width * scale;

                    svgBuilder.AppendLine(
                        $"<line x1=\"{x1.Normalize()}\" y1=\"{y1.Normalize()}\" x2=\"{x2.Normalize()}\" y2=\"{y2.Normalize()}\" style=\"stroke:rgb(0,0,0);stroke-width:{strokeWidth}\" />");
                }

                if(wall is DoorOrWindow)
                {
                    var x = (wall as DoorOrWindow).Point.X + Math.Abs(offsetX);
                    var y = (wall as DoorOrWindow).Point.Y + Math.Abs(offsetY) + currentLevelOffset;

                    svgBuilder.AppendLine(
                        $"<circle cx=\"{x.Normalize()}\" cy=\"{y.Normalize()}\" r=\"{scale}\"  style=\"fill:{((wall as DoorOrWindow).IsWindow ? "blue" : "green")}\" />");
                }

                var rectX = wall.BoundingBox.MinX + Math.Abs(offsetX);
                var rectY = wall.BoundingBox.MinY + Math.Abs(offsetY) + currentLevelOffset;
                var rectWidth = wall.BoundingBox.Width;
                var rectHeight = wall.BoundingBox.Height;

                svgWidth = Math.Max(svgWidth, Convert.ToInt32(rectX + rectWidth));
                svgHeight = Math.Max(svgHeight, Convert.ToInt32(rectY + rectWidth));

                svgBuilder.AppendLine(
                    $"<!-- {wall.Description} -->");

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

            var originX = line.Direction.X >= 0 ? boundingBox.Min.X : boundingBox.Max.X;
            var originY = line.Direction.Y >= 0 ? boundingBox.Min.Y : boundingBox.Max.Y;

            var offsetX = ((boundingBox.Max.X - boundingBox.Min.X) / 2)
                * (Math.Abs(line.Direction.Y) * (line.Direction.X >= 0 ? 1 : -1));
            var offsetY = ((boundingBox.Max.Y - boundingBox.Min.Y) / 2)
                * (Math.Abs(line.Direction.X) * (line.Direction.Y >= 0 ? 1 : -1));

            if (Math.Abs(line.Direction.X.Normalize()) > 0 && Math.Abs(line.Direction.Y.Normalize()) > 0)
            {
                offsetX = 0;
                offsetY = 0;
            }

            var sourceX = (originX + offsetX) * scale;
            var sourceY = (originY + offsetY) * scale;
            var destinationX = ((originX + (line.Direction.X * line.Length)) + offsetX) * scale;
            var destinationY = ((originY + (line.Direction.Y * line.Length)) + offsetY) * scale;

            //Normalize the line to go from left to right
            return sourceX <= destinationX ? new Line()
            {
                X1 = sourceX,
                Y1 = sourceY,
                X2 = destinationX,
                Y2 = destinationY
            } : new Line()
            {
                X1 = destinationX,
                Y1 = destinationY,
                X2 = sourceX,
                Y2 = sourceY
            };
        }
        private static Rectangle GetRectangleFromBoundingBox(double scale, BoundingBoxXYZ box)
        {
            return new Rectangle()
            {
                MinX = box.Min.X * scale,
                MinY = box.Min.Y * scale,
                MaxX = box.Max.X * scale,
                MaxY = box.Max.Y * scale
            };
        }
    }
}
