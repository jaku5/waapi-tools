# Waapi Tools

Waapi Tools is a collection of utilities designed to streamline tasks in Audiokinetic's Wwise middleware using the Wwise Authoring API (WAAPI) and Wwise Authoring Query Language (WAQL).

Currently, the project includes the following tool:

- **ActormixerSanitizer**: A utility that converts Actor-Mixers into Virtual Folders when they share all properties with their parent Actor-Mixer.

## Prerequisites

- **.NET 9 Runtime**: This project is framework-dependent, so you must have the .NET 9 runtime installed on your system. You can download it from the official [.NET website](https://dotnet.microsoft.com/).
- **Wwise 2022.1.x**: Or any newer version.

## Installation

1. **Download the Release**:
   - Navigate to the [Releases](https://github.com/your-username/your-repository/releases) section of this repository.
   - Download the latest release, which includes:
     - `JPAudioWaapiTools.dll`
     - A `Commands` folder containing `JPAudioWaapiTools.json`.

2. **Copy files to Wwise installation folder**:
   - Locate your Wwise installation directory.
   - Copy the downloaded `JPAudioWaapiTools.dll` and the `Commands` folder into the following subfolder of your Wwise installation.
	   - **Windows**: `%WWISEROOT%\Authoring\Data\Add-ons\`
	   - **macOS**: `/Library/Application Support/Audiokinetic/Wwise<version>/Authoring/Data/Add-ons`
   - For more information and other installation methods, refer to [Audiokinetic documentation](https://www.audiokinetic.com/en/public-library/2022.1.18_8567/?source=SDK&id=defining_custom_commands.html).

## Usage

1. Open Wwise.
2. Access the **WAAPI Tools** menu from the context menu or the **Extra** menu in Wwise.
3. Select the command to run the tool.

### Actormixer Sanitizer
#### Backgorund
As per [Audiokinetic guidelines](https://www.audiokinetic.com/en/public-library/2022.1.18_8567/?source=SDK&id=goingfurther_optimizingmempools_reducing_memory.html) Actor-Mixers should not be used solely for organizing sounds as they introduce some memory overhead compared to Work Units or Virtual Folders.

#### How it works
The tool will:
- Identify actor-mixers that can be converted into virtual folders based on the following criteria (all conditions must be met):
	- actor-mixer and its parent share all properties values
	- actor-mixer and its parent share all RTPCs lists and curves (or actor-mixer has no RTPCs)
	- actor-mixer and its parent share states (or actor-mixer has no state groups)
	- actor-mixer is not referenced by any event action
- List all actor-mixer candidates' names and IDs and prompt you to confirm the conversion.
- Perform the conversion if confirmed or exit otherwise.
#### Known Issues and Limitations 
- I haven't found a way to compare state property values so it is possible the tool will identify an Actor-Mixer as a candidate for conversion even though it has different state properties values than their parent. To mitigate this, the tool will print the note if a candidate an Actor-Mixer has any state group so the user can inspect it manually.
- The tool will omit any actor-mixer that does not have actor-mixer as a direct parent (e.g., is under Work Unit or Virtual Folder). This functionality should be improved and hopefully will be addressed in a future release.
>**NOTE**
>Please note that this tool is considered experimental; be careful when using it in production and preferably have source control setup to inspect the diffs or restore a backups. Especially since it is meant to be used at the and of production, after mixing stage, when you know you won't need these Actor-Mixers for mixing tasks. That said you should be able to undo all the changes made by the tool with <kbd>ctrl</kbd> + <kbd>z</kbd>.

## License

This project is licensed under the Apache License, Version 2.0. See the [LICENSE](LICENSE) file for details.

---

Feel free to contribute by submitting pull requests or reporting issues to help improve this project!
