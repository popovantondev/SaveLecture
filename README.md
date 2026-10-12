# SaveLecture

[Русский](README.ru.md) · [Deutsch](README.de.md) · [English](README.md)

Teams captions and slides, together. SaveLecture keeps permitted learning materials in one place so you can return to them later.

**Windows 11 · x64 · Educational project · preview 0.1.0-preview.1**

[Guide](https://popovantondev.github.io/SaveLecture/Guide-en.html) · [Source](https://github.com/popovantondev/SaveLecture) · [Report an issue](https://github.com/popovantondev/SaveLecture/issues/new)

![SaveLecture](website/assets/demo-en.png)

Actual interface. All text, names and images in this example are artificial.

## First launch

1. Obtain permission from every participant and permission for the materials before launching.
2. Extract the whole package into a writable folder. Verify the published archive’s SHA-256. The release is still being prepared.
3. Read the first-launch notice. No collection starts before acknowledgement. Choose Russian, German or English.
4. After the first acknowledgement, collection begins and the main window opens. Later launches collect materials and show status in the system tray without opening the main window. Open it using the tray icon. Enable Teams captions and keep shared content visible.
5. Close hides the app in the tray. Pause suspends collection; Resume continues the same lecture. Stop ends it after confirmation. Exit also ends collection.

## Only with every participant’s permission

Before saving, obtain informed consent from every participant and permission for the learning materials. Explain the purpose, what is saved and where. Stop collecting if consent is refused or withdrawn. Educational purpose and local storage alone do not establish whether recording is lawful.

The first notice acknowledges the user’s understanding, not consent from other people. Respect refusal, institutional rules, copyright and retention periods. Delete data on request after stopping; never share recordings, error logs or meeting screenshots in public issues. Archive beside the app may be shared when placed in Public or a synced folder. Choose a personal folder with suitable access permissions. The project does not guarantee data-protection compliance.

## Startup and requirements

In Settings, turn “Start with Windows” on or off and click “Done”. When enabled, the app starts at Windows sign-in in the system tray. After acknowledging the first-launch notice, saving starts automatically on each launch. A hidden main window does not mean collection has stopped: the tray shows its status. Pause suspends collection, Stop ends recording, and Exit closes the app. Main recording requires Windows x64, .NET Framework 4.8, Windows PowerShell and Teams Desktop with captions enabled. Audio and microphone input are not recorded. SaveLecture itself does not upload lectures; Teams captions depend on Microsoft’s service. InstallAutostart.ps1 and UninstallAutostart.ps1 offer an alternative setup without administrator access. Manual folder cleanup requires Python 3.10+, Pillow and NumPy; main recording works without them.

## A transparent readiness status

The candidate is tested for saving and interface behavior. A full live Teams run, physical DPI changes and eight actual hours of operation have not been confirmed. No public binary release has been published yet.

[Rights](RIGHTS.md) · [Components](THIRD_PARTY_NOTICES.md) · [Build](docs/BUILDING.md) · [Verification](docs/VERIFICATION.md)
