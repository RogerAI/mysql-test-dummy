# MySQL Test Dummy #

<img src="https://github.com/RogerAI/mysql-test-dummy/blob/ee6eff9475249dd716f9ba0339499f2066e0371f/CorpayOne.MysqlTestDummy/icon.png"
     alt="Project Logo" width="120" />

Do you need a record in a table for testing? Do you not really care what the value of that record is as long as it is present?

If so then MySQL Test Dummy is for you!

This library provides 2 simple methods:

```
// Get the id of an existing record, if any, or create a new one and return the id.
Dummy.GetOrCreateId<TId>(IDbConnection connection, string tableName);

// Create a new record in the named table, even if records exist, and return the id.
Dummy.CreateId<TId>(IDbConnection connection, string tableName);
```

Both these methods take in an open database connection to a MySQL 5 database and the name of a table to target. In addition you define the type of the primary key to be returned, either
`int` or `Guid`.

MySQL Test Dummy will generate a record in the database (or return the existing one for `GetOrCreateId`) including generating all necessary data required by
foreign keys on the target table. It will walk the graph of dependent tables in order to create the full object model necessary.

The return value is simply the primary key of the newly inserted (or selected) record.

## Options ##

Both methods optionally take options of the type `DummyOptions<TId>` to provide finer grained control of generated data.

You can provide a specific value for a column in the target table with the `WithColumnValue(string columnName, object? value)` method. This will insert the provided value into the column with the matching (case-sensitive) name instead of generating random data.

You can also provide the id of an existing record as a foreign key instead of having the library generate/pick a random record to use with `WithForeignKey(string columnName, TId value)`. This assumes the foreign keys are of the same type as the primary key.

Further options such as controlling the random seed for data generation, setting values for all optional columns, the default email address domain for email fields, etc are available.

## Example ##

Create a specific product and use the product id as the foreign key in the order record:

```
var productId = Dummy.CreateId(connection, "Products",
                new DummyOptions<int>().WithColumnValue("Name", "Beans"));

var orderId = Dummy.CreateId(connection, "Orders",
                new DummyOptions<int>().WithForeignKey("ProductId", productId));
```
