# Waapi Tools

Waapi Tools is a collection of utilities designed to streamline tasks in Audiokinetic's Wwise middleware using the Wwise Authoring API (WAAPI) and Wwise Authoring Query Language (WAQL).

Currently, the project includes the following tools:

- **[PropertyContainerAuditor](src/PropertyContainerAuditor/README.md)**: A utility that converts Actor-Mixers / Property Containers into Virtual Folders if they are not utilized for mixing tasks.
![PropertyContainerAuditor](images/PropertyContainerAuditor.png)
- **[TransitionAuditioner](src/TransitionAuditioner/README.md)**: Audition a single interactive-music transition in-editor, without playing through the material that precedes it.
![TransitionAuditioner](images/TransitionAuditioner.png)
- More tools coming soon.

## Prerequisites

- **Wwise 2023.1.x** or any newer version.
- **WAAPI enabled** in Wwise: **Project → User Preferences → Enable Wwise Authoring API**. The tools communicate with Wwise over the Wwise Authoring API.
- **Supported OS:** Windows 10[^1], Windows 11.
[^1]: Note for Windows 10 users: Windows 10 does not include the Segoe Fluent Icons font by default. To ensure proper icon rendering, install the Segoe Fluent Icons font as described in the [Microsoft documentation](https://learn.microsoft.com/en-us/windows/apps/design/iconography/segoe-fluent-icons-font#how-do-i-get-this-font)

## Installation

First, **download the latest release** from the [Releases](https://github.com/jaku5/waapi-tools/releases) section. Each release bundles every tool's executable plus the shared `jpaudio-waapi-tools.json` command file:

- `PropertyContainerAuditor.exe`
- `TransitionAuditioner.exe`
- `jpaudio-waapi-tools.json`

There are two ways to run the tools (both require WAAPI enabled — see [Prerequisites](#prerequisites)).

### Option A — Run standalone (no setup)

Just launch the tool's `.exe` directly (e.g. double-click it) while Wwise is open. The tool connects to Wwise over WAAPI on its own — nothing to copy, nothing to configure. This is the quickest way to try the tools.

### Option B — Wwise Extra menu integration

If you'd rather launch the tools from inside Wwise, register them as custom commands:

1. Locate your Wwise installation directory.
2. Copy the tool executables (`PropertyContainerAuditor.exe`, `TransitionAuditioner.exe`) into `%WWISEROOT%\Authoring\Data\Add-ons`, and `jpaudio-waapi-tools.json` into the `%WWISEROOT%\Authoring\Data\Add-ons\Commands` subfolder.
3. Restart Wwise. The tools now appear under the **Extra** menu.

For more information and other installation methods, refer to the [Audiokinetic documentation](https://www.audiokinetic.com/en/public-library/2022.1.18_8567/?source=SDK&id=defining_custom_commands.html).

## Usage

1. Open Wwise (with WAAPI enabled).
2. Launch a tool — either run its `.exe` directly (Option A) or select its command from the **Extra** menu (Option B).

## Tools

Each tool has its own detailed documentation:

- **[Property Container Auditor](src/PropertyContainerAuditor/README.md)** — background, conversion criteria, and usage notes.
- **[Transition Auditioner](src/TransitionAuditioner/README.md)** — background and step-by-step audition workflow.

## License

This project is licensed under the Apache License, Version 2.0. See the [LICENSE](LICENSE) file for details.

---

Feel free to contribute by submitting pull requests or reporting issues to help improve this project!
