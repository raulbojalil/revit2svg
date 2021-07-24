using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using DesignAutomationFramework;
using Autodesk.Revit.ApplicationServices;
using System.IO;

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
                SvgRenderer.Render(doc);
                tx.RollBack();
            }
        }

        public ExternalDBApplicationResult OnShutdown(ControlledApplication application)
        {
            return ExternalDBApplicationResult.Succeeded;
        }
    }

}

