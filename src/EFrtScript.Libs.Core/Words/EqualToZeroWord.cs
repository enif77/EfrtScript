/* Copyright (C) Premysl Fara and Contributors */

namespace EFrtScript.Libs.Core.Words;

using EFrtScript.Extensions;


internal class EqualToZeroWord : IWord
{
    public string Name => "0=";
    public bool IsImmediate => false;
    public int ExecutionToken { get; set; }


    public int Execute(IInterpreter interpreter)
    {
        interpreter.StackExpect(1);

        interpreter.StackPush(interpreter.StackPopInteger() == 0);

        return 1;
    }
}
