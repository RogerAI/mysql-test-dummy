using System;
using System.Data;
using System.Runtime.InteropServices;
using Dapper;
using MySqlConnector;

namespace CorpayOne.MysqlTestDummy.Tests;

public class DatabaseFixture : IDisposable
{
    private const string DatabaseName = "MysqlDummyTest218902B3146E";

    private readonly MySqlConnection _rootConnection;
    private readonly MySqlConnection _testDatabaseConnection;

    public DatabaseFixture()
    {
        SqlMapper.AddTypeHandler(new GuidStringMapperHandler());
        SqlMapper.RemoveTypeMap(typeof(Guid));
        SqlMapper.RemoveTypeMap(typeof(Guid?));

        var rootConnectionString = "server=localhost;port=3329;uid=root;pwd=hunter2;database=db;";
        var testConnectionString = $"server=localhost;port=3329;uid=root;pwd=hunter2;database={DatabaseName};";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            rootConnectionString += "TlsCipherSuites=TLS_DHE_RSA_WITH_AES_256_GCM_SHA384;";
            testConnectionString += "TlsCipherSuites=TLS_DHE_RSA_WITH_AES_256_GCM_SHA384;";
        }

        _rootConnection = new MySqlConnection(rootConnectionString);

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

            CREATE TABLE Categories
            (
                Id int(11) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                Name varchar(300) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE UserCategories
            (
                UserId int(11) NOT NULL,
                CategoryId int(11) NOT NULL,
                Created TIMESTAMP NOT NULL,
                Level int NOT NULL,
                PRIMARY KEY (UserId,CategoryId),
                FOREIGN KEY FK_UserCategories__UserId (UserId) REFERENCES Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
                FOREIGN KEY FK_UserCategories__CategoryId (CategoryId) REFERENCES Categories (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Orders
            (
                Id int(11) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                UserId int(11) NOT NULL,
                ProductId int(11) NOT NULL,
                FOREIGN KEY FK_Orders__UserId (UserId) REFERENCES Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION,
                FOREIGN KEY FK_Orders__ProductId (ProductId) REFERENCES Products (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE OrderNotes
            (
                Id int(11) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                OrderId int(11) NOT NULL,
                UserId int(11) NULL,
                Note text NOT NULL,
                FOREIGN KEY FK_OrderNotes__OrderId (OrderId) REFERENCES Orders (Id) ON DELETE CASCADE ON UPDATE NO ACTION,
                FOREIGN KEY FK_OrderNotes__UserId (UserId) REFERENCES Users (Id) ON DELETE NO ACTION ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Bids
            (
                BidId binary(16) NOT NULL PRIMARY KEY,
                Amount bigint(20) NOT NULL,
                Note varchar(64) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Node
            (
                Id bigint(20) NOT NULL PRIMARY KEY AUTO_INCREMENT,
                Value tinyint NOT NULL,
                ParentId bigint(20) NULL,
                FOREIGN KEY FK_Node__ParentId (ParentId) REFERENCES Node (Id) ON DELETE CASCADE ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IdentityUsers
            (
                UserId varchar(255) NOT NULL,
                UserName varchar(256) DEFAULT NULL,
                Email varchar(256) DEFAULT NULL,
                PRIMARY KEY (UserId),
                UNIQUE KEY UserNameIndex (UserName)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IdentityRoles
            (
                Id varchar(255) NOT NULL,
                Name varchar(256) DEFAULT NULL,
                PRIMARY KEY (Id),
                UNIQUE KEY Name (Name)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE IdentityUserRoles
            (
                UserId varchar(255) NOT NULL,
                RoleId varchar(255) NOT NULL,
                PRIMARY KEY (UserId, RoleId),
                KEY IX_AspNetUserRoles_RoleId (RoleId),
                CONSTRAINT FK_IdentityUserRoles_RoleId FOREIGN KEY (RoleId) REFERENCES IdentityRoles (Id) ON DELETE CASCADE,
                CONSTRAINT FK_IdentityUserRoles_UserId FOREIGN KEY (UserId) REFERENCES IdentityUsers (UserId) ON DELETE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            -- Define a resolvable circular reference.

            CREATE TABLE EventActors
            (
                `Id` int NOT NULL AUTO_INCREMENT,
                `RefType` int NOT NULL,
                `RefId` int NOT NULL,
                PRIMARY KEY (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE FooApplicationUsers
            (
                Id int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                Name varchar(300) NOT NULL,
                Email varchar(256) NOT NULL,
                EventActorId int NOT NULL,
                FOREIGN KEY FK_FooApplicationUsers__EventActors (EventActorId) REFERENCES EventActors (Id) ON DELETE CASCADE ON UPDATE NO ACTION
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Events
            (
                `Id` int NOT NULL AUTO_INCREMENT,
                `EventActorId` int NOT NULL,
                `EventJson` json NOT NULL,
                `PerformedById` int DEFAULT NULL,
                PRIMARY KEY (`Id`),
                KEY `FK_Events_EventActors` (`EventActorId`),
                KEY `FK_Events_FooApplicationUsers` (`PerformedById`),
                CONSTRAINT `FK_Events_EventActors` FOREIGN KEY (`EventActorId`) REFERENCES `EventActors` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE,
                CONSTRAINT `FK_Events_FooApplicationUsers` FOREIGN KEY (`PerformedById`) REFERENCES `FooApplicationUsers` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            ALTER TABLE EventActors
            ADD COLUMN CreatedById int NULL,
            ADD FOREIGN KEY `FK_EventActors__FooApplicationUsers` (CreatedById) REFERENCES FooApplicationUsers (Id)
                ON DELETE NO ACTION ON UPDATE NO ACTION;

            -- End resolvable circular reference tables

            -- Define an unresolvable circular reference

            CREATE TABLE Aardvarks
            (
                Id int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                Name varchar(300) NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE Bears
            (
                Id int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                Email varchar(300) NOT NULL COMMENT 'Bears are online',
                AardvarkId int NOT NULL,
                CONSTRAINT `FK_Bears__Aardvarks` FOREIGN KEY (`AardvarkId`) REFERENCES `Aardvarks` (`Id`) ON DELETE CASCADE ON UPDATE CASCADE
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            ALTER TABLE Aardvarks
            ADD COLUMN BearId int NOT NULL,
            ADD FOREIGN KEY `FK_Aardvarks__Bears` (BearId) REFERENCES Bears (Id)
                ON DELETE NO ACTION ON UPDATE NO ACTION;

            -- End unresolvable circular reference tables

            CREATE TABLE `SystemEntries` (
                Id int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                `RefType` int NOT NULL,
                `RefId` int NOT NULL,
                UNIQUE KEY `UQ_SystemEntries__RefId_RefType` (`RefId`,`RefType`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;

            CREATE TABLE `HashedLookups` (
                Id int NOT NULL PRIMARY KEY AUTO_INCREMENT,
                `Name` varchar(128) NOT NULL,
                `Path` varchar(1024) NOT NULL,
                `SystemEntryId` INT NULL,
                CONSTRAINT `FK_HashedLookups__SystemEntries`
                    FOREIGN KEY (`SystemEntryId`) REFERENCES `SystemEntries` (`Id`)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        command.ExecuteNonQuery();

        _testDatabaseConnection = new MySqlConnection(testConnectionString);

        _testDatabaseConnection.Open();
    }

    public IDbConnection GetConnection() => _testDatabaseConnection;

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