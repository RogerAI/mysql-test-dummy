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
                ImageUrl varchar(512) NOT NULL,
                SKU char(12) NOT NULL,
                Subtitle varchar(120) NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Users
            (
                Id int(11) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                Name varchar(300) NOT NULL,
                Email varchar(256) NOT NULL,
                Country char(2) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Orders
            (
                Id int(11) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                UserId int(11) NOT NULL,
                ProductId int(11) NOT NULL,
                FOREIGN KEY `FK_Orders__UserId` (UserId) REFERENCES Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
                FOREIGN KEY `FK_Orders__ProductId` (ProductId) REFERENCES Products (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE OrderNotes
            (
                Id int(11) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                OrderId int(11) NOT NULL,
                UserId int(11) NULL,
                Note text NOT NULL,
                FOREIGN KEY `FK_OrderNotes__OrderId` (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
                FOREIGN KEY `FK_OrderNotes__UserId` (UserId) REFERENCES Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Bids
            (
                BidId binary(16) NOT NULL PRIMARY KEY,
                Amount bigint(20) NOT NULL,
                Note varchar(64) NOT NULL
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