# Revit to SVG DA4R Add-In

This is a DA4R (Design Automation for Revit) add-in for exporting a Revit file to SVG.

If you have a version other than Revit 2021, fix the reference to the RevitAPI.dll according to the Revit version you are using. Additionally, fix the reference to the Revit add-ins folder in the Post Build event CLI. Use the following tool to test locally: 

https://github.com/Autodesk-Forge/design.automation-csharp-revit.local.debug.tool 

## Deploying to the Cloud
The project is already configured to output an app bundle file (.zip) to be uploaded to Forge. Use the following Postman files to configure the cloud application and upload the zip file to Forge: 

https://github.com/Autodesk-Forge/forge-tutorial-postman/tree/master/DA4Revit 
