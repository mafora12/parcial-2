using System;
using System.Collections.Generic;
using System.Linq;

namespace GameStoreExample
{
    public enum ItemCategory { Weapon, Armor, Accessory, Supply }

    public class Item
    {
        public string Name { get; }
        public ItemCategory Category { get; }
        public decimal Price { get; }

        public Item(string name, ItemCategory category, decimal price)
        {
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("El nombre del item es obligatorio.", nameof(name));
            if (price <= 0) throw new ArgumentException("El precio debe ser positivo.", nameof(price));
            Name = name.Trim();
            Category = category;
            Price = price;
        }

        public override string ToString() => $"{Name} [{Category}] - {Price}g";
        public override bool Equals(object? obj) =>
            obj is Item i && i.Name == Name && i.Category == Category && i.Price == Price;
        public override int GetHashCode() => HashCode.Combine(Name, Category, Price);
    }

    public class InventoryEntry
    {
        public Item Item { get; private set; }
        public int Quantity { get; private set; }

        public InventoryEntry(Item item, int quantity)
        {
            Item = item ?? throw new ArgumentNullException(nameof(item));
            Quantity = Math.Max(0, quantity);
        }

        public void Add(int qty) { if (qty > 0) Quantity += qty; }
        public void Subtract(int qty)
        {
            if (qty < 0) throw new ArgumentException("qty must be >= 0", nameof(qty));
            if (qty > Quantity) throw new InvalidOperationException("No hay suficiente cantidad para sustraer.");
            Quantity -= qty;
        }
    }

    // Resultado de operaciones para uso en UI/menús
    public class OperationResult
    {
        public bool Success { get; }
        public string Message { get; }

        public OperationResult(bool success, string message = "")
        {
            Success = success;
            Message = message ?? "";
        }

        public static OperationResult Ok(string msg = "") => new(true, msg);
        public static OperationResult Fail(string msg = "") => new(false, msg);
    }

    public class Store
    {

        private readonly Dictionary<string, InventoryEntry> _inventory =
            new(StringComparer.OrdinalIgnoreCase);

        public string Name { get; }

        public Store(string name, IEnumerable<(Item item, int qty)> initialInventory)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Store" : name.Trim();
            if (initialInventory != null)
            {
                foreach (var (it, q) in initialInventory)
                    AddOrUpdate(it, q);
            }

            if (!_inventory.Any(kv => kv.Value.Quantity > 0))
                throw new InvalidOperationException("La tienda debe iniciar con al menos un artículo con cantidad > 0.");
        }

        private static string MakeKey(string name, ItemCategory cat) =>
            $"{name.Trim()}|{cat}";


        public void AddOrUpdate(Item item, int qty)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            var key = MakeKey(item.Name, item.Category);

            if (_inventory.TryGetValue(key, out var existing))
            {
                if (existing.Item.Price != item.Price)
                    throw new InvalidOperationException($"Conflicto de precio para '{item.Name}' en categoría {item.Category}.");
                existing.Add(qty);
            }
            else
            {
                _inventory[key] = new InventoryEntry(item, Math.Max(0, qty));
            }
        }

        // API pública: obtener cantidad disponible
        public int GetQuantity(string name, ItemCategory category)
        {
            var key = MakeKey(name, category);
            return _inventory.TryGetValue(key, out var entry) ? entry.Quantity : 0;
        }

        // API pública: obtener una copia del inventario para mostrar en menús
        // Devuelve tuplas (Item, Quantity)
        public List<(Item item, int qty)> GetInventorySnapshot()
        {
            return _inventory.Values.Select(e => (e.Item, e.Quantity)).ToList();
        }

        // API pública: buscar un Item en la tienda por nombre y categoría (null si no existe)
        public Item? FindItem(string name, ItemCategory category)
        {
            var key = MakeKey(name, category);
            return _inventory.TryGetValue(key, out var entry) ? entry.Item : null;
        }

        // Compra atómica por lista de (Item, qty) — útil para menús que construyen un carrito
        public OperationResult TryPurchase(Player player, List<(Item item, int qty)> cart)
        {
            if (player == null) return OperationResult.Fail("Jugador inválido.");
            if (cart == null || cart.Count == 0) return OperationResult.Fail("Carrito vacío.");

            decimal total = 0m;
            var checks = new List<(string key, int qty, decimal price)>();

            foreach (var (it, qty) in cart)
            {
                if (it == null) return OperationResult.Fail("Item inválido en el carrito.");
                if (qty <= 0) return OperationResult.Fail("Cantidad inválida en el carrito.");

                var key = MakeKey(it.Name, it.Category);
                if (!_inventory.TryGetValue(key, out var entry)) return OperationResult.Fail($"El artículo '{it.Name}' no existe en la tienda.");
                if (entry.Quantity < qty) return OperationResult.Fail($"Cantidad insuficiente de '{it.Name}' en la tienda.");
                if (entry.Item.Price != it.Price) return OperationResult.Fail($"Precio inconsistente para '{it.Name}'.");
                total += it.Price * qty;
                checks.Add((key, qty, it.Price));
            }

            if (player.Gold < total) return OperationResult.Fail("Fondos insuficientes.");

            // Aplicar cambios
            player.Gold -= total;
            foreach (var (key, qty, price) in checks)
            {
                var entry = _inventory[key];
                entry.Subtract(qty);
                player.AddToInventory(entry.Item, qty);
            }

            return OperationResult.Ok($"Compra completada. Total: {total}g");
        }


        public OperationResult TryPurchaseByName(Player player, string name, ItemCategory category, int qty)
        {
            var item = FindItem(name, category);
            if (item == null) return OperationResult.Fail("Artículo no encontrado.");
            return TryPurchase(player, new List<(Item item, int qty)> { (item, qty) });
        }


        public OperationResult TryBuyFromPlayer(Player player, List<(Item item, int qty)> itemsToSell)
        {
            if (player == null) return OperationResult.Fail("Jugador inválido.");
            if (itemsToSell == null || itemsToSell.Count == 0) return OperationResult.Fail("Lista de venta vacía.");

            foreach (var (it, qty) in itemsToSell)
            {
                if (it == null) return OperationResult.Fail("Item inválido en la lista de venta.");
                if (qty <= 0) return OperationResult.Fail("Cantidad inválida en la lista de venta.");
                if (player.GetQuantity(it.Name, it.Category) < qty) return OperationResult.Fail($"No tienes suficientes '{it.Name}' para vender.");
            }

            foreach (var (it, qty) in itemsToSell)
            {
                var key = MakeKey(it.Name, it.Category);
                if (_inventory.TryGetValue(key, out var entry))
                {
                    if (entry.Item.Price != it.Price) return OperationResult.Fail($"La tienda no acepta '{it.Name}' por conflicto de precio.");
                }
            }

            decimal totalGain = 0m;
            foreach (var (it, qty) in itemsToSell) totalGain += it.Price * qty;

            foreach (var (it, qty) in itemsToSell)
            {
                bool removed = player.RemoveFromInventory(it, qty);
                if (!removed) return OperationResult.Fail("Error al quitar items del jugador.");
                try { AddOrUpdate(it, qty); }
                catch
                {

                    player.AddToInventory(it, qty);
                    return OperationResult.Fail("Conflicto al añadir a la tienda.");
                }
            }

            player.Gold += totalGain;
            return OperationResult.Ok($"Venta completada. Ganaste {totalGain}g");
        }

        public bool CanSellAnything() => _inventory.Any(kv => kv.Value.Quantity > 0);
    }

    public class Player
    {
        public string Name { get; }
        public decimal Gold { get; set; }

        private readonly Dictionary<string, InventoryEntry> _equipment =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, InventoryEntry> _supply =
            new(StringComparer.OrdinalIgnoreCase);

        public Player(string name, decimal startingGold = 0m)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Player" : name.Trim();
            Gold = Math.Max(0m, startingGold);
        }

        private static string MakeKey(string name, ItemCategory cat) =>
            $"{name.Trim()}|{cat}";

        private static bool IsConsumable(ItemCategory cat) => cat == ItemCategory.Supply;


        public void AddToInventory(Item item, int qty)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (qty <= 0) return;

            var key = MakeKey(item.Name, item.Category);
            var target = IsConsumable(item.Category) ? _supply : _equipment;

            if (target.TryGetValue(key, out var entry)) entry.Add(qty);
            else target[key] = new InventoryEntry(item, qty);
        }


        public bool RemoveFromInventory(Item item, int qty)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (qty <= 0) return false;

            var key = MakeKey(item.Name, item.Category);
            var target = IsConsumable(item.Category) ? _supply : _equipment;

            if (!target.TryGetValue(key, out var entry)) return false;
            if (entry.Quantity < qty) return false;

            entry.Subtract(qty);
            if (entry.Quantity == 0) target.Remove(key);
            return true;
        }


        public int GetQuantity(string name, ItemCategory category)
        {
            var key = MakeKey(name, category);
            var target = IsConsumable(category) ? _supply : _equipment;
            return target.TryGetValue(key, out var entry) ? entry.Quantity : 0;
        }


        public List<(Item item, int qty, string group)> GetInventorySnapshot()
        {
            var list = new List<(Item item, int qty, string group)>();
            list.AddRange(_equipment.Values.Select(e => (e.Item, e.Quantity, "Equipment")));
            list.AddRange(_supply.Values.Select(e => (e.Item, e.Quantity, "Supply")));
            return list;
        }
    }

    internal class Program
    {
        private static void Main()
        {
            try
            {

                var sword = new Item("Short Sword", ItemCategory.Weapon, 10m);
                var shield = new Item("Wooden Shield", ItemCategory.Armor, 8m);
                var ring = new Item("Ring of Luck", ItemCategory.Accessory, 25m);
                var potion = new Item("Health Potion", ItemCategory.Supply, 3m);

                var initial = new List<(Item item, int qty)>
                {
                    (sword, 5),
                    (shield, 2),
                    (potion, 10)
                };

                var store = new Store("Mercado del Pueblo", initial);
                var player = new Player("Miguel", startingGold: 50m);


                while (true)
                {
                    Console.WriteLine();
                    Console.WriteLine("=== MENÚ PRINCIPAL ===");
                    Console.WriteLine("1) Ver inventario de la tienda");
                    Console.WriteLine("2) Ver inventario del jugador");
                    Console.WriteLine("3) Comprar por nombre");
                    Console.WriteLine("4) Comprar con carrito ");
                    Console.WriteLine("5) Vender al comerciante ");
                    Console.WriteLine("0) Salir");
                    Console.Write("Elige una opción: ");
                    var opt = Console.ReadLine()?.Trim();

                    if (opt == "0") break;

                    switch (opt)
                    {
                        case "1":
                            Console.WriteLine($"Inventario de {store.Name}:");
                            foreach (var (it, q) in store.GetInventorySnapshot())
                                Console.WriteLine($"  {it.Name} [{it.Category}] - {it.Price}g x{q}");
                            break;

                        case "2":
                            Console.WriteLine($"{player.Name} - Oro: {player.Gold}g");
                            foreach (var (it, q, group) in player.GetInventorySnapshot())
                                Console.WriteLine($"  [{group}] {it.Name} [{it.Category}] - {it.Price}g x{q}");
                            break;

                        case "3":
                            Console.Write("Nombre del artículo: ");
                            var name = Console.ReadLine() ?? "";
                            Console.Write("Categoría (Weapon/Armor/Accessory/Supply): ");
                            var catStr = Console.ReadLine() ?? "";
                            if (!Enum.TryParse<ItemCategory>(catStr, true, out var cat))
                            {
                                Console.WriteLine("Categoría inválida.");
                                break;
                            }
                            Console.Write("Cantidad: ");
                            if (!int.TryParse(Console.ReadLine(), out var qty) || qty <= 0)
                            {
                                Console.WriteLine("Cantidad inválida.");
                                break;
                            }

                            var result = store.TryPurchaseByName(player, name, cat, qty);
                            Console.WriteLine(result.Success ? $"OK: {result.Message}" : $"Error: {result.Message}");
                            break;

                        case "4":

                            var cart = new List<(Item item, int qty)>();
                            var it1 = store.FindItem("Short Sword", ItemCategory.Weapon);
                            if (it1 != null) cart.Add((it1, 1));
                            var it2 = store.FindItem("Health Potion", ItemCategory.Supply);
                            if (it2 != null) cart.Add((it2, 2));
                            var resCart = store.TryPurchase(player, cart);
                            Console.WriteLine(resCart.Success ? $"OK: {resCart.Message}" : $"Error: {resCart.Message}");
                            break;

                        case "5":

                            var sellItem = new Item("Short Sword", ItemCategory.Weapon, 10m);
                            if (player.GetQuantity(sellItem.Name, sellItem.Category) > 0)
                            {
                                var sellRes = store.TryBuyFromPlayer(player, new List<(Item item, int qty)> { (sellItem, 1) });
                                Console.WriteLine(sellRes.Success ? $"OK: {sellRes.Message}" : $"Error: {sellRes.Message}");
                            }
                            else
                            {
                                Console.WriteLine("No tienes Short Sword para vender en este ejemplo.");
                            }
                            break;

                        default:
                            Console.WriteLine("Opción no válida.");
                            break;
                    }
                }

                Console.WriteLine("Saliendo. ¡Hasta luego!");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Excepción no controlada: " + ex);
            }
        }
    }
}
