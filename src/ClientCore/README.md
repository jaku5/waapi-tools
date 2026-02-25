# ClientCore

This project contains portions of the **Audiokinetic Wwise SDK** C# WAAPI client, specifically the WAMP protocol implementation and WAAPI URI constants.

## Provenance

These files originate from the Wwise SDK sample code, typically found at:

```
<Wwise Installation>/SDK/samples/WwiseAuthoringAPI/cs/WaapiClientSample/
```

The original source is distributed by Audiokinetic as part of the Wwise SDK installer. As of this writing, Audiokinetic does not publish an official NuGet package for the C# WAAPI client.

## Files

| File | Description |
|------|-------------|
| `Client.cs` | Core WAAPI client — wraps the WAMP protocol layer with string-only I/O |
| `Wamp.cs` | WAMP (Web Application Messaging Protocol) implementation over WebSocket |
| `Uri.cs` | Auto-generated WAAPI URI constants for all available endpoints |

## License

Copyright (c) 2020 Audiokinetic Inc. — Licensed under the Apache License, Version 2.0.
See the individual file headers for full license text.

## Modifications

- Namespace changed from `Wwise.WaapiClient` to `JPAudio.WaapiTools.ClientCore`
- `Uri.cs`: Added `getProjectInfo` and `diff` URI constants not present in the original SDK sample
- `Client.cs`: Added `Disconnect()` method for explicit connection teardown
