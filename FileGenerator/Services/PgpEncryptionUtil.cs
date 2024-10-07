using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO;
using System.IO;

public static class PgpEncryptionUtil
{
    public static void EncryptFile(string inputFilePath, string outputFilePath, string publicKeyPath, bool armor = true, bool withIntegrityCheck = true)
    {
        using (Stream publicKeyStream = File.OpenRead(publicKeyPath))
        using (Stream outputStream = File.Create(outputFilePath))
        using (Stream encryptedOut = armor ? new ArmoredOutputStream(outputStream) : outputStream)
        {
            PgpPublicKey encKey = ReadPublicKey(publicKeyStream);
            using (MemoryStream memoryStream = new MemoryStream())
            {
                EncryptFile(memoryStream, inputFilePath, encKey, withIntegrityCheck);
                memoryStream.Seek(0, SeekOrigin.Begin);
                memoryStream.CopyTo(encryptedOut);
            }
        }
    }

    public static void DecryptFile(string inputFilePath, string outputFilePath, string privateKeyPath, string passPhrase)
    {
        // Ensure the output directory exists
        string outputDirectory = Path.GetDirectoryName(outputFilePath);
        if (!Directory.Exists(outputDirectory))
        {
            Directory.CreateDirectory(outputDirectory);
        }

        using (Stream inputStream = File.OpenRead(inputFilePath))
        using (Stream keyIn = File.OpenRead(privateKeyPath))
        using (MemoryStream memoryStream = new MemoryStream())
        {
            DecryptFile(inputStream, memoryStream, keyIn, passPhrase.ToCharArray());
            memoryStream.Seek(0, SeekOrigin.Begin);
            using (Stream outputStream = File.Create(outputFilePath))
            {
                memoryStream.CopyTo(outputStream);
            }
        }
    }

    private static PgpPublicKey ReadPublicKey(Stream publicKeyStream)
    {
        PgpPublicKeyRingBundle pgpPub = new PgpPublicKeyRingBundle(PgpUtilities.GetDecoderStream(publicKeyStream));
        foreach (PgpPublicKeyRing kRing in pgpPub.GetKeyRings())
        {
            foreach (PgpPublicKey key in kRing.GetPublicKeys())
            {
                if (key.IsEncryptionKey)
                {
                    return key;
                }
            }
        }
        throw new ArgumentException("No encryption key found in public key ring.");
    }

    private static PgpPrivateKey FindSecretKey(Stream keyIn, long keyID, char[] passPhrase)
    {
        PgpSecretKeyRingBundle pgpSec = new PgpSecretKeyRingBundle(PgpUtilities.GetDecoderStream(keyIn));
        PgpSecretKey pgpSecKey = pgpSec.GetSecretKey(keyID);

        if (pgpSecKey == null)
        {
            return null;
        }

        return pgpSecKey.ExtractPrivateKey(passPhrase);
    }

    private static void EncryptFile(Stream outputStream, string inputFilePath, PgpPublicKey encKey, bool withIntegrityCheck)
    {
        try
        {
            var bytes = File.ReadAllBytes(inputFilePath);
            var bOut = new MemoryStream();

            var comData = new PgpCompressedDataGenerator(CompressionAlgorithmTag.Zip);
            var cos = comData.Open(bOut); // open it with the final destination
            var pOut = new PgpLiteralDataGenerator().Open(cos, PgpLiteralData.Binary, new FileInfo(inputFilePath));
            pOut.Write(bytes, 0, bytes.Length);
            pOut.Close();
            comData.Close();

            var cBytes = bOut.ToArray();
            var encGen = new PgpEncryptedDataGenerator(SymmetricKeyAlgorithmTag.Cast5, withIntegrityCheck, new SecureRandom());
            encGen.AddMethod(encKey);

            var cOut = encGen.Open(outputStream, cBytes.Length);
            cOut.Write(cBytes, 0, cBytes.Length);
            cOut.Close();
        }
        catch (PgpException e)
        {
            throw new IOException("Error in encryption", e);
        }
    }

    private static void DecryptFile(Stream inputStream, Stream outputStream, Stream keyIn, char[] passPhrase)
    {
        inputStream = PgpUtilities.GetDecoderStream(inputStream);
        PgpObjectFactory pgpF = new PgpObjectFactory(inputStream);
        PgpEncryptedDataList enc;

        PgpObject o = pgpF.NextPgpObject();
        if (o is PgpEncryptedDataList)
        {
            enc = (PgpEncryptedDataList)o;
        }
        else
        {
            enc = (PgpEncryptedDataList)pgpF.NextPgpObject();
        }

        PgpPrivateKey sKey = null;
        PgpPublicKeyEncryptedData pbe = null;
        foreach (PgpPublicKeyEncryptedData pked in enc.GetEncryptedDataObjects())
        {
            sKey = FindSecretKey(keyIn, pked.KeyId, passPhrase);

            if (sKey != null)
            {
                pbe = pked;
                break;
            }
        }

        if (sKey == null)
        {
            throw new ArgumentException("No private key found in secret key ring.");
        }

        Stream clear = pbe.GetDataStream(sKey);
        PgpObjectFactory plainFact = new PgpObjectFactory(clear);
        PgpObject message = plainFact.NextPgpObject();

        if (message is PgpCompressedData cData)
        {
            PgpObjectFactory pgpFact = new PgpObjectFactory(cData.GetDataStream());
            message = pgpFact.NextPgpObject();
        }

        if (message is PgpLiteralData ld)
        {
            Stream unc = ld.GetInputStream();
            Streams.PipeAll(unc, outputStream);
        }
        else if (message is PgpOnePassSignatureList)
        {
            throw new PgpException("Encrypted message contains a signed message - not literal data.");
        }
        else
        {
            throw new PgpException("Message is not a simple encrypted file - type unknown.");
        }

        if (pbe.IsIntegrityProtected() && !pbe.Verify())
        {
            throw new PgpException("Message failed integrity check.");
        }
    }
}