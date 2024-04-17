#region Using directives
using System;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using FTOptix.HMIProject;
using FTOptix.NetLogic;
using FTOptix.UI;
using FTOptix.NativeUI;
using FTOptix.WebUI;
using FTOptix.CoreBase;
using FTOptix.Retentivity;
using FTOptix.Core;
#endregion
using RockwellAutomation.LogixDesigner;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using FTOptix.WebUI;
using Grpc.Core;
using System.IO;
using static RockwellAutomation.LogixDesigner.LogixProject;

public class LogixSDKLogic : BaseNetLogic
{
    public override void Start()
    {
        // Insert code to be executed when the user-defined logic is started
        status = LogicObject.GetVariable("Status");
        mode = LogicObject.GetVariable("ControllerMode");
    }

    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
        status = null;
        project = null;
    }
    [ExportMethod]
    public void LoadProject(out int r)
    {
        string filename = @"C:\PLC\SDK1.ACD";
        string filepath = LogicObject.GetVariable("FilePath").Value;
        char separator = Path.AltDirectorySeparatorChar;
        string separators = $"{separator}{separator}{separator}";
        filename = filepath.Split(separators)[1];
        try
        {
            project = LogixProject.OpenLogixProjectAsync(filename).Result;
            status.Value = $"Project Opened :{filename}";
            Log.Info(status.Value);
        }
        catch (LogixSdkException ex)
        {
            status.Value = $"Unable to open project at {filename}";
            Log.Warning(status.Value);
            Log.Warning(ex.Message);
            r = -1;
            return;
        }
        r = 0;
    }

    [ExportMethod]
    public void SetCommPath(out int r)
    {
        //string commPath = @"AB_ETH-1\192.168.1.31\Backplane\0";
        string commpath1 = LogicObject.GetVariable("CommPath").Value;

        try
        {
            project.SetCommunicationsPathAsync(commpath1).Wait();
            status.Value = $"Path :{commpath1} is set";
            Log.Info(status.Value);
        }
        catch (LogixSdkException ex)
        {
            status.Value = $"Unable to set path {commpath1}";
            Log.Warning(status.Value);
            Log.Warning(ex.Message);
            r = -1;
            return;
        }
        r = 0;
    }

    [ExportMethod]
    public void ReadControllerMode(out int r,out Boolean isProgram)
    {
        try
        {
            LogixProject.ControllerMode result = project.ReadControllerModeAsync().Result;
            if (result != LogixProject.ControllerMode.Program)
            {
                isProgram = false;
            }
            else
            {
                isProgram = true;
            }
            mode.Value = ControllerModeToString(result);
            status.Value = $"Controller mode: {mode.Value}";
            Log.Info(status.Value);
        }
        catch (LogixSdkException ex)
        {
            status.Value = "Unable to read controller mode";
            Log.Warning(status.Value);
            Log.Warning(ex.Message);
            r = 1;
            isProgram = false;
            return;
        }
        r = 0;
    }

    [ExportMethod]
    public void UploadNewProgram(out int r)
    {
        string commPath = LogicObject.GetVariable("CommPath").Value;
        string filepath = LogicObject.GetVariable("FilePath").Value;
        char separator = Path.AltDirectorySeparatorChar;
        string separators = $"{separator}{separator}{separator}";
        string acdPath = filepath.Split(separators)[1];
        try
        {
            LogixProject.UploadToNewProjectAsync(acdPath, commPath).Wait();
            status.Value = $"Create a new ACD file: {acdPath},with Communicate {commPath}";
            Log.Info(status.Value);
        }
        catch (LogixSdkException ex)
        {
            status.Value = $"Unable to upload to new project {acdPath}";
            Log.Warning(status.Value);
            Log.Warning(ex.Message);
            r = -1;
            return;
        }

        Log.Warning($"newProjectPath = {acdPath} controllerPath = {commPath} UploadToNewProject DONE.");
        r = 0;
    }
    [ExportMethod]
    public void UploadProgram(out int r)
    {
        //upload need first open the acd project,and set the comm path correct
        try
        {
            project.UploadAsync().Wait();
            status.Value = $"Uploaded project";
            Log.Info(status.Value);
        }
        catch (LogixSdkException ex)
        {
            status.Value = $"Unable to upload project";
            Log.Warning(status.Value);
            Log.Warning(ex.Message);
            r = -1;
            return;
        }
        try
        {
            project.SaveAsync().Wait();
            status.Value = $"Saved project";
            Log.Info(status.Value);
        }
        catch (LogixSdkException ex)
        {
            status.Value = $"Unable to save project";
            Log.Warning(status.Value);
            Log.Warning(ex.Message);
            r = -1;
            return;
        }
        r = 0;
    }
    [ExportMethod]
    public void DownloadProgram(out int r)
    {
        //upload need first open the acd project,and set the comm path correct
        //program mode is the only mode can be download program 
        Boolean isProgram;
        int read_r;
        ReadControllerMode(out read_r,out isProgram);
        if (isProgram == true)
        {
            try
            {
                project.DownloadAsync().Wait();
                status.Value = $"Downloaded project";
                Log.Info(status.Value);
            }
            catch (LogixSdkException ex)
            {
                status.Value = $"Unable to download project";
                Log.Warning(status.Value);
                Log.Warning(ex.Message);
                r = -1;
                return;
            }

            // Download modifies the project.
            // Without saving, if used file will be opened again, commands which need correlation
            // between program in the controller and opened project like LoadImageFromSDCard or StoreImageOnSDCard
            // may won't be able to succeed because project in the controller won't match opened project.
            try
            {
                project.SaveAsync().Wait();
                status.Value = $"Saved project";
                Log.Info(status.Value);
            }
            catch (LogixSdkException ex)
            {
                status.Value = $"Unable to saved project";
                Log.Warning(status.Value);
                Log.Warning(ex.Message);
                r = -1;
                return;
            }
            status.Value = $"Download Done";
            Log.Info("Download DONE.");
            r = read_r;
        }
        else
        {
            status.Value = $"Unable to donwload project, mode is NOT program";
            Log.Warning(status.Value);
            r = read_r;
            return;
        }
    }

    private string ControllerModeToString(LogixProject.ControllerMode result)
    {
        switch (result)
        {
            case LogixProject.ControllerMode.Faulted:
                return "Faulted";
            case LogixProject.ControllerMode.Program:
                return "Program";
            case LogixProject.ControllerMode.Run:
                return "Run";
            case LogixProject.ControllerMode.Test:
                return "Test";
            default:
                throw new ArgumentOutOfRangeException("Controller mode is unrecognized");
        }
    }

    //private LongRunningTask myLongRunningTask;
    private IUAVariable status;
    private IUAVariable mode;
    private LogixProject project;
}
