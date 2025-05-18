using Silk.NET.Vulkan;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SharpVulkanEngine
{
    internal class ShaderUtils
    {
        public static string CompileShader(string path)
        {
            // test compiler
            var sdkRoot = Environment.GetEnvironmentVariable("VULKAN_SDK");
            var glsl = Path.Combine(sdkRoot!, "Bin", "glslc.exe");
            var compiledPath = path + ".spv";
            ConsoleUtils.Run($"{glsl} {path} -o {compiledPath}").ThrowOnError();
            return compiledPath;
        }

        public static ShaderModule CreateShaderModule(string path)
        {
            if (!path.EndsWith(".spv", StringComparison.OrdinalIgnoreCase))
                path = CompileShader(path);
            return VulkanDevice.Current.CreateShaderModule(path);
        }
    }
}
