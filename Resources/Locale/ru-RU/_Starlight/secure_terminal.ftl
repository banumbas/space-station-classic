## Secure Command Terminal – UI strings

secure-terminal-window-title = Безопасный терминал
secure-terminal-requests-header = Запросы
secure-terminal-information-header = Информация
secure-terminal-authorization-header = Авторизация

secure-terminal-select-request = Чтобы просмотреть подробности, выберите запрос из списка слева.

secure-terminal-request-button = Запрос
secure-terminal-request-button-confirm = Подтверждать?
secure-terminal-authorize-button = Авторизовать
secure-terminal-deny-button = Запретить/Отменить
secure-terminal-recall-button = Вспомнить Оружейную
secure-terminal-recall-locked = { $минуты ->
    [1] Возврат возможен через 1 минуту.
   *[other] Recall available in {$minutes} minutes.
}
secure-terminal-used-note = Этот арсенал был окончательно активирован или отозван в этом раунде и не может быть развернут снова.
secure-terminal-already-used = Этот ресурс уже использовался в этом раунде и не может быть запрошен снова.

secure-terminal-auth-waiting = По этому запросу нет активного предложения.
secure-terminal-auth-desc = Текущее предложение — нет ответа = [color=red]red[/color], согласовано = [color=green]green[/color]:
secure-terminal-awaiting-member = Ожидание {$label}

secure-terminal-pending-countdown-label = Срок действия истекает через {$minutes}м {$seconds}с…
secure-terminal-countdown-label = Активация через {$minutes}м {$seconds}с…

secure-terminal-fee-note = Плата за обработку: {$fee}
secure-terminal-salary-note = Зарплата станции уменьшена на {$penalty}% из-за затрат на мобилизацию.
secure-terminal-delay-note = { $минуты ->
    [1] Расчетное время прибытия: 1 минута после авторизации.
   *[other] ETA: {$minutes} minutes after authorization.
}

secure-terminal-requires-no-war-note = Отключено во время военных операций.
secure-terminal-requires-war-note = Доступно только во время военных операций.
secure-terminal-requires-alert-note = Требуется, чтобы оповещение {$level} было активным.
secure-terminal-alert-time-remaining = { $минуты ->
    [1] Оповещение должно быть активным еще 1 минуту, прежде чем его можно будет запросить.
   *[other] Alert must be active for {$minutes} more minutes before this can be requested.
}
secure-terminal-on-cooldown-note = { $минуты ->
    [1] По перезарядке — доступно через 1 минуту.
   *[other] On cooldown — available in {$minutes} minutes.
}
secure-terminal-requires-alert-suffix = Нужно: {$level}
secure-terminal-requires-war-suffix = Требуется: военные операции

secure-terminal-reason = Укажите причину запроса:

## Server → global announcements

secure-terminal-proposal-created = {$request} запрошен и ожидает совместной авторизации.
secure-terminal-proposal-created-reason = {$request} запрошен и ожидает совместной авторизации. Причина: {$reason}
secure-terminal-proposal-denied = Запрос {$request} был отменен.
secure-terminal-proposal-denied-cc = Запрос {$request} отклонен Центральным командованием.
secure-terminal-radio-proposal = {$request} был предложен. Пожалуйста, подойдите к ближайшему устройству аутентификации по ключ-карте, чтобы авторизовать или отклонить запрос.
secure-terminal-radio-proposal-reason = {$request} был предложен. Пожалуйста, подойдите к ближайшему устройству аутентификации по ключ-карте, чтобы авторизовать или отклонить запрос. Причина: {$reason}
secure-terminal-radio-denied = Запрос {$request} был отменен.
secure-terminal-activation-countdown = {$request} полностью авторизован.
    Активация через {$minutes} минут.
    Зарплата станции была уменьшена в связи с мобилизационными расходами.
secure-terminal-unknown-job = Неизвестный

## Popup messages

secure-terminal-no-station = Для этой консоли не найдено станций.
secure-terminal-request-denied = Доступ запрещен.
secure-terminal-authorize-denied = У вас нет необходимого разрешения для совместного подписания этого запроса.
secure-terminal-requires-war = Этот запрос доступен только после официального объявления военных операций.
secure-terminal-wrong-alert = Текущий уровень оповещения не соответствует требованиям этого запроса.
secure-terminal-alert-not-long-enough = Уровень оповещения не был активен достаточно долго, чтобы разрешить это. Пожалуйста, подождите и повторите попытку.
secure-terminal-recall-too-soon = Арсенал не был развернут достаточно долго, чтобы его можно было вспомнить. Пожалуйста, подождите.
secure-terminal-on-cooldown = Этот запрос находится на восстановлении.
secure-terminal-already-pending = Предложение по этому запросу уже находится на рассмотрении.
secure-terminal-already-active = Другой запрос уже находится на рассмотрении или активируется. Подождите, пока он завершится, прежде чем создавать новый.
secure-terminal-no-active-proposal = По этому запросу не найдено активное предложение.
secure-terminal-already-authorized = Вы уже одобрили это предложение.
secure-terminal-already-activated = Этот терминал уже одобрил это предложение.
secure-terminal-auth-note = Этот терминал предназначен только для авторизации.
secure-terminal-authorized-by = Внимание — запрос {$request} разрешен. Авторизовано: {$signatories}.
secure-terminal-armory-recalled = {$request} Выдан приказ об отзыве. Развертывание арсенала отменено.
secure-terminal-awaiting-admin = Внимание — запрос {$request} отправлен. Ожидает разрешения Центрального командования.
secure-terminal-admin = Запрос одобрения администратора для: {$request}
    Причина: {$reason}
    Используйте AGhost для одобрения/отклонения запроса.

## Request names & descriptions

secure-terminal-warops-security-name = Группа ядерного реагирования
secure-terminal-warops-security-desc = Развертывает подразделение безопасности ERT, специализирующееся на военных операциях. Доступно только во время военных операций.
    Используйте, когда станция подвергается прямому вооруженному нападению во время объявленной военной операции.
secure-terminal-warops-security-announcement = Группа экстренного реагирования — специализированная служба безопасности — получила разрешение и находится в пути. Предполагаемое время прибытия: 30 минут.

secure-terminal-ert-security-name = ЭРТ Безопасность
secure-terminal-ert-security-desc = Развертывает деталь безопасности ERT.
secure-terminal-ert-security-announcement = Группа экстренного реагирования — служба безопасности — получила разрешение и находится в пути. Предполагаемое время прибытия: 10 минут.

secure-terminal-ert-engineering-name = ЭРТ Инжиниринг
secure-terminal-ert-engineering-desc = Развертывает инженерную группу ERT для оказания помощи в работе с критически важной инфраструктурой станции.
    Рекомендуется, когда на станции произошли катастрофические структурные, атмосферные или энергетические сбои, выходящие за рамки местных возможностей ремонта.
secure-terminal-ert-engineering-announcement = Группа экстренного реагирования (инженерная часть) получила разрешение и находится в пути. Предполагаемое время прибытия: 10 минут.

secure-terminal-ert-medical-name = ЭРТ Медицинский
secure-terminal-ert-medical-desc = Развертывает медицинское подразделение ERT для сортировки массовых раненых и неотложной хирургии.
    Рекомендуется, когда медицинское отделение станции перегружено, выведено из строя или разрушено.
secure-terminal-ert-medical-announcement = Группа экстренного реагирования (медицинская часть) получила разрешение и находится в пути. Предполагаемое время прибытия: 10 минут.

secure-terminal-ert-janitorial-name = ЭРТ Уборка
secure-terminal-ert-janitorial-desc = Развертывает подразделение уборки ERT для очистки опасных объектов и восстановления станции.
    Рекомендуется после крупномасштабного биологического, химического загрязнения или загрязнения окружающей среды, требующего быстрой дезактивации.
secure-terminal-ert-janitorial-announcement = Группа экстренного реагирования — санитарная часть — получила разрешение и находится в пути. Предполагаемое время прибытия: 10 минут.

secure-terminal-ert-chaplain-name = Капеллан ERT
secure-terminal-ert-chaplain-desc = Направляет капеллана ERT для морального духа экипажа и поддержки последних обрядов.
    Обеспечивает пастырскую поддержку и поддерживает моральный дух экипажа во время длительных чрезвычайных ситуаций.
secure-terminal-ert-chaplain-announcement = Группа экстренного реагирования — капеллан — получила разрешение и находится в пути. Предполагаемое время прибытия: 10 минут.

secure-terminal-ert-cburn-name = ЭРТ ОХГОРЕНИЕ
secure-terminal-ert-cburn-desc = Развертывает деталь ERT CBURN.
secure-terminal-ert-cburn-announcement = Группа экстренного реагирования — подразделение CBURN — получила разрешение и находится в пути. Предполагаемое время прибытия: 15 минут.

secure-terminal-code-gamma-name = Код ГАММА
secure-terminal-code-gamma-desc = Повышает уровень оповещения станции до [color=palevioletred]ГАММА[/color]. Военное положение — все гражданские лица должны сопровождаться охраной в безопасные районы.
    Охрана должна быть всегда на вооружении. Все гражданские лица должны явиться к ближайшему руководителю штаба и быть сопровожденными в безопасное место. Включается аварийное освещение.
secure-terminal-code-gamma-announcement = Внимание! Кодекс ГАММА вводится в действие в ближайшее время. Будет введено военное положение. Весь экипаж немедленно докладывает ближайшему руководителю штаба.

secure-terminal-end-gamma-name = Завершить оповещение ГАММА
secure-terminal-end-gamma-desc = Снимает оповещение [color=palevioletred]ГАММА[/color] и возвращает станцию ​​в зеленый цвет. Требуется, чтобы ГАММА была активна не менее 15 минут.
secure-terminal-end-gamma-announcement = Код ГАММА снимается. Станция восстанавливается в обычном режиме. Будьте начеку и ждите дальнейших указаний от руководителя вашего штаба.

secure-terminal-code-psi-name = Код PSI
secure-terminal-code-psi-desc = Повышает уровень оповещения станции до [color=mediumpurple]PSI[/color]. Обнаружены враждебные синтетические подразделения — избегайте несогласных киборгов и ищите командный состав.
    Указывает на враждебную или неконформную деятельность киборгов. Весь экипаж должен избегать неизвестных боргов, оставаться в группах и обращаться за советом к начальнику штаба.
secure-terminal-code-psi-announcement = Внимание! Командование утвердило Кодекс PSI. Кремниевые устройства, не относящиеся к NanoTrasen, были идентифицированы как активная угроза. Весь экипаж — доложить ближайшему руководителю штаба.

secure-terminal-end-psi-name = Конец оповещения PSI
secure-terminal-end-psi-desc = Снимает тревогу [color=mediumpurple]PSI[/color] и возвращает станцию ​​в зеленый цвет. Требуется, чтобы PSI был активен в течение как минимум 15 минут.
secure-terminal-end-psi-announcement = Кодекс PSI снимается. Выявленная синтетическая угроза нейтрализована. Станция возвращается к обычному режиму работы.

secure-terminal-armory-gamma-name = Гамма Оружейная
secure-terminal-armory-gamma-desc = Отправляет [color=palevioletred]Гамма-арсенал[/color] — тайник с тяжелым оружием для ГАММА-ситуаций. Одноразовое внедрение.
    Выдает уполномоченному персоналу сверхмощное охранное оборудование.
secure-terminal-armory-gamma-announcement = Гамма-Арсенал получил разрешение и уже в пути.

secure-terminal-armory-psi-name = Пси-Арсенал
secure-terminal-armory-psi-desc = Отправляет [color=mediumpurple]Пси-арсенал[/color] — антикибернетическое оружие для пси-ситуаций. Одноразовое внедрение.
    Предоставляет инструменты, необходимые для нейтрализации несоответствующего кремния.
secure-terminal-armory-psi-announcement = Пси-Арсенал получил разрешение и уже в пути.

secure-terminal-med-pod-name = Модуль неотложной медицинской помощи
secure-terminal-med-pod-desc = Отправляет капсулу неотложной медицинской помощи — сортировочную группу быстрого развертывания с хирургическим и реанимационным оборудованием.
    Используйте, когда массовые жертвы превышают медицинские возможности станции.
secure-terminal-med-pod-announcement = Капсула неотложной медицинской помощи получила разрешение и находится в пути. Предполагаемое время прибытия: 5 минут.

secure-terminal-nukerequest-name = Код самоуничтожения
secure-terminal-nukerequest-desc = Запросите коды ядерного самоуничтожения.
    Злоупотребление системой ядерных запросов недопустимо ни при каких обстоятельствах.
    Передача не гарантирует ответа.

secure-terminal-code-violet-name = Код Фиолетовый
secure-terminal-code-violet-desc = Повышает уровень оповещения станции до [color=Violet]Violet[/color].

secure-terminal-end-violet-name = Конец фиолетовому оповещению
secure-terminal-end-violet-desc = Снимает оповещение [color=Violet]Violet[/color] и возвращает станцию ​​в зеленый цвет. Требуется, чтобы Вайолет была активна не менее 10 минут.

secure-terminal-emergency-maintenance-name = Доступ для аварийного обслуживания
secure-terminal-emergency-maintenance-desc = Предоставьте доступ для экстренного обслуживания.
secure-terminal-emergency-maintenance-announcement = Сняты ограничения доступа к техническому обслуживанию и внешним шлюзам.

secure-terminal-end-emergency-maintenance-name = Отозвать доступ к экстренному обслуживанию
secure-terminal-end-emergency-maintenance-desc = Отозвать доступ к экстренному обслуживанию.
secure-terminal-end-emergency-maintenance-announcement = Снова добавлены ограничения доступа к техническому обслуживанию и внешним шлюзам.

secure-terminal-emergency-station-name = Аварийный доступ на всю станцию
secure-terminal-emergency-station-desc = Активируйте аварийный доступ ко всей станции.
secure-terminal-emergency-station-announcement = Ограничения доступа ко всем шлюзам станции сняты в связи с продолжающимся кризисом. Законы о вторжении на территорию по-прежнему применяются, если командование не прикажет иное.

secure-terminal-end-emergency-station-name = Деактивировать экстренный доступ на всю станцию
secure-terminal-end-emergency-station-desc = Деактивируйте аварийный доступ ко всей станции.
secure-terminal-end-emergency-station-announcement = Снова добавлены ограничения доступа ко всем шлюзам станции. Если вы застряли, обратитесь за помощью к искусственному интеллекту станции или к коллеге.
