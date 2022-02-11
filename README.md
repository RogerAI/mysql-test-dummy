# MySQL Test Dummy #

Do you need a record in a table for testing? Do you not really care what the value of that record is as long as it is present?

If so then MySQL Test Dummy is for you!

This library provides 2 simple methods:

```
Dummy.GetOrCreateId<TId>(IDbConnection connection, string tableName);
Dummy.CreateId<TId>(IDbConnection connection, string tableName);
```

Both these methods take in an open database connection to a MySQL 5 database and the name of a table to target. In addition you define the type of the primary key to be returned, either
`int` or `Guid`. MySQL Test Dummy will generate a record in the database (or return the existing one for `GetOrCreateId`) including generating all necessary data required by
foreign keys on the target table.

The return value is simply the primary key of the newly inserted (or selected) record.
