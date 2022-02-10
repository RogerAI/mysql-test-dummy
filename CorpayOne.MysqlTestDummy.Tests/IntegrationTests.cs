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
        }

        [Fact]
        public void SimpleIntId_UserTable_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Users");

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");
        }
    }
}
