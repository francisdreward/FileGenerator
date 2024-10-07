```markdown
# FileGenerator

The `FileGenerator` project allows for the generation of output files with configurable settings.

## AppSettings

The `AppSettings` class provides the following configuration options:

- `DateFormat`: Specifies the format for date values.
- `DateTimeFormat`: Specifies the format for date and time values.
- `AddDays`: Specifies the number of days to add for file name date.
- `IsHistoric`: Indicates whether the generation is for historic data.
- `IsDaily`: Indicates whether the generation is for daily data.
- `Encrypt`: Indicates whether the output file should be encrypted.
- `Decrypt`: Indicates whether the input file should be decrypted.
- `Promocodes`: A list of promotional codes.
- `FilePath`: The path where the output file should be saved.
- `PublicKeyPath`: The path to the public key for encryption.
- `PrivateKeyPath`: The path to the private key for decryption.

## Usage

To use the `FileGenerator` project, follow these steps:

1. Configure the desired settings in the `AppSettings` class.
2. Build and run the project.
3. The output file will be generated based on the specified settings.

```