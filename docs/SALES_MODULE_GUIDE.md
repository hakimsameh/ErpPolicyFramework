# Sales Module Guide — Invoice vs Return Policies

**Scenario:** You have Sales Invoice and Sales Return. Each needs **different** policies.  
**Solution:** Use **separate contexts** — one for Invoice, one for Return. Each context runs its own policy pipeline.

---

## How It Works

| Document Type   | Context Type          | Executor                            | Policies run              |
|-----------------|----------------------|-------------------------------------|---------------------------|
| **Sales Invoice** | `SalesInvoiceContext`  | `IPolicyExecutor<SalesInvoiceContext>` | Invoice policies only    |
| **Sales Return**  | `SalesReturnContext`   | `IPolicyExecutor<SalesReturnContext>`  | Return policies only     |

When you call `_invoiceExecutor.ExecuteAsync(invoiceContext)`, **only** invoice policies run.  
When you call `_returnExecutor.ExecuteAsync(returnContext)`, **only** return policies run.

---

## Sales Invoice Policies

| Policy                    | Code    | When it runs           | What it checks                                      |
|---------------------------|---------|------------------------|------------------------------------------------------|
| CustomerBlacklistPolicy   | SAL-002 | Always                 | Customer not on blacklist                            |
| CustomerCreditLimitPolicy | SAL-001 | Only when `IsCredit`   | Invoice total + balance ≤ credit limit               |
| StockAvailabilityPolicy   | SAL-003 | Always                 | Each line has sufficient stock                       |
| NegativeStockOnInvoicePolicy | SAL-004 | Always            | No line would result in negative stock               |

---

## Sales Return Policies

| Policy                    | Code    | What it checks                                      |
|---------------------------|---------|-----------------------------------------------------|
| ReturnPeriodPolicy        | SAL-103 | (Return date − Sale date) ≤ MaxReturnPeriodDays     |
| CustomerBoughtProductPolicy | SAL-101 | Customer bought each item on the original sale    |
| ProductReturnablePolicy   | SAL-102 | Each product is eligible for return                 |

---

## Usage in Your Code

### 1. Register the module

```csharp
builder.Services.AddPolicyFramework();
builder.Services.AddPoliciesFromAssemblies(
    typeof(CustomerCreditLimitPolicy).Assembly  // PolicyFramework.Modules.Sales
);
```

### 2. Create Sales Invoice (in your service/API)

```csharp
public class SalesInvoiceService(
    IPolicyExecutor<SalesInvoiceContext> _policies,
    IStockRepository _stock,
    ICustomerRepository _customer)
{
    public async Task<Result> CreateInvoiceAsync(CreateInvoiceCommand cmd)
    {
        var context = new SalesInvoiceContext
        {
            CustomerCode    = cmd.CustomerCode,
            IsCredit        = cmd.PaymentType == PaymentType.Credit,
            DocumentTotal   = cmd.TotalAmount,
            Currency        = cmd.Currency,
            DocumentDate    = DateTimeOffset.UtcNow,
            CreatedBy       = cmd.UserId,
            LineItems       = cmd.Lines.Select(l => new SalesInvoiceLineItem(
                l.ItemCode, l.WarehouseCode, l.Uom, l.Quantity)).ToList(),

            CreditLimit     = cmd.IsCredit ? () => _customer.GetCreditLimitAsync(cmd.CustomerCode) : null,
            CurrentBalance  = cmd.IsCredit ? () => _customer.GetBalanceAsync(cmd.CustomerCode) : null,
            IsCustomerBlacklisted = () => _customer.IsBlacklistedAsync(cmd.CustomerCode),
            GetStockForItem  = (item, wh) => _stock.GetCurrentStockAsync(item, wh)
        };

        var result = await _policies.ExecuteAsync(context);
        if (result.IsFailure)
            return Result.Fail(result.BlockingViolations.Select(v => v.Message));

        // Proceed with save
        await _invoiceRepo.SaveAsync(cmd);
        return Result.Ok();
    }
}
```

### 3. Create Sales Return (in your service/API)

```csharp
public class SalesReturnService(
    IPolicyExecutor<SalesReturnContext> _policies,
    ISaleRepository _sale,
    IProductRepository _product)
{
    public async Task<Result> CreateReturnAsync(CreateReturnCommand cmd)
    {
        var originalSale = await _sale.GetByIdAsync(cmd.OriginalSaleId);

        var context = new SalesReturnContext
        {
            CustomerCode           = originalSale.CustomerCode,
            OriginalSaleDocumentId = originalSale.DocumentId,
            OriginalSaleDate       = originalSale.DocumentDate,
            ReturnDate             = DateTimeOffset.UtcNow,
            MaxReturnPeriodDays    = 30,  // from config or master data
            CreatedBy              = cmd.UserId,
            LineItems              = cmd.Lines.Select(l => new SalesReturnLineItem(l.ItemCode, l.Quantity)).ToList(),

            CustomerBoughtItemOnSale = (saleId, itemCode) =>
                _sale.LineExistsAsync(saleId, itemCode),
            IsProductReturnable = (itemCode) =>
                _product.IsReturnableAsync(itemCode)
        };

        var result = await _policies.ExecuteAsync(context);
        if (result.IsFailure)
            return Result.Fail(result.BlockingViolations.Select(v => v.Message));

        await _returnRepo.SaveAsync(cmd);
        return Result.Ok();
    }
}
```

---

## Key Point

**Different document types → different contexts → different executors → different policies.**

You inject both executors where needed:

```csharp
public MyController(
    IPolicyExecutor<SalesInvoiceContext> _invoicePolicies,
    IPolicyExecutor<SalesReturnContext> _returnPolicies)
{
    // Use _invoicePolicies for invoice flows
    // Use _returnPolicies for return flows
}
```

No need to filter or bypass — the framework routes by context type automatically.
