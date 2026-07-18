# EtlAnalytics.RulesEngine 🚀

Welcome to the **EtlAnalytics.RulesEngine**! If you're new to the concept of a "Rules Engine," think of this library as a way to let your users (or yourself) change how your program behaves without having to rewrite or redeploy the whole application.

---

## 💡 What is a Rules Engine?

Imagine you are building a pizza delivery app. Usually, you might hard-code a rule like:
`if (orderTotal > 50) { applyDiscount = true; }`

But what if you want to change that limit to $60 tomorrow? Or offer a special discount only for a specific city? Instead of changing your C# code and restarting your server, you can store these instructions as **Rules** in a database. This library is the "engine" that reads those instructions and makes them happen.

---

## 📦 Flexible Storage Options

This library is built with flexibility in mind. You are not locked into any specific way of storing your rules. By implementing the `IBusinessRuleStore` interface, you can source your rules from anywhere:

*   **Relational Databases**: Store rules in SQL Server, PostgreSQL, or MySQL for dynamic, real-time updates.
*   **Static Files**: Use XML or JSON files to keep rules alongside your source code for version control.
*   **Centralized APIs**: Fetch rule definitions from a remote web service to share logic across multiple microservices.
*   **In-Memory/Hardcoded**: For testing or fixed logic, you can even store rules in a simple C# list.

---

## 🌍 Multi-Database Support

The engine is designed to be database-agnostic. While it defaults to SQL Server, you can use any database that has a .NET driver (PostgreSQL, MySQL, SQLite, etc.).

### 1. Connection Provider Examples

To switch databases, you just need to implement `IRuleDbProvider` for your chosen driver.

#### **SQL Server (Microsoft.Data.SqlClient)**
```csharp
public class SqlServerRuleDbProvider : IRuleDbProvider
{
    public IDbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);
}
```

#### **PostgreSQL (Npgsql)**
```csharp
public class PostgresRuleDbProvider : IRuleDbProvider
{
    public IDbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);
}
```

#### **MySQL (MySqlConnector)**
```csharp
public class MySqlRuleDbProvider : IRuleDbProvider
{
    public IDbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);
}
```

### 2. Registering in your Application
```csharp
// Register the provider for your specific database
builder.Services.AddScoped<IRuleDbProvider, PostgresRuleDbProvider>();
```

---

## 📊 Cross-Database SQL Syntax

When writing **T-SQL** rules that use **Piping** (receiving data from a previous rule), the syntax for reading the `@PreviousResultJson` parameter varies by database.

| Database | Parameter Prefix | JSON Extraction Example |
| :--- | :--- | :--- |
| **SQL Server** | `@` | `CROSS APPLY OPENJSON(@PreviousResultJson) WITH (Val INT '$.Status')` |
| **PostgreSQL** | `:` | `SELECT * FROM table WHERE data ->> 'Status' = :PreviousResultJson` |
| **MySQL** | `?` | `SELECT * FROM table WHERE JSON_EXTRACT(?PreviousResultJson, '$.Status') = 1` |

### Database-Specific Rule Examples

#### **SQL Server Example**
```sql
SELECT TOP 1 * FROM Discounts 
CROSS APPLY OPENJSON(@PreviousResultJson) WITH (CustomerType NVARCHAR(50)) p
WHERE p.CustomerType = 'VIP'
```

#### **PostgreSQL Example**
```sql
SELECT * FROM "Discounts" 
WHERE "CustomerType" = CAST(:PreviousResultJson->>'CustomerType' AS TEXT)
LIMIT 1;
```

#### **MySQL Example**
```sql
SELECT * FROM Discounts 
WHERE CustomerType = JSON_UNQUOTE(JSON_EXTRACT(?PreviousResultJson, '$.CustomerType'))
LIMIT 1;
```

---

## 🛠️ Key Concepts to Learn

Before you start coding, here are the four main parts of this engine:

1.  **Business Rule**: A single piece of logic written in **T-SQL** (for database queries) or **C# Script** (for logic).
2.  **Rule Bundle**: A "playlist" of rules. They run one after another in a specific order.
3.  **Context**: A box of data you give to the engine so the rules can see it (e.g., "The current list of orders").
4.  **Store**: The place where your rules are saved (usually your SQL database).

---

## 🚀 Quick Start Guide (The 5-Step Setup)

### 1. Install the Library
Add the project reference to your application:
```bash
dotnet add reference EtlAnalytics.RulesEngine.csproj
```

### 2. Prepare your "Data Box" (Context)
The engine needs to know what data your rules should work with. Create a class that inherits from `RuleExecutionContext`.

```csharp
// This is your custom 'Context'. Everything inside here is visible to your rules.
public class PizzaAppContext : RuleExecutionContext
{
    public double OrderTotal { get; set; }
    public string CustomerCity { get; set; }
}
```

### 3. Setup Your Database (The Store)
The engine needs to find your rules in a database. Here is the recommended SQL schema to store your Rules and Bundles.

#### **SQL Server Schema**
```sql
CREATE TABLE dbo.BusinessRules (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    RuleType NVARCHAR(50) NOT NULL, -- 'TSQL' or 'CSharp'
    Code NVARCHAR(MAX) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE dbo.BusinessRuleBundles (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    Name NVARCHAR(255) NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1
);

CREATE TABLE dbo.BusinessRuleBundleItems (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    BundleId INT NOT NULL,
    RuleId INT NOT NULL,
    SequenceOrder INT NOT NULL,
    CONSTRAINT FK_BundleItems_Bundle FOREIGN KEY (BundleId) REFERENCES dbo.BusinessRuleBundles(Id) ON DELETE CASCADE
);
```

#### **PostgreSQL Schema**
```sql
CREATE TABLE BusinessRules (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    RuleType VARCHAR(50) NOT NULL,
    Code TEXT NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE BusinessRuleBundles (
    Id SERIAL PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE BusinessRuleBundleItems (
    Id SERIAL PRIMARY KEY,
    BundleId INT NOT NULL REFERENCES BusinessRuleBundles(Id) ON DELETE CASCADE,
    RuleId INT NOT NULL REFERENCES BusinessRules(Id),
    SequenceOrder INT NOT NULL
);
```

#### **MySQL Schema**
```sql
CREATE TABLE BusinessRules (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    Description TEXT NULL,
    RuleType VARCHAR(50) NOT NULL,
    Code TEXT NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE BusinessRuleBundles (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    Name VARCHAR(255) NOT NULL,
    IsActive BOOLEAN NOT NULL DEFAULT TRUE
);

CREATE TABLE BusinessRuleBundleItems (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    BundleId INT NOT NULL,
    RuleId INT NOT NULL,
    SequenceOrder INT NOT NULL,
    FOREIGN KEY (BundleId) REFERENCES BusinessRuleBundles(Id) ON DELETE CASCADE,
    FOREIGN KEY (RuleId) REFERENCES BusinessRules(Id)
);
```

#### Implementing IBusinessRuleStore with Dapper
You can make your store database-agnostic by using the same `IRuleDbProvider` you created earlier. This allows the same Store code to work for SQL Server, PostgreSQL, or MySQL.

```csharp
public class AppRuleStore : IBusinessRuleStore
{
    private readonly string _connectionString;
    private readonly IRuleDbProvider _dbProvider;

    public AppRuleStore(IConfiguration config, IRuleDbProvider dbProvider)
    {
        _connectionString = config.GetConnectionString("DefaultConnection");
        _dbProvider = dbProvider;
    }

    public async Task<BusinessRule?> GetBusinessRuleByIdAsync(int id)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        // Note: Use the parameter prefix correct for your DB (@ for SQL Server, : for Postgres)
        return await db.QueryFirstOrDefaultAsync<BusinessRule>(
            "SELECT * FROM BusinessRules WHERE Id = @Id", new { Id = id });
    }

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        using var db = _dbProvider.CreateConnection(_connectionString);
        var bundle = await db.QueryFirstOrDefaultAsync<BusinessRuleBundle>(
            "SELECT * FROM BusinessRuleBundles WHERE Name = @Name", new { Name = name });

        if (bundle != null)
        {
            var items = await db.QueryAsync<BusinessRuleBundleItem>(
                "SELECT * FROM BusinessRuleBundleItems WHERE BundleId = @Id ORDER BY SequenceOrder", 
                new { Id = bundle.Id });
            bundle.Items = items.ToList();
        }
        return bundle;
    }
}
```

#### **Option B: Storing Rules in XML (File-based)**
If you prefer to keep your rules in a file (for version control or simple projects), you can implement a Store that reads from XML.

```csharp
public class XmlRuleStore : IBusinessRuleStore
{
    private readonly List<BusinessRule> _rules;
    private readonly List<BusinessRuleBundle> _bundles;

    public XmlRuleStore(string filePath)
    {
        var doc = XDocument.Load(filePath);
        // Load rules and bundles using LINQ to XML or XmlSerializer
        _rules = doc.Descendants("Rule").Select(r => new BusinessRule { ... }).ToList();
        _bundles = doc.Descendants("Bundle").Select(b => new BusinessRuleBundle { ... }).ToList();
    }

    public Task<BusinessRule?> GetBusinessRuleByIdAsync(int id) => 
        Task.FromResult(_rules.FirstOrDefault(r => r.Id == id));

    public Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name) => 
        Task.FromResult(_bundles.FirstOrDefault(b => b.Name == name));
}
```

#### **Option C: Sourcing Rules from an API (JSON or XML)**
You can also centralize your rules behind a web service. This is useful if you want multiple applications to share the same rule definitions.

##### **JSON Data Format**
The engine expects your API to return data in a structure like this:
```json
{
  "rules": [
    {
      "id": 101,
      "name": "CheckInventory",
      "ruleType": "CSharp",
      "code": "return Items.All(i => i.InStock);",
      "isActive": true
    }
  ],
  "bundles": [
    {
      "name": "OrderValidation",
      "items": [
        { "ruleId": 101, "sequenceOrder": 1 }
      ]
    }
  ]
}
```

##### **Implementing an API Store**
```csharp
public class ApiRuleStore : IBusinessRuleStore
{
    private readonly HttpClient _http;

    public ApiRuleStore(HttpClient http) => _http = http;

    public async Task<BusinessRule?> GetBusinessRuleByIdAsync(int id)
    {
        // Fetch from a JSON API
        return await _http.GetFromJsonAsync<BusinessRule>($"https://api.rules.com/rules/{id}");
    }

    public async Task<BusinessRuleBundle?> GetBusinessRuleBundleByNameAsync(string name)
    {
        // Or fetch from an XML API
        var xmlString = await _http.GetStringAsync($"https://api.rules.com/bundles?name={name}");
        var doc = XDocument.Parse(xmlString);
        return ParseBundleFromXml(doc);
    }
}
```

---

### 4. Register the Engine
In your `Program.cs` (or where you setup your services), tell your app how to use the engine:

```csharp
builder.Services.AddScoped<IBusinessRuleStore, AppRuleStore>();
builder.Services.AddScoped<BusinessRuleEngine<PizzaAppContext>>();
```

### 5. Run a Rule!
Now you can inject the engine into your classes and use it:

```csharp
public class CheckoutService
{
    private readonly BusinessRuleEngine<PizzaAppContext> _engine;

    public CheckoutService(BusinessRuleEngine<PizzaAppContext> engine) => _engine = engine;

    public async Task ProcessCheckout(double total, string city)
    {
        var myData = new PizzaAppContext { OrderTotal = total, CustomerCity = city };
        
        // Let's assume you have a rule in your DB named "CalculateDiscount"
        var rule = await _store.GetRuleByName("CalculateDiscount");
        
        // Execute the rule!
        var result = await _engine.ExecuteRuleAsync(rule, myData);
        
        Console.WriteLine($"Rule Result: {result}");
    }
}
```

---

## 📝 Writing Your First Rules

### The C# Script Rule
In your database, you might save a rule with this code:
```csharp
// You can use properties from your PizzaAppContext directly!
if (OrderTotal > 100) {
    Log("Big spender detected!"); // You can log messages to a watch window
    return 20.0; // Give them $20 off
}
return 0.0;
```

### The T-SQL Rule
If your rule needs to check the database, write a SQL script:
```sql
-- The engine automatically provides @PreviousResultJson
-- This allows you to use data from a previous rule in a bundle!
SELECT * FROM HolidayCoupons 
WHERE MinSpend <= @OrderTotal 
  AND City = @CustomerCity
```

---

## 🔄 Advanced Tip: Rule Piping (Connecting Rules)

One of the coolest features is **Piping**. When you run a **Bundle** (a sequence of rules), the result of Rule #1 is automatically handed to Rule #2 as `PreviousResult`.

**Example Bundle:**
1.  **Rule #1 (C#)**: Checks if user is a "VIP". Returns `true`.
2.  **Rule #2 (SQL)**: Receives `true`. Runs a special query that only VIPs can see.

---

## 🌳 Branching & Conditional Logic

You can create complex workflows by triggering different **Rule Bundles** based on logic within a rule. This is done using the `RunBundle` function available in C# rules.

### Example: The "Smart Discount" Workflow

Imagine you have two separate bundles:
- `HighValueBundle`: Contains 5 complex rules for big spenders.
- `StandardBundle`: Contains 2 simple rules for everyone else.

You can create a "Router" rule in C# to decide which one to run:

#### **Router Rule (C#)**
```csharp
// The 'RunBundle' function is built-in to the context!
if (OrderTotal > 500) {
    Log("Switching to High Value ruleset...");
    return await RunBundle("HighValueBundle");
} else {
    Log("Using standard ruleset.");
    return await RunBundle("StandardBundle");
}
```

### Conditional Execution in T-SQL

In T-SQL, you can use the result of a previous rule to filter your current query.

#### **Rule #2 (SQL)**
```sql
-- Assuming Rule #1 returned a boolean (true/false)
-- We can use OPENJSON to check that boolean before returning data
SELECT 
    CASE 
        WHEN p.Result = 1 THEN 'Eligible for Extra Points'
        ELSE 'Standard Points'
    END as Status
FROM OPENJSON(@PreviousResultJson) WITH (Result BIT '$') p
```

---

---

## ❓ Need Help?
- **Logs**: Always pass a logging action to `ExecuteRuleAsync` to see what's happening inside: `log => Console.WriteLine(log)`.
- **Errors**: If a C# rule has a typo, the engine will return a clear compilation error in the logs.

Happy coding! 🍕
