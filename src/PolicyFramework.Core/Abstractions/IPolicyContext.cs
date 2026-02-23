namespace PolicyFramework.Core.Abstractions;

/// <summary>
/// Marker interface for all policy context objects.
/// Every ERP module defines its own strongly-typed context carrying the data
/// a policy needs to make its evaluation decision.
/// Contexts are plain data-carrier (value) objects — no business logic.
/// </summary>
public interface IPolicyContext { }
