server-ban-string-infinity = Навсегда
server-ban-no-name = Не найдено. ({ $hwid })
server-time-ban =
    Временный бан на { $mins } { $mins ->
    [одна] минута
    [несколько] минут
    *[другие] минуты
    }.
server-perma-ban = Постоянный бан
server-role-ban =
    Временный запрет на работу на { $mins } { $mins ->
    [одна] минута
    [несколько] минут
    *[другие] минуты
    }.
server-perma-role-ban = Постоянный запрет на работу
server-time-ban-string =
    > **Преступник**
    > **Войти:** ``{ $targetName }``
    > **Раздор:** { $targetLink }

    > **Администратор**
    > **Войти:** ``{ $adminName }``
    > **Раздор:** { $adminLink }

    > **Время**
    > **Расширенное:** { $TimeNow }
    > **Срок действия истекает:** { $expiresString }

    > **Причина:** { $reason }

    > **Уровень серьезности:** { $severity }
server-ban-footer = { $server } | Раунд: #{ $round }
server-perma-ban-string =
    > **Преступник**
    > **Войти:** ``{ $targetName }``
    > **Раздор:** { $targetLink }

    > **Администратор**
    > **Войти:** ``{ $adminName }``
    > **Раздор:** { $adminLink }

    > **Время**
    > **Расширенное:** { $TimeNow }

    > **Причина:** { $reason }

    > **Уровень серьезности:** { $severity }
server-role-ban-string =
    > **Преступник**
    > **Войти:** ``{ $targetName }``
    > **Раздор:** { $targetLink }

    > **Администратор**
    > **Войти:** ``{ $adminName }``
    > **Раздор:** { $adminLink }

    > **Время**
    > **Расширенное:** { $TimeNow }
    > **Срок действия истекает:** { $expiresString }

    > **Роли:** { $roles }

    > **Причина:** { $reason }

    > **Уровень серьезности:** { $severity }
server-perma-role-ban-string =
    > **Преступник**
    > **Войти:** ``{ $targetName }``
    > **Discord:** ``{ $targetLink }``

    > **Администратор**
    > **Войти:** ``{ $adminName }``
    > **Раздор:** { $adminLink }

    > **Время**
    > **Расширенное:** { $TimeNow }

    > **Роли:** { $roles }

    > **Причина:** { $reason }

    > **Уровень серьезности:** { $severity }
