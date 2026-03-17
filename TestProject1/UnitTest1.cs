using NUnit.Framework;
using GameStoreExample;
using System;
using System.Collections.Generic;

namespace GameStoreTests
{
    [TestFixture]
    public class ItemTests
    {
        [Test]
        public void CreateItem_ValidData_ShouldWork()
        {
            // En C#, los decimales deben llevar una 'm' al final
            var item = new Item("Sword", ItemCategory.Weapon, 10m);

            Assert.AreEqual("Sword", item.Name);
            Assert.AreEqual(ItemCategory.Weapon, item.Category);
            Assert.AreEqual(10m, item.Price);
        }

        [Test]
        public void CreateItem_InvalidPrice_ShouldThrow()
        {
            // El constructor de Item lanza ArgumentException si el precio es <= 0
            Assert.Throws<ArgumentException>(() =>
                new Item("Sword", ItemCategory.Weapon, 0m));
        }

        [Test]
        public void CreateItem_EmptyName_ShouldThrow()
        {
            Assert.Throws<ArgumentException>(() =>
                new Item("", ItemCategory.Armor, 5m));
        }
    }

    [TestFixture]
    public class StoreCreationTests
    {
        [Test]
        public void CreateStore_WithValidInventory_ShouldWork()
        {
            var sword = new Item("Sword", ItemCategory.Weapon, 10m);

            var store = new Store("Test Store", new List<(Item item, int qty)>
            {
                (sword, 5)
            });

            Assert.AreEqual(5, store.GetQuantity("Sword", ItemCategory.Weapon));
        }

        [Test]
        public void CreateStore_WithoutStock_ShouldThrow()
        {
            var sword = new Item("Sword", ItemCategory.Weapon, 10m);

            // La tienda exige al menos un artículo con cantidad > 0 al inicio
            Assert.Throws<InvalidOperationException>(() =>
                new Store("Empty", new List<(Item item, int qty)>
                {
                    (sword, 0)
                }));
        }
    }

    [TestFixture]
    public class PlayerTests
    {
        [Test]
        public void CreatePlayer_WithGold_ShouldWork()
        {
            var player = new Player("Hero", 100m);
            Assert.AreEqual(100m, player.Gold);
        }

        [Test]
        public void Player_GoldCannotBeNegative()
        {
            // Tu constructor usa Math.Max(0, startingGold), así que -50 se vuelve 0
            var player = new Player("Hero", -50m);
            Assert.AreEqual(0m, player.Gold);
        }
    }

    [TestFixture]
    public class PurchaseTests
    {
        [Test]
        public void BuyItem_ShouldReduceGoldAndStock()
        {
            var sword = new Item("Sword", ItemCategory.Weapon, 10m);
            var store = new Store("Shop", new List<(Item item, int qty)> { (sword, 5) });
            var player = new Player("Hero", 100m);

            var result = store.TryPurchase(player, new List<(Item item, int qty)> { (sword, 2) });

            Assert.IsTrue(result.Success);
            Assert.AreEqual(80m, player.Gold); // 100 - (10 * 2)
            Assert.AreEqual(3, store.GetQuantity("Sword", ItemCategory.Weapon));
        }

        [Test]
        public void BuyItem_WithoutEnoughGold_ShouldFail()
        {
            var sword = new Item("Sword", ItemCategory.Weapon, 50m);
            var store = new Store("Shop", new List<(Item item, int qty)> { (sword, 5) });
            var player = new Player("Hero", 10m);

            var result = store.TryPurchase(player, new List<(Item item, int qty)> { (sword, 1) });

            Assert.IsFalse(result.Success);
            Assert.AreEqual("Fondos insuficientes.", result.Message);
        }

        [Test]
        public void BuyFromDifferentStores_ShouldWork()
        {
            var sword = new Item("Sword", ItemCategory.Weapon, 10m);
            var potion = new Item("Potion", ItemCategory.Supply, 5m);

            var store1 = new Store("Shop1", new List<(Item item, int qty)> { (sword, 5) });
            var store2 = new Store("Shop2", new List<(Item item, int qty)> { (potion, 5) });
            var player = new Player("Hero", 100m);

            store1.TryPurchase(player, new List<(Item item, int qty)> { (sword, 2) }); // -20g
            store2.TryPurchase(player, new List<(Item item, int qty)> { (potion, 4) }); // -20g

            Assert.AreEqual(60m, player.Gold);
        }
    }

    [TestFixture]
    public class InventoryUpdateTests
    {
        [Test]
        public void BoughtItem_ShouldAppearInCorrectGroup()
        {
            var potion = new Item("Potion", ItemCategory.Supply, 5m);
            var store = new Store("Shop", new List<(Item item, int qty)> { (potion, 5) });
            var player = new Player("Hero", 100m);

            store.TryPurchase(player, new List<(Item item, int qty)> { (potion, 2) });

            // GetQuantity ya busca internamente en el diccionario correcto (Supply)
            var qty = player.GetQuantity("Potion", ItemCategory.Supply);

            Assert.AreEqual(2, qty);
        }
    }
}