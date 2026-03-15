using Nexum.Results;

namespace Nexum.Examples.ResultsValidation.ResultDemo;

// Demonstrates the railway-oriented programming primitives on Result<T>:
//   Map     — transform the success value (no-op on failure)
//   Bind    — chain another result-producing function (no-op on failure)
//   GetValueOrDefault — safe fallback when result is a failure
public static class ResultChainingDemo
{
    public static void Run()
    {
        Console.WriteLine("\n--- Result chaining demo ---");

        // --- Map: transform Guid to a URL string ---
        var successResult = Result<Guid>.Ok(Guid.NewGuid());
        Result<string> urlResult = successResult.Map(id => $"/products/{id}");
        Console.WriteLine($"  Map (success): {urlResult.Value}");

        var failureResult = Result<Guid>.Fail(new NexumError("NOT_FOUND", "Product not found."));
        Result<string> failedUrl = failureResult.Map(id => $"/products/{id}");
        Console.WriteLine($"  Map (failure propagated): IsFailure={failedUrl.IsFailure}, Code={failedUrl.Error.Code}");

        // --- Bind: chain a second validation step ---
        var priceResult = Result<decimal>.Ok(49.99m);
        Result<string> formattedPrice = priceResult.Bind(price =>
            price > 0
                ? Result<string>.Ok(price.ToString("C"))
                : Result<string>.Fail(new NexumError("NEGATIVE_PRICE", "Price must be positive.")));
        Console.WriteLine($"  Bind (success): {formattedPrice.Value}");

        var negativePriceResult = Result<decimal>.Ok(-5m);
        Result<string> bindFailed = negativePriceResult.Bind(price =>
            price > 0
                ? Result<string>.Ok(price.ToString("C"))
                : Result<string>.Fail(new NexumError("NEGATIVE_PRICE", "Price must be positive.")));
        Console.WriteLine($"  Bind (failure): IsFailure={bindFailed.IsFailure}, Code={bindFailed.Error.Code}");

        // --- GetValueOrDefault: safe extraction without throwing ---
        var knownId = Guid.NewGuid();
        var hasValue = Result<Guid>.Ok(knownId);
        Console.WriteLine($"  GetValueOrDefault (success): {hasValue.GetValueOrDefault(Guid.Empty)}");

        var noValue = Result<Guid>.Fail(new NexumError("MISSING", "No value available."));
        Console.WriteLine($"  GetValueOrDefault (failure fallback): {noValue.GetValueOrDefault(Guid.Empty)}");
    }
}
