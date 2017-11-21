using SharpDisasm;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace DisAsmLib
{
	public unsafe class Disassembler
    {
	    [DllImport("clrjit.dll")]
	    private static extern void* getJit();

	    [DllImport("kernel32.dll", SetLastError = true)]
	    static extern bool VirtualProtect(
		    void* lpAddress,
		    void* dwSize,
		    uint flNewProtect,
		    uint* lpflOldProtect
	    );

	    [StructLayout(LayoutKind.Sequential)]
	    private struct CORINFO_METHOD_INFO
	    {
		    public void* ftn;
		    public void* scope;
		    public byte* ILCode;
		    public uint ILCodeSize;
		    public ushort maxStack;
		    public ushort EHcount;
		    public uint options;
	    };

	    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
	    private delegate int CompileMethod(
		    void* pThis,
		    void* compHnd,
		    CORINFO_METHOD_INFO* info,
		    uint flags,
		    byte** entryAddress,
		    uint* nativeSizeOfCode
	    );

	    private struct HookInfo
	    {
		    public CompileMethod OriginalCompileMethod;
		    public CompileMethod MyCompileMethod;
		    public void* pJit;
	    }

	    public void Initialize()
	    {
			_methods.TryGetValue(IntPtr.Zero, out MethodCode dummy);

			mHookInfo.pJit = *(void**)getJit();
			mHookInfo.MyCompileMethod = DetouredCompileMethod;
			RuntimeHelpers.PrepareDelegate(mHookInfo.MyCompileMethod);
			uint p = Protect(mHookInfo.pJit, 0x04, (void*)0x4);
			mHookInfo.OriginalCompileMethod = Marshal.GetDelegateForFunctionPointer(new IntPtr(*(void**)mHookInfo.pJit), typeof(CompileMethod)) as CompileMethod;
			RuntimeHelpers.PrepareDelegate(mHookInfo.OriginalCompileMethod);
			*(void**)mHookInfo.pJit = (void*)Marshal.GetFunctionPointerForDelegate(mHookInfo.MyCompileMethod);
			Protect(mHookInfo.pJit, p, (void*)0x4);
		}

	    public IEnumerable<MethodDisassmbled> Disasm(Type type)
	    {
			var methods = type.GetMethods(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

			foreach (var methodInfo in methods)
		    {
				var msilReader = new MsilReader(methodInfo);
				var methodHandleValue = methodInfo.MethodHandle.Value;

				_methods.AddOrUpdate(methodHandleValue, _ => new MethodCode()
				{
					MsilCode = methodInfo.GetMethodBody().GetILAsByteArray()
				}, (ptr, bytes) => bytes);

				RuntimeHelpers.PrepareMethod(methodInfo.MethodHandle);
		    }

			foreach (var methodInfo in methods)
			{
				var methodHandleValue = methodInfo.MethodHandle.Value;
				if(_methods.TryGetValue(methodHandleValue, out MethodCode methodCode))
				{
					yield return new MethodDisassmbled()
					{
						MethodInfo = methodInfo,
						MsilCode = ReadMsilCode(methodInfo),
						AsmCode = ReadAsmCode(methodCode.AsmCode, methodCode.AsmCodeSize),
						AsmPtr = methodCode.AsmCode
					};
				}
			}
	    }

		private string ReadAsmCode(IntPtr intPtr, uint size)
		{
			var disasm = new SharpDisasm.Disassembler(
				intPtr,
				(int)size,
				ArchitectureMode.x86_64,
				0,
				true);
			var sb = new StringBuilder();
			foreach (var insn in disasm.Disassemble())
				sb.AppendLine(insn.ToString());

			return sb.ToString();
		}

		private string ReadMsilCode(MethodInfo methodInfo)
		{
			var msilReader = new MsilReader(methodInfo);
			var sb = new StringBuilder();
			while (msilReader.Read())
			{
				sb.AppendLine(msilReader.Current.ToString());
			}

			return sb.ToString();
		}

		private static HookInfo mHookInfo;
		private static ConcurrentDictionary<IntPtr, MethodCode> _methods = new ConcurrentDictionary<IntPtr, MethodCode>();

		static int DetouredCompileMethod(
			void* pThis,
			void* compHnd,
			CORINFO_METHOD_INFO* info,
			uint flags,
			byte** nativeAddress,
			uint* nativeSizeOfCode)
		{
			// Возвращаемое значение
			int nRet;
			byte* code = info->ILCode;
			uint size = info->ILCodeSize;
			IntPtr methodPtr = new IntPtr(info->ftn);

			nRet = mHookInfo.OriginalCompileMethod(pThis, compHnd, info, flags, nativeAddress, nativeSizeOfCode);

			if (_methods.TryGetValue(methodPtr, out MethodCode mcode))
			{
				mcode.AsmCode = new IntPtr(*nativeAddress);
				mcode.AsmCodeSize = *nativeSizeOfCode;
			}

			return nRet;
		}

		private static uint Protect(void* address, uint protection, void* size)
		{
			if (!VirtualProtect(address, size, protection, &protection))
				throw new Win32Exception();
			return protection;
		}
	}
	public class MethodCode
	{
		public byte[] MsilCode { get; set; }
		public IntPtr AsmCode { get; set; }
		public uint AsmCodeSize { get; set; }
	}

	public struct MethodDisassmbled
	{
		public MethodInfo MethodInfo { get; set; }

		public string MsilCode { get; set; }

		public string AsmCode { get; set; }

		public IntPtr AsmPtr { get; set; }
	}
}
