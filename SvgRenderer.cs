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
using OtherElement = Revit2Svg.Models.OtherElement;
using Level = Revit2Svg.Models.Level;
using Document = Revit2Svg.Models.Document;
using ElementType = Revit2Svg.Models.ElementType;
using System.IO;
using Newtonsoft.Json;

namespace Revit2Svg
{
    public class SvgRenderer
    {
        public void Render(Autodesk.Revit.DB.Document doc, double scale = 10, bool renderLines = true, 
            bool renderRects = true, bool renderFloorNames = true)
        {
            var document = ExtractDataFromDocument(doc);

            double drawingY = 0;
            var svgBuilder = new StringBuilder();

            double svgWidth = 0;
            double svgHeight = 0;

            foreach (var level in document.Levels)
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

                var levelRectX = (level.InnerBoundingBox.MinX * scale) + Math.Abs(offsetX);
                var levelRectY = (level.InnerBoundingBox.MinY * scale) + Math.Abs(offsetY) + drawingY;
                var levelRectHeight = level.InnerBoundingBox.Height * scale;
                var levelRectWidth = level.InnerBoundingBox.Width * scale;

                if (renderRects)
                {
                    svgWidth = Math.Max(svgWidth, levelRectWidth);
                    svgBuilder.AppendLine(
                            $"<rect x=\"{levelRectX.Normalize()}\" y=\"{levelRectY.Normalize()}\" width=\"{levelRectWidth.Normalize()}\" height=\"{levelRectHeight.Normalize()}\" style=\"stroke:rgb(255,255,0);fill: none;stroke-width:1\" />");
                }

                foreach (var element in level.Elements.OrderBy(x => x.Type))
                {
                    if (renderLines && element is Wall)
                    {
                        var x1 = ((element as Wall).Line.X1 * scale) + Math.Abs(offsetX);
                        var y1 = ((element as Wall).Line.Y1 * scale) + Math.Abs(offsetY) + drawingY;
                        var x2 = ((element as Wall).Line.X2 * scale) + Math.Abs(offsetX);
                        var y2 = ((element as Wall).Line.Y2 * scale) + Math.Abs(offsetY) + drawingY;
                        var strokeWidth = (element as Wall).Width * scale;

                        //Correcting the Y axis
                        y1 = levelRectY - (y1 - levelRectY) + levelRectHeight;
                        y2 = levelRectY - (y2 - levelRectY) + levelRectHeight;

                        svgWidth = Math.Max(svgWidth, x2);

                        svgBuilder.AppendLine(
                            $"<line x1=\"{x1.Normalize()}\" y1=\"{y1.Normalize()}\" x2=\"{x2.Normalize()}\" y2=\"{y2.Normalize()}\" style=\"stroke:rgb(0,0,0);stroke-width:{strokeWidth}\" />");
                    }

                    if (renderRects)
                    {
                        var rectX = (element.BoundingBox.MinX * scale) + Math.Abs(offsetX);
                        var rectY = (element.BoundingBox.MinY * scale) + Math.Abs(offsetY) + drawingY;
                        var rectWidth = element.BoundingBox.Width * scale;
                        var rectHeight = element.BoundingBox.Height * scale;

                        //Correcting the Y axis
                        rectY = levelRectY - (rectY - levelRectY) - rectHeight + levelRectHeight;

                        svgWidth = Math.Max(svgWidth, rectX + rectWidth);

                        svgBuilder.AppendLine(
                                $"<rect x=\"{rectX.Normalize()}\" y=\"{rectY.Normalize()}\" width=\"{rectWidth.Normalize()}\" height=\"{rectHeight.Normalize()}\" style=\"stroke:rgb(255,0,0);fill: none;stroke-width:1\" />");
                    }

                    if (element is OtherElement)
                    {
                        var x = ((element as OtherElement).Point.X * scale) + Math.Abs(offsetX);
                        var y = ((element as OtherElement).Point.Y * scale) + Math.Abs(offsetY) + drawingY;

                        //Correcting the Y axis
                        y = levelRectY - (y - levelRectY) + levelRectHeight;

                        svgBuilder.AppendLine(
                            $"<circle cx=\"{x.Normalize()}\" cy=\"{y.Normalize()}\" r=\"{scale}\"  style=\"fill:{((element as OtherElement).Type == ElementType.Window ? "blue" : "green")}\" />");
                    }
                }

                drawingY += (level.BoundingBox.Height * scale);
                svgHeight = drawingY;
            }

            File.WriteAllText(@".\svg_data.html", $"<!DOCTYPE html><html><body><svg height=\"{svgHeight}\" width=\"{svgWidth}\">\r\n{svgBuilder}</svg></body></html>");
            File.WriteAllText(@".\metadata.json", JsonConvert.SerializeObject(document));
        }

        private Document ExtractDataFromDocument(Autodesk.Revit.DB.Document doc)
        {
            var document = new Document()
            {
                Title = doc.Title,
                Latitude = doc.SiteLocation.Latitude / (Math.PI / 180),
                Longitude = doc.SiteLocation.Longitude / (Math.PI / 180),
                TimeZone = doc.SiteLocation.TimeZone,
                Units = doc.DisplayUnitSystem.ToString(),
                AngleFromTrueNorth = doc.ActiveProjectLocation.GetProjectPosition(XYZ.Zero).Angle,
                Levels = new List<Level>()
            };


            var levels = (new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Level)))
               .ToElements()
               .Select(x => x as Autodesk.Revit.DB.Level)
               .OrderBy(x => x.Elevation)
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

            document.Levels = levels.Select(x => new Level()
            {
                Type = ElementType.Level,
                Name = x.Name,
                Elevation = x.Elevation,
                InnerBoundingBox = Rectangle.MinMax,
                BoundingBox = Rectangle.MinMax,
                Elements = new List<Element>()
            }).ToList();

            for (var i = 0; i < document.Levels.Count - 1; i++)
            {
                var height = document.Levels[i + 1].Elevation - document.Levels[i].Elevation;
                document.Levels[i].HeightM = TryConvertToMeters(height);
                document.Levels[i].HeightFt = height;
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
                                Type = ElementType.Wall,
                                Name = $"{wall.Name} ({actualLength} {units.TypeId})",
                                Line = FixLine(line, boundingBox),
                                BoundingBox = GetRectangleFromBoundingBox(boundingBox),
                                Width = wall.Width,
                            };

                            document.Levels[levelIndex].InnerBoundingBox.EnsureContainsRectangle(wallElement.BoundingBox);

                            document.Levels[levelIndex].BoundingBox.EnsureContainsRectangle(wallElement.BoundingBox);
                            document.Levels[levelIndex].Elements.Add(wallElement);

                            break;
                        }

                    case Autodesk.Revit.DB.FamilyInstance familyInstance when familyInstance is Autodesk.Revit.DB.FamilyInstance:
                        {
                            var boundingBox = element.get_BoundingBox(null);
                            var location = familyInstance.Location as LocationPoint;
                            var rectangle = GetRectangleFromBoundingBox(boundingBox);
                            var level = levels.First(x => x.Id.IntegerValue == familyInstance.LevelId.IntegerValue);
                            var levelIndex = levels.IndexOf(level);

                            var doorOrWindowElement = new OtherElement()
                            {
                                Type = familyInstance.Category.Id.IntegerValue == -2000014 ? ElementType.Window : ElementType.Door,
                                Name = $"{familyInstance.Name}",
                                Point = new Point() { X = location.Point.X, Y = location.Point.Y },
                                BoundingBox = GetRectangleFromBoundingBox(boundingBox)
                            };

                            document.Levels[levelIndex].BoundingBox.EnsureContainsRectangle(doorOrWindowElement.BoundingBox);
                            document.Levels[levelIndex].Elements.Add(doorOrWindowElement);

                            break;
                        }
                }
            }

            foreach(var level in document.Levels)
            {
                if (level.Elements.Count == 0)
                {
                    level.InnerBoundingBox = Rectangle.Zero;
                    level.BoundingBox = Rectangle.Zero;
                }

                level.WidthM = TryConvertToMeters(level.InnerBoundingBox.Width);
                level.WidthFt = TryConvertToFeet(level.InnerBoundingBox.Width);
            }

            return document;
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
            } 
            : new Line()
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

        private double TryConvertToMeters(double distance)
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

        private double TryConvertToFeet(double distance)
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
