using Nexum.Examples.Batching.Domain;

namespace Nexum.Examples.Batching.Data;

/// <summary>
/// In-memory product store seeded with 20 products.
/// Simulates a database that a real batch handler would query.
/// </summary>
public sealed class ProductStore
{
    private static readonly IReadOnlyDictionary<int, Product> s_products = BuildStore();

    public static IReadOnlyDictionary<int, Product> All => s_products;

    private static Dictionary<int, Product> BuildStore()
    {
        Product[] items =
        [
            new(1, "Wireless Keyboard", 49.99m),
            new(2, "Optical Mouse", 29.99m),
            new(3, "USB-C Hub", 39.99m),
            new(4, "27\" Monitor", 349.99m),
            new(5, "Laptop Stand", 24.99m),
            new(6, "Webcam HD 1080p", 79.99m),
            new(7, "Headset Stereo", 59.99m),
            new(8, "Mechanical Keyboard", 129.99m),
            new(9, "Trackpad", 89.99m),
            new(10, "External SSD 1TB", 109.99m),
            new(11, "HDMI Cable 2m", 9.99m),
            new(12, "USB-C Cable 1m", 7.99m),
            new(13, "Power Strip 6-outlet", 19.99m),
            new(14, "Desk Lamp LED", 34.99m),
            new(15, "Notebook A5", 4.99m),
            new(16, "Whiteboard Markers", 8.99m),
            new(17, "Ergonomic Chair", 299.99m),
            new(18, "Standing Desk", 499.99m),
            new(19, "Cable Management Kit", 14.99m),
            new(20, "Screen Cleaning Kit", 6.99m),
        ];

        return items.ToDictionary(p => p.Id);
    }
}
