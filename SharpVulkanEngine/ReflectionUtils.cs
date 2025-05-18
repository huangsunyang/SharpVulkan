using Silk.NET.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace SharpVulkanEngine
{
    public unsafe static class ReflectionUtils
    {
        public static string ClassToString<T>(this T t)
        {
            if (t == null)
                return "null";

            var type = t.GetType();
            var sb = new StringBuilder(type.Name);
            sb.AppendLine();
            foreach (var fieldInfo in type.GetFields())
            {
                sb.Append("  ");
                sb.Append(fieldInfo.Name);
                sb.Append(": ");
                if (fieldInfo.GetCustomAttribute<FixedBufferAttribute>() != null)
                    sb.Append(PrintFixedBuffer(ref t, fieldInfo));
                else if (fieldInfo.FieldType == typeof(Bool32))
                    sb.Append(fieldInfo.GetValue(t)?.Equals(true));
                else
                    sb.Append(fieldInfo.GetValue(t));
                sb.AppendLine();
            }
            return sb.ToString();
        }

        static string PrintFixedBuffer<T>(ref T obj, FieldInfo field)
        {
            // Calculate field offset
            IntPtr offset = Marshal.OffsetOf<T>(field.Name);
            byte* structPtr = (byte*)Unsafe.AsPointer(ref obj);
            byte* bufferPtr = structPtr + offset.ToInt64();
            return PointerUtils.ToString(bufferPtr)!;
        }
    }
}
