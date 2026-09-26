# Map Mouse Select 0.2.0

Mod for Nuclear Option 0.34.1. Unzip it and put the `MapMouseSelect` folder into `BepInEx\plugins`. Needs BepInEx 5 x64. Every key below is rebindable in the game's own Controls menu.

## English

### Added

- Marquee: drag a box on the map to select in bulk, in both map modes.
- Shift while dragging adds, Ctrl removes, and the box colour shows which one is active.
- New keys:
  - `\` keeps only the unit you are pointing at
  - `-` drops the unit you are pointing at
  - tap `Delete` drops your oldest target, hold it to keep only the newest
  - `Ctrl+E` leaves an aircraft safely, which means parked on the ground at one of your own airbases and nothing anywhere else (vanilla's eject is still `Ctrl+Shift+E`)
- Wrecks: a destroyed building keeps its map icon, darkened, instead of vanishing.
- Two buttons in the Target List pane: `NTL` for neutrals and `DED` for wrecks, and Reset restores them with the game's own filters.

### Changed from vanilla

- A plain click in flight replaces the target list instead of adding to it.
- A drag ending on empty map clears the selection; vanilla leaves it alone.
- Panning moved to the middle mouse button, camera jump to a double left click on the map.
- With the map open, bindings sitting on a bare Shift or Ctrl are held off, so the marquee modifiers do not fire them too.
- A click on a unit that cannot be a target does nothing, instead of playing the selection sound and changing nothing.

### Vanilla bugs fixed

- Shift+click on a selected unit deselects it, where vanilla routes that to the wrong call.
- A selected unit's icon stays clickable, so its tooltip and the Shift-click deselect work again.
- Filter dimming is no longer lost after a deselect.
- Target List rows no longer outlive the selection (unselect all, respawn, spawn screen).
- The wheel no longer moves the camera's field of view behind menus, or in the flyby view while the map is open.
- Dragging the map moves it exactly as far as the mouse, instead of further the slower the game runs and in bursts.

## Русский

### Добавлено

- Рамка: тянешь мышь по карте и выделяешь пачкой, работает в обоих режимах карты.
- Зажатый Shift добавляет, зажатый Ctrl убирает, цвет рамки показывает, какой режим сейчас.
- Новые клавиши:
  - `\` оставляет только тот юнит, на который смотришь
  - `-` убирает юнит, на который смотришь
  - короткое `Delete` убирает самую старую цель, долгое оставляет только последнюю
  - `Ctrl+E` безопасно выпускает из самолёта, то есть только стоя на земле на своей базе, в остальных случаях ничего (ванильный eject так и остался на `Ctrl+Shift+E`)
- Обломки: уничтоженное здание остаётся на карте затемнённой иконкой, а не пропадает.
- Две кнопки в панели Target List: `NTL` для нейтралов и `DED` для обломков, сброс фильтров возвращает и их.

### Изменено по сравнению с ванилью

- Обычный клик в полёте заменяет список целей, а не докидывает в него.
- Протяжка, закончившаяся на пустой карте, снимает выделение; в ванили всё остаётся выделенным.
- Панорамирование переехало на среднюю кнопку мыши, прыжок камеры – на двойной клик по карте.
- Пока карта открыта, клавиши на одном Shift или Ctrl отключаются, чтобы модификаторы рамки не срабатывали и как они.
- Клик по юниту, который не может быть целью, ничего не делает, а не играет звук выделения.

### Починено в ваниле

- Shift+клик по выделенному юниту снимает выделение, в ванили этот случай уходит не в тот вызов.
- Клики по уже выделенному юниту снова работают: ваниль сбрасывает raycast-таргет иконки и возвращает его только при снятии.
- Затемнение отфильтрованных юнитов больше не теряется после снятия выделения.
- Строки в панели Target List больше не остаются после очистки выделения (снять всё, респавн, экран выбора самолёта).
- Колесо мыши больше не меняет FOV камеры за меню и в режиме облёта при открытой карте.
- Протяжка карты двигает её ровно на столько же, на сколько сдвинулась мышь: раньше карта уезжала дальше тем сильнее, чем ниже fps, и двигалась рывками.
