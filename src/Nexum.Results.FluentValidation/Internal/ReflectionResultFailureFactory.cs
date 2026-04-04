using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Nexum.Results.FluentValidation.Internal;

/// <summary>
/// Cached delegate factory for creating Result.Fail(NexumError) for generic TResult.
/// Uses static generic class pattern — JIT initializes once per closed TResult type.
/// Zero allocations on hot path after first call.
/// Fallback when no <see cref="IResultFailureFactory"/> is registered in DI.
/// </summary>
internal static class ReflectionResultFailureFactory<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] TResult>
{
    private static readonly Func<NexumError, TResult>? s_factory = BuildFactory();

    public static bool CanCreate => s_factory is not null;

    public static TResult Create(NexumError error)
    {
        return s_factory is not null
            ? s_factory(error)
            : throw new InvalidOperationException(
                $"Cannot create failure Result for type '{typeof(TResult).FullName}'.");
    }

    private static Func<NexumError, TResult>? BuildFactory()
    {
        Type type = typeof(TResult);
        if (!type.IsValueType || !type.IsGenericType)
        {
            return null;
        }

        Type genericDef = type.GetGenericTypeDefinition();

        // Result<T> (which is Result<T, NexumError> internally)
        if (genericDef == typeof(Result<>))
        {
            MethodInfo? fail = type.GetMethod("Fail", BindingFlags.Public | BindingFlags.Static,
                null, [typeof(NexumError)], null);
            return fail is not null
                ? (Func<NexumError, TResult>)Delegate.CreateDelegate(
                    typeof(Func<NexumError, TResult>), fail)
                : null;
        }

        // Result<T, NexumError> (explicit two-type-param form)
        if (genericDef == typeof(Result<,>)
            && type.GetGenericArguments()[1] == typeof(NexumError))
        {
            MethodInfo? fail = type.GetMethod("Fail", BindingFlags.Public | BindingFlags.Static,
                null, [typeof(NexumError)], null);
            return fail is not null
                ? (Func<NexumError, TResult>)Delegate.CreateDelegate(
                    typeof(Func<NexumError, TResult>), fail)
                : null;
        }

        return null;
    }
}
