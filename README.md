# DUID: The high entropy, web-friendly, globally unique identifier

DUID is a fully-featured replacement for GUIDs (Globally Unique Identifiers) that is more compact, web-friendly, and provides more entropy than GUIDs.
I created this UUID type as a replacement for GUIDs in my projects, improving on GUID shortcomings.

I pronounce it *doo-id*, but it can also be pronounced like *dude*, which is by design :)

**Key security features:**

- Uses the latest .NET cryptographic random number generator
- More entropy than GUID v4 (128 bits vs 122 bits)
- No embedded timestamp (reduces predictability and improves strength)
- Self-contained; does not use any packages

**Usage features:**

- High performance, with minimal allocations
- 16 bytes in size; 22 characters as a string
- Always starts with a letter (can be used as-is for programming language variable names)
- URL-safe
- Can be validated, parsed, and compared
- Can be created from and converted to byte arrays
- UTF-8 encoding support
- JSON serialization support
- TypeConverter support
- Debug support (displays as string in debugger)

## Nuget

**Yes, you can also find DUID on nuget.** Look for the package named `argentini.duid`.

## Usage

Similar to `Guid.NewGuid()`, you can generate a new DUID by calling the static `NewDuid` method:

```csharp
var duid = Duid.NewDuid();
```
This will produce a new DUID, for example: `aZ3x9Kf8LmN2QvW1YbXcDe`.

There are a ton of overloads and extension methods for converting, validating, parsing, and comparing DUIDs.
Here are some examples:

```csharp
// Represents an empty DUID (all zeros); "AAAAAAAAAAAAAAAAAAAAAA"
var emptyDuid = Duid.Empty;

// Get a string value for a DUID
var duid = Duid.NewDuid();
var duidString = duid.ToString();

if (Duid.TryParse("aZ3x9Kf8LmN2QvW1YbXcDe", out var duid)
{
    // Successfully parsed DUID
}

if (Duid.IsValidString("aZ3x9Kf8LmN2QvW1YbXcDe"))
{
    // Successfully validated
}

if (duid1 == duid2)
{
    // Comparison works as expected
}
```
There is also a JSON converter for System.Text.Json that provides seamless serialization and deserialization of DUIDs:

```csharp
var options = new JsonSerializerOptions();
options.Converters.Add(new DuidJsonConverter());

var user = new User
{
    Id = Duid.NewDuid(),
    FirstName = "Turd",
    LastName = "Ferguson"
};

var json = JsonSerializer.Serialize(user, options);

/*
    json =
    {
        id: "xw5x7Kf6LmN3QvW1YbXcc0",
        firstName: "Turd",
        lastName: "Ferguson"
    }
*/
```
This scratches the surface of what's available. Try using DUIDs in your project to explore all the features it provides.
