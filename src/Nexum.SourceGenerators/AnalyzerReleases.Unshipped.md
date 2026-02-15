; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
NEXUM001 | Nexum.SourceGenerators | Error | No handler found for message type
NEXUM002 | Nexum.SourceGenerators | Error | Duplicate handler for message type
NEXUM003 | Nexum.SourceGenerators | Warning | Handler missing marker attribute
NEXUM004 | Nexum.SourceGenerators | Warning | Marker attribute without handler interface
NEXUM005 | Nexum.SourceGenerators | Info | Interceptor generated for dispatch call-site
NEXUM006 | Nexum.SourceGenerators | Warning | Cannot intercept dispatch — concrete type unknown
NEXUM007 | Nexum.SourceGenerators | Info | Interceptor skipped — handler not in compilation
NEXUM008 | Nexum.SourceGenerators | Warning | NexumEndpoint attribute on type not implementing ICommand or IQuery
NEXUM009 | Nexum.SourceGenerators | Error | Duplicate endpoint route pattern
