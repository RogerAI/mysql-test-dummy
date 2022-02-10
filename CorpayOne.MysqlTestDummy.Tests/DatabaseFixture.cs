using System;
using System.Data;
using MySqlConnector;

namespace CorpayOne.MysqlTestDummy.Tests;

public class DatabaseFixture : IDisposable
{
    private const string DatabaseName = "MysqlDummyTest218902B3146E";

    private readonly MySqlConnection _rootConnection;
    private readonly MySqlConnection _testDatabaseConnection;

    public DatabaseFixture()
    {
        _rootConnection = new MySqlConnection("server=localhost;port=3329;uid=root;pwd=hunter2;database=db");

        try
        {
            _rootConnection.Open();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Failed to run the tests because MySQL container was not running on port 3329. Run from container/ folder before running tests.",
                ex);
        }

        using var command = _rootConnection.CreateCommand();

        command.CommandText = $"DROP DATABASE IF EXISTS {DatabaseName}; CREATE DATABASE {DatabaseName}";

        command.ExecuteNonQuery();

        command.CommandText = @$"
            USE {DatabaseName};

            CREATE TABLE Products 
            (
                Id int(11) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                Name varchar(64) NOT NULL,
                Created TIMESTAMP NOT NULL,
                ImageUrl varchar(512) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        command.ExecuteNonQuery();

        _testDatabaseConnection = new MySqlConnection($"server=localhost;port=3329;uid=root;pwd=hunter2;database={DatabaseName}");

        _testDatabaseConnection.Open();
    }

    public IDbConnection GetConnection()
    {
        return _testDatabaseConnection;
    }

    public void Dispose()
    {
        _testDatabaseConnection.Dispose();

        using (var command = _rootConnection.CreateCommand())
        {
            command.CommandText = $"DROP DATABASE {DatabaseName};";

            command.ExecuteNonQuery();
        }

        _rootConnection.Dispose();
    }
}