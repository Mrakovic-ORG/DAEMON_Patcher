using System;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using OpCodes = dnlib.DotNet.Emit.OpCodes;

namespace DEAMON_Patcher
{
    internal static class Program
    {
        private static readonly string SupposedDaemonDirectory = $@"{Environment.ExpandEnvironmentVariables("%ProgramW6432%")}\DAEMON Tools Lite";

        public static void Main()
        {
            Console.Title = "DAEMON Patcher";
            SetupInstruction();

            Console.ReadKey();
            Environment.Exit(0);
        }

        private static void SetupInstruction(string message = null)
        {
            Console.Clear();

            // In case there is an message display it. (it is supposed to be an issue message)
            if (message != null) Console.WriteLine($"{message}\nTry again.\n");

            // If it cannot find the supposed default DAEMON directory manually asks for the path
            if (!Directory.Exists(SupposedDaemonDirectory)) ManualPatchApp();

            // Else asks if it should patch automatically
            Console.Write("Successfully found a DAEMON directory, are you willing to proceed to an automatic patch?\n0: No\n1: Yes\n# ");
            var choice = Console.ReadKey().Key;

            switch (choice)
            {
                case ConsoleKey.D0:
                case ConsoleKey.NumPad0:
                    Console.Clear();
                    ManualPatchApp();
                    break;
                case ConsoleKey.D1:
                case ConsoleKey.NumPad1:
                    Console.Clear();
                    AutomaticPatchApp();
                    break;
                default:
                    Console.Clear();
                    SetupInstruction();
                    break;
            }
        }

        private static void SaveModule(ModuleDefMD module, string fileName)
        {
            try
            {
                module.NativeWrite(fileName, new NativeModuleWriterOptions(module, false)
                {
                    Logger = DummyLogger.NoThrowInstance, MetadataOptions = {Flags = MetadataFlags.PreserveAll}
                });

                Console.WriteLine("Successfully patched.");
            }
            catch (Exception err)
            {
                Console.WriteLine($"\nFailed to save file.\n{err.Message}");
            }
        }

        private static void PatchApp(string inputModulePath, string outputModulePath)
        {
            // Load module from backup file
            // TODO: Load/Write file without making any backup (without being memory dependent)
            var module = ModuleDefMD.Load(inputModulePath);

            // Loop true all the methods
            foreach (var type in module.GetTypes())
            {
                // Loop true methods
                foreach (var method in type.Methods)
                {
                    // Unlock features
                    // There is the same method name on few other classes but only those who uses a param name of "state" are used to activate or not a feature by the feature guid
                    if ((method.Name == "IsFeatureActivated" || method.Name == "FeatureCanBeTrial") &&
                        method.IsStatic &&
                        method.ParamDefs.Count > 0 &&
                        method.ParamDefs.All(parameter => parameter.Name.String == "state"))
                    {
                        Console.WriteLine($"Patching: {method.FullName}");
                        var methodInstr = method.Body.Instructions;

                        /*
                         * Clear all instructions and insert return true
                         * ldc.i4.1
                         * ret
                         */
                        methodInstr.Clear();
                        methodInstr.Add(OpCodes.Ldc_I4_1.ToInstruction());
                        methodInstr.Add(OpCodes.Ret.ToInstruction());
                    }

                    // Change default license type to paid
                    if (method.Name == "get_CurLicenseType")
                    {
                        Console.WriteLine($"Patching: {method.FullName}");
                        var methodInstr = method.Body.Instructions;

                        /*
                         * Clear all instructions and insert return (UInt32)3
                         * ldc.i4.3
                         * ret
                         */
                        methodInstr.Clear();
                        methodInstr.Add(OpCodes.Ldc_I4_3.ToInstruction());
                        methodInstr.Add(OpCodes.Ret.ToInstruction());
                    }

                    // Change default license text
                    if (method.Name == "get_LicenseText")
                    {
                        Console.WriteLine($"Patching: {method.FullName}");
                        var methodInstr = method.Body.Instructions;

                        /*
                         * Clear all instructions and insert return (string)"Mrakovic-ORG"
                         * ldstr "Mrakovic-ORG"
                         * ret
                         */
                        methodInstr.Clear();
                        methodInstr.Add(OpCodes.Ldstr.ToInstruction("Mrakovic-ORG"));
                        methodInstr.Add(OpCodes.Ret.ToInstruction());
                    }

                    // Hide hardware id
                    if (method.Name == "get_HardWareID" || method.Name == "get_HardWareIDText")
                    {
                        Console.WriteLine($"Removing: {method.FullName}");
                        var methodInstr = method.Body.Instructions;

                        /*
                         * Clear all instructions and insert return (string)""
                         * ldstr ""
                         * ret
                         */
                        methodInstr.Clear();
                        methodInstr.Add(OpCodes.Ldstr.ToInstruction(""));
                        methodInstr.Add(OpCodes.Ret.ToInstruction());
                    }

                    // Remove Google Analytics spyware
                    if (method.Name == "SendMessage" && method.ParamDefs.Count >= 2)
                    {
                        Console.WriteLine($"Removing: {method.FullName}");
                        var methodInstr = method.Body.Instructions;

                        /*
                         * Clear all instructions
                         * ret
                         */
                        methodInstr.Clear();
                        methodInstr.Add(OpCodes.Ret.ToInstruction());
                    }
                }
            }

            // Finally save the module
            SaveModule(module, outputModulePath);
        }

        private static void ManualPatchApp()
        {
            // PatchApp welcome message
            Console.Write("Application Path: ");

            // Parse path by console line
            var getLine = Console.ReadLine();
            var directoryName = Path.GetFullPath(getLine?.Replace("\"", ""));

            // In case the path is not a directory replace directoryName to the file directory name
            if (!Directory.Exists(directoryName)) directoryName = Path.GetDirectoryName(directoryName);

            var file2Patch = $"{directoryName}\\DotNetCommon.dll";
            var file2PatchBak = $"{file2Patch}.bak";

            // Throw back at setup if the file we looking for is not found
            if (!File.Exists(file2Patch))
            {
                SetupInstruction(@"Unable to locate DotNetCommon.dll within that directory.");
            }

            // Make a backup in case there is none
            try
            {
                if (!File.Exists(file2PatchBak)) File.Move(file2Patch, file2PatchBak);
            }
            catch
            {
                SetupInstruction("Could not make a backup, try to run the application with an higher privilege.");
            }

            // Patch the app
            PatchApp(file2PatchBak, file2Patch);
        }

        private static void AutomaticPatchApp()
        {
            var file2Patch = $"{SupposedDaemonDirectory}\\DotNetCommon.dll";
            var file2PatchBak = $"{file2Patch}.bak";

            // Throw back at setup if the file we looking for is not found
            if (!File.Exists(file2Patch))
            {
                SetupInstruction(@"Unable to locate DotNetCommon.dll within that directory.");
            }

            // Make a backup in case there is none
            try
            {
                if (!File.Exists(file2PatchBak)) File.Move(file2Patch, file2PatchBak);
            }
            catch
            {
                SetupInstruction("Could not make a backup, try to run the application with an higher privilege.");
            }

            // Patch the app
            PatchApp(file2PatchBak, file2Patch);
        }
    }
}