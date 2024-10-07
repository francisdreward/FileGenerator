using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;

namespace FileGenerator;

public class Program
{
    public static List<string> TransactionCode = new List<string>() { "05", "06" };
    public static List<string> TransactionTokenIndicator = new List<string>() { "0", "1" };
    public static void Main()
    {
        var settings = LoadSettings();

        Console.WriteLine("Beginning process...");
        List<Transaction> records = new List<Transaction>();
        Random random = new Random();

        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = "|"
        };

        try
        {
            // Get files
            Console.WriteLine("Grabbing file");
            using (var reader = new StreamReader(settings.FilePath + "Input/gary-tests.csv"))
            using (var csv = new CsvReader(reader, config))
            {
                records = csv.GetRecords<Transaction>().ToList();
            }
            Console.WriteLine("Creating test data...");

            int promoCodeIndex = 0;
            int promoCodeCount = settings.Promocodes?.Count ?? 0;

            foreach (var record in records)
            {
                if (promoCodeCount > 0)
                {
                    record.PromoCode = settings.Promocodes[promoCodeIndex];
                    promoCodeIndex++;
                    if (promoCodeIndex >= promoCodeCount)
                    {
                        promoCodeIndex = 0;
                    }
                }
                record.MerchantAcquirerBin = Faker.RandomNumber.Next(100000, 999999).ToString();
                record.MerchantCardAcceptorId = Faker.RandomNumber.Next(111111111, 999999999).ToString();
                record.SettlementDate = GetRandomDateBetweenRange(DateTime.Now.AddDays(-90), DateTime.Now.AddDays(-20), settings.DateFormat);
                record.SettlementAmount = (random.NextDouble() * 100 + random.NextDouble()).ToString();
                record.SettlementCurrencyCodeNumeric = Faker.RandomNumber.Next(100, 999).ToString();
                record.SettlementBillingAmount = (random.NextDouble() * 100 + random.NextDouble()).ToString();
                record.SettlementBillingCurrency = Faker.Currency.ThreeLetterCode();
                record.SettlementUSDAmount = (random.NextDouble() * 100 + random.NextDouble()).ToString();
                record.MerchantCity = Faker.Address.City();
                record.MerchantState = Faker.Address.UkCounty();
                record.MerchantPostalCode = Faker.Address.UkPostCode();
                record.MerchantName = Faker.Company.Name();
                record.MerchantLocalPurchaseDate = GetRandomDateBetweenRange(DateTime.Now.AddDays(-120), DateTime.Now.AddDays(-50), settings.DateFormat);
                record.MerchantDateTimeGMT = GetRandomDateBetweenRange(DateTime.Now.AddDays(-90), DateTime.Now.AddDays(-20), settings.DateTimeFormat);
                record.VipTransactionId = Faker.RandomNumber.Next(1000, 9999).ToString();
                record.AuthCode = Faker.RandomNumber.Next(9).ToString();
                record.MerchantCategoryCode = Faker.RandomNumber.Next(0, 9).ToString();
                int transactionIndex = random.Next(TransactionTokenIndicator.Count);
                record.TransactionCode = TransactionCode[transactionIndex];
                record.TokenTransactionIndicator = TransactionTokenIndicator[transactionIndex];
                record.TimeStamp = DateTime.Now.ToString(settings.DateTimeFormat);
                record.CurrencyCodeNumeric = Faker.Currency.ThreeLetterCode();
                record.BillingAmount = (random.NextDouble() * 100 + random.NextDouble()).ToString();
                record.BillingCurrencyCode = Faker.Currency.ThreeLetterCode();
                record.VisaMerchantId = Faker.RandomNumber.Next(1000000, 9999999).ToString();
                record.VisaMerchantName = Faker.Company.Name();
                record.VisaStoreId = Faker.RandomNumber.Next(100000, 999999).ToString();
                record.VisaStoreName = Faker.Company.Name();
                record.TokenRequesterId = Faker.RandomNumber.Next(100000, 999999).ToString();
                record.POSEntryMode = Faker.RandomNumber.Next(0, 9).ToString();
            }

            Console.WriteLine($"{records.Count} transactions created with test data.");

            string properheaders = "SequenceNumber|User.RTMUserId|Card.CardId|User.PromoCode|Card.LastFour|Transaction.MerchantAcquirerBin|Transaction.MerchantCardAcceptorId|Transaction.SettlementDate|Transaction.SettlementAmount|Transaction.SettlementCurrencyCodeNumeric|Transaction.SettlementBillingAmount|Transaction.SettlementBillingCurrency|Transaction.SettlementUSDAmount|Transaction.MerchantCity|Transaction.MerchantState|Transaction.MerchantPostalCode|Transaction.MerchantCountryCode|Transaction.MerchantName|Transaction.MerchantLocalPurchaseDate|Transaction.MerchantDateTimeGMT|Transaction.VipTransactionId|Transaction.AuthCode|Transaction.MerchantCategoryCode|Transaction.TransactionCode|Transaction.TokenTransactionIndicator|Transaction.TimeStamp|Transaction.TransactionAmount|Transaction.CurrencyCodeNumeric|Transaction.BillingAmount|Transaction.BillingCurrencyCode|Transaction.VisaMerchantId|Transaction.VisaMerchantName|Transaction.VisaStoreId|Transaction.VisaStoreName|Transaction.TokenRequesterId|Transaction.POSEntryMode";
            string fileNameDate = DateTime.Now.AddDays(settings.AddDays).ToString(settings.DateFormat);
            string fileNameNumber = $"{Faker.RandomNumber.Next(111111, 999999)}_{Faker.RandomNumber.Next(111, 999)}";
            string fileType = settings.IsDaily ? "DAILY" : settings.IsHistoric ? "HISTORY" : "Unknown";
            string fileName = $"TRANSACTIONS-{fileType}_REWARDS_{fileNameDate}_{fileNameNumber}.data.csv";

            Console.WriteLine($"Writing {fileName}...");
            config.HasHeaderRecord = false;
            using (var writer = new StreamWriter($"{settings.FilePath}/Output/{fileName}"))
            using (var csv = new CsvWriter(writer, config))
            {
                writer.WriteLine(properheaders);
                csv.WriteRecords(records);
            }
            Console.WriteLine("Transaction written, now creating meta file.");

            // Need to read it again to hash...
            Metadata metaData = new Metadata()
            {
                clientCode = "Reward",
                dataFiles = new List<DataFile>()
                {
                    new DataFile()
                    {
                        fileName = fileName,
                        recordCount = records.Count, // For the header?
                        sha256Hash = GetSHA256Hash($"{settings.FilePath}Output/{fileName}")
                    }
                },
                dataType = $"TRANSACTIONS-{fileType}"
            };
            string metaFileName = $"TRANSACTIONS-{fileType}_REWARDS_{fileNameDate}_{fileNameNumber}.Metadata.json";
            Console.WriteLine($"Writing Meta file: {metaFileName}");
            string json = JsonSerializer.Serialize(metaData);
            File.WriteAllText($"{settings.FilePath}Output/{metaFileName}", json);

            // PGP Encrypt the files
            if (settings.Encrypt)
            {
                // Encrypt CSV file
                PgpEncryptionUtil.EncryptFile($"{settings.FilePath}Output/{fileName}", $"{settings.FilePath}Output/Encrypted/{fileName}.pgp", settings.PublicKeyPath);

                // Encrypt JSON Metadata file
                PgpEncryptionUtil.EncryptFile($"{settings.FilePath}Output/{metaFileName}", $"{settings.FilePath}Output/Encrypted/{metaFileName}.pgp", settings.PublicKeyPath);

                Console.WriteLine("Files have been PGP encrypted successfully.");
            }
            
            if (settings.Decrypt)
            {
                // test decrypt
                PgpEncryptionUtil.DecryptFile($"{settings.FilePath}Output/Encrypted/{fileName}.pgp", $"{settings.FilePath}Output/Decrypted/{fileName}", settings.PrivateKeyPath, "b9efe2LL");

                PgpEncryptionUtil.DecryptFile($"{settings.FilePath}Output/Encrypted/{metaFileName}.pgp", $"{settings.FilePath}Output/Decrypted/{metaFileName}", settings.PrivateKeyPath, "b9efe2LL");

                Console.WriteLine("Encrypted files have now been Decrypted.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Whoops: {ex.Message}");
        }
    }

    static AppSettings LoadSettings()
    {
        var json = File.ReadAllText("appsettings.json");
        return JsonSerializer.Deserialize<AppSettings>(json);
    }

    public static string GetRandomDateBetweenRange(DateTime startDate, DateTime endDate, string format)
    {
        Random random = new Random();
        TimeSpan span = endDate - startDate;
        TimeSpan newSpan = new TimeSpan(0, random.Next(0, (int)span.TotalMinutes), 0);
        DateTime newDate = startDate + newSpan;
        return newDate.ToString(format);
    }

    public static string GetSHA256Hash(string file)
    {
        using (FileStream stream = File.OpenRead(file))
        {
            var sha = new SHA256Managed();
            byte[] checksum = sha.ComputeHash(stream);
            return BitConverter.ToString(checksum).Replace("-", String.Empty).ToLower();
        }
    }
}

public class Transaction
{
    public string SequenceNumber { get; set; }
    public string RTMUserId { get; set; }
    public string CardId { get; set; }
    public string PromoCode { get; set; }
    public string LastFour { get; set; }
    public string MerchantAcquirerBin { get; set; }
    public string MerchantCardAcceptorId { get; set; }
    public string SettlementDate { get; set; }
    public string SettlementAmount { get; set; }
    public string SettlementCurrencyCodeNumeric { get; set; }
    public string SettlementBillingAmount { get; set; }
    public string SettlementBillingCurrency { get; set; }
    public string SettlementUSDAmount { get; set; }
    public string MerchantCity { get; set; }
    public string MerchantState { get; set; }
    public string MerchantPostalCode { get; set; }
    public string MerchantCountryCode { get; set; }
    public string MerchantName { get; set; }
    public string MerchantLocalPurchaseDate { get; set; }
    public string MerchantDateTimeGMT { get; set; }
    public string VipTransactionId { get; set; }
    public string AuthCode { get; set; }
    public string MerchantCategoryCode { get; set; }
    public string TransactionCode { get; set; }
    public string TokenTransactionIndicator { get; set; }
    public string TimeStamp { get; set; }
    public string TransactionAmount { get; set; }
    public string CurrencyCodeNumeric { get; set; }
    public string BillingAmount { get; set; }
    public string BillingCurrencyCode { get; set; }
    public string VisaMerchantId { get; set; }
    public string VisaMerchantName { get; set; }
    public string VisaStoreId { get; set; }
    public string VisaStoreName { get; set; }
    public string TokenRequesterId { get; set; }
    public string POSEntryMode { get; set; }
}

public class Metadata
{
    public string clientCode { get; set; }
    public List<DataFile> dataFiles { get; set; }
    public string dataType { get; set; }
}

public class DataFile
{
    public string sha256Hash { get; set; }
    public int recordCount { get; set; }
    public string fileName { get; set; }
    public DateTime? contentStartDate { get; set; }
    public DateTime? contentEndDate { get; set; }
}