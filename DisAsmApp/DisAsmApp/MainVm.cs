using DisAsmLib;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.CodeDom.Compiler;
using System.Text;
using System.Windows.Input;

namespace DisAsmApp
{
	public class MainVm : ViewModelBase
	{
		private Compiler _compiler;
		private Disassembler _disassembler;
		private string _sourceCode = @"using System;
class Test {
	public void Do() {" +
	"	Console.WriteLine(\"Enter something:\");\n" +
	@"Console.ReadLine();
	}

	public int Generate()
	{
		var rnd = new Random();
		var a = rnd.Next(0, 100);
		var b = rnd.Next(0, 100);
		return Sum(a, b);
	}

	public int Sum(int a, int b)
	{
		return a + b;
	}
}";
		private string _msilCode;
		private string _asmCode;
		private string _sourceCodeErrors;
		private bool _isDebug;

		public MainVm()
		{
			_compiler = new Compiler();
			_disassembler = new Disassembler();
			_disassembler.Initialize();
			RunCommand = new RelayCommand(Compile);
		}

		public bool IsDebug
		{
			get { return _isDebug; }
			set
			{
				_isDebug = value;
				RaisePropertyChanged();
			}
		}

		public string SourceCode
		{
			get { return _sourceCode; }
			set
			{
				_sourceCode = value;
				RaisePropertyChanged();
			}
		}

		public string SourceCodeErrors
		{
			get { return _sourceCodeErrors; }
			set
			{
				_sourceCodeErrors = value;
				RaisePropertyChanged();
			}
		}

		public string MsilCode
		{
			get { return _msilCode; }
			private set
			{
				_msilCode = value;
				RaisePropertyChanged();
			}
		}

		public string AsmCode
		{
			get { return _asmCode; }
			private set
			{
				_asmCode = value;
				RaisePropertyChanged();
			}
		}

		private void Compile()
		{
			var result = _compiler.Compile(SourceCode, IsDebug);

			if (result.Errors.HasErrors)
			{
				StringBuilder sb = new StringBuilder();

				foreach (CompilerError error in result.Errors)
				{
					sb.AppendLine($"Error ({error.ErrorNumber}): {error.ErrorText}");
				}

				SourceCodeErrors = sb.ToString();
			}
			else
			{
				SourceCodeErrors = string.Empty;
				var msilSb = new StringBuilder();
				var asmSb = new StringBuilder();

				var assembly = result.CompiledAssembly;
				Type type = assembly.GetTypes()[0];

				var disassembled = _disassembler.Disasm(type);
				foreach(var method in disassembled)
				{
					msilSb.AppendLine($";;; {method.MethodInfo.Name} {method.MethodInfo.MethodHandle.Value.ToString("X2")}");
					msilSb.Append(method.MsilCode);
					msilSb.AppendLine();
					asmSb.AppendLine($";;; {method.MethodInfo.Name} {method.AsmPtr.ToString("X2")}");
					asmSb.Append(method.AsmCode);
					asmSb.AppendLine();
				}

				MsilCode = msilSb.ToString();
				AsmCode = asmSb.ToString();
			}
		}

		public ICommand RunCommand { get; private set; }
	}
}
	
