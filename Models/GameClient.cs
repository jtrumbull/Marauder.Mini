using System.Diagnostics;

namespace Marauder.Mini.Models;

/// <summary>
/// Game client extension methods
/// </summary>
public static class GameClientExtensions
{
    /// <summary>
    /// Find a module by ModuleName
    /// </summary>
    /// <param name="process"></param>
    /// <param name="name"></param>
    /// <returns>First module with matching name</returns>
    /// <exception cref="ProcessModuleNotFoundException"></exception>
    public static ProcessModule GetModuleByName(this Process process, string name)
    {
        foreach(ProcessModule module in process.Modules)
        {
            if(module.ModuleName == name) return module;
        }
        throw new ProcessModuleNotFoundException(name);
    }
}

/// <summary>
/// Game client model
/// </summary>
public class GameClient : IDisposable
{
    public event EventHandler Exited = default!;

    protected const string ProcessName = "D2R";
    protected const string ModuleName = "D2R.exe";

    private readonly Process _process;
    private readonly ProcessModule _module;
    //private readonly GameReader _reader;
    private bool _running;
    //private bool _threadRunning;
    private nint _processHandle;

    /// <summary>
    /// Game client constructor
    /// </summary>
    public GameClient()
    {
        _process = GetProcessByName(ProcessName);
        _module = _process.GetModuleByName(ModuleName);
        
        _process.Exited += (sender, e) => OnExited(e);
    }

    protected virtual void OnExited(EventArgs e)
    {
        Exited?.Invoke(this, e);
    }
    
    /// <summary>
    /// Creates a new process handle
    /// </summary>
    /// <returns></returns>
    public nint OpenProcess()
    {
        var access = Win32.PROCESS_QUERY_INFORMATION | Win32.PROCESS_VM_READ;
        _processHandle = Win32.OpenProcess(access, false, _process.Id);
        _running = true;
        return _processHandle;
    }

    /// <summary>
    /// Closes the process handle
    /// </summary>
    public void CloseProcess()
    {
        if (!_running) return;
        Win32.CloseHandle(_processHandle);
        _running = false;
    }

    public bool HasExited => _process.HasExited;
    public nint GetProcessId() => _process.Id;
    public nint GetProcessHandle() => _processHandle;
    public nint GetBaseAddress() => _module.BaseAddress;
    public string GetModuleName() => _module.ModuleName;
    public nint GetModuleMemorySize() => _module.ModuleMemorySize;

    /// <summary>
    /// Dispose of the game client
    /// </summary>
    public void Dispose()
    {
        CloseProcess();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Find a process by name
    /// </summary>
    /// <param name="name"></param>
    /// <returns>First process with matching name</returns>
    /// <exception cref="ProcessNotFoundException"></exception>
    public static Process GetProcessByName(string name)
    {
        var processes = Process.GetProcessesByName(name);

        if (processes.Length == 0) 
            throw new ProcessNotFoundException(name);

        return processes.First();
    }

    /// <summary>
    /// Is on primary screen
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public bool IsOnPrimaryScreen()
    {
        if (Win32.GetWindowRect(_process.MainWindowHandle, out Structs.Rectangle rect))
        {
            int centerX = (rect.Left + rect.Right) / 2;
            int centerY = (rect.Top + rect.Bottom) / 2;
            var windowScreen = Screen.FromPoint(new Point(centerX, centerY));
            return windowScreen.Primary;
        }
        else
        {
            throw new Exception("Could not identify the window rectangle");
        }
    }

    /// <summary>
    /// Move process window to the center of the primary screen
    /// </summary>
    /// <returns></returns>
    /// <exception cref="Exception"></exception>
    public async Task MoveToPrimaryScreen()
    {
        if (Win32.GetWindowRect(_process.MainWindowHandle, out Structs.Rectangle rect))
        {
            var area = Screen.PrimaryScreen!.WorkingArea;
            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;
            int newX = area.Left + (area.Width - rect.Width) / 2;
            int newY = area.Top + (area.Height - rect.Height) / 2;
            
            // bool success = Win32.MoveWindow(
            //     _process.MainWindowHandle,
            //     newX,
            //     newY,
            //     width,
            //     height,
            //     true);
            
            await Task.Run(() =>
            {
                Win32.ShowWindowAsync(_process.MainWindowHandle, Win32.SW_RESTORE);
            });

            bool success = Win32.SetWindowPos(
                _process.MainWindowHandle,
                Win32.HWND_TOP,
                newX,
                newY,
                width,
                height,
                Win32.SWP_NOZORDER | Win32.SWP_NOACTIVATE);
            
            if (!success) throw new Exception("Failed to move window");
        }
        else
        {
            throw new Exception("Could not identify the window rectangle");
        }
    }

    public async Task UpdateAsync()
    {
        await Task.Delay(0);
        throw new NotImplementedException();
    }
}

public class ProcessNotFoundException(string name) 
    : Exception($"Could not find process with name {name}");

public class ProcessModuleNotFoundException(string name) 
    : Exception($"Could not find process module with name {name}");