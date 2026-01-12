using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.Example;
using Fudie.Firestore.Example.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// =============================================================================
// HOST CONFIGURATION WITH DEPENDENCY INJECTION
// =============================================================================

// Configure Firestore Emulator (start with: firebase emulators:start)
Environment.SetEnvironmentVariable("FIRESTORE_EMULATOR_HOST", "127.0.0.1:8080");

var builder = Host.CreateApplicationBuilder(args);

// Register DbContext with Scoped lifetime (recommended for EF Core)
builder.Services.AddDbContext<ExampleDbContext>(options =>
    options.UseFirestore("demo-project"));

// Register the demo service
builder.Services.AddScoped<StoreService>();

var host = builder.Build();

// Run the demo
using var scope = host.Services.CreateScope();
var storeService = scope.ServiceProvider.GetRequiredService<StoreService>();
await storeService.RunDemoAsync();

// =============================================================================
// SERVICE THAT USES THE DBCONTEXT (TYPICAL PATTERN)
// =============================================================================

public class StoreService(ExampleDbContext context)
{
    public async Task RunDemoAsync()
    {
        Console.WriteLine("Fudie.Firestore.EntityFrameworkCore - Example\n");
        Console.WriteLine("Using Firestore Emulator at 127.0.0.1:8080\n");

        // CREATE
        await CreateEntitiesAsync();

        // READ
        await ReadEntitiesAsync();

        // UPDATE
        await UpdateEntitiesAsync();

        // DELETE
        await DeleteEntitiesAsync();

        Console.WriteLine("\n=== Example completed ===");
    }

    private async Task CreateEntitiesAsync()
    {
        Console.WriteLine("=== CREATE ===\n");

        // 1. Create a Category (Root Collection - target for References)
        var category = new Category
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Electronics",
            Description = "Electronic devices and accessories",
            IsActive = true
        };

        context.Categories.Add(category);
        await context.SaveChangesAsync();
        Console.WriteLine($"Created Category: {category.Name}");

        // 2. Create a Store with ComplexType and ArrayOf
        var store = new Store
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Tech Store Downtown",
            Email = "contact@techstore.com",
            Phone = "+1-555-0123",
            IsActive = true,

            // ComplexType - embedded object
            Address = new Address
            {
                Street = "123 Main Street",
                City = "New York",
                State = "NY",
                ZipCode = "10001",
                Country = "USA"
            },

            // ArrayOf ComplexTypes - array of embedded objects
            OpeningHours =
            [
                new OpeningHour { Day = DayOfWeek.Monday, OpenTime = "09:00", CloseTime = "18:00" },
                new OpeningHour { Day = DayOfWeek.Tuesday, OpenTime = "09:00", CloseTime = "18:00" },
                new OpeningHour { Day = DayOfWeek.Saturday, OpenTime = "10:00", CloseTime = "16:00" },
                new OpeningHour { Day = DayOfWeek.Sunday, IsClosed = true }
            ]
        };

        context.Stores.Add(store);
        await context.SaveChangesAsync();
        Console.WriteLine($"Created Store: {store.Name}");
        Console.WriteLine($"  Address (ComplexType): {store.Address.Street}, {store.Address.City}");
        Console.WriteLine($"  OpeningHours (ArrayOf): {store.OpeningHours.Count} days");

        // 3. Create a Product in SubCollection with Reference
        var product = new Product
        {
            Id = Guid.NewGuid().ToString(),
            Name = "Wireless Headphones",
            Description = "High-quality wireless headphones",
            Price = 149.99m,
            Stock = 50,
            IsAvailable = true,

            // Array of strings
            Tags = ["wireless", "audio", "bluetooth"],

            // Reference to Category
            Category = category
        };

        store.Products.Add(product);
        await context.SaveChangesAsync();
        Console.WriteLine($"Created Product (SubCollection): {product.Name}");
        Console.WriteLine($"  Tags (Array): [{string.Join(", ", product.Tags)}]");
        Console.WriteLine($"  Category (Reference): {category.Name}");
    }

    private async Task ReadEntitiesAsync()
    {
        Console.WriteLine("\n=== READ ===\n");

        // Query with Include (SubCollection)
        var stores = await context.Stores
            .Include(s => s.Products)
            .ToListAsync();

        foreach (var store in stores)
        {
            Console.WriteLine($"Store: {store.Name}");
            Console.WriteLine($"  Address: {store.Address.City}, {store.Address.Country}");
            Console.WriteLine($"  Products: {store.Products.Count}");

            foreach (var product in store.Products)
            {
                Console.WriteLine($"    - {product.Name}: ${product.Price}");
            }
        }

        // Query with Where
        var activeStores = await context.Stores
            .Where(s => s.IsActive)
            .ToListAsync();

        Console.WriteLine($"\nActive stores: {activeStores.Count}");
    }

    private async Task UpdateEntitiesAsync()
    {
        Console.WriteLine("\n=== UPDATE ===\n");

        var store = await context.Stores.FirstOrDefaultAsync();
        if (store != null)
        {
            var originalStreet = store.Address.Street;
            store.Address.Street = "456 Broadway";
            store.Phone = "+1-555-9999";
            await context.SaveChangesAsync();
            Console.WriteLine($"Updated Store address: {originalStreet} -> {store.Address.Street}");

            // Verify update by reading again
            var updatedStore = await context.Stores.FirstOrDefaultAsync(s => s.Id == store.Id);
            Console.WriteLine($"Verified from DB: {updatedStore?.Address.Street}");
        }
    }

    private async Task DeleteEntitiesAsync()
    {
        Console.WriteLine("\n=== DELETE ===\n");

        // Delete categories first (no dependencies)
        var categories = await context.Categories.ToListAsync();
        context.Categories.RemoveRange(categories);
        await context.SaveChangesAsync();
        Console.WriteLine($"Deleted {categories.Count} category(ies)");

        // Delete stores (cascade deletes products in SubCollection)
        var stores = await context.Stores
            .Include(s => s.Products)
            .ToListAsync();
        context.Stores.RemoveRange(stores);
        await context.SaveChangesAsync();
        Console.WriteLine($"Deleted {stores.Count} store(s)");
    }
}
