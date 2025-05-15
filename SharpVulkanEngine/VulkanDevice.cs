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
		public readonly string[] ValidationLayers = { "VK_LAYER_KHRONOS_validation" };

		public void CreateVulkanInstance(IWindow window, bool enableValidationLayer = true)
		{
			if (enableValidationLayer && !CheckValidationLayerSupport(ValidationLayers))
			{
				throw new InvalidOperationException("Validation Layer not supported!");
			}

			var extensions = window.VkSurface!.GetRequiredExtensions(out var extCount);
			var extensionsArray = PointerUtils.ToStringArray(extensions, extCount);
			if (enableValidationLayer)
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

				if (enableValidationLayer)
				{
					createInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
					createInfo.PpEnabledLayerNames = ValidationLayers.ToBytePPtr(scope);
					var debugCreateInfo = new DebugUtilsMessengerCreateInfoEXT()
					{
						SType = StructureType.DebugUtilsMessengerCreateInfoExt,
						MessageSeverity = DebugUtilsMessageSeverityFlagsEXT.VerboseBitExt |
										  DebugUtilsMessageSeverityFlagsEXT.WarningBitExt |
										  DebugUtilsMessageSeverityFlagsEXT.ErrorBitExt,
						MessageType = DebugUtilsMessageTypeFlagsEXT.GeneralBitExt |
									  DebugUtilsMessageTypeFlagsEXT.PerformanceBitExt |
									  DebugUtilsMessageTypeFlagsEXT.ValidationBitExt,
						PfnUserCallback = (PfnDebugUtilsMessengerCallbackEXT)DebugCallback
					};
					createInfo.PNext = &debugCreateInfo;
				}

				Api.CreateInstance(in createInfo, null, out instance).ThrowOnError("CreateInstance failed!");
			}
		}

		private uint DebugCallback(DebugUtilsMessageSeverityFlagsEXT messageSeverity, DebugUtilsMessageTypeFlagsEXT messageType, DebugUtilsMessengerCallbackDataEXT* pCallbackData, void* pUserData)
		{
			Console.Write("Validation Layer: ");
			Console.WriteLine(PointerUtils.ToString(pCallbackData->PMessage));
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
	}
}
