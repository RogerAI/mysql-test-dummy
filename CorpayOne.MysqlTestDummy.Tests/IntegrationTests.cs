using System;
using System.Threading.Tasks;
using CorpayOne.MysqlTestDummy.Tests.Entities;
using Dapper;
using Dapper.Contrib.Extensions;
using Xunit;

namespace CorpayOne.MysqlTestDummy.Tests
{
    public class IntegrationTests : IClassFixture<DatabaseFixture>
    {
        private readonly DatabaseFixture _fixture;

        public IntegrationTests(DatabaseFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task SimpleIntId_ProductTable_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Products");

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");

            var product = await conn.GetAsync<Product>(id);

            Assert.NotNull(product);

            Assert.Equal(id, product.Id);

            Assert.Null(product.Subtitle);
            Assert.False(
                string.IsNullOrWhiteSpace(product.Name),
                $"Product name should not have been null or whitespace but was: '{product.Name}'");

            Assert.True(product.SKU.Length == 12, $"Expected SKU to be 12 characters but was: '{product.SKU}' ({product.SKU.Length} chars)");
        }

        [Fact]
        public async Task SimpleIntId_UserTable_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Users");

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");

            var user = await conn.GetAsync<User>(id);

            Assert.NotNull(user);

            Assert.Equal(id, user.Id);
        }

        [Fact]
        public async Task SimpleIntId_UserTable_SetsCountryColumn()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Users");

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");

            var user = await conn.GetAsync<User>(id);

            Assert.NotNull(user);

            Assert.Equal("US", user.Country);
        }

        [Fact]
        public async Task SimpleIntId_OrderTable_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Orders");

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");

            var order = await conn.GetAsync<Order>(id);

            Assert.NotNull(order);

            var user = await conn.GetAsync<User>(order.Id);

            Assert.NotNull(user);
        }

        [Fact]
        public void SimpleGuidId_BidTable_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<Guid>(conn, "Bids");

            Assert.True(id != Guid.Empty, $"Expected id to be a non-empty guid but was {id}.");
        }

        [Fact]
        public void SimpleIntId_IterateRandomSeeds_AllCreated()
        {
            var conn = _fixture.GetConnection();

            for (var i = 4500; i < 4500 + 600; i++)
            {
                try
                {
                    Dummy.CreateId(conn, "Products", new DummyOptions<int>().WithRandomSeed(i));
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to create product with random seed {i}.", ex);
                }
            }
        }

        [Fact]
        public async Task SimpleIntId_SetNullableColumn_SetsValue()
        {
            const string productSubtitle = "Ants, so many ants";

            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId(conn, "Products", new DummyOptions<int>().WithColumnValue(nameof(Product.Subtitle), productSubtitle));

            var product = await conn.GetAsync<Product>(id);

            Assert.Equal(productSubtitle, product.Subtitle);
        }

        [Fact]
        public async Task SimpleIntId_MultipleForeignKeys_SetsValue()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "OrderNotes");

            var orderNote = await conn.GetAsync<OrderNote>(id);

            Assert.NotNull(orderNote.Note);
            Assert.Null(orderNote.UserId);

            var order = await conn.GetAsync<Order>(orderNote.OrderId);

            Assert.NotNull(order);

            var product = await conn.GetAsync<Product>(order.ProductId);

            Assert.NotNull(product);

            var user = await conn.GetAsync<User>(order.UserId);

            Assert.NotNull(user);
        }

        [Fact]
        public async Task SimpleIntId_OptionalForeignKeySpecified_UsesValue()
        {
            var conn = _fixture.GetConnection();

            // Ignored.
            Dummy.CreateId<int>(conn, "Users");
            Dummy.CreateId<int>(conn, "Users");

            // Used.
            var userId = Dummy.CreateId<int>(conn, "Users");

            // Ignored.
            Dummy.CreateId<int>(conn, "Users");

            var orderNoteId = Dummy.CreateId(conn, "OrderNotes", new DummyOptions<int>().WithForeignKey("UserId", userId));

            var orderNote = await conn.GetAsync<OrderNote>(orderNoteId);

            Assert.Equal(userId, orderNote.UserId);
        }

        [Fact]
        public async Task SimpleIntId_ForcePopulate_Creates()
        {
            var conn = _fixture.GetConnection();

            var productId = Dummy.CreateId(
                conn,
                "Products",
                new DummyOptions<int>
                {
                    ForcePopulateOptionalColumns = true
                });

            var product = await conn.GetAsync<Product>(productId);

            Assert.NotNull(product);
            Assert.NotNull(product.SKU);
        }

        [Fact]
        public async Task SimpleIntId_RecursiveTable_Creates()
        {
            var conn = _fixture.GetConnection();

            var nodeId = Dummy.CreateId<int>(
                conn,
                "Node");

            var node = await conn.GetAsync<Node>(nodeId);

            Assert.NotNull(node);

            Assert.Null(node.ParentId);
        }

        [Fact]
        public async Task SimpleIntId_RecursiveTableForcePopulate_Creates()
        {
            var conn = _fixture.GetConnection();

            var nodeId = Dummy.CreateId(
                conn,
                "Node",
                new DummyOptions<int>().MustForcePopulateOptionalColumns());

            var node = await conn.GetAsync<Node>(nodeId);

            Assert.NotNull(node);

            Assert.NotNull(node.ParentId);

            var parent = await conn.GetAsync<Node>(node.ParentId.Value);

            Assert.NotNull(parent);

            Assert.Null(parent.ParentId);
        }

        [Fact]
        public async Task CompoundId_SimpleData_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<(int, int)>(
                conn,
                "UserCategories");

            var userCategory = await conn.QueryAsync<UserCategory>(
                "SELECT * FROM UserCategories WHERE UserId = @userId AND CategoryId = @categoryId",
                new
                {
                    userId = id.Item1,
                    categoryId = id.Item2
                });

            Assert.NotNull(userCategory);
        }
    }
}
