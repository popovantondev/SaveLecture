# Components and platform dependencies

The Windows application uses Windows APIs, Windows PowerShell and .NET Framework WPF/WinForms. No .NET or PowerShell redistributable is bundled. Microsoft Teams is an external requirement; Microsoft’s platform terms apply. SaveLecture is an independent educational demonstration and is not endorsed by Microsoft.

Optional image cleanup imports Pillow and NumPy from a separately installed Python 3.10+ environment. These packages and Python are not included in the candidate archive. Their own licenses apply: [Python](https://docs.python.org/3/license.html), [Pillow](https://github.com/python-pillow/Pillow/blob/main/LICENSE), [NumPy](https://github.com/numpy/numpy/blob/main/LICENSE.txt). The main recorder does not require Python.

Caption collection uses adaptations of the supplied TeamsSpeechRecognition sources. Frame comparison uses adaptations of the supplied SlideFilter algorithm; optional cleanup imports the supplied `presentation_deduper.py`. No external copyright or license declaration was found in that supplied Python file. The provenance of the supplied adaptations and any third-party permissions have not been independently verified. Publication and the SaveLecture rights statement do not grant rights belonging to third parties. This note is not a license grant for that file.

The interface example images and texts are generated artificial fixtures. They contain no meeting recordings. The app icon is a locally drawn design. Its resemblance to Teams should be reviewed before release; Microsoft names and marks belong to their owners.

## Deutsch

Windows, .NET Framework, PowerShell und Teams sind Plattformvoraussetzungen. Python, Pillow und NumPy sind nur für die optionale Bereinigung nötig und nicht enthalten. Herkunft und etwaige Drittrechte der bereitgestellten Quelltextadaptionen wurden nicht unabhängig bestätigt. Die Veröffentlichung gewährt keine Rechte Dritter. Künstliche Beispiele enthalten keine Meeting-Daten. Plattformrechte gelten separat.

## Русский

Windows, .NET Framework, PowerShell и Teams — зависимости платформы. Python, Pillow и NumPy нужны только для дополнительной очистки и не входят в комплект. Происхождение предоставленных адаптаций кода и возможные сторонние права независимо не подтверждены. Эта публикация не предоставляет права, принадлежащие третьим лицам. Искусственные примеры не содержат данных встреч. Права платформ действуют отдельно.
