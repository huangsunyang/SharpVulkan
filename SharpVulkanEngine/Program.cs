using SharpVulkanEngine;
using Silk.NET.Maths;
using Silk.NET.Windowing;

unsafe class MainWindow
{
	const int WIDTH = 800;
	const int HEIGHT = 600;

	private IWindow? window;
	private VulkanDevice device = new();

	public static void Main()
	{
		var app = new MainWindow();
		app.Run();
	}

	public void Run()
	{
		InitWindow();
		InitVulkan();
		MainLoop();
		CleanUp();
	}

	private void InitWindow()
	{
		//Create a window.
		var options = WindowOptions.DefaultVulkan with
		{
			Size = new Vector2D<int>(WIDTH, HEIGHT),
			Title = "Vulkan"
		};

		window = Window.Create(options);
		window.Initialize();

		if (window.VkSurface is null)
		{
			throw new Exception("Windowing platform doesn't support Vulkan.");
		}
	}

	private void InitVulkan()
	{
		device.CreateVulkanInstance(window!);
		device.CreateSurface(window!);
		device.PickPhysicalDevice();
		device.CreateLogicalDevice();
		device.CreateSwapchain(window!);
		device.CreateImageViews();
		device.CreateGraphicsPipeline();
	}

	private void MainLoop()
	{
		window!.Run();
	}

	private void CleanUp()
	{
		window?.Dispose();
		device.CleanUp();
	}
}