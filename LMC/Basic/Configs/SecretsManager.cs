// Copyright 2025-2026 LinearTeam
// 
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
// 
//        http://www.apache.org/licenses/LICENSE-2.0
// 
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

using System.Collections.Concurrent;

namespace LMC.Basic.Configs;

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

public static class SecretsManager
{
    private readonly static string s_secretsPath = Path.Combine(Current.LMCPath, "secrets.json.enc");
    private readonly static string s_keyPath = Path.Combine(Current.LMCPath, "secrets.key");
    private readonly static object s_ioLock = new();
    private static Secrets s_instance = null!;
    private readonly static object s_instanceLock = new();
    public readonly static ConcurrentDictionary<string, string> SensitiveData = new();
    
    public static Secrets Instance
    {
        get
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
            if (s_instance == null)
            {
                lock (s_instanceLock)
                {
                    s_instance ??= LoadInternal();
                }
            }
            return s_instance;
        }
    }

    public static void Save()
    {
        Save(Instance);
    }

    public static void Save(Secrets secrets)
    {
        lock (s_ioLock)
        {
            var key = GetOrCreateKey();
            var json = JsonSerializer.Serialize(secrets, new JsonSerializerOptions{WriteIndented = true});
            var encrypted = Encrypt(json, key);
            File.WriteAllBytes(s_secretsPath, encrypted);
            s_instance = secrets;
        }
    }

    public static Secrets Load()
    {
        lock (s_ioLock)
        {
            return LoadInternal();
        }
    }

    private static Secrets LoadInternal()
    {
        var key = GetOrCreateKey();
        if (!File.Exists(s_secretsPath))
            return new Secrets();

        var encrypted = File.ReadAllBytes(s_secretsPath);
        var json = Decrypt(encrypted, key);
        return JsonSerializer.Deserialize<Secrets>(json) ?? new Secrets();
    }

    private static byte[] GetOrCreateKey()
    {
        if (File.Exists(s_keyPath))
            return File.ReadAllBytes(s_keyPath);

        using var rng = RandomNumberGenerator.Create();
        byte[] key = new byte[24]; 
        rng.GetBytes(key);
        File.WriteAllBytes(s_keyPath, key);
        return key;
    }

    private static byte[] Encrypt(string plainText, byte[] key)
    {
        using var tdes = TripleDES.Create();
        tdes.Key = key;
        tdes.Mode = CipherMode.CBC;
        tdes.Padding = PaddingMode.PKCS7;
        tdes.GenerateIV();
        using var ms = new MemoryStream();
        ms.Write(tdes.IV, 0, tdes.IV.Length);
        using var cs = new CryptoStream(ms, tdes.CreateEncryptor(), CryptoStreamMode.Write);
        var bytes = Encoding.UTF8.GetBytes(plainText);
        cs.Write(bytes, 0, bytes.Length);
        cs.FlushFinalBlock();
        return ms.ToArray();
    }

    private static string Decrypt(byte[] cipherData, byte[] key)
    {
        using var tdes = TripleDES.Create();
        tdes.Key = key;
        tdes.Mode = CipherMode.CBC;
        tdes.Padding = PaddingMode.PKCS7;
        byte[] iv = new byte[tdes.BlockSize / 8];
        Array.Copy(cipherData, 0, iv, 0, iv.Length);
        tdes.IV = iv;
        using var ms = new MemoryStream(cipherData, iv.Length, cipherData.Length - iv.Length);
        using var cs = new CryptoStream(ms, tdes.CreateDecryptor(), CryptoStreamMode.Read);
        using var sr = new StreamReader(cs, Encoding.UTF8);
        return sr.ReadToEnd();
    }
}