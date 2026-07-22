using System.Text;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math.EC.Rfc8032;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;


namespace KeyGen;

class Program
{
    static void Main(string[] args)
    {
        
        var keyPairGenerator = new Ed25519KeyPairGenerator();
        keyPairGenerator.Init(new Ed25519KeyGenerationParameters(new SecureRandom()));
        var keyPair = keyPairGenerator.GenerateKeyPair();
        var privateKeyParams = (Ed25519PrivateKeyParameters)keyPair.Private;
        var publicKeyParams = (Ed25519PublicKeyParameters)keyPair.Public;
        Console.WriteLine("PEM:");
        Console.WriteLine("\nBase64:");
        Console.WriteLine("private: \n{0}\npublic: \n{1}", Convert.ToBase64String(privateKeyParams.GetEncoded()) ,Convert.ToBase64String(publicKeyParams.GetEncoded()));
    }
}