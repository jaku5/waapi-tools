# ClientJson

This project provides a JSON convenience layer over the [ClientCore](../ClientCore/) WAAPI client, wrapping all I/O in `Newtonsoft.Json.Linq.JObject` for easier consumption.

## Provenance

This file originates from the **Audiokinetic Wwise SDK** C# WAAPI client sample, typically found at:

```
<Wwise Installation>/SDK/samples/WwiseAuthoringAPI/cs/WaapiClientSample/
```

As of this writing, Audiokinetic does not publish an official NuGet package for the C# WAAPI client.

## Files

| File | Description |
|------|-------------|
| `JsonClient.cs` | JSON wrapper over `ClientCore.Client` — serializes/deserializes all args and results as `JObject` |
| `IJsonClient.cs` | Interface extracted for dependency injection and testability (not part of original SDK) |

## License

Copyright (c) 2020 Audiokinetic Inc. — Licensed under the Apache License, Version 2.0.
See the individual file headers for full license text.

## Modifications

- Namespace changed from `Wwise.WaapiClient` to `JPAudio.WaapiTools.ClientJson`
- Added `IJsonClient` interface (not present in the original SDK sample)
- Added `Disconnect()` method
