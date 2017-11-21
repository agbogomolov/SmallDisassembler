using System.Reflection.Emit;
using System.Text;

namespace DisAsmLib
{
	public struct MsilInstruction
	{
		public readonly byte[] Data;
		public readonly OpCode Instruction;

		public MsilInstruction(OpCode code, byte[] data)
		{
			Instruction = code;

			Data = data;
		}


		public override string ToString()
		{
			var builder = new StringBuilder();

			builder.Append(Instruction.Name + " ");

			if (Data != null && Data.Length > 0)
			{
				builder.Append("0x");

				foreach (byte b in Data)
				{
					builder.Append(b.ToString("x2"));
				}
			}

			return builder.ToString();
		}
	}
}