using System.Collections.Immutable;
using Nexum.SourceGenerators.Tests.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Nexum.SourceGenerators.Tests;

[Trait("Category", "SourceGenerator")]
public sealed class InterceptorGenerationTests
{
    [Fact]
    public void Command_WithDispatchCall_GeneratesInterceptorAsync()
    {
        // Arrange
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> CreateOrderAsync()
                {
                    var command = new CreateOrderCommand("test");
                    return await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumInterceptors.g.cs");

        GeneratedSourceResult interceptorSource = result.Results[0].GeneratedSources
            .Single(s => s.HintName == "NexumInterceptors.g.cs");
        string generatedCode = interceptorSource.SourceText.ToString();

        // Verify expected elements in generated code
        generatedCode.Should().Contain("class NexumInterceptors");
        generatedCode.Should().Contain("InterceptsLocationAttribute");
        generatedCode.Should().Contain("Intercept_DispatchAsync_");
        generatedCode.Should().Contain("System.Runtime.CompilerServices.Unsafe.As");
        generatedCode.Should().Contain("TestApp.CreateOrderCommand");
        generatedCode.Should().Contain("System.Guid"); // Result type uses metadata name
        generatedCode.Should().NotContain("guid"); // Should not use keyword alias
    }

    [Fact]
    public void Query_WithDispatchCall_GeneratesInterceptorAsync()
    {
        // Arrange
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record GetOrderQuery(Guid Id) : IQuery<string>;

            [QueryHandler]
            public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
            {
                public ValueTask<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("order");
            }

            public class Consumer
            {
                private readonly IQueryDispatcher _dispatcher;

                public Consumer(IQueryDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<string> GetOrderAsync(Guid id)
                {
                    var query = new GetOrderQuery(id);
                    return await _dispatcher.DispatchAsync(query);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumInterceptors.g.cs");

        GeneratedSourceResult interceptorSource = result.Results[0].GeneratedSources
            .Single(s => s.HintName == "NexumInterceptors.g.cs");
        string generatedCode = interceptorSource.SourceText.ToString();

        // Verify expected elements in generated code
        generatedCode.Should().Contain("class NexumInterceptors");
        generatedCode.Should().Contain("Intercept_DispatchAsync_");
        generatedCode.Should().Contain("TestApp.GetOrderQuery");
        generatedCode.Should().Contain("System.String"); // Result type uses metadata name
        generatedCode.Should().Contain("IQueryDispatcher");
        generatedCode.Should().Contain("DispatchInterceptedQueryAsync");
    }

    [Fact]
    public void StreamQuery_WithStreamAsyncCall_GeneratesInterceptorAsync()
    {
        // Arrange
        const string Source = """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record StreamOrdersQuery() : IStreamQuery<string>;

            [StreamQueryHandler]
            public sealed class StreamOrdersQueryHandler : IStreamQueryHandler<StreamOrdersQuery, string>
            {
                public async IAsyncEnumerable<string> HandleAsync(StreamOrdersQuery query, CancellationToken ct = default)
                {
                    yield return "order1";
                    yield return "order2";
                }
            }

            public class Consumer
            {
                private readonly IQueryDispatcher _dispatcher;

                public Consumer(IQueryDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public IAsyncEnumerable<string> StreamOrdersAsync()
                {
                    var query = new StreamOrdersQuery();
                    return _dispatcher.StreamAsync(query);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumInterceptors.g.cs");

        GeneratedSourceResult interceptorSource = result.Results[0].GeneratedSources
            .Single(s => s.HintName == "NexumInterceptors.g.cs");
        string generatedCode = interceptorSource.SourceText.ToString();

        // Verify expected elements in generated code
        generatedCode.Should().Contain("class NexumInterceptors");
        generatedCode.Should().Contain("Intercept_DispatchAsync_");
        generatedCode.Should().Contain("TestApp.StreamOrdersQuery");
        generatedCode.Should().Contain("IAsyncEnumerable");
        generatedCode.Should().Contain("StreamInterceptedAsync");
    }

    [Fact]
    public void AbstractMessageType_DoesNotGenerateInterceptorAsync()
    {
        // Arrange
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> CreateOrderAsync(ICommand<Guid> command)
                {
                    // Call with interface-typed argument - can't intercept
                    return await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        // NexumHandlerRegistry.g.cs should be generated (handler exists)
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumHandlerRegistry.g.cs");

        // But NexumInterceptors.g.cs should NOT be generated (no concrete call-site)
        result.Results[0].GeneratedSources.Should().NotContain(s => s.HintName == "NexumInterceptors.g.cs");
    }

    [Fact]
    public void InterceptorGeneration_EmitsNEXUM005DiagnosticAsync()
    {
        // Arrange
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> CreateOrderAsync()
                {
                    var command = new CreateOrderCommand("test");
                    return await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        ImmutableArray<Diagnostic> diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().Contain(d => d.Id == "NEXUM005");

        Diagnostic nexum005 = diagnostics.Single(d => d.Id == "NEXUM005");
        nexum005.Severity.Should().Be(DiagnosticSeverity.Info);
        nexum005.GetMessage().Should().Contain("Interceptor generated");
        nexum005.GetMessage().Should().Contain("command"); // Kind
        nexum005.GetMessage().Should().Contain("CreateOrderCommand");
    }

    [Fact]
    public void MultipleDispatchCalls_GeneratesMultipleInterceptorsAsync()
    {
        // Arrange
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;
            public record GetOrderQuery(Guid Id) : IQuery<string>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            [QueryHandler]
            public sealed class GetOrderQueryHandler : IQueryHandler<GetOrderQuery, string>
            {
                public ValueTask<string> HandleAsync(GetOrderQuery query, CancellationToken ct = default)
                    => ValueTask.FromResult("order");
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _commandDispatcher;
                private readonly IQueryDispatcher _queryDispatcher;

                public Consumer(ICommandDispatcher commandDispatcher, IQueryDispatcher queryDispatcher)
                {
                    _commandDispatcher = commandDispatcher;
                    _queryDispatcher = queryDispatcher;
                }

                public async ValueTask<Guid> CreateOrderAsync()
                {
                    var command = new CreateOrderCommand("test");
                    return await _commandDispatcher.DispatchAsync(command);
                }

                public async ValueTask<string> GetOrderAsync(Guid id)
                {
                    var query = new GetOrderQuery(id);
                    return await _queryDispatcher.DispatchAsync(query);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumInterceptors.g.cs");

        GeneratedSourceResult interceptorSource = result.Results[0].GeneratedSources
            .Single(s => s.HintName == "NexumInterceptors.g.cs");
        string generatedCode = interceptorSource.SourceText.ToString();

        // Should generate multiple interceptor methods
        generatedCode.Should().Contain("Intercept_DispatchAsync_0001");
        generatedCode.Should().Contain("Intercept_DispatchAsync_0002");
        generatedCode.Should().Contain("CreateOrderCommand");
        generatedCode.Should().Contain("GetOrderQuery");

        // Should emit two NEXUM005 diagnostics
        var nexum005Diagnostics = result.Results[0].Diagnostics.Where(d => d.Id == "NEXUM005").ToList();
        nexum005Diagnostics.Should().HaveCount(2);
    }

    [Fact]
    public void NoDispatchCalls_DoesNotGenerateInterceptorsAsync()
    {
        // Arrange
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CreateOrderCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(CreateOrderCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        // NexumHandlerRegistry.g.cs should be generated
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumHandlerRegistry.g.cs");

        // But NexumInterceptors.g.cs should NOT be generated (no call-sites)
        result.Results[0].GeneratedSources.Should().NotContain(s => s.HintName == "NexumInterceptors.g.cs");

        // Should not emit NEXUM005
        result.Results[0].Diagnostics.Should().NotContain(d => d.Id == "NEXUM005");
    }

    [Fact]
    public void ResultTypeUsesMetadataNames_NotKeywordAliasesAsync()
    {
        // Arrange - Test all primitive types that have C# aliases
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record StringCommand() : ICommand<string>;
            public record IntCommand() : ICommand<int>;
            public record BoolCommand() : ICommand<bool>;

            [CommandHandler]
            public sealed class StringCommandHandler : ICommandHandler<StringCommand, string>
            {
                public ValueTask<string> HandleAsync(StringCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult("result");
            }

            [CommandHandler]
            public sealed class IntCommandHandler : ICommandHandler<IntCommand, int>
            {
                public ValueTask<int> HandleAsync(IntCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(42);
            }

            [CommandHandler]
            public sealed class BoolCommandHandler : ICommandHandler<BoolCommand, bool>
            {
                public ValueTask<bool> HandleAsync(BoolCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(true);
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<string> ExecuteStringAsync()
                {
                    return await _dispatcher.DispatchAsync(new StringCommand());
                }

                public async ValueTask<int> ExecuteIntAsync()
                {
                    return await _dispatcher.DispatchAsync(new IntCommand());
                }

                public async ValueTask<bool> ExecuteBoolAsync()
                {
                    return await _dispatcher.DispatchAsync(new BoolCommand());
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumInterceptors.g.cs");

        GeneratedSourceResult interceptorSource = result.Results[0].GeneratedSources
            .Single(s => s.HintName == "NexumInterceptors.g.cs");
        string generatedCode = interceptorSource.SourceText.ToString();

        // Verify metadata names are used (NOT C# keyword aliases)
        generatedCode.Should().Match("*System.String*"); // Not "string"
        generatedCode.Should().Match("*System.Int32*");  // Not "int"
        generatedCode.Should().Match("*System.Boolean*"); // Not "bool"

        // These patterns should appear in generic type arguments and Unsafe.As calls
        generatedCode.Should().Match("*ValueTask<global::System.String>*");
        generatedCode.Should().Match("*ValueTask<global::System.Int32>*");
        generatedCode.Should().Match("*ValueTask<global::System.Boolean>*");
    }

    [Fact]
    public void ExternalCommandType_WithoutHandler_EmitsNEXUM007AndNoInterceptorAsync()
    {
        // Arrange - Simulate cross-assembly scenario: CommandA has handler, CommandB doesn't
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record CommandA(string Name) : ICommand<Guid>;
            public record CommandB(int Value) : ICommand<string>;

            [CommandHandler]
            public sealed class CommandAHandler : ICommandHandler<CommandA, Guid>
            {
                public ValueTask<Guid> HandleAsync(CommandA command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            // No handler for CommandB - simulates external assembly handler

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> ExecuteAAsync()
                {
                    return await _dispatcher.DispatchAsync(new CommandA("test"));
                }

                public async ValueTask<string> ExecuteBAsync()
                {
                    return await _dispatcher.DispatchAsync(new CommandB(42));
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        ImmutableArray<Diagnostic> diagnostics = result.Results[0].Diagnostics;

        // Should emit NEXUM007 for CommandB (no handler in compilation)
        diagnostics.Should().Contain(d => d.Id == "NEXUM007");
        Diagnostic nexum007 = diagnostics.Single(d => d.Id == "NEXUM007");
        nexum007.Severity.Should().Be(DiagnosticSeverity.Info);
        nexum007.GetMessage().Should().Contain("CommandB");
        nexum007.GetMessage().Should().Contain("no handler was found in the current compilation");

        // Should emit NEXUM005 for CommandA (has handler, interceptor generated)
        diagnostics.Should().Contain(d => d.Id == "NEXUM005");
        Diagnostic nexum005 = diagnostics.Single(d => d.Id == "NEXUM005");
        nexum005.GetMessage().Should().Contain("CommandA");

        // NexumInterceptors.g.cs should be generated
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumInterceptors.g.cs");

        GeneratedSourceResult interceptorSource = result.Results[0].GeneratedSources
            .Single(s => s.HintName == "NexumInterceptors.g.cs");
        string generatedCode = interceptorSource.SourceText.ToString();

        // Should generate interceptor for CommandA only
        generatedCode.Should().Contain("CommandA");
        generatedCode.Should().NotContain("CommandB"); // No interceptor for CommandB
    }

    [Fact]
    public void InterfaceTypedArgument_EmitsNEXUM006Async()
    {
        // Arrange - Call with interface-typed argument
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record ConcreteCommand(string Name) : ICommand<Guid>;

            [CommandHandler]
            public sealed class ConcreteCommandHandler : ICommandHandler<ConcreteCommand, Guid>
            {
                public ValueTask<Guid> HandleAsync(ConcreteCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> ExecuteAsync()
                {
                    // Interface-typed argument - cannot determine concrete type at compile time
                    ICommand<Guid> command = new ConcreteCommand("test");
                    return await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        ImmutableArray<Diagnostic> diagnostics = result.Results[0].Diagnostics;

        // Should emit NEXUM006 (cannot intercept - concrete type unknown)
        diagnostics.Should().Contain(d => d.Id == "NEXUM006");
        Diagnostic nexum006 = diagnostics.Single(d => d.Id == "NEXUM006");
        nexum006.Severity.Should().Be(DiagnosticSeverity.Warning);
        nexum006.GetMessage().Should().Contain("Cannot intercept dispatch");
        nexum006.GetMessage().Should().Contain("concrete message type cannot be determined at compile time");

        // NexumHandlerRegistry.g.cs should be generated (handler exists)
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumHandlerRegistry.g.cs");

        // But NexumInterceptors.g.cs should NOT be generated (no concrete call-site)
        result.Results[0].GeneratedSources.Should().NotContain(s => s.HintName == "NexumInterceptors.g.cs");
    }

    [Fact]
    public void KnownHandler_StillGeneratesInterceptorAsync()
    {
        // Arrange - Regression test: concrete command with handler
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record TestCommand(string Value) : ICommand<int>;

            [CommandHandler]
            public sealed class TestCommandHandler : ICommandHandler<TestCommand, int>
            {
                public ValueTask<int> HandleAsync(TestCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(42);
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<int> ExecuteAsync()
                {
                    var command = new TestCommand("test");
                    return await _dispatcher.DispatchAsync(command);
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert - Happy path still works
        result.Results[0].GeneratedSources.Should().Contain(s => s.HintName == "NexumInterceptors.g.cs");

        GeneratedSourceResult interceptorSource = result.Results[0].GeneratedSources
            .Single(s => s.HintName == "NexumInterceptors.g.cs");
        string generatedCode = interceptorSource.SourceText.ToString();

        generatedCode.Should().Contain("class NexumInterceptors");
        generatedCode.Should().Contain("Intercept_DispatchAsync_");
        generatedCode.Should().Contain("TestApp.TestCommand");
        generatedCode.Should().Contain("System.Int32");

        // Should emit NEXUM005 diagnostic
        ImmutableArray<Diagnostic> diagnostics = result.Results[0].Diagnostics;
        diagnostics.Should().Contain(d => d.Id == "NEXUM005");
        Diagnostic nexum005 = diagnostics.Single(d => d.Id == "NEXUM005");
        nexum005.GetMessage().Should().Contain("TestCommand");
    }

    [Fact]
    public void InvalidHandler_NEXUM004_NoInterceptorGeneratedAsync()
    {
        // Arrange - Handler with [CommandHandler] but not implementing ICommandHandler
        const string Source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using Nexum.Abstractions;

            namespace TestApp;

            public record InvalidCommand(string Name) : ICommand<Guid>;

            [CommandHandler]  // Has attribute but doesn't implement interface
            public sealed class InvalidCommandHandler
            {
                public ValueTask<Guid> HandleAsync(InvalidCommand command, CancellationToken ct = default)
                    => ValueTask.FromResult(Guid.NewGuid());
            }

            public class Consumer
            {
                private readonly ICommandDispatcher _dispatcher;

                public Consumer(ICommandDispatcher dispatcher)
                {
                    _dispatcher = dispatcher;
                }

                public async ValueTask<Guid> ExecuteAsync()
                {
                    return await _dispatcher.DispatchAsync(new InvalidCommand("test"));
                }
            }
            """;

        CSharpCompilation compilation = GeneratorTestHelper.CreateCompilation(Source);
        GeneratorDriver driver = GeneratorTestHelper.CreateDriver(compilation);

        // Act
        GeneratorDriverRunResult result = driver.GetRunResult();

        // Assert
        ImmutableArray<Diagnostic> diagnostics = result.Results[0].Diagnostics;

        // Should emit NEXUM004 (attribute without interface)
        diagnostics.Should().Contain(d => d.Id == "NEXUM004");
        Diagnostic nexum004 = diagnostics.Single(d => d.Id == "NEXUM004");
        nexum004.Severity.Should().Be(DiagnosticSeverity.Warning);
        nexum004.GetMessage().Should().Contain("InvalidCommandHandler");
        nexum004.GetMessage().Should().Contain("does not implement");

        // NexumInterceptors.g.cs should NOT be generated (invalid handler = no handler)
        result.Results[0].GeneratedSources.Should().NotContain(s => s.HintName == "NexumInterceptors.g.cs");

        // Should NOT emit NEXUM005 (no interceptor generated)
        diagnostics.Should().NotContain(d => d.Id == "NEXUM005");
    }
}
