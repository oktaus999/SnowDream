# ❄ SnowDream

**Realistic snow for your Windows desktop — with drifts on your windows, icicles, wind and New Year fireworks.**

*by øktaus*

[English](#english) · [Русский](#русский)

<p align="center">
  <img src="docs/settings-en.png" width="760" alt="SnowDream settings window">
</p>

---

## English

SnowDream makes it snow right on top of your desktop. Flakes drift with the wind, land on the top edges of your open windows and slowly build up hilly snowdrifts. Close or minimize a window and its drift falls down — and can land on the windows below. Icicles grow along the top edge of the screen and shatter when they fall.

### Features

- **Real snow physics.** Snow piles up on the visible top edges of windows into natural, hilly drifts, slides off the edges, follows the rounded corners of Windows 11 windows, and covers the taskbar and the bottom of the screen.
- **Windows interact with snow.** Close or minimize a window and its drift collapses. Jerk a window sharply and the snow flies off by inertia; drag it gently and the snow stays.
- **Icicles.** They grow down from the top edge of every monitor, drip now and then, break off and shatter into bouncing ice shards. Adjustable growth speed.
- **Wind.** Choose the direction (left, auto, right) and strength — from snow falling straight down to a slanted blizzard.
- **Blow all snow away.** A gust sweeps across the screen in the wind direction and lifts every drift, icicle and shard into the air. After ~5 seconds it calms down.
- **Flake styles.** Soft dots, rotating six-armed stars, blocks or pixel snowflakes — the last two with blocky, step-like drifts.
- **Cursor.** Flakes swirl around your mouse pointer.
- **New Year.** A live countdown in the settings, and fireworks on your desktop at 00:00 on January 1st (or right now, with a button).
- **Doesn't get in the way.** Click-through overlay; snow can get quiet or hide in maximized windows and hides in games and fullscreen video.
- **Lightweight.** ~1–3% CPU, rendered on its own thread, a single ~100 KB `.exe` with no dependencies.
- Intensity, fall speed and flake size, performance mode (30 fps / 60 fps / monitor refresh rate), start with Windows, Russian / English.

### Download

Grab `SnowDream.exe` from the [Releases](../../releases) page and run it. No installation needed.

> Windows SmartScreen may warn you because the app isn't signed. Click **More info → Run anyway**.

### Usage

- **Left-click** the snowflake in the tray to open the settings.
- **Right-click** it for a quick menu: settings, stop snowfall, blow the snow away, quit.
- `SnowDream.exe --blow` blows the snow away and `SnowDream.exe --fireworks` launches fireworks in the running app (handy for shortcuts).
- If you don't see the snowflake, Windows 11 may have hidden it under the **^** arrow next to the clock — drag it onto the taskbar.

### Requirements

Windows 10 or 11 (x64) with .NET Framework 4.x, which ships with Windows.

### Build from source

No SDK required: the project is built with the C# compiler that comes with Windows.

```bat
build.bat
```

The result is `SnowDream.exe` in the same folder.

| File | What's inside |
| --- | --- |
| `Snow.cs` | WinAPI, settings, entry point |
| `App.cs` | app lifetime, tray, render thread and the main frame |
| `Sim.cs` | windows, drifts and their physics |
| `Draw.cs` | flakes, cursor, fireworks, drawing |
| `Ice.cs` | icicles |
| `Styles.cs` | flake styles and blocky drifts |
| `Ui.cs` | settings window with custom-drawn controls |
| `build.bat` | build script |

### Support

If you like SnowDream, you can [support the author](https://www.donationalerts.com/r/oktau7) ♥

---

## Русский

SnowDream запускает снег прямо поверх рабочего стола. Снежинки летят с ветром, ложатся на верхние края открытых окон и постепенно наметают холмистые сугробы. Закроешь или свернёшь окно, и его сугроб рухнет вниз, а может и засыпать окна под ним. По верхнему краю экрана растут сосульки, а падая, они разбиваются.

### Возможности

- **Настоящая физика снега.** Снег копится на видимых краях окон естественными холмистыми сугробами, сползает с краёв, повторяет скруглённые углы окон Windows 11, засыпает панель задач и низ экрана.
- **Окна взаимодействуют со снегом.** Закрыл или свернул окно, и сугроб обрушивается. Резко дёрнул окно, и снег слетает по инерции, а при плавном перетаскивании держится.
- **Сосульки.** Растут вниз от верхнего края каждого монитора, иногда капают, срываются и разбиваются на подпрыгивающие осколки. Скорость роста настраивается.
- **Ветер.** Можно выбрать направление (влево, авто, вправо) и силу: от снега строго вниз до косой метели.
- **«Сдуть весь снег».** Порыв ветра проносится по экрану в сторону ветра и поднимает в воздух сугробы, сосульки и осколки. Секунд через 5 он стихает.
- **Виды снежинок.** Мягкие точки, вращающиеся шестилучевые звёзды, блоки или пиксельные снежинки. У двух последних сугробы тоже блочные, ступеньками.
- **Курсор.** Снежинки облетают мышку.
- **Новый год.** Живой отсчёт в настройках и салют на рабочем столе 1 января в 00:00, а кнопкой можно запустить его прямо сейчас.
- **Не мешает.** Клики проходят сквозь снег. В развёрнутых окнах снег может стать тихим или спрятаться, а в играх и полноэкранном видео прячется.
- **Лёгкий.** Около 1–3% процессора, рисуется в отдельном потоке, один `.exe` примерно на 100 КБ без зависимостей.
- Сила снегопада, скорость падения и размер снежинок, режим нагрузки (30 / 60 кадров в секунду / частота монитора), автозапуск, русский / английский.

### Скачать

Скачай `SnowDream.exe` со страницы [Releases](../../releases) и запусти. Установка не нужна.

> Windows SmartScreen может предупредить, что приложение не подписано. Нажми **Подробнее → Выполнить в любом случае**.

### Как пользоваться

- **Левый клик** по снежинке в трее открывает настройки.
- **Правый клик** открывает быстрое меню: настройки, остановить снегопад, сдуть снег, выход.
- `SnowDream.exe --blow` сдувает снег, а `SnowDream.exe --fireworks` запускает салют у запущенной программы. Удобно повесить на ярлык.
- Если снежинки не видно, Windows 11 могла спрятать её под стрелочку **^** у часов. Перетащи её на панель задач.

### Требования

Windows 10 или 11 (x64) с .NET Framework 4.x, он уже есть в Windows.

### Сборка из исходников

SDK не нужен: проект собирается компилятором C#, который встроен в Windows.

```bat
build.bat
```

Готовый `SnowDream.exe` появится в той же папке.

### Поддержать

Если SnowDream тебе понравился, можно [поддержать автора](https://www.donationalerts.com/r/oktau7) ♥

---

MIT License · © 2026 øktaus
