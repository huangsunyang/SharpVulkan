// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Reflection;

namespace ImGuiVulkan
{
	class Program
	{
		static void Main(string[] args)
		{
			new ImGuiVulkanApplication().Run();
        }

        internal static byte[] LoadEmbeddedResourceBytes(string path)
		{
            var name = typeof(Program).Assembly.GetName().Name;
            using (var s = typeof(Program).Assembly.GetManifestResourceStream(name + "." + path))
            {
                using (var ms = new MemoryStream())
                {
                    s.CopyTo(ms);
                    return ms.ToArray();
                }
            }
        }
    }
}
