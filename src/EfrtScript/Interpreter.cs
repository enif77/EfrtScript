/* Copyright (C) Premysl Fara and Contributors */

namespace EFrtScript;

using EFrtScript.Extensions;
using EFrtScript.IO;
using EFrtScript.Words;


public class Interpreter : IInterpreter
{
    public IInterpreterState State { get; }
    public IOutputWriter Output { get; }
    public IInputSource? CurrentInputSource =>
        (State.InputSourceStack.Count > 0) 
            ? State.InputSourceStack.Peek() 
            : null;


    public Interpreter(IOutputWriter outputWriter)
    {
        State = new InterpreterState();
        Output = outputWriter ?? throw new ArgumentNullException(nameof(outputWriter));
        
        this.SetNumericConversionRadix(10);
    }


    #region words compilation

    public INonPrimitiveWord? WordBeingDefined { get; private set; }


    public void BeginNewWordCompilation(string wordName)
    {
        if (IsCompiling)
        {
            Throw(-29, "compiler nesting");
        }

        WordBeingDefined = new NonPrimitiveWord(wordName);
        _interpreterState = InterpreterStateCode.Compiling;
    }


    public void EndNewWordCompilation()
    {
        if (IsCompiling == false)
        {
            Throw(-14, "not in a new word compilation");
        }

        this.RegisterWord(WordBeingDefined ?? throw new InvalidOperationException(nameof(WordBeingDefined) + " is null."));

        WordBeingDefined = null;
        _interpreterState = InterpreterStateCode.Interpreting;
    }

    #endregion


    #region execution

    private InterpreterStateCode _interpreterState;
    public bool IsCompiling => _interpreterState == InterpreterStateCode.Compiling;
    public bool IsExecutionTerminated => _interpreterState == InterpreterStateCode.Breaking || _interpreterState == InterpreterStateCode.Terminating;

    public event EventHandler<InterpreterEventArgs>? ExecutingWord;
    public event EventHandler<InterpreterEventArgs>? WordExecuted;


    public void Interpret(string src)
    {
        State.InputSourceStack.Push(
            new InputSource(
                new StringSourceReader(src)));
        
        while (true)
        {
            var word = CurrentInputSource!.ReadWord();
            if (word == null)
            {
                break;
            }

            var wordName = word.ToUpperInvariant();

            if (IsCompiling)
            {
                CompileWord(wordName);
            }
            else
            {
                ExecuteWord(wordName);
            }
            
            if (IsExecutionTerminated)
            {
                break;
            }
        }
        
        // Execution broken. Return to interpreting mode.
        if (_interpreterState == InterpreterStateCode.Breaking)
        {
            _interpreterState = InterpreterStateCode.Interpreting;
            //State.SetStateValue(false);
        }
            
        // Restore the previous input source, if any.
        if (State.InputSourceStack.Count > 0)
        {
            State.InputSourceStack.Pop();
        }
    }


    public int ExecuteWord(IWord word)
    {
        if (word == null) throw new ArgumentNullException(nameof(word));

        return ExecuteWordInternal(word);
    }
    

    public void Reset()
    {
        State.Reset();
        _interpreterState = InterpreterStateCode.Interpreting;
    }
    
    
    public void Abort()
    {
        State.Stack.Clear();
        State.InputSourceStack.Clear();

        // TODO: Clear the heap?
        // TODO: Clear the exception stack?

        Quit();
    }


    public void Quit()
    {
        State.ReturnStack.Clear();
        _interpreterState = InterpreterStateCode.Breaking;
    }
    
    
    public void Throw(int exceptionCode, string? message = null)
    {
        ThrowInternal(exceptionCode, message);
    }
    
    
    public void TerminateExecution()
    {
        _interpreterState = InterpreterStateCode.Terminating;
    }


    private void CompileWord(string wordName)
    {
        if (this.IsWordRegistered(wordName))
        {
            var word = State.Words[wordName];
            if (word.IsImmediate)
            {
                ExecuteWordInternal(State.Words[wordName]);
            }
            else
            {
                WordBeingDefined!.AddWord(word);
            }

            return;
        }
        
        if (wordName == WordBeingDefined?.Name)
        {
            // Recursive call of the currently defined word.
            WordBeingDefined.AddWord(WordBeingDefined);
            
            return;
        }
        
        if (Parser.TryParseNumber(wordName, out var result))
        {
            WordBeingDefined!.AddWord(new ConstantValueWord(result));

            return;
        }

        ThrowInternal(-13, $"The '{wordName}' word is undefined.", true);
    }


    private void ExecuteWord(string wordName)
    {
        if (this.IsWordRegistered(wordName))
        {
            ExecuteWordInternal(State.Words[wordName]);
            
            return;
        }

        if (Parser.TryParseNumber(wordName, out var result))
        {
            // This will show the value in the output of the TRACE word.
            ExecuteWordInternal(new ConstantValueWord(result));

            return;
        }

        ThrowInternal(-13, $"The '{wordName}' word is undefined.", true);
    }


    private int ExecuteWordInternal(IWord word)
    {
        State.CurrentWord = word;

        try
        {
            ExecutingWord?.Invoke(this, new InterpreterEventArgs(word));

            return State.CurrentWord.Execute(this);
        }
        catch (InterpreterException ex)
        {
            Throw(ex.ExceptionCode, ex.Message);
        }
        catch (ExecutionException)
        {
            // Here we can do nothing, because all exceptions were already handled.
        }
        catch (Exception ex)
        {
            Throw(-100, ex.Message);
        }
        finally
        {
            WordExecuted?.Invoke(this, new InterpreterEventArgs(word));
        }

        return 1;
    }


    private void ThrowInternal(int exceptionCode, string? message = null, bool doNotThrowAfterAbort = false)
    {
        if (exceptionCode == 0)
        {
            return;
        }

        if (State.ExceptionStack.IsEmpty)
        {
            switch (exceptionCode)
            {
                case -1: break;
                case -2: Output.WriteLine(message ?? "Execution aborted!"); break;
                default:
                    Output.WriteLine($"Execution aborted: [{exceptionCode}] {message ?? string.Empty}");
                    break;
            }

            Abort();

            // Used when an unknown word is executed.
            if (doNotThrowAfterAbort)
            {
                return;
            }

            // Throw an exception here, so we never return to the caller!
            throw new ExecutionException();
        }

        // Restore the previous execution state.
        var exceptionFrame = State.ExceptionStack.Pop();

        State.Stack.Top = exceptionFrame!.StackTop;
        State.ReturnStack.Top = exceptionFrame.ReturnStackTop;
        State.InputSourceStack.Top = exceptionFrame.InputSourceStackTop;
        State.CurrentWord = exceptionFrame.ExecutingWord ?? throw new InvalidOperationException("Exception frame without a executing word reference.");

        // Will be caught by the CATCH word.
        throw new InterpreterException(exceptionCode, message);
    }

    #endregion
}
