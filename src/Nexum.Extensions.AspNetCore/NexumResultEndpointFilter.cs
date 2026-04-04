using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Nexum.Abstractions;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// An <see cref="IEndpointFilter"/> that inspects endpoint return values via
/// <see cref="IResultAdapter{TResult}"/> and converts failure results to
/// <see cref="ProblemDetails"/> HTTP responses.
/// </summary>
/// <remarks>
/// <para>
/// This filter uses <see cref="Type.MakeGenericType"/> at runtime to resolve
/// <c>IResultAdapter&lt;T&gt;</c> for the concrete result type. For NativeAOT scenarios,
/// use the Source Generator path which emits typed inline code with zero reflection.
/// </para>
/// <para>
/// Adapter types are cached in a <see cref="ConcurrentDictionary{TKey, TValue}"/>
/// to avoid repeated <see cref="Type.MakeGenericType"/> calls (Z7 compliance).
/// </para>
/// </remarks>
[RequiresDynamicCode("MakeGenericType is used for IResultAdapter<T> resolution. Use SG-generated endpoints for NativeAOT.")]
public sealed class NexumResultEndpointFilter(IServiceProvider serviceProvider) : IEndpointFilter
{
    // Cache: result Type → closed IResultAdapter<T> service Type (or null if MakeGenericType fails)
    private static readonly ConcurrentDictionary<Type, Type?> s_adapterTypeCache = new();

    // Cache: closed IResultAdapter<T> Type → MethodInfo tuple (IsSuccess, GetValue, GetError)
    private static readonly ConcurrentDictionary<Type, (MethodInfo IsSuccess, MethodInfo GetValue, MethodInfo GetError)> s_methodCache = new();

    private static readonly Type s_resultAdapterOpenGeneric = typeof(IResultAdapter<>);

    /// <inheritdoc />
    [UnconditionalSuppressMessage("Trimming", "IL2055", Justification = "MakeGenericType is called with a runtime type from GetType(). Class is annotated [RequiresDynamicCode].")]
    [UnconditionalSuppressMessage("Trimming", "IL2070", Justification = "GetMethod is called on a type produced by MakeGenericType. Class is annotated [RequiresDynamicCode].")]
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        object? result = await next(context).ConfigureAwait(false);

        if (result is null)
        {
            return result;
        }

        Type resultType = result.GetType();

        // Resolve IResultAdapter<T> service type (cached)
        Type? adapterServiceType = s_adapterTypeCache.GetOrAdd(
            resultType,
            static type =>
            {
                try
                {
                    return s_resultAdapterOpenGeneric.MakeGenericType(type);
                }
                catch (ArgumentException)
                {
                    return null;
                }
            });

        if (adapterServiceType is null)
        {
            return result;
        }

        // Resolve adapter from DI
        object? adapter = serviceProvider.GetService(adapterServiceType);
        if (adapter is null)
        {
            return result;
        }

        // Get cached method info
        (MethodInfo isSuccessMethod, MethodInfo getValueMethod, MethodInfo getErrorMethod) = s_methodCache.GetOrAdd(
            adapterServiceType,
            static type =>
            {
                MethodInfo isSuccess = type.GetMethod("IsSuccess")!;
                MethodInfo getValue = type.GetMethod("GetValue")!;
                MethodInfo getError = type.GetMethod("GetError")!;
                return (isSuccess, getValue, getError);
            });

        bool isSuccess = (bool)isSuccessMethod.Invoke(adapter, [result])!;

        if (isSuccess)
        {
            object? value = getValueMethod.Invoke(adapter, [result]);
            return TypedResults.Ok(value);
        }

        object? error = getErrorMethod.Invoke(adapter, [result]);
        if (error is null)
        {
            return TypedResults.Ok(result);
        }

        NexumEndpointOptions options = serviceProvider
            .GetRequiredService<IOptions<NexumEndpointOptions>>().Value;

        ProblemDetails problemDetails = NexumResultProblemDetailsMapper.CreateProblemDetails(error, options);
        return TypedResults.Problem(problemDetails);
    }

    /// <summary>
    /// Resets the internal caches. For testing purposes only.
    /// </summary>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    internal static void ResetCacheForTesting()
    {
        s_adapterTypeCache.Clear();
        s_methodCache.Clear();
    }
}
