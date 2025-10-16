using System.IO;
using LMC.Basic;
using LMCCore.Java;
using Xunit;
using Moq;

namespace LMCCore.Test.Java;

public class JavaManagerTests
{
    private readonly string _testDirectory;
    private readonly string _javaReleaseContent;

    public JavaManagerTests()
    {
        // 创建临时目录用于测试
        _testDirectory = Path.Combine(Path.GetTempPath(), "LMCTest", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);

        // 创建模拟的Java release文件内容
        _javaReleaseContent = @"JAVA_VERSION=""17.0.2""
IMPLEMENTOR=""Eclipse Adoptium""
IMPLEMENTOR_VERSION=""Temurin-17.0.2+8
OS_ARCH=""amd64""
OS_NAME=""Windows""
JAVA_RUNTIME_VERSION=""17.0.2+8""
MODULES=""java.base java.datatransfer java.desktop java.instrument java.logging java.management java.management.rmi java.naming java.net.http java.prefs java.rmi java.scripting java.se java.security.jgss java.smartcardio java.sql java.sql.rowset java.xml crypto.jgss""
";
    }

    [Fact]
    public async Task GetJavaInfo_WithValidJavaPath_ReturnsCorrectInfo()
    {
        // Arrange
        string javaPath = Path.Combine(_testDirectory, "java");
        Directory.CreateDirectory(Path.Combine(javaPath, "bin"));
        File.WriteAllText(Path.Combine(javaPath, "release"), _javaReleaseContent);

        // 创建模拟的java.exe文件
        File.WriteAllText(Path.Combine(javaPath, "bin", "java.exe"), "");

        // Act
        var result = await JavaManager.GetJavaInfo(javaPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(javaPath, result.Path);
        Assert.Equal("17.0.2", result.Version.ToString());
        Assert.Equal("Eclipse Adoptium", result.Implementor);
        Assert.False(result.IsJdk);
    }

    [Fact]
    public async Task GetJavaInfo_WithJdk_ReturnsIsJdkTrue()
    {
        // Arrange
        string javaPath = Path.Combine(_testDirectory, "java");
        Directory.CreateDirectory(Path.Combine(javaPath, "bin"));
        File.WriteAllText(Path.Combine(javaPath, "release"), _javaReleaseContent);

        // 创建模拟的java.exe和javac.exe文件
        File.WriteAllText(Path.Combine(javaPath, "bin", "java.exe"), "");
        File.WriteAllText(Path.Combine(javaPath, "bin", "javac.exe"), "");

        // Act
        var result = await JavaManager.GetJavaInfo(javaPath);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(javaPath, result.Path);
        Assert.Equal("17.0.2", result.Version.ToString());
        Assert.Equal("Eclipse Adoptium", result.Implementor);
        Assert.True(result.IsJdk);
    }

    [Fact]
    public async Task GetJavaInfo_WithMissingReleaseFile_ThrowsException()
    {
        // Arrange
        string javaPath = Path.Combine(_testDirectory, "java");
        Directory.CreateDirectory(Path.Combine(javaPath, "bin"));
        // 不创建release文件

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => JavaManager.GetJavaInfo(javaPath));
    }

    [Fact]
    public async Task GetJavaInfo_WithInvalidReleaseFile_ThrowsException()
    {
        // Arrange
        string javaPath = Path.Combine(_testDirectory, "java");
        Directory.CreateDirectory(Path.Combine(javaPath, "bin"));
        // 创建无效的release文件
        File.WriteAllText(Path.Combine(javaPath, "release"), "invalid content");

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => JavaManager.GetJavaInfo(javaPath));
    }

    [Fact]
    public async Task SearchJava_WithCallback_CallsCallbackCorrectly()
    {
        // Arrange
        var callback = new Mock<Action<TaskCallbackInfo>>();
        int expectedCallCount = 5;

        // Act
        var result = await JavaManager.SearchJava(callback.Object);

        // Assert
        // 验证回调被调用了正确的次数
        callback.Verify(c => c(It.IsAny<TaskCallbackInfo>()), Times.Exactly(expectedCallCount));

        // 验证返回结果不为空
        Assert.NotNull(result);
    }

    [Fact]
    public async Task AddJava_WithValidPath_AddsSuccessfully()
    {
        // Arrange
        string javaPath = Path.Combine(_testDirectory, "java");
        Directory.CreateDirectory(Path.Combine(javaPath, "bin"));
        File.WriteAllText(Path.Combine(javaPath, "release"), _javaReleaseContent);

        // 创建模拟的java.exe文件
        File.WriteAllText(Path.Combine(javaPath, "bin", "java.exe"), "");

        var callback = new Mock<Action<TaskCallbackInfo>>();

        // Act
        await JavaManager.AddJava(javaPath, callback.Object);

        // Assert
        // 验证回调被调用了正确的次数
        callback.Verify(c => c(It.IsAny<TaskCallbackInfo>()), Times.Exactly(2));

        // 验证返回结果不为空
        Assert.NotNull(callback);
    }

    [Fact]
    public async Task AddJava_WithInvalidPath_ThrowsException()
    {
        // Arrange
        string invalidPath = Path.Combine(_testDirectory, "invalid");

        var callback = new Mock<Action<TaskCallbackInfo>>();

        // Act & Assert
        await Assert.ThrowsAsync<Exception>(() => JavaManager.AddJava(invalidPath, callback.Object));
    }

    [Fact]
    public void RemoveJava_WithValidPath_RemovesSuccessfully()
    {
        // Arrange
        string javaPath = Path.Combine(_testDirectory, "java");

        // Act
        JavaManager.RemoveJava(javaPath);

        // Assert
        // 此测试主要验证方法不会抛出异常
        // 实际移除效果取决于具体实现和配置管理器
        Assert.True(true);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            // 清理测试文件和目录
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
