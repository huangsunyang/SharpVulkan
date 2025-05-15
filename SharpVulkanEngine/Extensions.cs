using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Silk.NET.Core;
using Silk.NET.Vulkan;

namespace SharpVulkanEngine
{
	public unsafe static class Extensions
	{
		public static void ThrowOnError(this Result result) => result.ThrowOnError("");
		
		public static void ThrowOnError(this Result result, string msg)
		{
			if (result != Result.Success)
				throw new Exception($"{msg} error: {result}");
		}

		public static void DebugPrint<T>(this IEnumerable<T> values, string? title = null)
		{
			if (title != null)
				Console.WriteLine(title);

			foreach (var value in values)
			{
				if (title != null)
					Console.Write("  ");
				Console.WriteLine(value);
			}

			Console.WriteLine();
		}
	}
}
