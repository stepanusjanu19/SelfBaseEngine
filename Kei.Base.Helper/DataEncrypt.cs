using Kei.Base.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Kei.Base.Helper
{
    public class DataEncrypt
    {

        public static string base64Encode(string data)
        {
            try
            {
                byte[] encData_byte = new byte[data.Length];
                encData_byte = System.Text.Encoding.UTF8.GetBytes(data);
                string encodedData = Convert.ToBase64String(encData_byte);
                return encodedData;
            }
            catch
            {
                return data;
            }
        }

        public static string base64Decode(string data)
        {
            try
            {
                System.Text.UTF8Encoding encoder = new System.Text.UTF8Encoding();
                System.Text.Decoder utf8Decode = encoder.GetDecoder();

                byte[] todecode_byte = Convert.FromBase64String(data);
                int charCount = utf8Decode.GetCharCount(todecode_byte, 0, todecode_byte.Length);
                char[] decoded_char = new char[charCount];
                utf8Decode.GetChars(todecode_byte, 0, todecode_byte.Length, decoded_char, 0);
                string result = new String(decoded_char);
                return result;
            }
            catch
            {
                return data;
            }
        }

        /// <summary>
        /// Encrypts specified plaintext using Rijndael symmetric key algorithm
        /// and returns a base64-encoded result.
        /// </summary>
        public static string Encrypt(string plainText)
        {
            try
            {
                int keySize = EnvironmentCrypto.KEYSIZE;
                string initVector = EnvironmentCrypto.INITVECTOR;
                int passwordIterations = EnvironmentCrypto.PASSWORDITERATIONS;
                string hashAlgorithm = EnvironmentCrypto.HASHALGORITHM;
                string saltValue = EnvironmentCrypto.SALTVALUE;
                string passPhrase = EnvironmentCrypto.PASSPHRASE;

                // Convert strings into byte arrays.
                // Let us assume that strings only contain ASCII codes.
                // If strings include Unicode characters, use Unicode, UTF7, or UTF8
                // encoding.
                byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
                byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);

                // Convert our plaintext into a byte array.
                // Let us assume that plaintext contains UTF8-encoded characters.
                byte[] plainTextBytes = Encoding.UTF8.GetBytes(plainText);

                // First, we must create a password, from which the key will be derived.
                // This password will be generated from the specified passphrase and
                // salt value. The password will be created using the specified hash
                // algorithm. Password creation can be done in several iterations.
                PasswordDeriveBytes password = new PasswordDeriveBytes(
                                                                passPhrase,
                                                                saltValueBytes,
                                                                hashAlgorithm,
                                                                passwordIterations);

                // Use the password to generate pseudo-random bytes for the encryption
                // key. Specify the size of the key in bytes (instead of bits).
                byte[] keyBytes = password.GetBytes(keySize / 8);

                // Create uninitialized Rijndael encryption object.
#pragma warning disable SYSLIB0022 // RijndaelManaged is obsolete — kept for backward compatibility. Use EncryptAes for new code.
                RijndaelManaged symmetricKey = new RijndaelManaged();
#pragma warning restore SYSLIB0022

                // It is reasonable to set encryption mode to Cipher Block Chaining
                // (CBC). Use default options for other symmetric key parameters.
                symmetricKey.Mode = CipherMode.CBC;

                // Generate encryptor from the existing key bytes and initialization
                // vector. Key size will be defined based on the number of the key
                // bytes.
                ICryptoTransform encryptor = symmetricKey.CreateEncryptor(
                                                                    keyBytes,
                                                                    initVectorBytes);

                // Define memory stream which will be used to hold encrypted data.
                MemoryStream memoryStream = new MemoryStream();

                // Define cryptographic stream (always use Write mode for encryption).
                CryptoStream cryptoStream = new CryptoStream(memoryStream,
                                                                encryptor,
                                                                CryptoStreamMode.Write);
                // Start encrypting.
                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);

                // Finish encrypting.
                cryptoStream.FlushFinalBlock();

                // Convert our encrypted data from a memory stream into a byte array.
                byte[] cipherTextBytes = memoryStream.ToArray();

                // Close both streams.
                memoryStream.Close();
                cryptoStream.Close();

                // Convert encrypted data into a base64-encoded string.
                string cipherText = Convert.ToBase64String(cipherTextBytes);

                // Return encrypted string.
                return cipherText;
            }
            catch
            {
                return plainText;
            }
        }

        /// <summary>
        /// Decrypts specified ciphertext using Rijndael symmetric key algorithm.
        /// </summary>
        public static string Decrypt(string cipherText)
        {
            try
            {
                int keySize = EnvironmentCrypto.KEYSIZE;
                string initVector = EnvironmentCrypto.INITVECTOR;
                int passwordIterations = EnvironmentCrypto.PASSWORDITERATIONS;
                string hashAlgorithm = EnvironmentCrypto.HASHALGORITHM;
                string saltValue = EnvironmentCrypto.SALTVALUE;
                string passPhrase = EnvironmentCrypto.PASSPHRASE;

                // Convert strings defining encryption key characteristics into byte
                // arrays. Let us assume that strings only contain ASCII codes.
                // If strings include Unicode characters, use Unicode, UTF7, or UTF8
                // encoding.
                byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
                byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);

                // Convert our ciphertext into a byte array.
                byte[] cipherTextBytes = Convert.FromBase64String(cipherText);

                // First, we must create a password, from which the key will be
                // derived. This password will be generated from the specified
                // passphrase and salt value. The password will be created using
                // the specified hash algorithm. Password creation can be done in
                // several iterations.
                PasswordDeriveBytes password = new PasswordDeriveBytes(
                                                                passPhrase,
                                                                saltValueBytes,
                                                                hashAlgorithm,
                                                                passwordIterations);

                // Use the password to generate pseudo-random bytes for the encryption
                // key. Specify the size of the key in bytes (instead of bits).
                byte[] keyBytes = password.GetBytes(keySize / 8);

                // Create uninitialized Rijndael encryption object.
#pragma warning disable SYSLIB0022 // RijndaelManaged is obsolete — kept for backward compatibility. Use DecryptAes for new code.
                RijndaelManaged symmetricKey = new RijndaelManaged();
#pragma warning restore SYSLIB0022

                // It is reasonable to set encryption mode to Cipher Block Chaining
                // (CBC). Use default options for other symmetric key parameters.
                symmetricKey.Mode = CipherMode.CBC;

                // Generate decryptor from the existing key bytes and initialization
                // vector. Key size will be defined based on the number of the key
                // bytes.
                ICryptoTransform decryptor = symmetricKey.CreateDecryptor(
                                                                    keyBytes,
                                                                    initVectorBytes);

                // Define memory stream which will be used to hold encrypted data.
                MemoryStream memoryStream = new MemoryStream(cipherTextBytes);

                // Define cryptographic stream (always use Read mode for encryption).
                CryptoStream cryptoStream = new CryptoStream(memoryStream,
                                                                decryptor,
                                                                CryptoStreamMode.Read);

                // Since at this point we don't know what the size of decrypted data
                // will be, allocate the buffer long enough to hold ciphertext;
                // plaintext is never longer than ciphertext.
                byte[] plainTextBytes = new byte[cipherTextBytes.Length];

                // Start decrypting.
                int decryptedByteCount = cryptoStream.Read(plainTextBytes,
                                                            0,
                                                            plainTextBytes.Length);

                // Close both streams.
                memoryStream.Close();
                cryptoStream.Close();

                // Convert decrypted data into a string.
                // Let us assume that the original plaintext string was UTF8-encoded.
                string plainText = Encoding.UTF8.GetString(plainTextBytes,
                                                            0,
                                                            decryptedByteCount);

                // Return decrypted string.
                return plainText;
            }
            catch
            {
                return cipherText;
            }
        }

        /// <summary>
        /// Computing hash from string with given hash algorithm
        /// </summary>
        /// <param name="plainText">string to be hashed</param>
        /// <param name="hashAlgorithm">SHA1 or MD5</param>
        /// <returns></returns>
        public static string ComputeHash(string plainText, string hashAlgorithm)
        {
            if (hashAlgorithm == null)
                hashAlgorithm = "";
            if (hashAlgorithm.ToUpper() == "MD5")
            {
                MD5 md5 = System.Security.Cryptography.MD5.Create();
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(plainText);
                byte[] hashmd5 = md5.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashmd5.Length; i++)
                {
                    sb.Append(hashmd5[i].ToString("x2"));
                }
                return sb.ToString();
            }
            else if (hashAlgorithm.ToUpper() == "SHA1")
            {
#pragma warning disable SYSLIB0021 // SHA1Managed is obsolete — kept for backward compatibility. Use ComputeHash(text, HashAlgorithmName.SHA256) for new code.
                SHA1Managed sha1 = new SHA1Managed();
#pragma warning restore SYSLIB0021
                byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(plainText);
                byte[] hashmd5 = sha1.ComputeHash(inputBytes);

                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashmd5.Length; i++)
                {
                    sb.Append(hashmd5[i].ToString("x2"));
                }
                return sb.ToString();
            }
            return "";
        }

        // ─── AES-256 (Modern Replacement for RijndaelManaged) ─────────────────────

        /// <summary>
        /// Encrypts <paramref name="plainText"/> using AES-256-CBC with a random IV.
        /// The key is derived from <see cref="EnvironmentCrypto"/> when not supplied.
        /// Returns a Base64 string: [16-byte IV] + [cipher text].
        /// </summary>
        /// <param name="plainText">The plain text to encrypt.</param>
        /// <param name="base64Key">Optional Base64-encoded 32-byte key. Defaults to <see cref="EnvironmentCrypto.PASSPHRASE"/>-derived key.</param>
        public static string EncryptAes(string plainText, string? base64Key = null)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText ?? string.Empty;

            try
            {
                var key = ResolveAesKey(base64Key);

                using var aes = Aes.Create();
                aes.Key = key;
                aes.Mode = CipherMode.CBC;
                aes.GenerateIV();

                using var encryptor = aes.CreateEncryptor();
                using var ms = new MemoryStream();

                // Prepend IV so decryption can recover it
                ms.Write(aes.IV, 0, aes.IV.Length);

                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    var plainBytes = Encoding.UTF8.GetBytes(plainText);
                    cs.Write(plainBytes, 0, plainBytes.Length);
                    cs.FlushFinalBlock();
                }

                return Convert.ToBase64String(ms.ToArray());
            }
            catch
            {
                return plainText;
            }
        }

        /// <summary>
        /// Decrypts an AES-256-CBC cipher text produced by <see cref="EncryptAes"/>.
        /// Expects the Base64 input to contain [16-byte IV] + [cipher text].
        /// </summary>
        /// <param name="cipherText">Base64 cipher text as returned by <see cref="EncryptAes"/>.</param>
        /// <param name="base64Key">Optional Base64-encoded 32-byte key. Defaults to <see cref="EnvironmentCrypto.PASSPHRASE"/>-derived key.</param>
        public static string DecryptAes(string cipherText, string? base64Key = null)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText ?? string.Empty;

            try
            {
                var key = ResolveAesKey(base64Key);
                var fullBytes = Convert.FromBase64String(cipherText);

                if (fullBytes.Length < 16)
                    return cipherText;

                var iv = fullBytes[..16];
                var encrypted = fullBytes[16..];

                using var aes = Aes.Create();
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;

                using var decryptor = aes.CreateDecryptor();
                using var ms = new MemoryStream(encrypted);
                using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
                using var sr = new System.IO.StreamReader(cs, Encoding.UTF8);

                return sr.ReadToEnd();
            }
            catch
            {
                return cipherText;
            }
        }

        /// <summary>
        /// Computes a hash of <paramref name="plainText"/> using the specified
        /// <see cref="HashAlgorithmName"/>. Supports SHA256, SHA512, SHA384, SHA1, MD5.
        /// Prefers SHA256 or stronger for new code.
        /// </summary>
        /// <param name="plainText">String to hash.</param>
        /// <param name="algorithm">Hash algorithm (e.g. <see cref="HashAlgorithmName.SHA256"/>).</param>
        public static string ComputeHash(string plainText, HashAlgorithmName algorithm)
        {
            if (string.IsNullOrEmpty(plainText)) return string.Empty;

            var inputBytes = Encoding.UTF8.GetBytes(plainText);
            byte[] hashBytes;

            if (algorithm == HashAlgorithmName.SHA256)
                hashBytes = SHA256.HashData(inputBytes);
            else if (algorithm == HashAlgorithmName.SHA512)
                hashBytes = SHA512.HashData(inputBytes);
            else if (algorithm == HashAlgorithmName.SHA384)
                hashBytes = SHA384.HashData(inputBytes);
            else if (algorithm == HashAlgorithmName.SHA1)
            {
#pragma warning disable CA5350 // SHA1 is weak — provided for legacy compatibility only
                hashBytes = SHA1.HashData(inputBytes);
#pragma warning restore CA5350
            }
            else if (algorithm == HashAlgorithmName.MD5)
            {
#pragma warning disable CA5351 // MD5 is weak — provided for legacy compatibility only
                hashBytes = MD5.HashData(inputBytes);
#pragma warning restore CA5351
            }
            else
            {
#pragma warning disable SYSLIB0045 // HashAlgorithm.Create(string) is obsolete — used as fallback for unknown algorithm names
                using var ha = System.Security.Cryptography.HashAlgorithm.Create(algorithm.Name!)!;
#pragma warning restore SYSLIB0045
                hashBytes = ha.ComputeHash(inputBytes);
            }

            return Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        /// <summary>
        /// Zeroes a byte array in memory to reduce the window in which
        /// sensitive key material remains accessible. Safe to call with <c>null</c>.
        /// </summary>
        public static void SecureClear(byte[]? buffer)
        {
            if (buffer == null) return;
            CryptographicOperations.ZeroMemory(buffer);
        }

        // ─── Private Helpers ──────────────────────────────────────────────────────

        private static byte[] ResolveAesKey(string? base64Key)
        {
            if (!string.IsNullOrEmpty(base64Key))
            {
                var k = Convert.FromBase64String(base64Key);
                if (k.Length != 32)
                    throw new ArgumentException("AES key must be exactly 32 bytes (256 bits).", nameof(base64Key));
                return k;
            }

            // Derive a 256-bit key from the existing EnvironmentCrypto passphrase using PBKDF2
            var salt = Encoding.ASCII.GetBytes(EnvironmentCrypto.SALTVALUE);
            using var kdf = new Rfc2898DeriveBytes(
                EnvironmentCrypto.PASSPHRASE,
                salt,
                EnvironmentCrypto.PASSWORDITERATIONS,
                HashAlgorithmName.SHA256);
            return kdf.GetBytes(32);
        }
    }
}
