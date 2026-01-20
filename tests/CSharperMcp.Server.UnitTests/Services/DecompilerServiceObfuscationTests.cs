using CSharperMcp.Server.Services;
using CSharperMcp.Server.Workspace;
using Microsoft.Extensions.Logging;

namespace CSharperMcp.Server.UnitTests.Services;

/// <summary>
/// Unit tests for DecompilerService obfuscation detection heuristics.
/// These tests verify that the IsLikelyObfuscated method correctly identifies obfuscated code patterns.
/// </summary>
[TestFixture]
internal class DecompilerServiceObfuscationTests
{
    private WorkspaceManager _workspaceManager = null!;
    private Mock<ILogger<DecompilerService>> _mockLogger = null!;
    private DecompilerService _decompilerService = null!;

    [SetUp]
    public void SetUp()
    {
        // Create real WorkspaceManager - it's not used in IsLikelyObfuscated tests
        var workspaceLogger = Mock.Of<ILogger<WorkspaceManager>>();
        _workspaceManager = new WorkspaceManager(workspaceLogger);

        _mockLogger = new Mock<ILogger<DecompilerService>>();
        _decompilerService = new DecompilerService(_workspaceManager, _mockLogger.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _workspaceManager?.Dispose();
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnFalse_ForNormalBclCode()
    {
        // Arrange - Normal BCL-style code
        var normalCode = @"
namespace System
{
    public class StringBuilder
    {
        private char[] _buffer;
        private int _length;

        public StringBuilder()
        {
            _buffer = new char[16];
        }

        public StringBuilder Append(string value)
        {
            // Implementation here
            return this;
        }

        public string ToString()
        {
            return new string(_buffer, 0, _length);
        }

        public int Capacity { get; set; }
        public int Length { get; set; }
    }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(normalCode);

        // Assert
        result.Should().BeFalse("Normal BCL-style code should not be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnFalse_ForNormalWorkspaceCode()
    {
        // Arrange - Normal application code
        var normalCode = @"
namespace MyApp.Services
{
    public class UserService
    {
        private readonly IUserRepository _userRepository;
        private readonly ILogger<UserService> _logger;

        public UserService(IUserRepository userRepository, ILogger<UserService> logger)
        {
            _userRepository = userRepository;
            _logger = logger;
        }

        public async Task<User> GetUserById(int userId)
        {
            _logger.LogInformation(""Fetching user {UserId}"", userId);
            return await _userRepository.FindByIdAsync(userId);
        }

        public async Task<bool> UpdateUser(User user)
        {
            ValidateUser(user);
            return await _userRepository.UpdateAsync(user);
        }

        private void ValidateUser(User user)
        {
            if (user == null)
                throw new ArgumentNullException(nameof(user));
        }
    }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(normalCode);

        // Assert
        result.Should().BeFalse("Normal application code should not be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnTrue_ForSingleCharTypeNames()
    {
        // Arrange - Code with single-character type names
        var obfuscatedCode = @"
namespace A
{
    public class a
    {
        private b _field;

        public c Method(d parameter)
        {
            return new c();
        }
    }

    public class b { }
    public class c { }
    public class d { }
    public interface e { }
    public struct f { }
    public enum g { }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(obfuscatedCode);

        // Assert
        result.Should().BeTrue("Code with many single-character type names should be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnTrue_ForObfuscatorNamingPatterns()
    {
        // Arrange - Code with typical obfuscator patterns PLUS short identifiers
        var obfuscatedCode = @"
namespace A00001
{
    public class a
    {
        private b c__01;
        private c d__02;
        private d e__03;
        private e f__04;

        public void g__05()
        {
            var h__06 = new h();
            var i__07 = new i();
            var j__08 = new j();
            var k__09 = new k();
            var l__10 = new l();
            var m__11 = new m();
        }

        public void n__12()
        {
            var o__13 = new o();
            var p__14 = new p();
        }
    }

    public class q
    {
        private int r__15;
    }

    public struct b { }
    public struct c { }
    public struct d { }
    public struct e { }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(obfuscatedCode);

        // Assert
        result.Should().BeTrue("Code with obfuscator naming patterns AND single-char types should be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnTrue_ForExcessiveUnicodeEscapes()
    {
        // Arrange - Code with excessive unicode escapes (not in strings) PLUS single-char types
        // Note: Using regular string (not verbatim) so \u sequences stay as literal text
        var obfuscatedCode =
            "namespace MyNamespace\n" +
            "{\n" +
            "    public class a\n" +
            "    {\n" +
            "        private int \\u0045\\u0046\\u0047;\n" +
            "        private string \\u0048\\u0049\\u004A;\n" +
            "        private bool \\u004B\\u004C\\u004D;\n" +
            "\n" +
            "        public void \\u004E\\u004F\\u0050()\n" +
            "        {\n" +
            "            var \\u0051\\u0052\\u0053 = 10;\n" +
            "            var \\u0054\\u0055\\u0056 = \"test\";\n" +
            "            var \\u0057\\u0058\\u0059 = new \\u005A\\u005B\\u005C();\n" +
            "            var \\u005D\\u005E\\u005F = new \\u0060\\u0061\\u0062();\n" +
            "            var \\u0063\\u0064\\u0065 = new \\u0066\\u0067\\u0068();\n" +
            "            var \\u0069\\u006A\\u006B = new \\u006C\\u006D\\u006E();\n" +
            "        }\n" +
            "\n" +
            "        public int \\u006F\\u0070\\u0071(int \\u0072\\u0073\\u0074)\n" +
            "        {\n" +
            "            return \\u0072\\u0073\\u0074 + \\u0075\\u0076\\u0077;\n" +
            "        }\n" +
            "\n" +
            "        private int \\u0075\\u0076\\u0077 = 42;\n" +
            "    }\n" +
            "\n" +
            "    public struct b { }\n" +
            "    public struct c { }\n" +
            "    public class d { }\n" +
            "    public class e { }\n" +
            "    public class f { }\n" +
            "}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(obfuscatedCode);

        // Assert
        result.Should().BeTrue("Code with excessive unicode escapes AND single-char types should be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnFalse_ForUnicodeEscapesInStringLiterals()
    {
        // Arrange - Normal code with unicode escapes only in string literals
        var normalCode = @"
namespace MyNamespace
{
    public class LocalizationService
    {
        public string GetGreeting()
        {
            return ""Hello \u4E16\u754C""; // Contains unicode in string, but identifiers are normal
        }

        public string GetSymbol()
        {
            return ""\u20AC""; // Euro symbol
        }

        public void ProcessText(string input)
        {
            var normalized = input.Replace(""\u00A0"", "" "");
            var trimmed = normalized.Trim();
        }
    }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(normalCode);

        // Assert
        result.Should().BeFalse("Unicode escapes in string literals should not trigger obfuscation detection");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnTrue_ForVeryShortAverageIdentifierLength()
    {
        // Arrange - Code with very short identifiers
        var obfuscatedCode = @"
public class A
{
    private int a;
    private int b;
    private int c;

    public int d(int e, int f)
    {
        int g = e + f;
        int h = g * 2;
        int i = h / 3;
        int j = i - a;
        int k = j + b;
        int l = k * c;
        return l;
    }

    public void m()
    {
        int n = 0;
        int o = 1;
        int p = 2;
        int q = 3;
    }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(obfuscatedCode);

        // Assert
        result.Should().BeTrue("Code with very short average identifier length should be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnFalse_ForLoopVariables()
    {
        // Arrange - Normal code with common loop variables (i, j, k)
        var normalCode = @"
public class MatrixProcessor
{
    public void ProcessMatrix(int[,] matrix)
    {
        int rows = matrix.GetLength(0);
        int columns = matrix.GetLength(1);

        for (int i = 0; i < rows; i++)
        {
            for (int j = 0; j < columns; j++)
            {
                matrix[i, j] = matrix[i, j] * 2;
            }
        }
    }

    public int CalculateSum(int[] numbers)
    {
        int sum = 0;
        for (int i = 0; i < numbers.Length; i++)
        {
            sum += numbers[i];
        }
        return sum;
    }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(normalCode);

        // Assert
        result.Should().BeFalse("Normal code with loop variables should not be flagged as obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnTrue_ForCompilerGeneratedPatterns()
    {
        // Arrange - Code with compiler-generated patterns PLUS single-char types (which could indicate obfuscation)
        var obfuscatedCode = @"
namespace A
{
    public class a
    {
        public int <>4__this;

        internal void <Method>b__0()
        {
            var <>s__1 = new object();
            var <>f__AnonymousType0 = new { };
            var <>s__2 = 42;
        }
    }

    public class b
    {
        public int <>9__0_0;
        public string <>9__0_1;
    }

    public class c
    {
        public int <>9__1_0;
    }

    [CompilerGenerated]
    private sealed class <PrivateImplementationDetails>
    {
        internal static readonly <PrivateImplementationDetails>.__StaticArrayInitTypeSize=16 A00001;
        internal static readonly <PrivateImplementationDetails>.__StaticArrayInitTypeSize=24 B00002;
    }

    public class d
    {
        public int <>4__that;
    }

    public class e
    {
        public string <>4__other;
    }

    public struct f { }
    public enum g { }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(obfuscatedCode);

        // Assert
        result.Should().BeTrue("Code with excessive compiler-generated patterns AND single-char types should be flagged as potentially obfuscated");
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnFalse_ForEmptyOrNullString()
    {
        // Act & Assert
        _decompilerService.IsLikelyObfuscated("").Should().BeFalse();
        _decompilerService.IsLikelyObfuscated(null!).Should().BeFalse();
        _decompilerService.IsLikelyObfuscated("   ").Should().BeFalse();
    }

    [Test]
    public void IsLikelyObfuscated_ShouldReturnFalse_ForSystemLinqExpressions()
    {
        // Arrange - Normal LINQ/lambda code
        var normalCode = @"
public class ProductService
{
    public List<Product> FilterProducts(List<Product> products, decimal minPrice)
    {
        return products
            .Where(p => p.Price >= minPrice)
            .OrderBy(p => p.Name)
            .Select(p => new Product
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price
            })
            .ToList();
    }

    public Product GetMostExpensive(IEnumerable<Product> products)
    {
        return products.OrderByDescending(p => p.Price).FirstOrDefault();
    }
}";

        // Act
        var result = _decompilerService.IsLikelyObfuscated(normalCode);

        // Assert
        result.Should().BeFalse("Normal LINQ code should not be flagged as obfuscated");
    }
}
