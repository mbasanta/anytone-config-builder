# Anytone Config Builder - .NET 10 Edition

A comprehensive .NET 10 implementation of the Anytone Config Builder, ported from Perl. This tool helps ham radio operators generate Anytone CPS (Customer Programming Software) configuration files from source CSV data.

## Features

- **Build Configuration Files** – Generate CPS-compatible CSV files (channels, zones, scanlists, talkgroups) from analog, digital-others, and digital-repeaters input files
- **Channel Consistency Checker** – Validate your CPS exports for common configuration errors and inconsistencies
- **Undo Builder** – Reverse-engineer CPS export files back to source builder input format for editing
- **Advanced Sorting Options** – Alphabetical, repeaters-first, or analog-first zone/talkgroup ordering
- **Nickname Management** – Automatic nickname generation for repeater channels to avoid duplicates
- **Web UI** – Convenient web interface for all three tools
- **CLI Support** – Command-line interface for automation and scripting

## Project Structure

```
src/
  AnytoneConfigBuilder.Core/          # Shared business logic library
    Models/                           # Data models (BuildOptions, BuildResult, etc.)
    Processing/                       # Core engines (BuildPipeline, ChannelChecker, UndoBuilder)
    Readers/                          # CSV input parsers
    Validation/                       # Input validation
    Constants/                        # Channel field constants

    Resources/                    # Embedded data files (channel-defaults.json)
    ConfigBuilder.cs                  # Facade for core operations
  AnytoneConfigBuilder.Cli/           # Command-line interface
    Program.cs                        # CLI entry point with command handlers

  AnytoneConfigBuilder.Web/           # ASP.NET Core web application
    Pages/
      Index.cshtml(.cs)              # Builder page - upload CSVs and generate output
      Checker/Index.cshtml(.cs)      # Checker page - validate CPS exports
      Undo/Index.cshtml(.cs)         # Undo page - reverse-engineer CPS exports
      Shared/_Layout.cshtml          # Layout with navigation

tests/
  AnytoneConfigBuilder.Core.Tests/   # Test suite
    BuildRegressionTests.cs          # Build pipeline tests
    UndoRegressionTests.cs           # Undo functionality tests
    EdgeCaseValidationTests.cs       # Input validation edge cases
    TestHelpers.cs                   # Test utilities

input-csv/                            # Sample input files for testing
  Analog.csv
  Digital-Others.csv
  Digital-Repeaters.csv
  TalkGroups.csv

output/                               # Example output files
  channels.csv
  zones.csv
  scanlists.csv
  talkgroups.csv

documentation/
  getting-started.md                 # User guide
  advanced-options.md                # Detailed option documentation
```

## Building

### Prerequisites
- .NET 10 SDK (preview)
- Windows (requires PowerShell for script execution)

### Build Commands

```bash
# Restore dependencies and build
dotnet build ./AnytoneConfigBuilder.slnx

# Build Release configuration
dotnet build ./AnytoneConfigBuilder.slnx -c Release

# Run tests
dotnet test ./AnytoneConfigBuilder.slnx

# Run tests with coverage
dotnet test ./AnytoneConfigBuilder.slnx /p:CollectCoverage=true
```

## Running

### Web Application

```bash
# Run the web application (default: https://localhost:5001)
cd src/AnytoneConfigBuilder.Web
dotnet run

# Run with custom port
dotnet run --urls "http://localhost:3000"
```

### Command-Line Interface

```bash
cd src/AnytoneConfigBuilder.Cli
dotnet run -- build \
  --analog ../../input-csv/Analog.csv \
  --digital-others ../../input-csv/Digital-Others.csv \
  --digital-repeaters ../../input-csv/Digital-Repeaters.csv \
  --talkgroups ../../input-csv/TalkGroups.csv \
  --output ./output

# Run checker
dotnet run -- check --channels ./output/channels.csv

# Run undo builder
dotnet run -- undo \
  --channels ./output/channels.csv \
  --zones ./output/zones.csv \
  --talkgroups ./output/talkgroups.csv \
  --output ./reconstructed

# View default channel settings
dotnet run -- defaults
```

## Configuration

### BuildOptions Enum Values

#### SortMode (Zone/Talkgroup Ordering)
- **Alpha** (default) – Alphabetically sort all zones and talkgroups (easiest to find in CPS)
- **RepeatersFirst** – Maintain input order, DMR repeaters appear before simplex/hotspots
- **AnalogFirst** – Maintain input order, analog channels appear first

#### HotspotTxPermitMode (Hotspot TX Permission)
- **SameColorCode** (default) – TX only when receiving same color code (conservative, prevents interference)
- **Always** – Allow TX anytime (permissive, may cause interference)

#### NicknameMode (Repeater Channel Nicknames)
- **Off** (default) – Use repeater names as-is
- **Prefix** – Add location nickname only if duplicate names exist (prefix format)
- **Suffix** – Add location nickname only if duplicate names exist (suffix format)
- **PrefixForced** – Always add location nickname (prefix format)
- **SuffixForced** – Always add location nickname (suffix format)

### Environment Variables

None required for local development. Web app uses in-memory sessions (configure for production with distributed cache).

## Web UI Features

### Builder Page (`/`)
- **File Upload** – Select 4 CSV files (Analog, Digital-Others, Digital-Repeaters, TalkGroups)
- **Advanced Options** – Sort mode, hotspot TX permit, nickname behavior
- **Individual Downloads** – Download each output file separately
- **Batch Download** – Download all files as a ZIP archive
- **Progress Indication** – Form submit button shows loading spinner
- **Better Error Messages** – Detailed feedback on validation failures (file size, format, etc.)

### Checker Page (`/Checker`)
- **Upload CPS Export** – Upload channels.csv from Anytone CPS
- **Consistency Report** – HTML report identifying:
  - Repeater frequency pair issues
  - Color code inconsistencies
  - Talkgroup/timeslot conflicts
  - Data quality issues

### Undo Page (`/Undo`)
- **Reverse-Engineer** – Upload CPS exports (channels, zones, talkgroups)
- **Reconstruct Inputs** – Generate source CSV files for editing
- **Batch Download** – All reconstructed files as ZIP

## Testing

### Run All Tests
```bash
dotnet test ./AnytoneConfigBuilder.slnx -v minimal
```

### Run Specific Test Class
```bash
dotnet test ./AnytoneConfigBuilder.slnx --filter "BuildRegressionTests"
dotnet test ./AnytoneConfigBuilder.slnx --filter "EdgeCaseValidationTests"
```

### Run Specific Test
```bash
dotnet test ./AnytoneConfigBuilder.slnx --filter "Build_WithSampleInputs_GeneratesExpectedOutputFiles"
```

### Test Coverage
- **BuildRegressionTests** – Validates build output generation with sample data
- **UndoRegressionTests** – Tests undo functionality and private-call marker handling
- **EdgeCaseValidationTests** – 54+ tests covering:
  - Channel name length validation (16 char max)
  - Frequency boundaries (0-500 MHz)
  - CTCSS/DCS tone validation
  - Color code ranges (0-16)
  - Call type validation (Group/Private)
  - Bandwidth, power, timeslot, and TX permit validation

## Deployment

### Docker (Containerized)
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10
WORKDIR /app
COPY --from=build /app/src/AnytoneConfigBuilder.Web/bin/Release/net10.0/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "AnytoneConfigBuilder.Web.dll"]
```

### IIS Hosting
1. Publish Release build: `dotnet publish -c Release`
2. Copy published folder to IIS application directory
3. Create IIS application pool (.NET CLR v4.0 Integrated Pipeline)
4. Configure web app with appropriate identity and permissions

### Docker Compose (Local Development)
```yaml
version: '3.8'
services:
  web:
    build: .
    ports:
      - "5001:5001"
    environment:
      - ASPNETCORE_URLS=https://+:5001
      - ASPNETCORE_ENVIRONMENT=Development
```

## Input CSV Format

### Analog.csv
Columns: Frequency, Name, Zone, Power, CTCSS, ...
```csv
145.000,Simplex 1,Test Zone,High,88.5,...
```

### Digital-Others.csv
Columns: Frequency, Name, Zone, ColorCode, CallType, TimeSlot, TxPermit, ...
```csv
145.100,Hotspot,Test Zone,1,Group Call,1,Same Color Code,...
```

### Digital-Repeaters.csv
Columns: RxFreq, TxFreq, Name, Zone, [TG1;TS1],[TG2;TS2],...
```csv
145.200,145.800,Repeater1,Test Zone,1;1,2;1,...
```

### TalkGroups.csv
Columns: ID, Name, Type
```csv
1,Local,Group Call
2,Statewide,Group Call
```

## Output CSV Format (CPS-Compatible)

All output files are generated with proper quoting and CRLF line endings for compatibility with Anytone CPS.

- **channels.csv** – Individual channel definitions
- **zones.csv** – Zone groupings (zone membership)
- **scanlists.csv** – Scan list definitions
- **talkgroups.csv** – Talk group definitions

## Troubleshooting

### Build Fails with CSV Errors
- Ensure input CSVs have proper headers
- Check for non-ASCII characters (UTF-8 with BOM not supported)
- Verify numeric fields (frequency, color code, timeslot) are valid numbers

### Channel Names Too Long
- Channel names are limited to 16 characters (CPS limitation)
- Use nickname options to automatically shorten names

### Undo Doesn't Reconstruct Digital-Repeaters
- Undo heuristic detects repeaters by zone+frequency combinations with 5+ channels
- Ensure your repeater definitions have multiple channels in the same zone/frequency

### Web App Sessions Not Persisting
- Web app uses in-memory sessions (fine for single-server deployment)
- For multi-server deployment, configure distributed cache (Redis, SQL Server, etc.)

## Performance Notes

- Typical build time: 1-5 seconds
- Checker analysis: < 1 second
- Undo reconstruction: 1-2 seconds
- Max file size limits: 2 MB per file
- Supports up to 1000s of channels efficiently

## Contributing

See [MIGRATION_NOTES.md](MIGRATION_NOTES.md) for Perl→C# porting decisions and architecture patterns.

## License

See LICENSE.md

## Support

For issues, questions, or feature requests, please refer to the documentation:
- [Getting Started Guide](documentation/getting-started.md)
- [Advanced Options](documentation/advanced-options.md)
