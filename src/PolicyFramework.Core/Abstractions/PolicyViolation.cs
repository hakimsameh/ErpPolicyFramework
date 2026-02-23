namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Represents a single, specific policy violation.
/// Immutable value object — safe to cache and share across threads.
/// </summary>
/// <param name="Code">
///     Machine-readable violation code. Convention: "MODULE-NNN" e.g. "INV-001".
///     Used for client-side localization lookup and programmatic handling.
/// </param>
/// <param name="Message">
///     Human-readable description of the violation. Should include relevant context values.
/// </param>
/// <param name="Severity">Severity classification for this violation.</param>
/// <param name="Field">
///     Optional: the DTO/context property that caused the violation.
///     Used by UI clients for field-level validation highlighting.
/// </param>
/// <param name="Metadata">
///     Optional: structured key-value pairs for additional diagnostics,
///     audit logging, or downstream event payload construction.
/// </param>
public readonly record struct PolicyViolation(
    string Code,
    string Message,
    PolicySeverity Severity,
    string? Field = null,
    IDictionary<string, object>? Metadata = null
);
