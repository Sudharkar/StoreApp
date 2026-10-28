using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.EntityFrameworkCore;
using Store.Domain.Entities;
using Store.Domain.Enums;

namespace Store.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(StoreDbContext db)
    {
        // Run pending migrations (creates the DB if it does not exist)
        await db.Database.MigrateAsync();

        if (!await db.Users.AnyAsync())
        {
            db.Users.AddRange(
                new User
                {
                    FullName = "Admin User",
                    Email = "admin@store.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123"),
                    Role = UserRole.Admin
                },
                new User
                {
                    FullName = "Customer One",
                    Email = "customer1@store.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                    Role = UserRole.Customer
                },
                new User
                {
                    FullName = "Customer Two",
                    Email = "customer2@store.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                    Role = UserRole.Customer
                },
                new User
                {
                    FullName = "Customer Three",
                    Email = "customer3@store.com",
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword("Customer@123"),
                    Role = UserRole.Customer
                });
        }

        if (!await db.Products.AnyAsync())
        {
            db.Products.AddRange(
                P("SKU-001", "Wireless Mouse", 799m, 50),
                P("SKU-002", "Mechanical Keyboard", 3499m, 30),
                P("SKU-003", "USB-C Hub", 1499m, 40),
                P("SKU-004", "27-inch Monitor", 15999m, 10),
                P("SKU-005", "Laptop Stand", 1299m, 25),
                P("SKU-006", "Webcam HD", 2499m, 20),
                P("SKU-007", "Noise Cancelling Headphones", 8999m, 15),
                P("SKU-008", "Bluetooth Speaker", 2999m, 35),
                P("SKU-009", "External SSD 1TB", 7499m, 12),
                P("SKU-010", "Power Bank 20000mAh", 1999m, 60),
                P("SKU-011", "Gaming Chair", 12999m, 5),
                P("SKU-012", "Desk Lamp", 899m, 45),
                P("SKU-013", "HDMI Cable 2m", 349m, 100),
                P("SKU-014", "Smart Watch", 5999m, 18),
                P("SKU-015", "Limited Edition Keycaps", 1999m, 1)); // last-unit item for the concurrency test
        }

        await db.SaveChangesAsync();
    }

    private static Product P(string sku, string name, decimal price, int stock) => new()
    {
        Sku = sku,
        Name = name,
        Price = price,
        StockQuantity = stock,
        IsActive = true
    };
}
