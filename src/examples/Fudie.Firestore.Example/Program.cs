using Fudie.Firestore.EntityFrameworkCore.Infrastructure;
using Fudie.Firestore.Example;
using Fudie.Firestore.Example.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// =============================================================================
// HOST CONFIGURATION WITH DEPENDENCY INJECTION
// =============================================================================

var builder = Host.CreateApplicationBuilder(args);

// Register DbContext - same pattern as UseSqlServer, UseNpgsql, etc.
// All configuration is read from appsettings.json "Firestore" section
builder.Services.AddDbContext<ExampleDbContext>((sp, options) =>
{
    options.UseFirestore(sp);
    options.LogTo(Console.WriteLine, LogLevel.Information, DbContextLoggerOptions.None);
});

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
        // CREATE
        await CreateEntitiesAsync();

        // READ
        await ReadEntitiesAsync();

        // UPDATE
        await UpdateEntitiesAsync();

        // DELETE
        await DeleteEntitiesAsync();
    }

    private async Task CreateEntitiesAsync()
    {
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

    }

    private async Task ReadEntitiesAsync()
    {
        // Query with Include (SubCollection)
        var stores = await context.Stores
            .Include(s => s.Products)
            .ToListAsync();

        // Query with Where (boolean filter)
        var activeStores = await context.Stores
            .Where(s => s.IsActive)
            .ToListAsync();
    }

    private async Task UpdateEntitiesAsync()
    {
        var store = await context.Stores.FirstOrDefaultAsync();
        if (store != null)
        {

            store.Phone = "+1-555-9999";
            store.Address.Street = "456 Broadway";

            store.OpeningHours[0].OpenTime = "10:00";


            /*var state = context.Entry(store).State;

            var entry = context.Entry(store);

            var changes = entry.Properties
                .Where(p => p.IsModified)
                .Select(p => new { p.Metadata.Name, p.OriginalValue, p.CurrentValue })
                .ToList();

            var complexChanges = entry.ComplexProperties
            .SelectMany(cp => cp.Properties
                .Where(p => p.IsModified)
                .Select(p => new
                {
                    Path = $"{cp.Metadata.Name}.{p.Metadata.Name}",
                    p.OriginalValue,
                    p.CurrentValue
                }))
            .ToList();*/


            
            await context.SaveChangesAsync();

            // Verify update by reading again
            var updatedStore = await context.Stores.FirstOrDefaultAsync(s => s.Id == store.Id);
        }
    }

    private async Task DeleteEntitiesAsync()
    {
        // Delete stores (cascade deletes products in SubCollection)
        var stores = await context.Stores
             .Include(s => s.Products)
             .ToListAsync();

        context.Stores.RemoveRange(stores);
        await context.SaveChangesAsync();


        // Delete categories 
        var categories = await context.Categories.ToListAsync();
        context.Categories.RemoveRange(categories);
        await context.SaveChangesAsync();
    }
}
