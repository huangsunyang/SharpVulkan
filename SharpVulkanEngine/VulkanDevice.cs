using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Core;
using Silk.NET.Windowing;
using Silk.NET.Core.Native;

namespace SharpVulkanEngine
{
	public unsafe class VulkanDevice
	{
		public Vk Api => Vk.GetApi();
		protected Instance instance;
		protected ExtDebugUtils? debugUtils;
		protected DebugUtilsMessengerEXT debugMessenger;

#if DEBUG
		public bool EnableValidationLayer = true;
#else
		public bool EnableValidationLayer = false;
#endif
		public readonly string[] ValidationLayers = { "VK_LAYER_KHRONOS_validation" };

		public void CreateVulkanInstance(IWindow window)
		{
			if (EnableValidationLayer && !CheckValidationLayerSupport(ValidationLayers))
			{
				throw new InvalidOperationException("Validation Layer not supported!");
			}

			var extensions = window.VkSurface!.GetRequiredExtensions(out var extCount);
			var extensionsArray = PointerUtils.ToStringArray(extensions, extCount);
			if (EnableValidationLayer)
			{
				extensionsArray = extensionsArray.Append(ExtDebugUtils.ExtensionName).ToArray();
			}

			extensionsArray.DebugPrint("Required Extensions:");
			if (!CheckExtensionSupport(extensionsArray!))
			{
				throw new InvalidOperationException("Some of Extensions not supported!");
			}

			using (var scope = new AutoRelaseScope())
			{
				var appInfo = new ApplicationInfo()
				{
					SType = StructureType.ApplicationInfo,
					PApplicationName = "Vulkan".ToBytePtr(scope),
					PEngineName = "No Engine".ToBytePtr(scope),
					EngineVersion = new Version32(1, 0, 0),
					ApiVersion = Vk.Version12
				};

				var createInfo = new InstanceCreateInfo()
				{
					SType = StructureType.InstanceCreateInfo,
					PApplicationInfo = &appInfo,
					EnabledExtensionCount = (uint)extensionsArray.Length,
					PpEnabledExtensionNames = extensionsArray!.ToBytePPtr(scope),
					EnabledLayerCount = 0,
					PNext = null,
				};

				if (EnableValidationLayer)
				{
					createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
					createInfo.PpEnabledLayerNames = ValidationLayers.ToBytePPtr(scope);
					var debugCreateInfo = DebugCreateInfo();
					createInfo.PNext = &debugCreateInfo;
				}

				Api.CreateInstance(in createInfo, null, out instance).ThrowOnError("CreateInstance failed!");
				
				// setup debug messenger
				if (EnableValidationLayer)
				{
					if (!Api.TryGetInstanceExtension(instance, out debugUtils))
						return;
					
					var debugCreateInfo = DebugCreateInfo();
					debugUtils!.CreateDebugUtilsMessenger(instance, in debugCreateInfo, null, out debugMessenger).ThrowOnError();
				}
				
				// local function
				DebugUtilsMessengerCreateInfoEXT DebugCreateInfo() => new DebugUtilsMessengerCreateInfoEXT()
				{
					SType = StructureType.DebugUtilsMessengerCreateInfoExt,
					MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
									  DebugUtilsMessageSeverityFlagsEXT.InfoBitExt |
									  DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
									  DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
					MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
								  DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
								  DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
					PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)DebugCallback
				};
			}
		}

		public void CleanUp()
		{
			if (EnableValidationLayer)
			{
				debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
			}

			Api.DestroyInstance(instance, null);
			Api.Dispose();
		}

		private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageType, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
		{
			string prefix = "";
			switch (messageSeverity)
			{
				case DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt:
					Console.ForegroundColor = ConsoleColor.DarkGray;
					prefix = "[Verbose]";
					break;
				case DebugUtilsMessageSeverityFlagsEXT.InfoBitExt:
					Console.ForegroundColor = ConsoleColor.White;
					prefix = "[Info]";
					break;
				case DebugUtilsMessageSeverityFlagsEXT.WarningBitExt:
					Console.ForegroundColor = ConsoleColor.Yellow;
					prefix = "[Warning]";
					break;
				case DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt:
					Console.ForegroundColor = ConsoleColor.Red;
					prefix = "[Error]";
					break;
			}
			
			var msg = PointerUtils.ToString(pCallbackData->PMessage);
			Console.WriteLine($"{DateTime.Now} {prefix} {msg}");
			Console.ForegroundColor = ConsoleColor.White;
			return Vk.False;
		}

		public bool CheckValidationLayerSupport(IEnumerable<string> requiredLayerNames)
		{
			uint layerCount = 0;
			Api.EnumerateInstanceLayerProperties(ref layerCount, null).ThrowOnError();

			var availableLayers = new LayerProperties[layerCount];
			fixed (LayerProperties* availableLayersPtr = availableLayers)
			{
				Api.EnumerateInstanceLayerProperties(ref layerCount, availableLayersPtr).ThrowOnError();
			}

			var availableLayerNames = availableLayers.Select(x => PointerUtils.ToString(x.LayerName));
			availableLayerNames.DebugPrint("Available layer names:");
			return requiredLayerNames.All(x => availableLayerNames.Contains(x));
		}

		public bool CheckExtensionSupport(IEnumerable<string> requiredExtensionNamess)
		{
			uint extensionCount = 0;
			Api.EnumerateInstanceExtensionProperties((string?)null, ref extensionCount, null).ThrowOnError();

			var availableExtensions = new ExtensionProperties[extensionCount];
			fixed (ExtensionProperties* availableExtensionsPtr = availableExtensions)
			{
				Api.EnumerateInstanceExtensionProperties((string?)null, ref extensionCount, availableExtensionsPtr).ThrowOnError();
			}
			var availableExtensionNames = availableExtensions.Select(x => PointerUtils.ToString(x.ExtensionName));
			availableExtensionNames.DebugPrint("Available extension names:");
			return requiredExtensionNamess.All(x => availableExtensionNames.Contains(x));
		}

		public void PickPhysicalDevice()
		{
			uint deviceCount = 0;
			Api.EnumeratePhysicalDevices(instance, ref deviceCount, null).ThrowOnError();

			if (deviceCount == 0)
			{
				throw new Exception("no gpu found with Vulkan support!");
			}
			
			var physicalDevices = new PhysicalDevice[deviceCount];
			fixed (PhysicalDevice* physicalDevicesPtr = physicalDevices)
			{
				Api.EnumeratePhysicalDevices(instance, ref deviceCount, physicalDevicesPtr);
			}

			physicalDevices.DebugPrint("physical devices");
		}
	}
}
