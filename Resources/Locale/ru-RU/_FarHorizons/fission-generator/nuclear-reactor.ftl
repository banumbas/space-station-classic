### Popups
reactor-smoke-start = {CAPITALIZE(THE($owner))} начинает дымить!
reactor-smoke-stop = {CAPITALIZE(THE($owner))} бросает курить.
reactor-fire-start = {CAPITALIZE(THE($owner))} начинает гореть!
reactor-fire-stop = {CAPITALIZE(THE($owner))} перестает гореть.

reactor-unanchor-melted = Вы не можете отсоединить {THE($owner)}, он вплавился в корпус!
reactor-unanchor-warning = Вы не можете отсоединить {THE($owner)}, пока он не пуст и не нагрелся выше 80C!
reactor-anchor-warning = Неверное положение привязки.

### Messages
reactor-smoke-start-message = ВНИМАНИЕ: {CAPITALIZE(THE($owner))} достиг опасной температуры: {$temperature}K. Немедленно вмешайтесь, чтобы предотвратить крах.
reactor-smoke-stop-message = {CAPITALIZE(THE($owner))} остыл ниже опасной температуры. Хорошего дня.
reactor-fire-start-message = ПРЕДУПРЕЖДЕНИЕ: {CAPITALIZE(THE($owner))} достиг КРИТИЧЕСКОЙ температуры: {$temperature}K. КАЛАС НЕИЗБЕЖЕН.
reactor-fire-stop-message = {CAPITALIZE(THE($owner))} остыл ниже критической температуры. Крах предотвращен.

reactor-temperature-dangerous-message = {CAPITALIZE(THE($owner))} имеет опасную температуру: {$temperature}K.
reactor-temperature-critical-message = {CAPITALIZE(THE($owner))} имеет критическую температуру: {$temperature}K.
reactor-temperature-cooling-message = {CAPITALIZE(THE($owner))} охлаждается: {$temperature}K.

reactor-melting-announcement = Ядерный реактор на борту станции начинает выходить из строя. Рекомендуется эвакуироваться с прилегающей территории.
reactor-melting-announcement-sender = Ядерная чрезвычайная ситуация

reactor-meltdown-announcement = Ядерный реактор на борту станции катастрофически перегрузился. Вероятны радиоактивный мусор, ядерные осадки и пожары охлаждающей жидкости. Настоятельно рекомендуется немедленно покинуть прилегающую территорию.
reactor-meltdown-announcement-sender = Ядерный кризис

### UI
comp-nuclear-reactor-ui-locked = Заблокировано
comp-nuclear-reactor-ui-insert-button = Вставлять
comp-nuclear-reactor-ui-remove-button = Удалять
comp-nuclear-reactor-ui-eject-button = Извлечь

comp-nuclear-reactor-ui-view-change = Изменить вид
comp-nuclear-reactor-ui-view-temp = Просмотр температуры
comp-nuclear-reactor-ui-view-neutron = Нейтронный вид
comp-nuclear-reactor-ui-view-fuel = Вид топлива

comp-nuclear-reactor-ui-status-panel = Статус реактора
comp-nuclear-reactor-ui-reactor-temp = Температура
comp-nuclear-reactor-ui-reactor-rads = Радиация
comp-nuclear-reactor-ui-reactor-therm = Тепловая энергия
comp-nuclear-reactor-ui-reactor-control = Управляющие стержни
comp-nuclear-reactor-ui-therm-format = { POWERWATTS($power) }т

comp-nuclear-reactor-ui-footer-left = Опасность: высокая радиация.
comp-nuclear-reactor-ui-footer-right = 1.0 ОБ. 1
