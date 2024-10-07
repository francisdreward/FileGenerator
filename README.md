
The following appsettings allow for the configuration of the output file.

public class AppSettings
{ 
    public string DateFormat { get; set; }
    public string DateTimeFormat { get; set; }
    //AddDays used for file name date
    public int AddDays { get; set; }
    public bool IsHistoric { get; set; }
    public bool IsDaily { get; set; }
    public bool Encrypt { get; set; }
    public bool Decrypt { get; set; }
    public List<string>? Promocodes { get; set; }
    public string? FilePath { get; set; }
    public string PublicKeyPath { get; set; }
    public string PrivateKeyPath { get; set; }
}
