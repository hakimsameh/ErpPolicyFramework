# PolicyFramework.Core

Generic ERP Policy Framework — core abstractions and execution engine for .NET 8.

## Installation

```bash
dotnet add package PolicyFramework.Core
```

## What it does

Runs a **pipeline of business-rule policies** before you save data. Call one method → framework runs all relevant policies → you get pass/fail + violations.

- Block negative stock adjustments
- Block unbalanced journal entries
- Block posting to closed periods
- And more — policies are pluggable

## Quick Start

```csharp
// 1. Register
builder.Services.AddPolicyFramework();
builder.Services.AddPoliciesFromAssemblies(typeof(MyPolicy).Assembly);

// 2. Inject and execute
public class MyService(IPolicyExecutor<MyContext> _policies)
{
    public async Task<Result> DoSomethingAsync(MyContext context)
    {
        var result = await _policies.ExecuteAsync(context);
        if (result.IsFailure)
            return Result.Fail(result.BlockingViolations.Select(v => v.Message));
        return Result.Ok();
    }
}

// Non-generic option: inject IPolicyExecutor when handling multiple context types
public class MyMultiContextService(IPolicyExecutor _policies)
{
    public async Task<Result> ProcessAsync(MyContext a, OtherContext b)
    {
        var r1 = await _policies.ExecuteAsync(a);
        var r2 = await _policies.ExecuteAsync(b);
        return r1.IsSuccess && r2.IsSuccess ? Result.Ok() : Result.Fail(...);
    }
}
```

## Add your own policy

```csharp
public sealed class MyPolicy : PolicyBase<MyContext>
{
    public override string PolicyName => "My.CustomRule";
    public override int Order => 10;
    public override Task<PolicyResult> EvaluateAsync(MyContext ctx, CancellationToken ct = default)
        => ctx.IsValid ? Task.FromResult(Pass()) : Task.FromResult(Fail("ERR-001", "Invalid"));
}
```

## Documentation

Full docs, examples, and modules (Inventory, Posting, Accounting, Sales):  
**[https://github.com/hakimsameh/ErpPolicyFramework](https://github.com/hakimsameh/ErpPolicyFramework)**

## Modules

| Package | Policies |
|---------|----------|
| PolicyFramework.Core | Abstractions, executor — required |
| PolicyFramework.Modules.Inventory | Stock, reorder, adjustment reason |
| PolicyFramework.Modules.Posting | Balanced entry, fiscal period, future date |
| PolicyFramework.Modules.Accounting | Active account, cost center, credit limit |
| PolicyFramework.Modules.Sales | Invoice & return policies |

## License

MIT
