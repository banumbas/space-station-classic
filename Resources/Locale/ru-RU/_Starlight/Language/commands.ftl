command-list-langs-desc = Перечислите языки, на которых ваша нынешняя сущность может говорить в текущий момент.
command-list-langs-help = Использование: {$command}
command-saylang-desc = Отправьте сообщение на определенном языке. Для выбора языка вы можете использовать либо название языка, либо его позицию в списке языков.
command-saylang-help = Использование: {$command} <идентификатор языка> <сообщение>. Пример: {$command} GalacticCommon «Привет, мир!». Пример: {$command} 1 «Привет, мир!»
command-language-select-desc = Выберите текущий язык вашего объекта. Вы можете использовать либо название языка, либо его позицию в списке языков.
command-language-select-help = Использование: {$command} <идентификатор языка>. Пример: {$command} 1. Пример: {$command} GalacticCommon
command-language-spoken = Говорят:
command-language-understood = Понял:
command-language-current-entry = {$id}. {$language} — {$name} (текущий)
command-language-entry = {$id}. {$language} - {$name}
command-language-invalid-number = Номер языка должен находиться в диапазоне от 0 до {$total}. Альтернативно используйте название языка.
command-language-invalid-language = Язык {$id} не существует или вы не можете на нем говорить.
# Toolshed

command-description-language-add = Добавляет новый язык в передаваемый объект. Два последних аргумента указывают, следует ли это произносить/понимать. Пример: «язык речи: добавить «Canilunzt» true true»
command-description-language-rm = Удаляет язык из передаваемого объекта. Работает аналогично языку:добавить. Пример: «собственный язык: rm «GalacticCommon» true true».
command-description-language-lsspoken = Перечисляет все языки, на которых может говорить сущность. Пример: «собственный язык: lsspoken»
command-description-language-lsunderstood = Перечисляет все языки, которые может понимать сущность. Пример: 'собственный язык: lssunderstood'
command-description-translator-addlang = Добавляет новый целевой язык в объект транслируемого переводчика. См. язык:добавить для подробностей.
command-description-translator-rmlang = Удаляет целевой язык из передаваемого по конвейеру объекта-переводчика. Подробности см. в разделе Language:rm.
command-description-translator-addrequired = Добавляет новый необходимый язык в объект транслируемого переводчика. Пример: 'ent 1234 переводчик:addrequired "GalacticCommon"'
command-description-translator-rmrequired = Удаляет необходимый язык из передаваемого по конвейеру объекта-переводчика. Пример: 'ent 1234 переводчик:rmrequired "GalacticCommon"'
command-description-translator-lsspoken = Перечисляет все разговорные языки для объекта-переводчика. Пример: 'ent 1234 переводчик:lsspoken'
command-description-translator-lsunderstood = Перечисляет все понятные языки для передаваемого по конвейеру объекта-переводчика. Пример: 'ent 1234 переводчик: lssunderstood'
command-description-translator-lsrequired = Перечисляет все необходимые языки для объекта транслируемого переводчика. Пример: 'ent 1234 переводчик:lsrequired'
command-language-error-this-will-not-work = Это не сработает.
command-language-error-not-a-translator = Сущность {$entity} не является транслятором.
