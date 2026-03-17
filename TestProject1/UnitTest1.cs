using NUnit.Framework;
using GameStore;
using System;
using System.Collections.Generic;

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


        [TestCase("Bow", ItemCategory.Weapon, 15)]
        [TestCase("Helmet", ItemCategory.Armor, 20)]
        [TestCase("Magic Ring", ItemCategory.Accessory, 50)]
        public void Item_Creation_Valid_Parametrized(string name, ItemCategory cat, decimal price)
        {
            var it = new Item(name, cat, price);
            Assert.That(it.Name, Is.EqualTo(name));
        }

        [TestCase("")]
     
        public void Item_Invalid_Name(string name)
        {
            Assert.Throws<ArgumentException>(() =>
                new Item(name, ItemCategory.Weapon, 10));
        }

        [TestCase(0)]
        [TestCase(-5)]
        public void Item_Invalid_Price(decimal price)
        {
            Assert.Throws<ArgumentException>(() =>
                new Item("Bad", ItemCategory.Supply, price));
        }

        [Test]
        public void Store_Creation_Valid()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (sword, 1) });

            Assert.That(store.GetQuantity("Short Sword", ItemCategory.Weapon), Is.EqualTo(1));
        }

        [Test]
        public void Store_Invalid_Stock()
        {
            Assert.Throws<InvalidOperationException>(() =>
                new Store("Bad",
                    new List<(Item, int)> { (sword, 0) }));
        }

        [TestCase(10)]
        [TestCase(50)]
        [TestCase(100)]
        public void Player_Creation_Parametrized(int gold)
        {
            var p = new Player("Hero", gold);
            Assert.That(p.Gold, Is.EqualTo(gold));
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
        public void Purchase_NotEnoughMoney()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (ring, 1) });

            var player = new Player("Poor", 10);

            var res = store.TryPurchase(player,
                new List<(Item, int)> { (ring, 1) });

            Assert.That(res.Success, Is.False);
        }

        [Test]
        public void Purchase_NotEnoughStock()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (potion, 1) });

            var player = new Player("Buyer", 100);

            var res = store.TryPurchase(player,
                new List<(Item, int)> { (potion, 2) });

            Assert.That(res.Success, Is.False);
        }

        [Test]
        public void Inventory_Grouping_AfterPurchase()
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
        public void Sell_To_Store_Success()
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

        [Test]
        public void Sell_Item_PlayerDoesNotHave()
        {
            var store = new Store("Shop",
                new List<(Item, int)> { (sword, 1) });

            var player = new Player("Seller", 0);

            var res = store.TryBuyFromPlayer(player,
                new List<(Item, int)> { (sword, 1) });

            Assert.That(res.Success, Is.False);
        }
    }
}