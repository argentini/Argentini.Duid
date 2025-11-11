# DUID: The high entropy, web-friendly, globally unique identifier

DUID is a replacement for GUIDs (Globally Unique Identifiers) that are more compact, URL-safe, and JavaScript entity name-safe. 
DUIDs are 22-character alphanumeric codes that always begin with a letter. Unlike GUID v4 there is no version code which means more entropy.
They have no timestamp information, so they are not sortable (which also improves strength by eliminating predictability).

Under the hood, DUIDs are generated using the latest .NET cryptographic random number generator.

**You can also find DUID on nuget.**

## Usage

Similar to `Guid.NewGuid()`, you can generate a new DUID by calling the static `NewDuid` method:

```csharp
var duid = Duid.NewDuid();
```
This will produce a new DUID, for example: `aZ3x9Kf8LmN2QvW1YbXcDe`.

There are a ton of overloads and extension methods for converting, validating, parsing, and comparing DUIDs.
Here are some examples:

```csharp
var emptyDuid = Duid.Empty; // Represents an empty DUID (all zeros); "AAAAAAAAAAAAAAAAAAAAAA"

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

var duid = Duid.NewDuid();
var json = JsonSerializer.Serialize(duid, options);
```
This scratches the surface of what's available. Try using DUIDs in your project to explore all the features it provides.
