using System;
using System.Threading.Tasks;
using CorpayOne.MysqlTestDummy.Tests.Entities;
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

            Assert.True(product.SKU.Length == 12, $"Expected SKU to be 12 characters but was: {product.SKU} ({product.SKU.Length} chars)");
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
        public async Task SimpleIntId_SetNullableColumn_SetsValue()
        {
            const string productSubtitle = "Ants, so many ants";

            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Products", new DummyOptions<int>().WithColumnValue(nameof(Product.Subtitle), productSubtitle));

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
    }
}
