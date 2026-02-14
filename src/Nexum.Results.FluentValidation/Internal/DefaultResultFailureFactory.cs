using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Nexum.Results.FluentValidation.Internal;

/// <summary>
/// Default implementation of <see cref="IResultFailureFactory"/> using reflection.
/// Delegates to cached reflection internally.
/// Registered by AddNexumFluentValidation — can be replaced by
/// a custom implementation for NativeAOT/trimming support.
/// </summary>
[RequiresUnreferencedCode("Uses reflection to discover Result.Fail methods. " +
    "Register a custom IResultFailureFactory for NativeAOT/trimming support.")]
internal sealed class DefaultResultFailureFactory : IResultFailureFactory
{
    private readonly ConcurrentDictionary<Type, Func<NexumError, object>?> _cache = new();

    public bool CanCreate(Type resultType)
        => GetOrBuildFactory(resultType) is not null;

    public object CreateFailure(Type resultType, NexumError error)
        => GetOrBuildFactory(resultType) is { } factory
            ? factory(error)
            : throw new InvalidOperationException(
                $"Cannot create failure Result for type '{resultType.FullName}'.");

    private Func<NexumError, object>? GetOrBuildFactory(Type resultType)
    {
        return _cache.GetOrAdd(resultType, static type =>
        {
            if (!type.IsValueType || !type.IsGenericType)
            {
                return null;
            }

            Type genericDef = type.GetGenericTypeDefinition();
            if (genericDef != typeof(Result<>) &&
                !(genericDef == typeof(Result<,>) && type.GetGenericArguments()[1] == typeof(NexumError)))
            {
                return null;
            }

            MethodInfo? fail = type.GetMethod("Fail", BindingFlags.Public | BindingFlags.Static,
                null, [typeof(NexumError)], null);
            return fail is not null
                ? error => fail.Invoke(null, [error])!
                : null;
        });
    }
}
