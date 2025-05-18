using Silk.NET.Core;
using Silk.NET.Core.Native;
using Silk.NET.Vulkan;
using Silk.NET.Vulkan.Extensions.EXT;
using Silk.NET.Vulkan.Extensions.KHR;
using Silk.NET.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using System.Text;
using System.Threading.Tasks;
using static System.Formats.Asn1.AsnWriter;

namespace SharpVulkanEngine
{
    public struct QueueFamilyIndex
    {
        public uint? graphicsFamilyIndex;
        public uint? presentFamilyIndex;

        public bool Complete() => graphicsFamilyIndex != null && presentFamilyIndex != null;
        public IEnumerable<uint> Unique() => new uint[] { graphicsFamilyIndex!.Value, presentFamilyIndex!.Value }.Distinct();
    }

    public struct SwapchainSupportDetails
    {
        public SurfaceCapabilitiesKHR capabilities;
        public SurfaceFormatKHR[] surfaceFormats;
        public PresentModeKHR[] presentModes;

        public bool IsAdequate() => surfaceFormats.Length > 0 && presentModes.Length > 0;
        public SurfaceFormatKHR ChooseSurfaceFormat()
        {
            foreach (var surfaceFormat in surfaceFormats)
            {
                if (surfaceFormat.ColorSpace == ColorSpaceKHR.SpaceSrgbNonlinearKhr && surfaceFormat.Format == Format.B8G8R8A8Srgb)
                    return surfaceFormat;
            }
            return surfaceFormats[0];
        }

        public PresentModeKHR ChoosePresentMode()
        {
            if (presentModes.Contains(PresentModeKHR.MailboxKhr))
                return PresentModeKHR.MailboxKhr;
            return PresentModeKHR.FifoKhr;
        }

        public Extent2D ChooseSwapExtent(IWindow window)
        {
            if (capabilities.CurrentExtent.Width != uint.MaxValue)
                return capabilities.CurrentExtent;

            var frameBufferSize = window.FramebufferSize;
            return new Extent2D()
            {
                Width = Math.Clamp((uint)frameBufferSize.X, capabilities.MinImageExtent.Width, capabilities.MaxImageExtent.Width),
                Height = Math.Clamp((uint)frameBufferSize.Y, capabilities.MinImageExtent.Height, capabilities.MaxImageExtent.Height),
            };
        }

        public uint ChooseImageCount()
        {
            if (capabilities.MaxImageCount == 0 || capabilities.MaxImageCount >= capabilities.MinImageCount + 1)
                return capabilities.MinImageCount + 1;
            return capabilities.MaxImageCount;
        }
    }

    public unsafe class VulkanDevice
    {
        public static Vk Api => Vk.GetApi();
        protected Instance instance;
        protected ExtDebugUtils? debugUtils;
        protected DebugUtilsMessengerEXT debugMessenger;

        KhrSurface khrSurface;
        SurfaceKHR surface;

        KhrSwapchain khrSwapchain;
        SwapchainKHR swapchain;
        Image[] swapchainImages;
        Format swapchainImageFormat;
        Extent2D swapchainImageExtent;

        ImageView[] swapchainImageViews;

        SwapchainSupportDetails swapchainSupportDetails;
        protected PhysicalDevice physicalDevice;

        protected Device logicalDevice;
        protected Queue graphicsQueue;
        protected Queue presentQueue;

        PipelineLayout pipelineLayout;

#if DEBUG
        public bool EnableValidationLayer = true;
#else
		public bool EnableValidationLayer = false;
#endif
        public readonly string[] ValidationLayers = { "VK_LAYER_KHRONOS_validation" };
        public readonly string[] DeviceExtensions = { KhrSwapchain.ExtensionName };

        public static VulkanDevice Current { get; private set; }
        public VulkanDevice() => Current = this;

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
            Api.DestroyPipelineLayout(logicalDevice, pipelineLayout, null);
            foreach (var imageView in swapchainImageViews)
            {
                Api.DestroyImageView(logicalDevice, imageView, null);
            }

            khrSwapchain.DestroySwapchain(logicalDevice, swapchain, null);
            Api.DestroyDevice(logicalDevice, null);

            if (EnableValidationLayer)
            {
                debugUtils?.DestroyDebugUtilsMessenger(instance, debugMessenger, null);
            }

            khrSurface.DestroySurface(instance, surface, null);
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

        public void CreateSurface(IWindow window)
        {
            if (!Api.TryGetInstanceExtension(instance, out khrSurface))
            {
                throw new NotSupportedException("KHR_Surface extension not found!");
            }
            surface = window.VkSurface!.Create<AllocationCallbacks>(instance.ToHandle(), null).ToSurface();
        }

        public void PickPhysicalDevice()
        {
            var physicalDeviceFound = false;
            var physicalDevices = Api.GetPhysicalDevices(instance);

            foreach (var device in physicalDevices)
            {
                if (IsSuitablePhysicalDevice(device))
                {
                    this.physicalDevice = device;
                    physicalDeviceFound = true;
                }
            }

            if (!physicalDeviceFound)
            {
                throw new Exception("no suitable physical device found!");
            }
        }

        protected SwapchainSupportDetails QuerySwapchainSupport(PhysicalDevice device)
        {
            var detail = new SwapchainSupportDetails();
            khrSurface.GetPhysicalDeviceSurfaceCapabilities(device, surface, out detail.capabilities).ThrowOnError();

            uint formatCount = 0;
            khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, ref formatCount, null).ThrowOnError();
            detail.surfaceFormats = new SurfaceFormatKHR[formatCount];
            fixed (SurfaceFormatKHR* formats = detail.surfaceFormats)
            {
                khrSurface.GetPhysicalDeviceSurfaceFormats(device, surface, ref formatCount, formats);
            }

            uint modeCount = 0;
            khrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, ref modeCount, null).ThrowOnError();
            detail.presentModes = new PresentModeKHR[modeCount];
            fixed (PresentModeKHR* modes = detail.presentModes)
            {
                khrSurface.GetPhysicalDeviceSurfacePresentModes(device, surface, ref modeCount, modes);
            }

            return detail;
        }

        protected bool IsSuitablePhysicalDevice(PhysicalDevice device)
        {
            var deviceProperties = Api.GetPhysicalDeviceProperties(device);
            var deviceFeatures = Api.GetPhysicalDeviceFeatures(device);
            Console.WriteLine(deviceProperties.ClassToString());
            Console.WriteLine(deviceFeatures.ClassToString());

            if (deviceProperties.DeviceType != PhysicalDeviceType.DiscreteGpu)
            {
                return false;
            }

            var queueFamilyIndex = FindQueueFamilyIndex(device);
            bool extensionSupport = CheckExtensionSupport(device);

            if (extensionSupport)
            {
                var details = QuerySwapchainSupport(device);
                extensionSupport &= details.IsAdequate();
            }

            return queueFamilyIndex.Complete() && extensionSupport;
        }

        QueueFamilyIndex FindQueueFamilyIndex(PhysicalDevice device)
        {
            uint queueFamilyCount = 0;
            Api.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, null);

            var queueFamilies = new QueueFamilyProperties[queueFamilyCount];
            fixed (QueueFamilyProperties* queueFamiliesPtr = queueFamilies)
            {
                Api.GetPhysicalDeviceQueueFamilyProperties(device, ref queueFamilyCount, queueFamiliesPtr);
            }

            // find queue with graphic bits and support present
            var queueFamilyIndex = new QueueFamilyIndex();
            for (uint i = 0; i < queueFamilyCount; i++)
            {
                var queueFamily = queueFamilies[i];
                Console.WriteLine(queueFamily.ClassToString());
                if (queueFamily.QueueFlags.HasFlag(QueueFlags.GraphicsBit))
                {
                    queueFamilyIndex.graphicsFamilyIndex = i;
                }

                khrSurface.GetPhysicalDeviceSurfaceSupport(device, i, surface, out var supportPresent);

                if (supportPresent)
                {
                    queueFamilyIndex.presentFamilyIndex = i;
                }
            }

            return queueFamilyIndex;
        }

        bool CheckExtensionSupport(PhysicalDevice device)
        {
            uint extensionsCount = 0;
            Api.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionsCount, null);

            var extensions = new ExtensionProperties[extensionsCount];
            fixed (ExtensionProperties* extensionsPtr = extensions)
            {
                Api.EnumerateDeviceExtensionProperties(device, (byte*)null, ref extensionsCount, extensionsPtr);
            }

            var extensionNames = extensions.Select(x => PointerUtils.ToString(x.ExtensionName));
            extensionNames.DebugPrint("Physical Device Extensions:");
            return DeviceExtensions.All(x => extensionNames.Contains(x));
        }

        public void CreateLogicalDevice()
        {
            using (var scope = new AutoRelaseScope())
            {
                float queuePriorities = 1.0f;

                var queueFamilyIndex = FindQueueFamilyIndex(physicalDevice);
                var uniqueFamilyIndex = queueFamilyIndex.Unique();
                var mem = GlobalMemory.Allocate(sizeof(DeviceQueueCreateInfo) * uniqueFamilyIndex.Count());
                var queueCreateInfos = (DeviceQueueCreateInfo*)Unsafe.AsPointer(ref mem.GetPinnableReference());
                scope.Add((nint)queueCreateInfos, AllocType.SilkMarshall);

                int i = 0;
                foreach (var queueFamily in uniqueFamilyIndex)
                {
                    queueCreateInfos[i++] = new DeviceQueueCreateInfo()
                    {
                        SType = StructureType.DeviceQueueCreateInfo,
                        QueueFamilyIndex = queueFamily,
                        QueueCount = 1,
                        PQueuePriorities = &queuePriorities
                    };
                }

                PhysicalDeviceFeatures deviceFeatures = new();
                var deviceCreateInfo = new DeviceCreateInfo()
                {
                    SType = StructureType.DeviceCreateInfo,
                    PQueueCreateInfos = queueCreateInfos,
                    QueueCreateInfoCount = (uint)uniqueFamilyIndex.Count(),
                    PEnabledFeatures = &deviceFeatures,
                    EnabledLayerCount = 0,
                    EnabledExtensionCount = (uint)DeviceExtensions.Count(),
                    PpEnabledExtensionNames = DeviceExtensions.ToBytePPtr(scope)
                };

                if (EnableValidationLayer)
                {
                    deviceCreateInfo.EnabledLayerCount = (uint)ValidationLayers.Length;
                    deviceCreateInfo.PpEnabledLayerNames = ValidationLayers.ToBytePPtr(scope);
                }

                Api.CreateDevice(physicalDevice, in deviceCreateInfo, null, out logicalDevice).ThrowOnError();

                Api.GetDeviceQueue(logicalDevice, queueFamilyIndex.graphicsFamilyIndex!.Value, 0, out graphicsQueue);
                Api.GetDeviceQueue(logicalDevice, queueFamilyIndex.presentFamilyIndex!.Value, 0, out presentQueue);
            }
        }

        public void CreateSwapchain(IWindow window)
        {
            var queueFamilyIndex = FindQueueFamilyIndex(physicalDevice);
            bool sameQueue = queueFamilyIndex.graphicsFamilyIndex == queueFamilyIndex.presentFamilyIndex;
            var indices = stackalloc[] { queueFamilyIndex.graphicsFamilyIndex!.Value, queueFamilyIndex.presentFamilyIndex!.Value };

            var swapchainDetail = QuerySwapchainSupport(physicalDevice);
            var surfaceFormat = swapchainDetail.ChooseSurfaceFormat();
            var presentMode = swapchainDetail.ChoosePresentMode();
            var extent = swapchainDetail.ChooseSwapExtent(window);
            var imageCount = swapchainDetail.ChooseImageCount();

            var createInfo = new SwapchainCreateInfoKHR()
            {
                SType = StructureType.SwapchainCreateInfoKhr,
                Surface = surface,
                MinImageCount = imageCount,
                ImageFormat = surfaceFormat.Format,
                ImageColorSpace = surfaceFormat.ColorSpace,
                ImageExtent = extent,
                ImageArrayLayers = 1,
                ImageUsage = ImageUsageFlags.ColorAttachmentBit,
                ImageSharingMode = sameQueue ? SharingMode.Exclusive : SharingMode.Concurrent,
                QueueFamilyIndexCount = sameQueue ? (uint)0 : 2,
                PQueueFamilyIndices = sameQueue ? null : indices,
                PreTransform = swapchainDetail.capabilities.CurrentTransform,
                CompositeAlpha = CompositeAlphaFlagsKHR.OpaqueBitKhr,
                PresentMode = presentMode,
                Clipped = Vk.True,
                OldSwapchain = default,
            };

            Api.TryGetDeviceExtension(instance, logicalDevice, out khrSwapchain)
               .ThrowOnError("VK_KHR_swapchain extension not supported");

            khrSwapchain.CreateSwapchain(logicalDevice, ref createInfo, null, out swapchain);

            // retrieve swapchain images and format/extent
            khrSwapchain.GetSwapchainImages(logicalDevice, swapchain, ref imageCount, null);
            swapchainImages = new Image[imageCount];
            fixed (Image* imagePtr = swapchainImages)
            {
                khrSwapchain.GetSwapchainImages(logicalDevice, swapchain, ref imageCount, imagePtr);
            }
            swapchainImageFormat = surfaceFormat.Format;
            swapchainImageExtent = extent;
        }

        public void CreateImageViews()
        {
            swapchainImageViews = new ImageView[swapchainImages.Length];
            for (int i = 0; i < swapchainImageViews.Length; i++)
            {
                var createInfo = new ImageViewCreateInfo()
                {
                    SType = StructureType.ImageViewCreateInfo,
                    Image = swapchainImages[i],
                    ViewType = ImageViewType.Type2D,
                    Format = swapchainImageFormat,
                    Components = new ComponentMapping()
                    {
                        R = ComponentSwizzle.Identity,
                        G = ComponentSwizzle.Identity,
                        B = ComponentSwizzle.Identity,
                        A = ComponentSwizzle.Identity,
                    },
                    SubresourceRange = new ImageSubresourceRange()
                    {
                        AspectMask = ImageAspectFlags.ColorBit,
                        BaseMipLevel = 0,
                        LevelCount = 1,
                        BaseArrayLayer = 0,
                        LayerCount = 1,
                    },
                };

                Api.CreateImageView(logicalDevice, ref createInfo, null, out swapchainImageViews[i]).ThrowOnError();
            }
        }

        public void CreateGraphicsPipeline()
        {
            var vertShaderModule = ShaderUtils.CreateShaderModule("Assets/vert.vert");
            var fragShaderModule = ShaderUtils.CreateShaderModule("Assets/frag.frag");

            using (var scope = new AutoRelaseScope())
            {
                var vertStageCreateInfo = new PipelineShaderStageCreateInfo()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.VertexBit,
                    Module = vertShaderModule,
                    PName = "main".ToBytePtr(scope),
                };

                var fragStageCreateInfo = new PipelineShaderStageCreateInfo()
                {
                    SType = StructureType.PipelineShaderStageCreateInfo,
                    Stage = ShaderStageFlags.FragmentBit,
                    Module = fragShaderModule,
                    PName = "main".ToBytePtr(scope),
                };

                var shaderStages = stackalloc[] { vertStageCreateInfo, fragStageCreateInfo };

                var vertextInputStateCreateInfo = new PipelineVertexInputStateCreateInfo()
                {
                    SType = StructureType.PipelineVertexInputStateCreateInfo,
                    VertexBindingDescriptionCount = 0,
                    PVertexBindingDescriptions = null,
                    VertexAttributeDescriptionCount = 0,
                    PVertexAttributeDescriptions = null,
                };

                var inputAssemblyStateCreateInfo = new PipelineInputAssemblyStateCreateInfo()
                {
                    SType = StructureType.PipelineInputAssemblyStateCreateInfo,
                    Topology = PrimitiveTopology.TriangleList,
                    PrimitiveRestartEnable = Vk.False,
                };

                var viewPort = new Viewport()
                {
                    X = 0,
                    Y = 0,
                    Width = swapchainImageExtent.Width,
                    Height = swapchainImageExtent.Height,
                    MinDepth = 0,
                    MaxDepth = 1,
                };

                var scissor = new Rect2D()
                {
                    Offset = new Offset2D(0, 0),
                    Extent = swapchainImageExtent,
                };

                var dynamicStates = stackalloc[] { DynamicState.Viewport, DynamicState.Scissor };
                var dynamicStateCreateInfo = new PipelineDynamicStateCreateInfo()
                {
                    SType = StructureType.PipelineDynamicStateCreateInfo,
                    DynamicStateCount = 2,
                    PDynamicStates = dynamicStates,
                };

                var viewPortCreateInfo = new PipelineViewportStateCreateInfo()
                {
                    SType = StructureType.PipelineViewportStateCreateInfo,
                    ViewportCount = 1,
                    ScissorCount = 1,
                };

                var rasterizationStateCreateInfo = new PipelineRasterizationStateCreateInfo()
                {
                    SType = StructureType.PipelineRasterizationStateCreateInfo,
                    DepthClampEnable = Vk.False,
                    RasterizerDiscardEnable = Vk.False,
                    PolygonMode = PolygonMode.Fill,
                    LineWidth = 1,
                    CullMode = CullModeFlags.BackBit,
                    FrontFace = FrontFace.Clockwise,
                    DepthBiasEnable = Vk.False,
                    DepthBiasClamp = 0,
                    DepthBiasConstantFactor = 0,
                    DepthBiasSlopeFactor = 0,
                };

                var multiSampleStateCreateInfo = new PipelineMultisampleStateCreateInfo()
                {
                    SType = StructureType.PipelineMultisampleStateCreateInfo,
                    SampleShadingEnable = Vk.False,
                    RasterizationSamples = SampleCountFlags.Count1Bit,
                    MinSampleShading = 1,
                    PSampleMask = null,
                    AlphaToCoverageEnable = Vk.False,
                    AlphaToOneEnable = Vk.False,
                };

                var colorBlendAttachment = new PipelineColorBlendAttachmentState()
                {
                    ColorWriteMask = ColorComponentFlags.RBit | ColorComponentFlags.GBit | ColorComponentFlags.BBit | ColorComponentFlags.ABit,
                    BlendEnable = false,
                    SrcAlphaBlendFactor = BlendFactor.One,
                    DstColorBlendFactor = BlendFactor.Zero,
                    ColorBlendOp = BlendOp.Add,
                    SrcColorBlendFactor = BlendFactor.One,
                    DstAlphaBlendFactor = BlendFactor.Zero,
                    AlphaBlendOp = BlendOp.Add,
                };

                var blendConsts = new float[4] { 0f, 0f, 0f, 0f };
                var colorBlendStateCreateInfo = new PipelineColorBlendStateCreateInfo()
                {
                    SType = StructureType.PipelineColorBlendStateCreateInfo,
                    LogicOpEnable = Vk.False,
                    LogicOp = LogicOp.Copy,
                    AttachmentCount = 1,
                    PAttachments = &colorBlendAttachment,
                };

                colorBlendStateCreateInfo.BlendConstants[0] = 0;
                colorBlendStateCreateInfo.BlendConstants[1] = 0;
                colorBlendStateCreateInfo.BlendConstants[2] = 0;
                colorBlendStateCreateInfo.BlendConstants[3] = 0;

                var layoutCreateInfo = new PipelineLayoutCreateInfo()
                {
                    SType = StructureType.PipelineLayoutCreateInfo,
                    SetLayoutCount = 0,
                    PSetLayouts = null,
                    PushConstantRangeCount = 0,
                    PPushConstantRanges = null,
                };

                Api.CreatePipelineLayout(logicalDevice, ref layoutCreateInfo, null, out pipelineLayout).ThrowOnError();
            }

            Api.DestroyShaderModule(logicalDevice, vertShaderModule, null);
            Api.DestroyShaderModule(logicalDevice, fragShaderModule, null);
        }

        public ShaderModule CreateShaderModule(string path)
        {
            var code = File.ReadAllBytes(path);
            fixed (byte* src = code)
            {
                ShaderModuleCreateInfo createInfo = new()
                {
                    SType = StructureType.ShaderModuleCreateInfo,
                    CodeSize = (uint)code.Length,
                    PCode = (uint*)src,
                };

                Api.CreateShaderModule(logicalDevice, ref createInfo, null, out var shaderModule).ThrowOnError();
                return shaderModule;
            }
        }
    }
}
