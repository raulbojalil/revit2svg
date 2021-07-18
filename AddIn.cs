using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using Autodesk.Revit.ApplicationServices;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Revit2Svg.Models;
using Revit2Svg;
using Rectangle = Revit2Svg.Models.Rectangle;
using Wall = Revit2Svg.Models.Wall;
using Line = Revit2Svg.Models.Line;

namespace Revit2Svg
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AddIn : IExternalDBApplication
    {
        public ExternalDBApplicationResult OnStartup(Autodesk.Revit.ApplicationServices.ControlledApplication app)
        {
            DesignAutomationBridge.DesignAutomationReadyEvent += HandleDesignAutomationReadyEvent;
            return ExternalDBApplicationResult.Succeeded;
        }

        public void HandleDesignAutomationReadyEvent(object sender, DesignAutomationReadyEventArgs e)
        {
            e.Succeeded = true;
            ExportToSvg(e.DesignAutomationData);
        }

        public static void ExportToSvg(DesignAutomationData data)
        {

            if (data == null) throw new ArgumentNullException(nameof(data));

            var rvtApp = data.RevitApp;
            if (rvtApp == null) throw new InvalidDataException(nameof(rvtApp));

            string modelPath = data.FilePath;
            if (string.IsNullOrWhiteSpace(modelPath)) throw new InvalidDataException(nameof(modelPath));

            var doc = data.RevitDoc;
            if (doc == null) throw new InvalidDataException(nameof(doc));

            using (var tx = new Transaction(doc))
            {
                tx.Start("Export to Svg");

                DrawWalls(doc);

                tx.RollBack();
            }
        }

        public static void DrawWalls(Document doc)
        {

            var walls = new List<Wall>();

            var offsetX = double.MaxValue;
            var offsetY = double.MaxValue;

            var svgWidth = 0;
            var svgHeight = 0;
            double scale = 10;
            var currentLevel = 0;

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
                    currentLevel++;

                currentLevelId = wall.LevelId;

                //var geometry = wall.get_Geometry(new Options());
                //foreach(var item in geometry)
                //{
                    
                //}

                var line = ((wall.Location as LocationCurve).Curve as Autodesk.Revit.DB.Line);
                var boundingBox = wall.get_BoundingBox(null);

                walls.Add(new Wall()
                {
                    Level = currentLevel,
                    Description = $"{wall.Name} (Level: {levels.FirstOrDefault(x => x.Id.IntegerValue == wall.LevelId.IntegerValue)?.Name ?? "Unknown"})",
                    Line = FixLine(scale, line, boundingBox),
                    BoundingBox = GetRectangleFromBoundingBox(scale, boundingBox),
                    Width = wall.Width
                });
            }

            levelHeights.Add(0, 0);

            foreach (var wall in walls.OrderBy(x => x.Level))
            {
                if (!levelHeights.ContainsKey(wall.Level + 1))
                    levelHeights.Add(wall.Level + 1, double.MinValue);

                if (wall.Line.Y1 > levelHeights[wall.Level + 1]) levelHeights[wall.Level + 1] = wall.Line.Y1;
                if (wall.Line.Y2 > levelHeights[wall.Level + 1]) levelHeights[wall.Level + 1] = wall.Line.Y2;

                if (wall.Line.X1 < offsetX) offsetX = wall.Line.X1;
                if (wall.Line.Y1 < offsetY) offsetY = wall.Line.Y1;
                if (wall.Line.X2 < offsetX) offsetX = wall.Line.X2;
                if (wall.Line.Y2 < offsetY) offsetY = wall.Line.Y2;
            }

            foreach (var wall in walls)
            {
                var x1 = wall.Line.X1 + Math.Abs(offsetX);
                var y1 = wall.Line.Y1 + Math.Abs(offsetY) + levelHeights[wall.Level] + (Math.Abs(offsetY) * wall.Level);
                var x2 = wall.Line.X2 + Math.Abs(offsetX);
                var y2 = wall.Line.Y2 + Math.Abs(offsetY) + levelHeights[wall.Level] + (Math.Abs(offsetY) * wall.Level);
                var strokeWidth = wall.Width * scale;

                var rectX = wall.BoundingBox.X + Math.Abs(offsetX);
                var rectY = wall.BoundingBox.Y + Math.Abs(offsetY) + levelHeights[wall.Level] + (Math.Abs(offsetY) * wall.Level);
                var rectWidth = wall.BoundingBox.Width;
                var rectHeight = wall.BoundingBox.Height;

                if (svgWidth < rectX + rectWidth) svgWidth = Convert.ToInt32(rectX + rectWidth);
                if (svgHeight < rectY + rectHeight) svgHeight = Convert.ToInt32(rectY + rectHeight);

                svgBuilder.AppendLine(
                    $"<!-- {wall.Description} -->");

                svgBuilder.AppendLine(
                    $"<line x1=\"{Utils.NormalizeDouble(x1)}\" y1=\"{Utils.NormalizeDouble(y1)}\" x2=\"{Utils.NormalizeDouble(x2)}\" y2=\"{Utils.NormalizeDouble(y2)}\" style=\"stroke:rgb(0,0,0);stroke-width:{strokeWidth}\" />");

                svgBuilder.AppendLine(
                    $"<rect x=\"{Utils.NormalizeDouble(rectX)}\" y=\"{Utils.NormalizeDouble(rectY)}\" width=\"{Utils.NormalizeDouble(rectWidth)}\" height=\"{Utils.NormalizeDouble(rectHeight)}\" style=\"stroke:rgb(255,0,0);fill: none;stroke-width:1\" />");


            }

            var contents = $"<!DOCTYPE html><html><body><svg height=\"{svgHeight}\" width=\"{svgWidth}\">\r\n{svgBuilder}</svg></body></html>";
            File.WriteAllText(@".\svg_data.html", contents);
        }

        private static Line FixLine(double scale, Autodesk.Revit.DB.Line line, BoundingBoxXYZ boundingBox)
        {
            var lineOriginX = line.Origin.X;
            var lineOriginY = line.Origin.Y;

            return new Line()
            {
                X1 = lineOriginX * scale,
                Y1 = lineOriginY * scale,
                X2 = (lineOriginX + (line.Direction.X * line.Length)) * scale,
                Y2 = (lineOriginY + (line.Direction.Y * line.Length)) * scale,
            };
           
        }

        public static Rectangle GetRectangleFromBoundingBox(double scale, BoundingBoxXYZ box)
        {
            return new Rectangle()
            {
                X = box.Min.X * scale,
                Y = box.Min.Y * scale,
                Width = (box.Max.X * scale) - (box.Min.X * scale),
                Height = (box.Max.Y * scale) - (box.Min.Y * scale)
            };
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            return ExternalDBApplicationResult.Succeeded;
        }
    }

}

