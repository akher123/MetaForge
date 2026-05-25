using System.Security.Cryptography;
using System.Text;
using MetaForge.Infrastructure.Services;

namespace MetaForge.UnitTests;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_ProducesNonLegacyFormat()
    {
        var hash = PasswordHasher.Hash("admin");

        Assert.False(PasswordHasher.IsLegacyHash(hash));
        Assert.True(PasswordHasher.Verify("admin", hash));
    }

    [Fact]
    public void Hash_ProducesUniqueHashesForSamePassword()
    {
        var hash1 = PasswordHasher.Hash("admin");
        var hash2 = PasswordHasher.Hash("admin");

        Assert.NotEqual(hash1, hash2);
        Assert.True(PasswordHasher.Verify("admin", hash1));
        Assert.True(PasswordHasher.Verify("admin", hash2));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hash = PasswordHasher.Hash("admin");

        Assert.False(PasswordHasher.Verify("wrong", hash));
    }

    [Fact]
    public void Verify_AcceptsLegacySha256Hash()
    {
        var legacy = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes("admin")));

        Assert.True(PasswordHasher.IsLegacyHash(legacy));
        Assert.True(PasswordHasher.Verify("admin", legacy));
    }

    [Fact]
    public void IsLegacyHash_DetectsPlaintextAdmin()
    {
        Assert.True(PasswordHasher.IsLegacyHash("admin"));
    }
}
