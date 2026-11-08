<!-- ppk-readme:head:start -->
<a href="https://popovantondev.github.io/SaveLecture/index-ru.html"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/header-ru-dark.2e06e918.svg"><img src="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/header-ru-light.1d79db53.svg" width="880" height="188" alt="SaveLecture — Сохраняет субтитры Teams и снимки демонстрации экрана. Windows 11 · x64, Предварительный выпуск 0.1.0-preview.4"></picture></a>
<!-- ppk-readme:head:end -->

# SaveLecture

**Русский** · [Deutsch](README.md) · [English](README.en.md)

<!-- ppk-readme:body:start -->
<p>
<a href="https://github.com/popovantondev/SaveLecture/releases/tag/v0.1.0-preview.4"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-download-ru-dark.ab7b2e41.svg"><img src="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-download-ru-light.6c5587e5.svg" width="120" height="36" alt="Скачать"></picture></a>
<a href="https://popovantondev.github.io/SaveLecture/index-ru.html"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-website-ru-dark.25964b9f.svg"><img src="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-website-ru-light.cf62425d.svg" width="92" height="36" alt="Сайт"></picture></a>
<a href="https://popovantondev.github.io/SaveLecture/Guide-ru.html"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-guide-ru-dark.b0865e9f.svg"><img src="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-guide-ru-light.6b340ca5.svg" width="161" height="36" alt="Руководство"></picture></a>
<a href="https://github.com/popovantondev/SaveLecture/issues/new"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-bug-ru-dark.79671373.svg"><img src="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/btn-bug-ru-light.f06c313c.svg" width="234" height="36" alt="Сообщить об ошибке"></picture></a>
</p>

<p><a href="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/website/assets/demo-ru.png"><picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/preview-ru-dark.1edab199.webp"><img src="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/preview-ru-light.c67006b6.webp" width="880" height="516" alt="Интерфейс программы SaveLecture"></picture></a><br>
<sub><a href="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/website/assets/demo-ru.png">Снимок целиком</a></sub></p>

<picture><source media="(prefers-color-scheme: dark)" srcset="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/divider-dark.ac532a77.svg"><img src="https://raw.githubusercontent.com/popovantondev/SaveLecture/main/docs/readme/divider-light.7e78c541.svg" width="880" height="6" alt="Разделитель"></picture>
<!-- ppk-readme:body:end -->

Субтитры и слайды Teams в одном месте. SaveLecture помогает сохранить разрешённые учебные материалы и спокойно вернуться к ним позже.

**Windows 11 · x64 · Учебный проект · предварительная версия 0.1.0-preview.4**

Настоящий интерфейс. Все тексты, имена и изображения в примере искусственные.

## Первый запуск

1. Получите согласие всех участников и разрешение на материалы до запуска программы.
2. Распакуйте весь архив выпуска в доступную для записи папку. Проверьте SHA-256 опубликованного архива.
3. При первом запуске прочитайте окно предупреждения. Пока оно не подтверждено, сбор не начинается. Можно выбрать русский, немецкий или английский.
4. После первого подтверждения начинается сбор и открывается главное окно. При следующих запусках окно не показывается: программа собирает материалы и показывает состояние в трее. Открыть окно можно через значок в трее. Включите субтитры в Teams Desktop и оставьте демонстрацию видимой.
5. Крестик скрывает программу в трей. Пауза временно прекращает сбор, Продолжить возобновляет ту же лекцию. Стоп с подтверждением завершает её. Выход также завершает сбор.

## Только с разрешения всех участников

До сохранения получите информированное согласие всех участников и разрешение на учебные материалы. Объясните цель, что сохраняется и где. При отказе или отзыве остановите сбор. Учебное назначение и локальное хранение сами по себе не устанавливают правомерность записи.

Первое предупреждение — подтверждение понимания условий пользователем, а не доказательство согласия остальных людей. Уважайте отказ, ограничения учреждения, авторские права и срок хранения. По запросу удаляйте данные после остановки; не отправляйте записи, журнал ошибок или снимки в публичные Issues. Папка Archive рядом с программой может быть общей, если программа находится в Public или синхронизируемой папке. Выберите личную папку с подходящими правами доступа. Содержимое встреч может включать персональные данные в смысле DSGVO (GDPR); тайная запись непубличной речи может быть уголовно наказуема по § 201 StGB. Заявления о гарантированном соблюдении Datenschutz проект не делает.

## Хранение и восстановление

Настройки → Хранилище позволяет выбрать папку после Стоп.  Один TXT относится к одной лекции до Стоп; снимки лежат в папках дат. _service содержит данные восстановления и индексы. После сбоя сохраняется ID незавершённой лекции. Не удаляйте служебные файлы во время записи. Настройки и журнал ошибок находятся в профиле пользователя: %LOCALAPPDATA%\SaveLecture. Имя для уведомлений задаётся в Транскрипции; пустой список отключает их. Итоговые слайды лежат в Archive/дата/scr. При постепенной дорисовке галерея оставляет наиболее полный кадр; заменённые промежуточные снимки переносятся в Archive/дата/Removed без удаления или перезаписи одноимённых файлов. _service хранит ссылки на переносы и историю. При выборе другой папки хранения старые записи сами туда не переносятся. Обновления одной реплики объединяются без учёта регистра, пунктуации и смены элемента Teams. В окне и TXT текущей лекции остаётся актуальная версия с временем первого появления; исходные обновления сохраняются для восстановления. Завершённые прежние TXT не переписываются. При почти одинаковом слайде и мелких изменениях курсора или панели галерея выбирает последний сохранённый снимок; предыдущий остаётся в Removed. TXT текущей лекции безопасно заменяется готовой временной копией. Файл .tmp может ненадолго появляться при сохранении; не удаляйте его во время работы.

Галерея может показывать снимок, уже сохранённый сегодня. Программа использует прежний файл, чтобы не создавать повторную копию. Счётчик «Сохранено» учитывает только новые файлы текущей записи и начинается с нуля после перезапуска или нового старта. Счётчик дубликатов показывает пропущенные повторные изображения. Существующие файлы остаются в архиве. «Сохранено» считает новые файлы текущей записи, включая промежуточные кадры, позже перенесённые в Removed. Галерея показывает итоговые версии. «Дубликаты» учитывает пропущенные повторы и каждый выполненный перенос промежуточного кадра в Removed по одному разу. Уже выполненные переносы не прибавляются повторно при перезапуске. «Текст» считает объединённые реплики, а не каждое промежуточное обновление Teams. Проверка продолжается с заданным интервалом, но неподвижный уже обработанный слайд не увеличивает счётчик каждую секунду. Чистый кадр после исчезновения диалога проходит проверку стабильности; скрытые участки не дорисовываются.

## Автозапуск и зависимости

В Настройках включите или выключите «Автозапуск в Windows» и нажмите «Готово». При включении программа запускается при входе в Windows и остаётся в трее. После подтверждения первого предупреждения сохранение начинается автоматически при каждом запуске. Отсутствие главного окна не означает остановку: состояние видно в трее. Пауза временно прекращает сбор, Стоп завершает запись, Выход завершает работу. Основной сбор требует Windows x64, .NET Framework 4.8, Windows PowerShell и Teams Desktop с включёнными субтитрами. Аудио и микрофон не записываются. SaveLecture сам не загружает лекции в облако; субтитры Teams зависят от сервиса Microsoft. InstallAutostart.ps1 и UninstallAutostart.ps1 — альтернативный способ настройки автозапуска без администратора. Ручная очистка папки требует Python 3.10+, Pillow и NumPy; основная запись работает без них.

## Если что-то не работает

Нет текста: проверьте субтитры Teams, видимость окна и статус приложения. Нет снимков: демонстрация должна быть видимой; проверьте ожидание, переключатель слайдов и доступ к папке. При ошибке не удаляйте _service; остановите приложение и сохраните копию архива. При сообщении о проблеме укажите версию, Windows и шаги воспроизведения, используйте только искусственный пример без имён, переписки, ссылок на встречи или ключей. Фиолетовое ожидание означает, что новых данных сейчас нет; в трее вращается фиолетовое кольцо. При отсутствии демонстрации показан последний сохранённый слайд. Субтитры имеют отдельный статус: после 15 секунд без новых строк показывается ожидание новых субтитров. Жёлтый статус — пауза, «Сбор включён» — программа готова получать данные; это не подтверждение наличия трансляции. Отсутствие демонстрации не завершает лекцию. В версии 0.1.0-preview.4 исправлена нарастающая задержка сохранения длинного TXT. Обновление не восстанавливает субтитры, которые Teams не передал программе. Полный рабочий день после исправления ещё не проверен.

## Прозрачно о готовности

Учебная предварительная версия прошла локальные проверки сохранения и интерфейса. Полный живой прогон Teams, физическая смена DPI и реальные восемь часов работы пока не подтверждены.

## Права

[Права](RIGHTS.md) · [Компоненты](THIRD_PARTY_NOTICES.md) · [Сборка](docs/BUILDING.md) · [Проверка](docs/VERIFICATION.md) · [Релиз](https://github.com/popovantondev/SaveLecture/releases/tag/v0.1.0-preview.4)
