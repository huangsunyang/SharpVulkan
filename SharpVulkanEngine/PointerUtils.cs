using Silk.NET.Core.Native;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpVulkanEngine
{
	public enum AllocType
	{
		Marshall,
		SilkMarshall,
	};

	public unsafe struct AutoRelaseScope: IDisposable
	{
		public AutoRelaseScope() { }

		List<(nint, AllocType)> ptrList = new();

		public void Dispose()
		{
			foreach (var (ptr, type) in ptrList)
			{
				if (type == AllocType.Marshall)
					Marshal.FreeHGlobal(ptr);
				if (type == AllocType.SilkMarshall)
					SilkMarshal.Free(ptr);
			}
		}

		public nint Add(nint ptr, AllocType type)
		{
			ptrList.Add((ptr, type));
			return ptr;
		}
	}

	public unsafe static class PointerUtils
	{
		public static string? ToString(byte * buffer) => Marshal.PtrToStringAnsi((IntPtr)buffer);
		public static string?[] ToStringArray(byte*[] buffers) => Enumerable.Range(0, buffers.Length).Select(i => ToString(buffers[i])).ToArray();
		public static string?[] ToStringArray(byte** buffers, int length) => Enumerable.Range(0, length).Select(i => ToString(buffers[i])).ToArray();
		public static string?[] ToStringArray(byte** buffers, uint length) => ToStringArray(buffers, (int)length);

		public static byte* ToBytePtr(this string str) => (byte*)Marshal.StringToHGlobalAnsi(str);
		public static byte** ToBytePPtr(this IEnumerable<string> strArray) => (byte**)SilkMarshal.StringArrayToPtr(strArray.ToArray());
		
		// use with auto release scope
		public static byte* ToBytePtr(this string str, AutoRelaseScope scope) => (byte*)scope.Add((nint)str.ToBytePtr(), AllocType.Marshall);
		public static byte** ToBytePPtr(this IEnumerable<string> strArray, AutoRelaseScope scope) => (byte**)scope.Add((nint)strArray.ToBytePPtr(), AllocType.SilkMarshall);
	}
}
