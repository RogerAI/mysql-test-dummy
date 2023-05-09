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

            Assert.True(product.SKU.Length == 12,
                $"Expected SKU to be 12 characters but was: '{product.SKU}' ({product.SKU.Length} chars)");
        }

        [Fact]
        public async Task SimpleIntId_ProductTableCaseInsensitive_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<int>(conn, "Products", new DummyOptions<int>()
                .WithColumnValue("naME", "Brussel Sprouts"));

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");

            var product = await conn.GetAsync<Product>(id);

            Assert.NotNull(product);
            Assert.Equal("Brussel Sprouts", product.Name);
        }

        [Fact]
        public async Task SimpleIntId_ProductTableCaseSensitive_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId(conn, "Products", new DummyOptions<int>
                {
                    ColumnsCaseSensitive = true
                }
                .WithColumnValue("naME", "Brussel Sprouts"));

            Assert.True(id > 0, $"Expected id to be greater than zero but was {id}.");

            var product = await conn.GetAsync<Product>(id);

            Assert.NotNull(product);
            Assert.NotEqual("Brussel Sprouts", product.Name);
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

            var id = Dummy.CreateId(conn, "Products",
                new DummyOptions<int>().WithColumnValue(nameof(Product.Subtitle), productSubtitle));

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

            var orderNoteId =
                Dummy.CreateId(conn, "OrderNotes", new DummyOptions<int>().WithForeignKey("UserId", userId));

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

        [Fact]
        public async Task CompoundId_TupleClass_Creates()
        {
            var conn = _fixture.GetConnection();

            var id = Dummy.CreateId<Tuple<int, int>>(
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

        [Fact]
        public async Task CompoundId_CreateTwice_CreatesUnique()
        {
            var conn = _fixture.GetConnection();

            var id1 = Dummy.CreateId<(int, int)>(
                conn,
                "UserCategories");
            var id2 = Dummy.CreateId<(int, int)>(
                conn,
                "UserCategories");

            Assert.NotEqual(id1, id2);

            var userCategory1 = await conn.QueryAsync<UserCategory>(
                "SELECT * FROM UserCategories WHERE UserId = @userId AND CategoryId = @categoryId",
                new
                {
                    userId = id1.Item1,
                    categoryId = id1.Item2
                });

            var userCategory2 = await conn.QueryAsync<UserCategory>(
                "SELECT * FROM UserCategories WHERE UserId = @userId AND CategoryId = @categoryId",
                new
                {
                    userId = id2.Item1,
                    categoryId = id2.Item2
                });

            Assert.NotNull(userCategory1);
            Assert.NotNull(userCategory2);
        }

        [Fact]
        public async Task IdentityUsers_Insert_Creates()
        {
            var conn = _fixture.GetConnection();

            var userId = Dummy.CreateId<Guid>(
                conn,
                "IdentityUsers");

            var user = await conn.GetAsync<IdentityUser>(userId);

            Assert.NotNull(user);
        }

        [Fact]
        public async Task CircularTableReferences_FooApplicationUsers_Insert_Creates()
        {
            var conn = _fixture.GetConnection();

            var userId = Dummy.CreateId(
                conn,
                "FooApplicationUsers",
                new DummyOptions<int>().MustForcePopulateOptionalColumns());

            var (userName, email) = await conn.QuerySingleAsync<(string, string)>(
                "SELECT Name, Email FROM FooApplicationUsers WHERE Id = @id",
                new { id = userId });

            Assert.NotNull(userName);
            Assert.True(email.Contains("@"), "Email should contain an @ symbol");

            var secondUserId = Dummy.CreateId(
                conn,
                "FooApplicationUsers",
                new DummyOptions<int>().MustForcePopulateOptionalColumns());

            var secondUserName = await conn.QuerySingleAsync<string>(
                "SELECT Name FROM FooApplicationUsers WHERE Id = @id",
                new { id = secondUserId });

            Assert.NotNull(secondUserName);

            var eventId = Dummy.CreateId(
                conn,
                "Events",
                new DummyOptions<int>().MustForcePopulateOptionalColumns());

            Assert.True(eventId > 0, "Event id should be non-zero");

            var eventId2 = Dummy.CreateId(
                conn,
                "Events",
                new DummyOptions<int>().MustForcePopulateOptionalColumns());

            Assert.True(eventId2 > 0, "Created event id should be non-zero");
        }

        [Fact]
        public void UnresolvableCircularReference_Aadvarks_Insert_Fails()
        {
            var conn = _fixture.GetConnection();

            try
            {
                Dummy.CreateId(
                    conn,
                    "Aardvarks",
                    new DummyOptions<int>().MustForcePopulateOptionalColumns());
            }
            catch (InvalidOperationException ex)
            {
                Assert.True(ex.Message.Contains("Bears") && ex.Message.Contains("AardvarkId")
                && ex.Message.Contains("circular reference"),
                    $"Unexpected exception message: {ex.Message}");
            }
        }

        [Fact]
        public void UniqueConstraintOnLinkedEntity_DoesNotFail()
        {
            var conn = _fixture.GetConnection();

            for (int i = 0; i < 200; i++)
            {
                Dummy.CreateId(
                    conn,
                    "HashedLookups",
                    new DummyOptions<int>()
                        .MustForcePopulateOptionalColumns());
            }
        }

        [Fact]
        public void SupportsBitColumn()
        {
            var conn = _fixture.GetConnection();

            var id1 = Dummy.CreateId<int>(
                conn,
                "UserViews");

            Assert.True(id1 > 0);

            var id2 = Dummy.CreateId<int>(
                conn,
                "UserViews",
                new DummyOptions<int>().WithColumnValue("HasViewed", true));

            Assert.True(id2 > 0);
        }
    }
}
