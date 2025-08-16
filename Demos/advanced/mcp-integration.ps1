#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Demonstrates ApiLens integration with Model Context Protocol (MCP).

.DESCRIPTION
    Shows how LLMs can use ApiLens through MCP to understand and explore
    .NET codebases by querying API documentation.
#>

param(
    [string]$IndexPath = ""
)

# Set default index path if not provided
if (-not $IndexPath) {
    $tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
    $IndexPath = Join-Path $tmpBase "indexes/mcp-demo-index"
}

Write-Host "`n🤖 ApiLens MCP Integration Demo" -ForegroundColor Cyan
Write-Host "===============================" -ForegroundColor Cyan
Write-Host "This demo shows how LLMs can use ApiLens to understand .NET APIs`n" -ForegroundColor Gray

# Ensure ApiLens is built
if (-not (Test-Path "./Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens*")) {
    Write-Host "Building ApiLens..." -ForegroundColor Yellow
    dotnet build ./Solutions/ApiLens.Cli/ApiLens.Cli.csproj --verbosity quiet
}

# Get the repository root (two levels up from Demos/advanced/)
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$apilens = Join-Path $repoRoot "Solutions/ApiLens.Cli/bin/Debug/net9.0/apilens"
if ($IsWindows) { $apilens += ".exe" }

# Create a realistic API documentation set
Write-Host "`n📚 Creating sample API documentation..." -ForegroundColor Yellow
$tmpBase = Join-Path ([System.IO.Path]::GetTempPath()) "apilens-demo"
$docsDir = Join-Path $tmpBase "docs/mcp-demo-docs"
New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

# E-commerce API documentation
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>ECommerce.Core</name></assembly>
    <members>
        <member name="T:ECommerce.Core.Models.Product">
            <summary>Represents a product in the e-commerce system.</summary>
        </member>
        <member name="P:ECommerce.Core.Models.Product.Id">
            <summary>Gets or sets the product identifier.</summary>
        </member>
        <member name="P:ECommerce.Core.Models.Product.Name">
            <summary>Gets or sets the product name.</summary>
        </member>
        <member name="P:ECommerce.Core.Models.Product.Price">
            <summary>Gets or sets the product price in USD.</summary>
        </member>
        <member name="T:ECommerce.Core.Services.IProductService">
            <summary>Defines operations for managing products.</summary>
        </member>
        <member name="M:ECommerce.Core.Services.IProductService.GetProductAsync(System.Guid)">
            <summary>Retrieves a product by its identifier.</summary>
            <param name="productId">The product identifier.</param>
            <returns>The product if found; otherwise, null.</returns>
        </member>
        <member name="M:ECommerce.Core.Services.IProductService.SearchProductsAsync(System.String)">
            <summary>Searches for products by name or description.</summary>
            <param name="searchTerm">The search term.</param>
            <returns>A list of matching products.</returns>
        </member>
        <member name="T:ECommerce.Core.Services.ProductService">
            <summary>
            Implementation of product management service.
            </summary>
            <seealso cref="T:ECommerce.Core.Services.IProductService"/>
        </member>
        <member name="T:ECommerce.Core.Cart.ShoppingCart">
            <summary>Represents a user's shopping cart.</summary>
            <seealso cref="T:System.Collections.Generic.IEnumerable`1"/>
        </member>
        <member name="M:ECommerce.Core.Cart.ShoppingCart.AddItem(ECommerce.Core.Models.Product,System.Int32)">
            <summary>Adds a product to the shopping cart.</summary>
            <param name="product">The product to add.</param>
            <param name="quantity">The quantity to add.</param>
            <exception cref="T:System.ArgumentNullException">Thrown when product is null.</exception>
            <exception cref="T:System.ArgumentOutOfRangeException">Thrown when quantity is less than 1.</exception>
        </member>
        <member name="M:ECommerce.Core.Cart.ShoppingCart.CalculateTotal">
            <summary>Calculates the total price of all items in the cart.</summary>
            <returns>The total price in USD.</returns>
        </member>
        <member name="T:ECommerce.Core.Orders.IOrderService">
            <summary>Defines operations for order processing.</summary>
        </member>
        <member name="M:ECommerce.Core.Orders.IOrderService.CreateOrderAsync(ECommerce.Core.Cart.ShoppingCart,System.String)">
            <summary>Creates a new order from a shopping cart.</summary>
            <param name="cart">The shopping cart to convert to an order.</param>
            <param name="userId">The user placing the order.</param>
            <returns>The created order.</returns>
            <exception cref="T:ECommerce.Core.Exceptions.EmptyCartException">Thrown when cart is empty.</exception>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/ECommerce.Core.xml"

# Payment processing documentation
@'
<?xml version="1.0"?>
<doc>
    <assembly><name>ECommerce.Payments</name></assembly>
    <members>
        <member name="T:ECommerce.Payments.IPaymentProcessor">
            <summary>Defines the contract for payment processing.</summary>
        </member>
        <member name="M:ECommerce.Payments.IPaymentProcessor.ProcessPaymentAsync(ECommerce.Payments.PaymentRequest)">
            <summary>Processes a payment request.</summary>
            <param name="request">The payment request details.</param>
            <returns>The payment result.</returns>
        </member>
        <member name="T:ECommerce.Payments.PaymentRequest">
            <summary>Contains payment request information.</summary>
        </member>
        <member name="P:ECommerce.Payments.PaymentRequest.Amount">
            <summary>Gets or sets the payment amount in USD.</summary>
        </member>
        <member name="P:ECommerce.Payments.PaymentRequest.CardNumber">
            <summary>Gets or sets the credit card number (encrypted).</summary>
        </member>
        <member name="T:ECommerce.Payments.StripePaymentProcessor">
            <summary>
            Processes payments using Stripe API.
            </summary>
            <seealso cref="T:ECommerce.Payments.IPaymentProcessor"/>
        </member>
        <member name="T:ECommerce.Payments.PayPalPaymentProcessor">
            <summary>
            Processes payments using PayPal API.
            </summary>
            <seealso cref="T:ECommerce.Payments.IPaymentProcessor"/>
        </member>
    </members>
</doc>
'@ | Set-Content "$docsDir/ECommerce.Payments.xml"

# Index the documentation
Write-Host "`nIndexing API documentation..." -ForegroundColor Yellow
& $apilens index $docsDir --clean --index $IndexPath | Out-Null
Write-Host "✅ Documentation indexed successfully!" -ForegroundColor Green

# Function to simulate MCP tool calls
function Invoke-MCPQuery {
    param(
        [string]$Query,
        [string]$QueryType = "name",
        [int]$MaxResults = 10
    )
    
    Write-Host "`n🤖 LLM Query:" -ForegroundColor Magenta
    Write-Host "   Tool: apilens.search" -ForegroundColor Gray
    Write-Host "   Parameters:" -ForegroundColor Gray
    Write-Host "     - query: '$Query'" -ForegroundColor Gray
    Write-Host "     - type: '$QueryType'" -ForegroundColor Gray
    Write-Host "     - max: $MaxResults" -ForegroundColor Gray
    
    # Show the actual CLI command
    $command = "apilens query '$Query' --type $QueryType --format json --max $MaxResults --index $IndexPath"
    Write-Host "`n💻 CLI Command:" -ForegroundColor DarkGray
    Write-Host "   $command" -ForegroundColor Yellow
    
    Write-Host "`n📤 Response:" -ForegroundColor Blue
    
    & $apilens query $Query --type $QueryType --format json --max $MaxResults --index $IndexPath
}

# Demonstrate LLM conversation scenarios
Write-Host "`n`n🎭 SCENARIO 1: User asks about shopping cart functionality" -ForegroundColor Yellow
Write-Host "User: 'How do I add items to a shopping cart in this e-commerce system?'" -ForegroundColor White

Write-Host "`nLLM thinks: I need to find shopping cart related APIs..." -ForegroundColor DarkGray
Invoke-MCPQuery -Query "ShoppingCart" -QueryType "name"

Write-Host "`nLLM thinks: Let me get more details about the AddItem method..." -ForegroundColor DarkGray
Invoke-MCPQuery -Query "M:ECommerce.Core.Cart.ShoppingCart.AddItem(ECommerce.Core.Models.Product,System.Int32)" -QueryType "id"

Write-Host "`n💬 LLM Response:" -ForegroundColor Green
Write-Host @"
Based on the API documentation, you can add items to a shopping cart using the `ShoppingCart.AddItem()` method:

```csharp
var cart = new ShoppingCart();
var product = // ... get product
cart.AddItem(product, quantity: 2);
```

Note: The method validates that:
- The product is not null (throws ArgumentNullException)
- The quantity is at least 1 (throws ArgumentOutOfRangeException)
"@

# Scenario 2
Write-Host "`n`n🎭 SCENARIO 2: User asks about payment processing options" -ForegroundColor Yellow
Write-Host "User: 'What payment processors are available?'" -ForegroundColor White

Write-Host "`nLLM thinks: I need to search for payment-related types..." -ForegroundColor DarkGray
Invoke-MCPQuery -Query "payment" -QueryType "content" -MaxResults 5

Write-Host "`nLLM thinks: Let me search for concrete payment processor implementations..." -ForegroundColor DarkGray
# Try searching with wildcards in content since name search requires exact match
Invoke-MCPQuery -Query "PaymentProcessor*" -QueryType "content"

Write-Host "`nLLM thinks: Since wildcard name searches aren't supported, let me try specific names..." -ForegroundColor DarkGray
Invoke-MCPQuery -Query "StripePaymentProcessor" -QueryType "name"

Write-Host "`n💬 LLM Response:" -ForegroundColor Green
Write-Host @"
Based on the API documentation, I found the payment processing system:

The system uses the **IPaymentProcessor** interface for payment processing, 
which includes a ProcessPaymentAsync(PaymentRequest) method.

I found a **StripePaymentProcessor** implementation that processes payments 
using the Stripe API. The search results suggest there may be other 
implementations as well (the pattern suggests a PayPalPaymentProcessor might 
exist too).

The modular design using the IPaymentProcessor interface allows you to:
- Support multiple payment providers
- Easily switch between providers
- Add new payment processors by implementing the interface
"@

# Scenario 3
Write-Host "`n`n🎭 SCENARIO 3: User asks about error handling" -ForegroundColor Yellow
Write-Host "User: 'What exceptions should I handle when creating orders?'" -ForegroundColor White

Write-Host "`nLLM thinks: I need to find order-related methods and their exceptions..." -ForegroundColor DarkGray
Invoke-MCPQuery -Query "CreateOrderAsync" -QueryType "name"

Write-Host "`nLLM thinks: Let me search for 'order' in the content to find related functionality..." -ForegroundColor DarkGray
Invoke-MCPQuery -Query "order" -QueryType "content" -MaxResults 5

Write-Host "`nLLM thinks: Let me get details about the IOrderService interface..." -ForegroundColor DarkGray
Invoke-MCPQuery -Query "T:ECommerce.Core.Orders.IOrderService" -QueryType "id"

Write-Host "`n💬 LLM Response:" -ForegroundColor Green
Write-Host @"
Based on the API documentation, when working with orders you should be aware of:

The **IOrderService.CreateOrderAsync** method is used to create orders from a shopping cart.
According to the documentation, this method can throw:

1. **EmptyCartException** - This is explicitly documented as being thrown when the cart is empty
2. **ArgumentNullException** - Standard validation when the cart parameter is null

Additionally, when working with the shopping cart before creating an order, be aware that:
- **ShoppingCart.AddItem** throws ArgumentNullException if the product is null
- **ShoppingCart.AddItem** throws ArgumentOutOfRangeException if quantity < 1

Example error handling:
```csharp
try {
    // Ensure cart has items
    if (!cart.HasItems) {
        throw new InvalidOperationException("Cannot create order from empty cart");
    }
    
    var order = await orderService.CreateOrderAsync(cart, userId);
} catch (EmptyCartException) {
    // Handle empty cart
} catch (ArgumentNullException) {
    // Handle null parameters
}
```
"@

# Scenario 4 - Specialized Commands for Advanced Analysis
Write-Host "`n`n🎭 SCENARIO 4: Using specialized commands for comprehensive API analysis" -ForegroundColor Yellow
Write-Host "User: 'I want to understand the complexity and usage patterns of this payment processing API'" -ForegroundColor White

Write-Host "`nLLM thinks: Let me use the specialized commands to get comprehensive insights..." -ForegroundColor DarkGray

Write-Host "`nStep 1: Find code examples to understand usage patterns" -ForegroundColor DarkGray
Invoke-MCPQuery -Query "ProcessPaymentAsync" -QueryType "name"

Write-Host "`nStep 2: Use examples command to find actual usage patterns" -ForegroundColor DarkGray
Write-Host "`n🤖 LLM Query:" -ForegroundColor Magenta
Write-Host "   Tool: apilens.examples" -ForegroundColor Gray
Write-Host "   Parameters:" -ForegroundColor Gray
Write-Host "     - pattern: 'payment'" -ForegroundColor Gray
Write-Host "     - max: 5" -ForegroundColor Gray

$command = "apilens examples 'payment' --format json --max 5 --index $IndexPath"
Write-Host "`n💻 CLI Command:" -ForegroundColor DarkGray
Write-Host "   $command" -ForegroundColor Yellow
Write-Host "`n📤 Response:" -ForegroundColor Blue
& $apilens examples "payment" --format json --max 5 --index $IndexPath

Write-Host "`nStep 3: Analyze exception handling requirements" -ForegroundColor DarkGray
Write-Host "`n🤖 LLM Query:" -ForegroundColor Magenta
Write-Host "   Tool: apilens.exceptions" -ForegroundColor Gray
Write-Host "   Parameters:" -ForegroundColor Gray
Write-Host "     - exception_type: 'ArgumentNullException'" -ForegroundColor Gray
Write-Host "     - details: true" -ForegroundColor Gray

$command = "apilens exceptions 'ArgumentNullException' --details --format json --index $IndexPath"
Write-Host "`n💻 CLI Command:" -ForegroundColor DarkGray
Write-Host "   $command" -ForegroundColor Yellow
Write-Host "`n📤 Response:" -ForegroundColor Blue
& $apilens exceptions "ArgumentNullException" --details --format json --index $IndexPath

Write-Host "`nStep 4: Assess API complexity for user guidance" -ForegroundColor DarkGray
Write-Host "`n🤖 LLM Query:" -ForegroundColor Magenta
Write-Host "   Tool: apilens.complexity" -ForegroundColor Gray
Write-Host "   Parameters:" -ForegroundColor Gray
Write-Host "     - min_params: 2" -ForegroundColor Gray
Write-Host "     - stats: true" -ForegroundColor Gray

$command = "apilens complexity --min-params 2 --stats --format json --index $IndexPath"
Write-Host "`n💻 CLI Command:" -ForegroundColor DarkGray
Write-Host "   $command" -ForegroundColor Yellow
Write-Host "`n📤 Response:" -ForegroundColor Blue
& $apilens complexity --min-params 2 --stats --format json --index $IndexPath

Write-Host "`n💬 LLM Response:" -ForegroundColor Green
Write-Host @"
Based on my comprehensive analysis using ApiLens specialized commands:

**API Usage Patterns:**
I found several code examples showing payment processing patterns. The API follows
a standard processor pattern with different implementations for Stripe and PayPal.

**Exception Handling:**
The payment system has robust error handling:
- ArgumentNullException for null payment requests
- Payment processors implement proper validation
- Consider handling provider-specific exceptions as well

**Complexity Analysis:**
- Most payment methods have 2-3 parameters (moderate complexity)
- The API is designed to be straightforward for developers
- Statistics show balanced complexity across the payment system

**Recommendations:**
1. Use the IPaymentProcessor interface for flexibility
2. Always handle ArgumentNullException in payment flows
3. The API complexity is moderate - suitable for intermediate developers
4. Consider the processor pattern for multiple payment providers
"@

# Show MCP configuration
Write-Host "`n`n📋 MCP Tool Configuration" -ForegroundColor Yellow  
Write-Host "Here's how ApiLens would be configured in an MCP server with all specialized commands:" -ForegroundColor Gray
Write-Host @"

{
  "tools": {
    "apilens": {
      "description": "Search and explore .NET API documentation with specialized analysis",
      "commands": {
        "search": {
          "description": "Search for .NET types, methods, and documentation",
          "parameters": {
            "query": { "type": "string", "required": true },
            "type": { 
              "type": "string", 
              "enum": ["name", "content", "namespace", "id", "assembly"],
              "default": "name"
            },
            "max": { "type": "integer", "default": 10 },
            "format": { "type": "string", "enum": ["table", "json", "markdown"], "default": "json" }
          }
        },
        "examples": {
          "description": "Find code examples and usage patterns",
          "parameters": {
            "pattern": { "type": "string", "required": false },
            "max": { "type": "integer", "default": 10 }
          }
        },
        "exceptions": {
          "description": "Find methods that throw specific exceptions",
          "parameters": {
            "exception_type": { "type": "string", "required": true },
            "details": { "type": "boolean", "default": false },
            "max": { "type": "integer", "default": 10 }
          }
        },
        "complexity": {
          "description": "Analyze method complexity and parameter counts",
          "parameters": {
            "min_complexity": { "type": "integer" },
            "min_params": { "type": "integer" },
            "max_params": { "type": "integer" },
            "stats": { "type": "boolean", "default": false },
            "max": { "type": "integer", "default": 20 }
          }
        }
      }
    }
  }
}
"@ -ForegroundColor DarkGray

Write-Host "`n✨ Demo Complete!" -ForegroundColor Cyan
Write-Host @"

Key Benefits for LLMs with Specialized Commands:
- 🔍 Fast, indexed search of API documentation with advanced Lucene syntax
- 📊 Structured JSON responses for easy parsing and automation
- 🔗 Cross-reference support to discover related types and hierarchies
- 📝 Complete documentation including parameters and exceptions
- 💡 Code examples command for learning API usage patterns
- ⚠️ Exception analysis for robust error handling guidance
- 📈 Complexity analysis for recommending appropriate APIs
- 🏗️ Comprehensive understanding of codebase architecture

This enables LLMs to:
- Answer questions about API usage with real code examples
- Generate correct, robust code with proper error handling
- Understand type relationships and design patterns
- Provide complexity-aware recommendations for different skill levels
- Analyze and explain existing codebases efficiently
- Guide users through API learning with targeted insights
- Navigate complex codebases with structured, semantic understanding
"@ -ForegroundColor Gray

# Cleanup
Write-Host "`n🧹 Cleanup Options" -ForegroundColor Yellow
Write-Host "Would you like to remove the demo files?" -ForegroundColor Gray
Write-Host "  - Demo index: $IndexPath" -ForegroundColor Gray
Write-Host "  - Sample docs: $docsDir" -ForegroundColor Gray
Write-Host "`nCleanup? (y/N): " -NoNewline -ForegroundColor Yellow
$cleanup = Read-Host
if ($cleanup -eq 'y') {
    Write-Host "`nCleaning up..." -ForegroundColor Gray
    
    # Clean up index
    if (Test-Path $IndexPath) {
        try {
            Remove-Item -Path $IndexPath -Recurse -Force -ErrorAction Stop
            Write-Host "✓ Demo index removed: $IndexPath" -ForegroundColor Green
        }
        catch {
            Write-Host "⚠ Could not remove index: $_" -ForegroundColor Yellow
        }
    }
    
    # Clean up docs
    if (Test-Path $docsDir) {
        try {
            Remove-Item -Path $docsDir -Recurse -Force -ErrorAction Stop
            Write-Host "✓ Sample docs removed: $docsDir" -ForegroundColor Green
        }
        catch {
            Write-Host "⚠ Could not remove docs: $_" -ForegroundColor Yellow
        }
    }
    
    Write-Host "`n✅ Cleanup complete" -ForegroundColor Green
}
else {
    Write-Host "`nDemo files retained for further exploration." -ForegroundColor Gray
}