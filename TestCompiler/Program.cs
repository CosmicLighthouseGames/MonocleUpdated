// See https://aka.ms/new-console-template for more information
using MonocleCompiler;

#if DEBUG
args = new string[] {
	"MonocleTest",
	"bin\\Debug\\net6.0",
};
#endif


CompilerBase compiler = new CompilerBase(args);
compiler.Compile(args[0]);
