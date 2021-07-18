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

            var lines = new List<StartEndLine>();

            var offsetX = double.MaxValue;
            var offsetY = double.MaxValue;

            var svgWidth = 0;
            var svgHeight = 0;
            var scale = 10;
            var currentLevel = 0;

            var svgBuilder = new StringBuilder();
            var levelHeights = new Dictionary<int, double>();

            ElementId currentLevelId = null;

            var levels = (new FilteredElementCollector(doc).OfClass(typeof(Level))).ToElements();

            var wallsCollector = (new FilteredElementCollector(doc).OfClass(typeof(Wall)));

            foreach (Wall wall in wallsCollector.OrderBy(x => (x as Wall).LevelId.IntegerValue))
            {
                if (currentLevelId == null)
                    currentLevelId = wall.LevelId;

                if (currentLevelId.IntegerValue != wall.LevelId.IntegerValue)
                    currentLevel++;

                currentLevelId = wall.LevelId;

                var line = ((wall.Location as LocationCurve).Curve as Line);

                lines.Add(new StartEndLine()
                {
                    X1 = line.Origin.X * scale,
                    Y1 = line.Origin.Y * scale,
                    X2 = (line.Origin.X + (line.Direction.X * line.Length)) * scale,
                    Y2 = (line.Origin.Y + (line.Direction.Y * line.Length)) * scale,
                    Width = wall.Width,
                    Level = currentLevel,
                    Description = $"{wall.Name} (Level: {levels.FirstOrDefault(x => x.Id.IntegerValue == wall.LevelId.IntegerValue)?.Name ?? "Unknown"})"
                });
            }

            levelHeights.Add(0, 0);

            foreach (var line in lines.OrderBy(x => x.Level))
            {
                if (!levelHeights.ContainsKey(line.Level + 1))
                    levelHeights.Add(line.Level + 1, double.MinValue);

                if (line.Y1 > levelHeights[line.Level + 1]) levelHeights[line.Level + 1] = line.Y1;
                if (line.Y2 > levelHeights[line.Level + 1]) levelHeights[line.Level + 1] = line.Y2;

                if (line.X1 < offsetX) offsetX = line.X1;
                if (line.Y1 < offsetY) offsetY = line.Y1;
                if (line.X2 < offsetX) offsetX = line.X2;
                if (line.Y2 < offsetY) offsetY = line.Y2;
            }

            foreach (var line in lines)
            {
                var x1 = line.X1 + Math.Abs(offsetX);
                var y1 = line.Y1 + Math.Abs(offsetY) + levelHeights[line.Level] + (Math.Abs(offsetY) * line.Level);
                var x2 = line.X2 + Math.Abs(offsetX);
                var y2 = line.Y2 + Math.Abs(offsetY) + levelHeights[line.Level] + (Math.Abs(offsetY) * line.Level);
                var strokeWidth = line.Width * scale;

                if (svgWidth < x1) svgWidth = Convert.ToInt32(x1);
                if (svgWidth < x2) svgWidth = Convert.ToInt32(x2);
                if (svgHeight < y1) svgHeight = Convert.ToInt32(y1);
                if (svgHeight < y2) svgHeight = Convert.ToInt32(y2);

                svgBuilder.AppendLine(
                    $"<!-- {line.Description} --><line x1=\"{Utils.NormalizeDouble(x1)}\" y1=\"{Utils.NormalizeDouble(y1)}\" x2=\"{Utils.NormalizeDouble(x2)}\" y2=\"{Utils.NormalizeDouble(y2)}\" style=\"stroke:rgb(0,0,0);stroke-width:{strokeWidth}\" />");
            }

            var contents = $"<!DOCTYPE html><html><body><svg height=\"{svgHeight}\" width=\"{svgWidth}\">{svgBuilder}</svg></body></html>";
            File.WriteAllText(@".\svg_data.html", contents);
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            return ExternalDBApplicationResult.Succeeded;
        }
    }

}

