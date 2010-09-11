using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using System.Reflection;

namespace bfnetc
{
    public static class BFCompiler
    {
        private readonly static Stack<System.Reflection.Emit.Label> _bracketStack = new Stack<System.Reflection.Emit.Label>();

        public static void Compile(string assemblyName, string outputFileName, string sourceCode)
        {
            _bracketStack.Clear();
            var asm = AppDomain.CurrentDomain.DefineDynamicAssembly(new AssemblyName(assemblyName),
                                                        AssemblyBuilderAccess.Save);

            var mod = asm.DefineDynamicModule(assemblyName, outputFileName);

            var mainClassTypeName = assemblyName + ".Program"; // e.g., hello.Program
            var type = mod.DefineType(mainClassTypeName, TypeAttributes.Class | TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Public);
            

            var pointerField = type.DefineField("pointer", typeof(Int16), FieldAttributes.Static | FieldAttributes.Private);
            var memoryField = type.DefineField("memory", typeof(Byte[]), FieldAttributes.Static | FieldAttributes.Private);

            var constructor = type.DefineConstructor(MethodAttributes.Static, CallingConventions.Standard, null);
            var cctorIlGen = constructor.GetILGenerator();
            GenerateConstructorBody(cctorIlGen, pointerField, memoryField);


            var mainMethod = type.DefineMethod("Execute", MethodAttributes.Public | MethodAttributes.Static);
            var ilGen = mainMethod.GetILGenerator();

            foreach (char c in sourceCode)
            {
                switch (c)
                {
                    case '>':
                        GenerateMovePointerForwardInstruction(ilGen, pointerField, memoryField);
                        break;
                    case '<':
                        GenerateMovePointerBackwardsInstruction(ilGen, pointerField, memoryField);
                        break;
                    case '+':
                        GenerateIncrementInstruction(ilGen, pointerField, memoryField);
                        break;
                    case '-':
                        GenerateDecrementInstruction(ilGen, pointerField, memoryField);
                        break;
                    case '.':
                        GenerateWriteInstruction(ilGen, pointerField, memoryField);
                        break;
                    case ',':
                        GenerateReadInstruction(ilGen, pointerField, memoryField);
                        break;
                    case '[':
                        GenerateOpenBracketInstruction(ilGen, pointerField, memoryField);
                        break;
                    case ']':
                        GenerateCloseBracketInstruction(ilGen, pointerField, memoryField);
                        break;
                }
            }
            ilGen.Emit(OpCodes.Ret);

            type.CreateType();
            asm.SetEntryPoint(mainMethod);
            asm.Save(outputFileName);
        }

        private static void GenerateConstructorBody(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            // construct the memory as byte[short.MaxValue]
            ilGen.Emit(OpCodes.Ldc_I4, 0x7fff);
            ilGen.Emit(OpCodes.Newarr, typeof(Byte));
            ilGen.Emit(OpCodes.Stsfld, memoryField);

            // Construct the pointer as short = 0
            ilGen.Emit(OpCodes.Ldc_I4_0);
            ilGen.Emit(OpCodes.Stsfld, pointerField);

            ilGen.Emit(OpCodes.Ret);
        }


        private static void GenerateMovePointerForwardInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            ilGen.Emit(OpCodes.Ldsfld, pointerField);
            ilGen.Emit(OpCodes.Ldc_I4_1);
            ilGen.Emit(OpCodes.Add);
            ilGen.Emit(OpCodes.Conv_I2);
            ilGen.Emit(OpCodes.Stsfld, pointerField);
        }

        private static void GenerateMovePointerBackwardsInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            ilGen.Emit(OpCodes.Ldsfld, pointerField);
            ilGen.Emit(OpCodes.Ldc_I4_1);
            ilGen.Emit(OpCodes.Sub);
            ilGen.Emit(OpCodes.Conv_I2);
            ilGen.Emit(OpCodes.Stsfld, pointerField);
        }

        private static void GenerateIncrementInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            ilGen.Emit(OpCodes.Ldsfld, memoryField);
            ilGen.Emit(OpCodes.Ldsfld, pointerField);
            ilGen.Emit(OpCodes.Ldelema, typeof(Byte));
            ilGen.Emit(OpCodes.Dup);
            ilGen.Emit(OpCodes.Ldobj, typeof(Byte));
            ilGen.Emit(OpCodes.Ldc_I4_1);
            ilGen.Emit(OpCodes.Add);
            ilGen.Emit(OpCodes.Conv_U1);
            ilGen.Emit(OpCodes.Stobj, typeof(Byte));
        }

        private static void GenerateDecrementInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            ilGen.Emit(OpCodes.Ldsfld, memoryField);
            ilGen.Emit(OpCodes.Ldsfld, pointerField);
            ilGen.Emit(OpCodes.Ldelema, typeof(Byte));
            ilGen.Emit(OpCodes.Dup);
            ilGen.Emit(OpCodes.Ldobj, typeof(Byte));
            ilGen.Emit(OpCodes.Ldc_I4_1);
            ilGen.Emit(OpCodes.Sub);
            ilGen.Emit(OpCodes.Conv_U1);
            ilGen.Emit(OpCodes.Stobj, typeof(Byte));
        }

        private static void GenerateWriteInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            ilGen.Emit(OpCodes.Ldsfld, memoryField);
            ilGen.Emit(OpCodes.Ldsfld, pointerField);
            ilGen.Emit(OpCodes.Ldelem_U1);
            ilGen.Emit(OpCodes.Call,
                       typeof (Console).GetMethod("Write", BindingFlags.Public | BindingFlags.Static, null,
                                                  new Type[] {typeof (Char)}, null));
        }

        private static void GenerateReadInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            ilGen.Emit(OpCodes.Ldsfld, memoryField);
            ilGen.Emit(OpCodes.Ldsfld, pointerField);
            ilGen.Emit(OpCodes.Ldelem_U1);
            ilGen.Emit(OpCodes.Call, typeof(Console).GetMethod("Read", BindingFlags.Public | BindingFlags.Static));
            ilGen.Emit(OpCodes.Conv_U1);
            ilGen.Emit(OpCodes.Stelem_I1);
        }



        private static void GenerateOpenBracketInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {
            var firstLabel = ilGen.DefineLabel();
            var secondLabel = ilGen.DefineLabel();
            ilGen.Emit(OpCodes.Br, secondLabel);
            ilGen.MarkLabel(firstLabel);
            _bracketStack.Push(firstLabel);
            _bracketStack.Push(secondLabel);
        }

        private static void GenerateCloseBracketInstruction(ILGenerator ilGen, FieldInfo pointerField, FieldInfo memoryField)
        {

            var secondLabel = _bracketStack.Pop();
            var firstLabel = _bracketStack.Pop();
            ilGen.MarkLabel(secondLabel);
            ilGen.Emit(OpCodes.Ldsfld, memoryField);
            ilGen.Emit(OpCodes.Ldsfld, pointerField);
            ilGen.Emit(OpCodes.Ldelem_U1);
            ilGen.Emit(OpCodes.Ldc_I4_0);
            ilGen.Emit(OpCodes.Bgt, firstLabel);
        }
    }
}
