using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Vulkan;
using Silk.NET.Core;
using Silk.NET.Windowing;

namespace SharpVulkanEngine
{
	public unsafe class VulkanDevice
	{
		public Vk Api => Vk.GetApi();
		protected Instance instance;
		public VulkanDevice(IWindow window)
		{
			var appInfo = new ApplicationInfo()
			{
				SType = StructureType.ApplicationInfo,
				PApplicationName = (byte*)Marshal.StringToHGlobalAnsi("Vulkan"),
				PEngineName = (byte*)Marshal.StringToHGlobalAnsi("No Engine"),
				EngineVersion = new Version32(1, 0, 0),
				ApiVersion = Vk.Version13
			};

			var createInfo = new InstanceCreateInfo()
			{
				SType = StructureType.InstanceCreateInfo,
				PApplicationInfo = &appInfo
			};
			
			var extensions = window.VkSurface!.GetRequiredExtensions(out var extCount);
			createInfo.EnabledExtensionCount = extCount;
			createInfo.PpEnabledExtensionNames = extensions;
		}
	}
}
