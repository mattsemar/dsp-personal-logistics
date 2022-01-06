using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using PersonalLogistics.Util;

// shamelessly stolen from this project https://github.com/hubastard/nebula
// Link to actual version
// https://github.com/hubastard/nebula/blob/1b0b758df85bd627b85050d75072c603e407f16c/NebulaModel/Utils/CryptoUtils.cs
namespace PersonalLogistics.ModPlayer
{
    public static class CryptoUtils
    {
        public static string NebulaModKeyFilePath = Path.Combine(new[] { Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), GameConfig.gameName, "player.key" });
        public static string PlogKeyFilePath = Path.Combine(GameConfig.overrideDocumentFolder, GameConfig.gameName, "PersonalLogistics", "player.plog.key");

        public static RSA GetOrCreateUserCert()
        {
            RSA rsa = RSA.Create();
            if (File.Exists(NebulaModKeyFilePath))
            {
                // Make a copy of the nebula client key that we can use without worrying
                CopyNebulaKey(NebulaModKeyFilePath, PlogKeyFilePath);
                rsa.FromXmlString(File.ReadAllText(PlogKeyFilePath));
            }
            else
            {
                // Use the nebula algo to create a key file but store it in an alternate location to avoid
                // breaking if they change their format (or whatever)
                File.WriteAllText(PlogKeyFilePath, rsa.ToXmlString(true));
            }

            return rsa;
        }

        private static void CopyNebulaKey(string sourceFile, string destFile)
        {
            var destDirName = Path.GetDirectoryName(destFile);
            if (!Directory.Exists(destDirName) && !string.IsNullOrWhiteSpace(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            if (File.Exists(destFile))
            {
                Log.Debug($"not overwriting our copy of nebula client key at {destFile}");
                return;
            }

            File.Copy(sourceFile, destFile);
        }

        public static byte[] GetPublicKey(RSA rsa)
        {
            return Convert.FromBase64String(rsa.ToXmlString(false).Substring(22, 172));
        }

        public static string Hash(byte[] input)
        {
            byte[] hash = new SHA1Managed().ComputeHash(input);
            return Convert.ToBase64String(hash);
        }

        public static string GetCurrentUserPublicKeyHash()
        {
            return Hash(GetPublicKey(GetOrCreateUserCert()));
        }

        public static Guid GetCurrentUserAndGameIdentifierGuid()
        {
            // yes MD5 is bad, ok, but we really just want a simple way to create a Guid 
            // that tells us that a user is the same one that connected yesterday
            using (MD5 md5 = MD5.Create())
            {
                byte[] insecureHash = md5.ComputeHash(Encoding.Default.GetBytes(GetCurrentUserPublicKeyHash()));
                return new Guid(insecureHash);
            }
        }
    }
}