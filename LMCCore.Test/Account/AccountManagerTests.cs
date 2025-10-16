using System.IO;
using System.Text.Json;
using LMCCore.Account;
using LMCCore.Account.Model;
using Xunit;

namespace LMCCore.Test.Account;

public class AccountManagerTests
{
    private readonly string _testDirectory;
    private readonly string _accountsPath;

    public AccountManagerTests()
    {
        // 创建临时目录用于测试
        _testDirectory = Path.Combine(Path.GetTempPath(), "LMCTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
        _accountsPath = Path.Combine(_testDirectory, "accounts.json");
    }

    [Fact]
    public void GenerateOfflineUuid_WithValidName_ReturnsValidUuid()
    {
        // Arrange
        string playerName = "TestPlayer";

        // Act
        var uuid = AccountManager.GenerateOfflineUuid(playerName);

        // Assert
        Assert.NotNull(uuid);
        Assert.True(Guid.TryParse(uuid, out _));
    }

    [Fact]
    public void GenerateOfflineUuid_WithSameName_ReturnsSameUuid()
    {
        // Arrange
        string playerName = "TestPlayer";

        // Act
        var uuid1 = AccountManager.GenerateOfflineUuid(playerName);
        var uuid2 = AccountManager.GenerateOfflineUuid(playerName);

        // Assert
        Assert.Equal(uuid1, uuid2);
    }

    [Fact]
    public void GenerateOfflineUuid_WithDifferentNames_ReturnsDifferentUuids()
    {
        // Arrange
        string playerName1 = "TestPlayer1";
        string playerName2 = "TestPlayer2";

        // Act
        var uuid1 = AccountManager.GenerateOfflineUuid(playerName1);
        var uuid2 = AccountManager.GenerateOfflineUuid(playerName2);

        // Assert
        Assert.NotEqual(uuid1, uuid2);
    }

    [Fact]
    public void Load_WithNonExistentFile_InitializesEmptyAccounts()
    {
        // Arrange
        // 不创建文件

        // Act
        AccountManager.Load();

        // Assert
        Assert.Empty(AccountManager.Accounts);
    }

    [Fact]
    public void Load_WithValidFile_LoadsAccounts()
    {
        // Arrange
        var accounts = new List<LMCCore.Account.Model.Account>
        {
            new OfflineAccount { Name = "Player1", Uuid = "uuid1" },
            new OfflineAccount { Name = "Player2", Uuid = "uuid2" }
        };
        var json = JsonSerializer.Serialize(accounts, new JsonSerializerOptions
        {
            Converters = { new AccountJsonConverter() }
        });
        File.WriteAllText(_accountsPath, json);

        // Act
        AccountManager.Load();

        // Assert
        Assert.Equal(2, AccountManager.Accounts.Count);
        Assert.Equal("Player1", AccountManager.Accounts[0].Name);
        Assert.Equal("Player2", AccountManager.Accounts[1].Name);
    }

    [Fact]
    public void Add_WithUniqueOfflineAccount_AddsSuccessfully()
    {
        // Arrange
        AccountManager.Load(); // 初始化空账户列表
        var account = new OfflineAccount { Name = "TestPlayer", Uuid = "test-uuid" };

        // Act
        AccountManager.Add(account);

        // Assert
        Assert.Single(AccountManager.Accounts);
        Assert.Equal("TestPlayer", AccountManager.Accounts[0].Name);
    }

    [Fact]
    public void Add_WithDuplicateOfflineAccount_ThrowsException()
    {
        // Arrange
        AccountManager.Load(); // 初始化空账户列表
        var account1 = new OfflineAccount { Name = "TestPlayer", Uuid = "test-uuid" };
        var account2 = new OfflineAccount { Name = "TestPlayer", Uuid = "test-uuid2" };
        AccountManager.Add(account1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => AccountManager.Add(account2));
    }

    [Fact]
    public void Add_WithUniqueMicrosoftAccount_AddsSuccessfully()
    {
        // Arrange
        AccountManager.Load(); // 初始化空账户列表
        var account = new MicrosoftAccount { Name = "TestPlayer", Uuid = "test-uuid" };

        // Act
        AccountManager.Add(account);

        // Assert
        Assert.Single(AccountManager.Accounts);
        Assert.Equal("TestPlayer", AccountManager.Accounts[0].Name);
    }

    [Fact]
    public void Add_WithDuplicateMicrosoftAccount_ThrowsException()
    {
        // Arrange
        AccountManager.Load(); // 初始化空账户列表
        var account1 = new MicrosoftAccount { Name = "TestPlayer1", Uuid = "test-uuid" };
        var account2 = new MicrosoftAccount { Name = "TestPlayer2", Uuid = "test-uuid" }; // 相同UUID
        AccountManager.Add(account1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => AccountManager.Add(account2));
    }

    [Fact]
    public void Add_WithUniqueAuthlibAccount_AddsSuccessfully()
    {
        // Arrange
        AccountManager.Load(); // 初始化空账户列表
        var account = new AuthlibAccount 
        { 
            Name = "TestPlayer", 
            Uuid = "test-uuid",
            Username = "testuser"
        };

        // Act
        AccountManager.Add(account);

        // Assert
        Assert.Single(AccountManager.Accounts);
        Assert.Equal("TestPlayer", AccountManager.Accounts[0].Name);
    }

    [Fact]
    public void Add_WithDuplicateAuthlibAccount_ThrowsException()
    {
        // Arrange
        AccountManager.Load(); // 初始化空账户列表
        var account1 = new AuthlibAccount 
        { 
            Name = "TestPlayer1", 
            Uuid = "test-uuid1",
            Username = "testuser"
        };
        var account2 = new AuthlibAccount 
        { 
            Name = "TestPlayer2", 
            Uuid = "test-uuid2",
            Username = "testuser"  // 相同用户名
        };
        AccountManager.Add(account1);

        // Act & Assert
        Assert.Throws<ArgumentException>(() => AccountManager.Add(account2));
    }

    [Fact]
    public void Remove_WithExistingAccount_RemovesSuccessfully()
    {
        // Arrange
        AccountManager.Load(); // 初始化空账户列表
        var account = new OfflineAccount { Name = "TestPlayer", Uuid = "test-uuid" };
        AccountManager.Add(account);
        Assert.Single(AccountManager.Accounts);

        // Act
        AccountManager.Remove(account);

        // Assert
        Assert.Empty(AccountManager.Accounts);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 清理测试文件和目录
            if (File.Exists(_accountsPath))
            {
                File.Delete(_accountsPath);
            }
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
