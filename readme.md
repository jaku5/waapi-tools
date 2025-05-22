# Waapi Tools

Waapi Tools is a collection of utilities designed to streamline tasks in Audiokinetic's Wwise middleware using the Wwise Authoring API (WAAPI) and Wwise Authoring Query Language (WAQL).

Currently, the project includes the following tool:

- **[ActormixerSanitizer](https://github.com/jaku5/waapi-tools#actormixer-sanitizer)**: A utility that converts Actor-Mixers into Virtual Folders when they share all properties with their parent Actor-Mixer.
- More tools coming soon™.

## Prerequisites

- **Wwise 2022.1.x** or any newer version.

## Installation

1. **Download the Release**:
   - Navigate to the [Releases](https://github.com/jaku5/waapi-tools/releases) section of this repository.
   - Download the latest release, which includes:
     - `ActormixerSanitizer.exe`
     - `jpaudio-waapi-tools.json`

2. **Copy files to the Wwise installation folder**:
   - Locate your Wwise installation directory.
   - Copy the downloaded `ActormixerSanitizer.exe` into `%WWISEROOT%\Authoring\Data\Add-ons` and `jpaudio-waapi-tools.json` into `%WWISEROOT%\Authoring\Data\Add-ons\Commands` subfolder of your Wwise installation.
   - For more information and other installation methods, refer to the [Audiokinetic documentation](https://www.audiokinetic.com/en/public-library/2022.1.18_8567/?source=SDK&id=defining_custom_commands.html).

## Usage

1. Open Wwise.
2. Access the **Extra** menu in Wwise.
3. Select the command to run the tool.

## Tools
### Actormixer Sanitizer
#### Background
As per [Audiokinetic guidelines](https://www.audiokinetic.com/en/public-library/2022.1.18_8567/?source=SDK&id=goingfurther_optimizingmempools_reducing_memory.html), Actor-Mixers should not be used solely for organizing sounds, as they introduce some memory overhead compared to Work Units or Virtual Folders.

#### How it works
The tool will:
- Identify Actor-Mixers that can be converted into virtual folders based on the following criteria (all conditions must be met):
    - Actor-Mixer has at least one ancestor Actor-Mixer.
    - Actor-Mixer has no overridden properties or values (i.e., all properties and values are the same as on the closest ancestor Actor-Mixer).
    - Actor-Mixer has all randomizable property values set to 0 and doesn't have any active randomizer set on them.
    - Actor-Mixer has no RTPCs with control inputs.
    - Actor-Mixer has no states with defined values.
    - Actor-Mixer is not referenced by any event action.
- List all Actor-Mixer candidates' names and IDs and prompt you to confirm the conversion.
- Perform the conversion if confirmed, or exit otherwise.
> [!NOTE]
> Please note that this tool is considered experimental; be careful when using it in production and preferably have source control set up to inspect the diffs or restore backups. Especially since it is meant to be used at the end of production, after the mixing stage, when you know you won't need these Actor-Mixers for mixing tasks. That said, you should be able to undo all the changes made by the tool with <kbd>Ctrl</kbd> + <kbd>Z</kbd>.

## License

This project is licensed under the Apache License, Version 2.0. See the [LICENSE](LICENSE) file for details.

---

Feel free to contribute by submitting pull requests or reporting issues to help improve this project!