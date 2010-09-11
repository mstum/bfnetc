using System;
using System.IO;

namespace bfnetc
{
    class Program
    {
        static void Main(string[] args)
        {
            string sourceFileName = args[0];
            string outputName = args[1];

            var sourceCode = File.ReadAllText(sourceFileName);
            BFCompiler.Compile(outputName, outputName + ".exe", sourceCode);
            Console.WriteLine("{0} compiled to {1}.exe", sourceFileName, outputName);
        }
    }
}
