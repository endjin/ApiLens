global using NSubstitute;
global using Shouldly;
global using Microsoft.VisualStudio.TestTools.UnitTesting;
global using System.Collections.Immutable;

[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
