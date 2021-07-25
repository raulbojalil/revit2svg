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
using Level = Revit2Svg.Models.Level;
using System.IO;
using Newtonsoft.Json;

namespace Revit2Svg
{
    public class SvgRenderer
    {
        public void Render(Document doc, double scale = 10, bool renderLines = true, 
            bool renderRects = true, bool renderFloorNames = true, double paddingBetweenLevels = 10)
        {
            var levels = ExtractLevelsFromDocument(doc);

            double drawingY = 0;
            var svgBuilder = new StringBuilder();

            double svgWidth = 0;
            double svgHeight = 0;

            foreach (var level in levels)
            {
                var offsetX = level.BoundingBox.MinX * scale * -1;
                var offsetY = level.BoundingBox.MinY * scale * -1;

                if (renderFloorNames)
                {
                    drawingY += 15; //Padding around the text

                    svgBuilder.AppendLine(
                        $"<text x=\"0\" y=\"{drawingY}\" fill=\"black\">{level.Name}</text>");

                    drawingY += 15; //Padding around the text
                }

                if(renderRects)
                {
                    var rectX = (level.InnerBoundingBox.MinX * scale) + Math.Abs(offsetX);
                    var rectY = (level.InnerBoundingBox.MinY * scale) + Math.Abs(offsetY) + drawingY;
                    var rectWidth = level.InnerBoundingBox.Width * scale;
                    var rectHeight = level.InnerBoundingBox.Height * scale;

                    svgWidth = Math.Max(svgWidth, rectWidth);
                    svgHeight = Math.Max(svgHeight, rectHeight);

                    svgBuilder.AppendLine(
                            $"<rect x=\"{rectX.Normalize()}\" y=\"{rectY.Normalize()}\" width=\"{rectWidth.Normalize()}\" height=\"{rectHeight.Normalize()}\" style=\"stroke:rgb(255,255,0);fill: none;stroke-width:1\" />");
                }


                foreach (var element in level.Elements)
                {
                    if (renderLines && element is Wall)
                    {
                        var x1 = ((element as Wall).Line.X1 * scale) + Math.Abs(offsetX);
                        var y1 = ((element as Wall).Line.Y1 * scale) + Math.Abs(offsetY) + drawingY;
                        var x2 = ((element as Wall).Line.X2 * scale) + Math.Abs(offsetX);
                        var y2 = ((element as Wall).Line.Y2 * scale) + Math.Abs(offsetY) + drawingY;
                        var strokeWidth = (element as Wall).Width * scale;

                        svgWidth = Math.Max(svgWidth, x2);
                        svgHeight = Math.Max(svgHeight, y2);

                        svgBuilder.AppendLine(
                            $"<line x1=\"{x1.Normalize()}\" y1=\"{y1.Normalize()}\" x2=\"{x2.Normalize()}\" y2=\"{y2.Normalize()}\" style=\"stroke:rgb(0,0,0);stroke-width:{strokeWidth}\" />");
                    }

                    if (renderRects && element is Wall)
                    {
                        var rectX = ((element as Wall).BoundingBox.MinX * scale) + Math.Abs(offsetX);
                        var rectY = ((element as Wall).BoundingBox.MinY * scale) + Math.Abs(offsetY) + drawingY;
                        var rectWidth = (element as Wall).BoundingBox.Width * scale;
                        var rectHeight = (element as Wall).BoundingBox.Height * scale;

                        svgWidth = Math.Max(svgWidth, rectWidth);
                        svgHeight = Math.Max(svgHeight, rectHeight);

                        svgBuilder.AppendLine(
                                $"<rect x=\"{rectX.Normalize()}\" y=\"{rectY.Normalize()}\" width=\"{rectWidth.Normalize()}\" height=\"{rectHeight.Normalize()}\" style=\"stroke:rgb(255,0,0);fill: none;stroke-width:1\" />");
                    }

                    if (element is DoorOrWindow)
                    {
                        var x = ((element as DoorOrWindow).Point.X * scale) + Math.Abs(offsetX);
                        var y = ((element as DoorOrWindow).Point.Y * scale) + Math.Abs(offsetY) + drawingY;

                        svgBuilder.AppendLine(
                            $"<circle cx=\"{x.Normalize()}\" cy=\"{y.Normalize()}\" r=\"{scale}\"  style=\"fill:{((element as DoorOrWindow).IsWindow ? "blue" : "green")}\" />");
                    }
                }

                drawingY += (level.BoundingBox.Height * scale);
            }

            File.WriteAllText(@".\svg_data.html", $"<!DOCTYPE html><html><body><svg height=\"{svgHeight}\" width=\"{svgWidth}\">\r\n{svgBuilder}</svg></body></html>");
            File.WriteAllText(@".\metadata.json", JsonConvert.SerializeObject(levels));
        }

        private Level[] ExtractLevelsFromDocument(Document doc)
        {
            var levels = (new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Level)))
               .ToElements()
               .Select(x => x as Autodesk.Revit.DB.Level)
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

            var extractedLevels = levels.Select(x => new Level() { 
                Name = x.Name, Elevation = x.Elevation, 
                InnerBoundingBox = new Rectangle() { MaxX = double.MinValue, MinX = double.MaxValue, MaxY = double.MinValue, MinY = double.MaxValue, },
                BoundingBox = new Rectangle() { MaxX = double.MinValue, MinX = double.MaxValue, MaxY = double.MinValue, MinY = double.MaxValue, },
                Elements = new List<Element>() }).ToArray();

            for (var i = 0; i < extractedLevels.Length - 1; i++)
            {
                var height = extractedLevels[i + 1].Elevation - extractedLevels[i].Elevation;
                extractedLevels[i].Height = height;
                extractedLevels[i].HeightM = ConvertToMeters(height);
                extractedLevels[i].HeightFt = ConvertToFeet(height);
            }

            foreach (var element in elements)
            {
                switch (element)
                {
                    case Autodesk.Revit.DB.Wall wall when wall is Autodesk.Revit.DB.Wall:
                        {
                            var line = ((wall.Location as LocationCurve).Curve as Autodesk.Revit.DB.Line);
                            var boundingBox = element.get_BoundingBox(null);
                            var units = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).GetUnitTypeId();
                            var length = wall.get_Parameter(BuiltInParameter.CURVE_ELEM_LENGTH).AsDouble();
                            var actualLength = UnitUtils.ConvertFromInternalUnits(length, units);
                            var level = levels.First(x => x.Id.IntegerValue == wall.LevelId.IntegerValue);
                            var levelIndex = levels.IndexOf(level);

                            var wallElement = new Wall()
                            {
                                Name = $"{wall.Name} ({actualLength} {units.TypeId})",
                                Line = FixLine(line, boundingBox),
                                BoundingBox = GetRectangleFromBoundingBox(boundingBox),
                                Width = wall.Width,
                            };

                            extractedLevels[levelIndex].InnerBoundingBox.EnsureContainsRectangle(wallElement.BoundingBox);

                            extractedLevels[levelIndex].BoundingBox.EnsureContainsRectangle(wallElement.BoundingBox);
                            extractedLevels[levelIndex].Elements.Add(wallElement);

                            break;
                        }

                    case Autodesk.Revit.DB.FamilyInstance doorOrWindow when doorOrWindow is Autodesk.Revit.DB.FamilyInstance:
                        {
                            var boundingBox = element.get_BoundingBox(null);
                            var location = doorOrWindow.Location as LocationPoint;
                            var rectangle = GetRectangleFromBoundingBox(boundingBox);
                            var level = levels.First(x => x.Id.IntegerValue == doorOrWindow.LevelId.IntegerValue);
                            var levelIndex = levels.IndexOf(level);

                            var doorOrWindowElement = new DoorOrWindow()
                            {
                                IsWindow = doorOrWindow.Category.Id.IntegerValue == -2000014,
                                Name = $"{doorOrWindow.Name}",
                                Point = new Point() { X = location.Point.X, Y = location.Point.Y },
                                BoundingBox = GetRectangleFromBoundingBox(boundingBox)
                            };

                            extractedLevels[levelIndex].BoundingBox.EnsureContainsRectangle(doorOrWindowElement.BoundingBox);
                            extractedLevels[levelIndex].Elements.Add(doorOrWindowElement);

                            break;
                        }
                }
            }

            foreach(var level in extractedLevels)
            {
                level.WidthM = ConvertToMeters(level.InnerBoundingBox.Width);
                level.WidthFt = ConvertToFeet(level.InnerBoundingBox.Width);
            }

            return extractedLevels.ToArray();
        }

        private Line FixLine(Autodesk.Revit.DB.Line line, BoundingBoxXYZ boundingBox)
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

            var sourceX = (originX + offsetX);
            var sourceY = (originY + offsetY);
            var destinationX = ((originX + (line.Direction.X * line.Length)) + offsetX);
            var destinationY = ((originY + (line.Direction.Y * line.Length)) + offsetY);

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
        private Rectangle GetRectangleFromBoundingBox(BoundingBoxXYZ box)
        {
            return new Rectangle()
            {
                MinX = box.Min.X,
                MinY = box.Min.Y,
                MaxX = box.Max.X,
                MaxY = box.Max.Y
            };
        }

        private double ConvertToMeters(double distance)
        {
            try
            {
                return UnitUtils.ConvertFromInternalUnits(distance, UnitTypeId.Meters);
            }
            catch
            {
                return -1;
            }
        }

        private double ConvertToFeet(double distance)
        {
            try
            {
                return UnitUtils.ConvertFromInternalUnits(distance, UnitTypeId.Feet);
            }
            catch
            {
                return -1;
            }
        }
    }
}
