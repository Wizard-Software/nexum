using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc;

namespace Nexum.Extensions.AspNetCore;

/// <summary>
/// Maps exceptions to ProblemDetails using configured mappings with type hierarchy traversal.
/// </summary>
internal static class NexumProblemDetailsMapper
{
    /// <summary>
    /// Attempts to create a <see cref="ProblemDetails"/> from the given exception
    /// using the configured mappings in <paramref name="options"/>.
    /// </summary>
    /// <param name="exception">The exception to map.</param>
    /// <param name="options">The problem details options containing exception mappings.</param>
    /// <param name="problemDetails">The resulting ProblemDetails, or <c>null</c> if unmapped.</param>
    /// <returns><c>true</c> if the exception was mapped; otherwise <c>false</c>.</returns>
    public static bool TryCreateProblemDetails(
        Exception exception,
        NexumProblemDetailsOptions options,
        [NotNullWhen(true)] out ProblemDetails? problemDetails)
    {
        // Walk the type hierarchy: exact type first, then base types
        Type? exceptionType = exception.GetType();
        while (exceptionType is not null && exceptionType != typeof(object))
        {
            if (options.ExceptionMappings.TryGetValue(exceptionType, out Func<Exception, ProblemDetails?>? factory))
            {
                problemDetails = factory(exception);
                if (problemDetails is not null)
                {
                    if (options.IncludeExceptionDetails)
                    {
                        problemDetails.Extensions["exceptionMessage"] = exception.Message;
                        problemDetails.Extensions["exceptionType"] = exception.GetType().FullName;
                        problemDetails.Extensions["stackTrace"] = exception.StackTrace;
                    }

                    return true;
                }
            }

            exceptionType = exceptionType.BaseType;
        }

        problemDetails = null;
        return false;
    }
}
