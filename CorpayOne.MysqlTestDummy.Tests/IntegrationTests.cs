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
        public void SimpleIntTableCreate()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Products");

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");
        }
    }
}
