# Build SaveLecture

Windows x64 with .NET Framework 4.8 is required. Run `Build.ps1` from Windows PowerShell in the project directory. It exports the vector brand and builds `SaveLecture.exe` as a Windows application without a console. No administrator account or paid service is needed. Keep `src/ui`, the two caption-reader scripts and `assets/localization.json` beside the executable.

Run `tests/RunTests.ps1` for storage and filtering acceptance. Run `tests/RunUiTests.ps1` in an interactive Windows desktop for UI acceptance; this also builds the console-free `UiPreview.exe`. Its `--content-preview` mode displays artificial examples without recording. Diagnostics write to the console only for test runners.

Optional cleanup: install Python 3.10+, Pillow and NumPy separately. `tests/test_cleanup.py` checks preview, preserved new text, reversible moves, timeline, repeated actions and stale previews using generated images. The cleanup interpreter defaults to `python.exe` on PATH; `PythonExecutable` in the user preferences can specify its full path. Main recording works independently.

After building a reviewed clean checkout, run `python tools/PreparePortable.py` to create a local portable ZIP and SHA-256 file under `publication/`. It includes only explicit runtime and documentation paths, blanks personal aliases and checks every packaged file hash. It does not publish or change the installed application.

## Deutsch

Unter Windows x64/.NET Framework 4.8 `Build.ps1` ausführen. UI-Dateien, Untertitel-Skripte und Sprachdatei zusammen mit der EXE behalten. Core-Tests: `tests/RunTests.ps1`. UI-Tests mit interaktivem Desktop: `tests/RunUiTests.ps1`. Optional Python 3.10+, Pillow und NumPy für Bereinigung installieren. Kein Administrator nötig.

## Русский

На Windows x64/.NET Framework 4.8 выполните `Build.ps1`. Сохраните рядом с EXE интерфейс, скрипты субтитров и языковой файл. Проверки хранения: `tests/RunTests.ps1`; интерфейса на интерактивном рабочем столе: `tests/RunUiTests.ps1`. Для дополнительной очистки отдельно нужны Python 3.10+, Pillow и NumPy. Администратор не требуется.
