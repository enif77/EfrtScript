/* Copyright (C) Premysl Fara and Contributors */

namespace EFrtScript.Libs.CoreExt.Tests;

using Xunit;

using EFrtScript.Extensions;
using EFrtScript.IO;


public class LibraryTests
{
    [Fact]
    public void HasExpectedName()
    {
        Assert.Equal("CORE-EXT", new Library().Name);
    }
    
    
    [Theory]
    [InlineData("HEX")]
    [InlineData("\\")]
    public void HasWordRegistered(string wordName)
    {
        var interpreter = new Interpreter(new NullOutputWriter());
        new Library().Initialize(interpreter);
        
        Assert.True(interpreter.IsWordRegistered(wordName));
    }
}