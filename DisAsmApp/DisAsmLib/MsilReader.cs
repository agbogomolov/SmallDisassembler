using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;

namespace DisAsmLib
{
	//taken from https://blogs.msdn.microsoft.com/zelmalki/2008/12/11/msil-parser/
	public class MsilReader
	{
		static readonly Dictionary<short, OpCode> _instructionLookup;
		static readonly object _syncObject = new object();
		readonly BinaryReader _methodReader;
		MsilInstruction _current;
		Module _module; // Need to resolve method, type tokens etc


		static MsilReader()
		{
			if (_instructionLookup == null)
			{
				lock (_syncObject)
				{
					if (_instructionLookup == null)
					{
						_instructionLookup = GetLookupTable();
					}
				}
			}
		}

		public MsilReader(MethodInfo method)
		{
			if (method == null)
			{
				throw new ArgumentException("method");
			}

			_module = method.Module;
			_methodReader = new BinaryReader(new MemoryStream(method.GetMethodBody().GetILAsByteArray()));
		}

		public MsilReader(ConstructorInfo contructor)
		{
			if (contructor == null)
			{
				throw new ArgumentException("contructor");
			}

			_module = contructor.Module;
			_methodReader = new BinaryReader(new MemoryStream(contructor.GetMethodBody().GetILAsByteArray()));
		}

		public MsilInstruction Current
		{
			get { return _current; }
		}


		public bool Read()
		{
			if (_methodReader.BaseStream.Length == _methodReader.BaseStream.Position)
			{
				return false;
			}

			int instructionValue;

			if (_methodReader.BaseStream.Length - 1 == _methodReader.BaseStream.Position)
			{
				instructionValue = _methodReader.ReadByte();
			}
			else
			{
				instructionValue = _methodReader.ReadUInt16();

				if ((instructionValue & OpCodes.Prefix1.Value) != OpCodes.Prefix1.Value)
				{
					instructionValue &= 0xff;
					_methodReader.BaseStream.Position--;
				}
				else
				{
					instructionValue = ((0xFF00 & instructionValue) >> 8) |
					                   ((0xFF & instructionValue) << 8);
				}
			}

			OpCode code;

			if (!_instructionLookup.TryGetValue((short)instructionValue, out code))
			{
				throw new InvalidProgramException();
			}

			int dataSize = GetSize(code.OperandType);

			var data = new byte[dataSize];

			_methodReader.Read(data, 0, dataSize);
			_current = new MsilInstruction(code, data);

			return true;
		}

		static int GetSize(OperandType opType)
		{
			int size = 0;

			switch (opType)
			{
				case OperandType.InlineNone:
					return 0;
				case OperandType.ShortInlineBrTarget:
				case OperandType.ShortInlineI:
				case OperandType.ShortInlineVar:
					return 1;

				case OperandType.InlineVar:
					return 2;

				case OperandType.InlineBrTarget:
				case OperandType.InlineField:
				case OperandType.InlineI:
				case OperandType.InlineMethod:
				case OperandType.InlineSig:
				case OperandType.InlineString:
				case OperandType.InlineSwitch:
				case OperandType.InlineTok:
				case OperandType.InlineType:
				case OperandType.ShortInlineR:
					return 4;
				case OperandType.InlineI8:

				case OperandType.InlineR:


					return 8;

				default:

					return 0;
			}
		}


		static Dictionary<short, OpCode> GetLookupTable()
		{
			var lookupTable = new Dictionary<short, OpCode>();

			FieldInfo[] fields = typeof(OpCodes).GetFields(BindingFlags.Static | BindingFlags.Public);

			foreach (FieldInfo field in fields)
			{
				var code = (OpCode)field.GetValue(null);

				lookupTable.Add(code.Value, code);
			}

			return lookupTable;
		}
	}
}