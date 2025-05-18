using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpVulkanEngine
{
    class ConsoleUtils
    {
        public static bool Run(string command, string cwd = "", string? taskName = null)
        {
                var (success, output, error) = CheckOutput(command, cwd);

                if (!string.IsNullOrWhiteSpace(output))
                    Console.WriteLine(output);

                if (!string.IsNullOrWhiteSpace(error))
                    Console.WriteLine(error);

                return success;
        }

        public static (bool, string, string) CheckOutput(string command, string cwd = "")
        {
            var process = CreateProcess(command, cwd);
            if (process == null)
                return (false, "", "");

            return CheckProcess(process);
        }

        protected static Process? CreateProcess(string command, string cwd = "")
        {
            var parts = command.Split(' ', 2);
            var processInfo = new ProcessStartInfo(parts[0])
            {
                Arguments = parts[1],
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = cwd
            };

            try
            {
                return Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return null;
            }
        }

        protected static (bool, string, string) CheckProcess(Process process)
        {
            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            return (process.ExitCode == 0, output, error);
        }
    }
}
