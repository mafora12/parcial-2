using NUnit.Framework;
using GameStore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GameStore.Tests
{
    [TestFixture]
    public class GameStoreTests
    {
        private Item sword;
        private Item shield;
        private Item ring;
        private Item potion;

        [SetUp]
        public void Setup()
        {
            sword = new Item("Short Sword", ItemCategory.Weapon, 10m);
            shield = new Item("Wooden Shield", ItemCategory.Armor, 8m);
            ring = new Item("Ring of Luck", ItemCategory.Accessory, 25m);
            potion = new Item("Health Potion", ItemCategory.Supply, 3m);
        }

        [Test]
        public void Item_Creation_Valid()
        {
            var it = new Item("Bow", ItemCategory.Weapon, 15m);
            Assert.That(it.Name, Is.EqualTo("Bow"));
        }

        [Test]
        public void Item_Creation_InvalidName()
        {
            Assert.Throws<ArgumentException>(() =>
                new Item("", ItemCategory.Weapon, 5m));
        }

        [Test]
        public void Item_Creation_InvalidPrice()
        {
            Assert.Throws<ArgumentException>(() =>
                new Item("Bad", ItemCategory.Supply, 0m));
        }

        [Test]
        public void Store_Creation_Valid()
        {
            var store = new Store("Tienda", new List<(Item, int)>
            {
                (sword, 1)
            });

            Assert.That(store.GetQuantity("Short Sword", ItemCategory.Weapon), Is.EqualTo(1));
        }

        [Test]
        public void Store_Creation_Invalid()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new Store("Empty", new List<(Item, int)>
                {
                    (sword, 0)
                }));
        }

        [Test]
        public void Player_Creation_Gold()
        {
            var p = new Player("Hero", 100);
            Assert.That(p.Gold, Is.EqualTo(100));
        }

        [Test]
        public void Purchase_Success()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (sword, 2) });

            var player = new Player("Hero", 50);

            var res = store.TryPurchase(player,
                new List<(Item, int)> { (sword, 1) });

            Assert.That(res.Success, Is.True);
            Assert.That(player.Gold, Is.EqualTo(40));
        }

        [Test]
        public void Purchase_NoMoney()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (ring, 1) });

            var player = new Player("Poor", 10);

            var res = store.TryPurchase(player,
                new List<(Item, int)> { (ring, 1) });

            Assert.That(res.Success, Is.False);
        }

        [Test]
        public void Purchase_NoStock()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (potion, 1) });

            var player = new Player("Buyer", 100);

            var res = store.TryPurchase(player,
                new List<(Item, int)> { (potion, 2) });

            Assert.That(res.Success, Is.False);
        }

        [Test]
        public void Purchase_MultipleStores()
        {
            var storeA = new Store("A",
                new List<(Item, int)> { (sword, 2) });

            var storeB = new Store("B",
                new List<(Item, int)> { (potion, 5) });

            var player = new Player("Traveler", 100);

            storeA.TryPurchase(player,
                new List<(Item, int)> { (sword, 1) });

            storeB.TryPurchase(player,
                new List<(Item, int)> { (potion, 2) });

            Assert.That(player.Gold, Is.EqualTo(84));
        }

        [Test]
        public void Inventory_Grouping()
        {
            var store = new Store("Shop",
                new List<(Item, int)>
                {
                    (sword, 2),
                    (potion, 5)
                });

            var player = new Player("Hero", 100);

            store.TryPurchase(player,
                new List<(Item, int)>
                {
                    (sword, 1),
                    (potion, 2)
                });

            Assert.That(player.GetQuantity("Short Sword", ItemCategory.Weapon), Is.EqualTo(1));
            Assert.That(player.GetQuantity("Health Potion", ItemCategory.Supply), Is.EqualTo(2));
        }

        [Test]
        public void Sell_To_Store()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (sword, 1) });

            var player = new Player("Seller", 0);
            player.AddToInventory(sword, 1);

            var res = store.TryBuyFromPlayer(player,
                new List<(Item, int)> { (sword, 1) });

            Assert.That(res.Success, Is.True);
            Assert.That(player.Gold, Is.EqualTo(10));
        }
    }
}