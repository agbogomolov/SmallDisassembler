using System.CodeDom.Compiler;
using Microsoft.CSharp;

namespace DisAsmLib
{
	public class Compiler
	{
		private CSharpCodeProvider _provider;

		public Compiler()
		{
			_provider = new CSharpCodeProvider();			
		}

		public CompilerResults Compile(string codeSource)
		{
			return Compile(codeSource, false);
		}

		public CompilerResults Compile(string codeSource, bool isDebug)
		{
			var _parameters = new CompilerParameters();
			_parameters.GenerateInMemory = true;
			_parameters.GenerateExecutable = false;
			_parameters.IncludeDebugInformation = isDebug;
			var result = _provider.CompileAssemblyFromSource(_parameters, codeSource);
			return result;
		}
	}
}
