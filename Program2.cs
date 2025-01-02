// using System.Diagnostics;
// using Marauder.Mini;

// var moduleName = "D2R.exe";
// var processName = moduleName.Replace(".exe", "");
// var processes = Process.GetProcessesByName(processName);

// if (processes.Length == 0) throw new Exception($"Process with name '{processName}' not found");
// if (processes.Length  > 1) throw new Exception($"Multiple proccess found. Expected 1, found {processes.Length}");

// var process = processes.First();
// var processHandle = Win32.OpenProcess(Win32.PROCESS_QUERY_INFORMATION | Win32.PROCESS_VM_READ, false, process.Id);

// Logger.Stat("PID", process.Id);
// Logger.Stat("Process handle", processHandle);

// ProcessModule? module = null;

// foreach (ProcessModule processModule in process.Modules)
// {
//     if (processModule.ModuleName == moduleName)
//     {
//         module = processModule;
//         break;
//     }
// }
// if (module == null) throw new Exception($"Process module not found");

// Logger.Stat("Module name", module.ModuleName);
// Logger.Stat("Module base address", $"0x{module.BaseAddress.ToString("X")}");
// Logger.Stat("Module base size", module.ModuleMemorySize);

// var screens = Screen.AllScreens;

// // Find and report offsets

// var gameReader = new GameReader(processHandle, module.BaseAddress, module.ModuleMemorySize);

// gameReader.Memory.CalculateOffsets();

// Logger.Stat("Offsets-> GameData", $"0x{gameReader.Memory.Offsets.GameData?.ToString("X")}");
// Logger.Stat("Offsets-> UnitTable", $"0x{gameReader.Memory.Offsets.UnitTable?.ToString("X")}");
// Logger.Stat("Offsets-> UI", $"0x{gameReader.Memory.Offsets.UI?.ToString("X")}");
// Logger.Stat("Offsets-> Hover", $"0x{gameReader.Memory.Offsets.Hover?.ToString("X")}");
// Logger.Stat("Offsets-> Expansion", $"0x{gameReader.Memory.Offsets.Expansion?.ToString("X")}");

// // Read player units (only works if in game)

// var playerUnits = gameReader.ReadPlayerUnits();

// Win32.CloseHandle(processHandle);