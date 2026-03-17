
// Program.cs
// Tienda para videojuego: compra y venta de objetos
// Compatible con .NET 7
// Copiar y pegar en un proyecto de consola y ejecutar.

using System;
using System.Collections.Generic;
using System.Linq;

namespace GameStoreExample
{
    // Categorías permitidas
    public enum ItemCategory { Weapon, Armor, Accessory, Supply }

    // Representa un artículo con nombre, categoría y precio positivo
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

    // Entrada de inventario en la tienda / jugador
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

    // La tienda: mantiene inventario y operaciones de compra/venta
    public class Store
    {
        // Usamos clave compuesta string "Name|Category" para permitir StringComparer.OrdinalIgnoreCase
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

            // Requisito: al inicio, cualquier tienda debe tener al menos un artículo para vender (qty > 0)
            if (!_inventory.Any(kv => kv.Value.Quantity > 0))
                throw new InvalidOperationException("La tienda debe iniciar con al menos un artículo con cantidad > 0.");
        }

        private static string MakeKey(string name, ItemCategory cat) =>
            $"{name.Trim()}|{cat}";

        // Añade o actualiza cantidad de un artículo en la tienda.
        // Si existe el mismo (name,category) con distinto precio -> lanza excepción.
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

        // Obtiene la cantidad disponible de un artículo (0 si no existe)
        public int GetQuantity(string name, ItemCategory category)
        {
            var key = MakeKey(name, category);
            return _inventory.TryGetValue(key, out var entry) ? entry.Quantity : 0;
        }

        // Intenta comprar (player compra desde la tienda) una lista de (Item, qty).
        // Operación atómica: si falla por fondos insuficientes o cantidades insuficientes, no cambia nada.
        // Devuelve true si la compra se completó.
        public bool TryPurchase(Player player, List<(Item item, int qty)> cart)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (cart == null || cart.Count == 0) return false;

            // Validaciones previas: existencia y cantidades
            decimal total = 0m;
            var checks = new List<(string key, int qty, decimal price)>();

            foreach (var (it, qty) in cart)
            {
                if (it == null) return false;
                if (qty <= 0) return false;

                var key = MakeKey(it.Name, it.Category);
                if (!_inventory.TryGetValue(key, out var entry)) return false; // no existe
                if (entry.Quantity < qty) return false; // cantidad insuficiente
                if (entry.Item.Price != it.Price) return false; // precio en tienda distinto al del item pasado (consistencia)
                total += it.Price * qty;
                checks.Add((key, qty, it.Price));
            }

            // Fondos del jugador
            if (player.Gold < total) return false;

            // Aplicar cambios: restar oro del jugador, añadir items al inventario del jugador, restar cantidades en tienda
            player.Gold -= total;

            foreach (var (key, qty, price) in checks)
            {
                var entry = _inventory[key];
                // restar cantidad en tienda
                entry.Subtract(qty);
                // añadir al inventario del jugador (usar el Item almacenado en la tienda para mantener la referencia correcta)
                player.AddToInventory(entry.Item, qty);
            }

            return true;
        }

        // El jugador vende artículos a la tienda.
        // Reglas:
        // - Si la tienda ya tiene el mismo (name,category) con precio distinto -> la tienda no acepta (devuelve false).
        // - Si la tienda no tiene el artículo, se añade con la cantidad vendida y con el precio del item que el jugador entrega.
        // - Se incrementa el oro del jugador por price * qty y se reduce el inventario del jugador.
        // - Operación atómica: si algo falla, no cambia nada.
        public bool TryBuyFromPlayer(Player player, List<(Item item, int qty)> itemsToSell)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (itemsToSell == null || itemsToSell.Count == 0) return false;

            // Validaciones: el jugador debe tener las cantidades que intenta vender
            foreach (var (it, qty) in itemsToSell)
            {
                if (it == null) return false;
                if (qty <= 0) return false;
                if (player.GetQuantity(it.Name, it.Category) < qty) return false;
            }

            // Validar conflicto de precio en tienda
            foreach (var (it, qty) in itemsToSell)
            {
                var key = MakeKey(it.Name, it.Category);
                if (_inventory.TryGetValue(key, out var entry))
                {
                    if (entry.Item.Price != it.Price)
                        return false; // la tienda no acepta porque ya vende el mismo name+category con distinto precio
                }
            }

            // Aplicar cambios: transferir cantidades y pagar al jugador
            decimal totalGain = 0m;
            foreach (var (it, qty) in itemsToSell)
            {
                totalGain += it.Price * qty;
            }

            // Realizar las operaciones: quitar del inventario del jugador y añadir a la tienda
            foreach (var (it, qty) in itemsToSell)
            {
                // quitar del jugador
                bool removed = player.RemoveFromInventory(it, qty);
                if (!removed)
                {
                    // Esto no debería ocurrir porque validamos antes, pero por seguridad revertimos (no hay cambios aplicados aún)
                    return false;
                }

                // añadir a la tienda (si existe, AddOrUpdate validará precio)
                try
                {
                    AddOrUpdate(it, qty);
                }
                catch
                {
                    // Si falla (por conflicto de precio), revertir: devolver los items al jugador
                    player.AddToInventory(it, qty);
                    return false;
                }
            }

            // Pagar al jugador
            player.Gold += totalGain;
            return true;
        }

        // Indica si la tienda puede vender algo (al menos un artículo con qty > 0)
        public bool CanSellAnything() => _inventory.Any(kv => kv.Value.Quantity > 0);

        // Para depuración: lista de inventario
        public IEnumerable<string> ListInventory()
        {
            foreach (var kv in _inventory.OrderBy(k => k.Key))
            {
                var entry = kv.Value;
                yield return $"{entry.Item.Name} [{entry.Item.Category}] - {entry.Item.Price}g x{entry.Quantity}";
            }
        }
    }

    // Jugador: oro y dos inventarios separados (Equipment y Supply)
    public class Player
    {
        public string Name { get; }
        public decimal Gold { get; set; }

        // Usamos clave compuesta string "Name|Category" para permitir StringComparer.OrdinalIgnoreCase
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

        // Añade items al inventario del jugador (separa por Equipment o Supply)
        public void AddToInventory(Item item, int qty)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (qty <= 0) return;

            var key = MakeKey(item.Name, item.Category);
            var target = IsConsumable(item.Category) ? _supply : _equipment;

            if (target.TryGetValue(key, out var entry))
            {
                // Si existe, simplemente sumar cantidad
                entry.Add(qty);
            }
            else
            {
                target[key] = new InventoryEntry(item, qty);
            }
        }

        // Quitar items del inventario del jugador. Devuelve true si se pudo quitar.
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

        // Obtener cantidad de un item en el inventario del jugador
        public int GetQuantity(string name, ItemCategory category)
        {
            var key = MakeKey(name, category);
            var target = IsConsumable(category) ? _supply : _equipment;
            return target.TryGetValue(key, out var entry) ? entry.Quantity : 0;
        }

        // Para depuración: listar inventario
        public IEnumerable<string> ListInventory()
        {
            foreach (var kv in _equipment.OrderBy(k => k.Key))
            {
                var entry = kv.Value;
                yield return $"[Equipment] {entry.Item.Name} [{entry.Item.Category}] - {entry.Item.Price}g x{entry.Quantity}";
            }
            foreach (var kv in _supply.OrderBy(k => k.Key))
            {
                var entry = kv.Value;
                yield return $"[Supply] {entry.Item.Name} [{entry.Item.Category}] - {entry.Item.Price}g x{entry.Quantity}";
            }
        }
    }

    // Ejemplo de uso y pruebas básicas
    internal class Program
    {
        private static void Main()
        {
            try
            {
                // Crear algunos items
                var sword = new Item("Short Sword", ItemCategory.Weapon, 10m);
                var shield = new Item("Wooden Shield", ItemCategory.Armor, 8m);
                var ring = new Item("Ring of Luck", ItemCategory.Accessory, 25m);
                var potion = new Item("Health Potion", ItemCategory.Supply, 3m);

                // Inventario inicial de la tienda (al menos un artículo con qty > 0)
                var initial = new List<(Item item, int qty)>
                {
                    (sword, 5),
                    (shield, 2),
                    (potion, 10)
                };

                var store = new Store("Mercado del Pueblo", initial);

                // Crear jugador
                var player = new Player("Miguel", startingGold: 50m);

                Console.WriteLine("Inventario inicial de la tienda:");
                foreach (var line in store.ListInventory()) Console.WriteLine("  " + line);
                Console.WriteLine();

                Console.WriteLine($"{player.Name} tiene {player.Gold}g");
                Console.WriteLine("Compra: 1 Short Sword, 3 Health Potion (compra múltiple en una sola transacción)");

                // Preparar carrito
                var cart = new List<(Item item, int qty)>
                {
                    (sword, 1),
                    (potion, 3)
                };

                bool bought = store.TryPurchase(player, cart);
                Console.WriteLine(bought ? "Compra completada." : "Compra fallida.");

                Console.WriteLine($"{player.Name} ahora tiene {player.Gold}g");
                Console.WriteLine("Inventario del jugador:");
                foreach (var line in player.ListInventory()) Console.WriteLine("  " + line);

                Console.WriteLine();
                Console.WriteLine("Inventario de la tienda después de la compra:");
                foreach (var line in store.ListInventory()) Console.WriteLine("  " + line);

                // Intento de compra con fondos insuficientes (compra atómica)
                Console.WriteLine();
                Console.WriteLine("Intento de compra con fondos insuficientes: 2 Ring of Luck (precio 25g cada uno)");
                var cart2 = new List<(Item item, int qty)> { (ring, 2) }; // total 50g
                bool bought2 = store.TryPurchase(player, cart2);
                Console.WriteLine(bought2 ? "Compra completada." : "Compra fallida por fondos o disponibilidad.");
                Console.WriteLine($"{player.Name} tiene {player.Gold}g (no debe haber cambiado si la compra falló)");

                // Venta del jugador a la tienda
                Console.WriteLine();
                Console.WriteLine("El jugador vende 1 Short Sword a la tienda.");
                var sellList = new List<(Item item, int qty)> { (sword, 1) };
                bool sold = store.TryBuyFromPlayer(player, sellList);
                Console.WriteLine(sold ? "Venta completada." : "Venta fallida.");
                Console.WriteLine($"{player.Name} ahora tiene {player.Gold}g");
                Console.WriteLine("Inventario del jugador:");
                foreach (var line in player.ListInventory()) Console.WriteLine("  " + line);
                Console.WriteLine("Inventario de la tienda:");
                foreach (var line in store.ListInventory()) Console.WriteLine("  " + line);

                // Intento de añadir un artículo con mismo name+category pero distinto precio -> debe lanzar excepción
                Console.WriteLine();
                Console.WriteLine("Intento de añadir a la tienda un 'Short Sword' con precio distinto (conflicto de precio).");
                var swordDifferentPrice = new Item("Short Sword", ItemCategory.Weapon, 12m);
                try
                {
                    store.AddOrUpdate(swordDifferentPrice, 1);
                    Console.WriteLine("Se añadió (esto no debería ocurrir).");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error esperado al añadir: " + ex.Message);
                }

                // Agregar un nuevo artículo (ring) a la tienda
                Console.WriteLine();
                Console.WriteLine("Añadiendo Ring of Luck a la tienda (cantidad 1).");
                store.AddOrUpdate(ring, 1);
                foreach (var line in store.ListInventory()) Console.WriteLine("  " + line);

                // Intento de comprar todo el inventario de un artículo y luego comprobar que no se puede comprar más
                Console.WriteLine();
                Console.WriteLine("Comprando todas las Health Potion restantes (cantidad igual a la disponible).");
                int potionsAvailable = store.GetQuantity("Health Potion", ItemCategory.Supply);
                var buyAllPotions = new List<(Item item, int qty)> { (potion, potionsAvailable) };
                bool boughtAll = store.TryPurchase(player, buyAllPotions);
                Console.WriteLine(boughtAll ? "Compra completada." : "Compra fallida.");
                Console.WriteLine("Cantidad de Health Potion en tienda ahora: " + store.GetQuantity("Health Potion", ItemCategory.Supply));
                Console.WriteLine("Intentando comprar 1 Health Potion más (debe fallar):");
                var buyOneMore = new List<(Item item, int qty)> { (potion, 1) };
                bool boughtMore = store.TryPurchase(player, buyOneMore);
                Console.WriteLine(boughtMore ? "Compra completada (ERROR)." : "Compra fallida como se esperaba.");

                // Comprobar si la tienda puede vender algo
                Console.WriteLine();
                Console.WriteLine("¿La tienda puede vender algo? " + (store.CanSellAnything() ? "Sí" : "No"));

                Console.WriteLine();
                Console.WriteLine("Fin de la demostración.");
            }
            catch (Exception ex)
            {
                Console.WriteLine("Excepción no controlada: " + ex);
            }
        }
    }
}