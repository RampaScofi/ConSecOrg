using ConSecOrg.Domain.Exceptions;
using ConSecOrg.Domain.ValueObjects;
using ConSecOrg.Infrastructure.Crypto;
using FluentAssertions;

namespace ConSecOrg.Infrastructure.Tests.Crypto;

public class GostCryptoServiceTests
{
    private readonly GostCryptoService _sut = new();
    private static readonly byte[] ValidKey64 = new byte[64]; // 32 crypto + 32 HMAC

    static GostCryptoServiceTests()
    {
        // Deterministic test key: bytes 0x01..0x40
        for (int i = 0; i < 64; i++) ValidKey64[i] = (byte)(i + 1);
    }

    [Fact]
    public void Encrypt_ThenDecrypt_RoundTrip_RestoresPlaintext()
    {
        var plaintext = System.Text.Encoding.UTF8.GetBytes("Hello, GOST Kuznechik!");

        var encrypted = _sut.Encrypt(plaintext, ValidKey64);
        var decrypted = _sut.Decrypt(encrypted, ValidKey64);

        decrypted.Should().BeEquivalentTo(plaintext, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Encrypt_EmptyPlaintext_RoundTrip()
    {
        var plaintext = Array.Empty<byte>();

        var encrypted = _sut.Encrypt(plaintext, ValidKey64);
        var decrypted = _sut.Decrypt(encrypted, ValidKey64);

        decrypted.Should().BeEmpty();
    }

    [Fact]
    public void Encrypt_LargePlaintext_RoundTrip()
    {
        var plaintext = new byte[4096];
        Random.Shared.NextBytes(plaintext);

        var encrypted = _sut.Encrypt(plaintext, ValidKey64);
        var decrypted = _sut.Decrypt(encrypted, ValidKey64);

        decrypted.Should().BeEquivalentTo(plaintext, o => o.WithStrictOrdering());
    }

    [Fact]
    public void Encrypt_TwoCallsWithSamePlaintext_ProduceDifferentCiphertexts()
    {
        // Unique nonce each time
        var plaintext = System.Text.Encoding.UTF8.GetBytes("same text");

        var enc1 = _sut.Encrypt(plaintext, ValidKey64);
        var enc2 = _sut.Encrypt(plaintext, ValidKey64);

        enc1.Nonce.Should().NotBeEquivalentTo(enc2.Nonce, "nonce must be random");
        enc1.CipherText.Should().NotBeEquivalentTo(enc2.CipherText);
    }

    [Fact]
    public void Decrypt_TamperedCiphertext_ThrowsIntegrityViolationException()
    {
        var plaintext = System.Text.Encoding.UTF8.GetBytes("sensitive data");
        var encrypted = _sut.Encrypt(plaintext, ValidKey64);

        // Flip one bit in the ciphertext
        var tamperedBytes = (byte[])encrypted.CipherText.Clone();
        tamperedBytes[0] ^= 0xFF;
        var tampered = new EncryptedContent(tamperedBytes, encrypted.Nonce, encrypted.Hmac);

        var act = () => _sut.Decrypt(tampered, ValidKey64);
        act.Should().Throw<IntegrityViolationException>();
    }

    [Fact]
    public void Decrypt_TamperedNonce_ThrowsIntegrityViolationException()
    {
        var plaintext = System.Text.Encoding.UTF8.GetBytes("sensitive data");
        var encrypted = _sut.Encrypt(plaintext, ValidKey64);

        var tamperedNonce = (byte[])encrypted.Nonce.Clone();
        tamperedNonce[0] ^= 0x01;
        var tampered = new EncryptedContent(encrypted.CipherText, tamperedNonce, encrypted.Hmac);

        var act = () => _sut.Decrypt(tampered, ValidKey64);
        act.Should().Throw<IntegrityViolationException>();
    }

    [Fact]
    public void Decrypt_TamperedHmac_ThrowsIntegrityViolationException()
    {
        var plaintext = System.Text.Encoding.UTF8.GetBytes("sensitive data");
        var encrypted = _sut.Encrypt(plaintext, ValidKey64);

        var tamperedHmac = (byte[])encrypted.Hmac.Clone();
        tamperedHmac[31] ^= 0xFF;
        var tampered = new EncryptedContent(encrypted.CipherText, encrypted.Nonce, tamperedHmac);

        var act = () => _sut.Decrypt(tampered, ValidKey64);
        act.Should().Throw<IntegrityViolationException>();
    }

    [Fact]
    public void Decrypt_WrongKey_ThrowsIntegrityViolationException()
    {
        var plaintext = System.Text.Encoding.UTF8.GetBytes("data");
        var encrypted = _sut.Encrypt(plaintext, ValidKey64);

        var wrongKey = new byte[64];
        Random.Shared.NextBytes(wrongKey);

        var act = () => _sut.Decrypt(encrypted, wrongKey);
        act.Should().Throw<IntegrityViolationException>();
    }

    [Fact]
    public void Encrypt_NullPlaintext_ThrowsArgumentNullException()
    {
        var act = () => _sut.Encrypt(null!, ValidKey64);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Encrypt_WrongKeyLength_ThrowsArgumentException()
    {
        var act = () => _sut.Encrypt([], new byte[32]);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Hash256_SameInput_ProducesSameHash()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("test data");

        var h1 = _sut.Hash256(data);
        var h2 = _sut.Hash256(data);

        h1.Should().BeEquivalentTo(h2, o => o.WithStrictOrdering());
        h1.Should().HaveCount(32);
    }

    [Fact]
    public void Hash256_DifferentInputs_ProduceDifferentHashes()
    {
        var h1 = _sut.Hash256(System.Text.Encoding.UTF8.GetBytes("input A"));
        var h2 = _sut.Hash256(System.Text.Encoding.UTF8.GetBytes("input B"));

        h1.Should().NotBeEquivalentTo(h2);
    }

    [Fact]
    public void ComputeHmac_ThenVerify_ReturnsTrue()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("authenticated message");
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        var mac = _sut.ComputeHmac(data, key);
        var valid = _sut.VerifyHmac(data, key, mac);

        valid.Should().BeTrue();
    }

    [Fact]
    public void VerifyHmac_TamperedData_ReturnsFalse()
    {
        var data = System.Text.Encoding.UTF8.GetBytes("authenticated message");
        var key = new byte[32];
        Random.Shared.NextBytes(key);

        var mac = _sut.ComputeHmac(data, key);
        data[0] ^= 0x01;
        var valid = _sut.VerifyHmac(data, key, mac);

        valid.Should().BeFalse();
    }
}
