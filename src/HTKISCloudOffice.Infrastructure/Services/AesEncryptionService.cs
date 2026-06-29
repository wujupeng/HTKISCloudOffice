using System.Security.Cryptography;
using System.Text;
using HTKISCloudOffice.Application.Interfaces;
using HTKISCloudOffice.Infrastructure.Configuration;

namespace HTKISCloudOffice.Infrastructure.Services;

public class AesEncryptionService : IAesEncryptionService
{
    private readonly byte[] _key;

    public AesEncryptionService(AesEncryptionConfig config)
    {
        _key = Convert.FromBase64String(config.key);
    }

    public string Encrypt(string plain_text)
    {
        using var aes = Aes.Create();
        aes.Key = _key;
        aes.GenerateIV();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var encryptor = aes.CreateEncryptor();
        var plain_bytes = Encoding.UTF8.GetBytes(plain_text);
        var encrypted = encryptor.TransformFinalBlock(plain_bytes, 0, plain_bytes.Length);

        var result = new byte[aes.IV.Length + encrypted.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(encrypted, 0, result, aes.IV.Length, encrypted.Length);
        return Convert.ToBase64String(result);
    }

    public string Decrypt(string cipher_text)
    {
        var bytes = Convert.FromBase64String(cipher_text);
        using var aes = Aes.Create();
        aes.Key = _key;
        var iv = new byte[16];
        Buffer.BlockCopy(bytes, 0, iv, 0, 16);
        aes.IV = iv;
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        using var decryptor = aes.CreateDecryptor();
        var cipher_data = new byte[bytes.Length - 16];
        Buffer.BlockCopy(bytes, 16, cipher_data, 0, cipher_data.Length);

        var decrypted = decryptor.TransformFinalBlock(cipher_data, 0, cipher_data.Length);
        return Encoding.UTF8.GetString(decrypted);
    }
}
