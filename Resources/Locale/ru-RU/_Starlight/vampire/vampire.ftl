## Base actions

alerts-vampire-blood-name = Кровь Пьяная
alerts-vampire-blood-desc = Показывает, сколько крови вы выпили. Вытяните клыки и щелкните левой кнопкой мыши по цели, чтобы выпить.

alerts-vampire-fed-name = Полнота крови
alerts-vampire-fed-desc = Ваша текущая полнота крови. Пейте кровь, чтобы оставаться сытыми.

roles-antag-vamire-name = Вампир
roles-antag-vampire-description = Кормитесь командой. Вытяните клыки и выпейте их кровь.

roles-antag-thrall-name = Тралл
roles-antag-thrall-objective = Служите своему господину верно и подчиняйтесь его приказам.

vampire-roundend-name = вампир

vampire-drink-start = Ты вонзаешь клыки в {CAPITALIZE(THE($target))}.

vampire-not-enough-blood = Недостаточно крови.

vampire-mouth-covered = Твой рот закрыт!
vampire-drink-invalid-target = Вы не можете пить кровь вампиров или их рабов.
vampire-target-protected-by-faith = Этого человека защищает их вера!
vampire-drink-target-empty = У этого существа нет крови!
vampire-drink-target-maxed = Вы уже выпили { $amount } единиц крови этой цели.
vampire-drink-target-hard-max = Вы выпили максимальное количество крови из этой цели ({ $amount } единиц).
vampire-full-power-achieved = Ваша вампирическая сущность достигла полной силы!
vampire-umbrae-full-power-fov = Тени подчиняются твоей воле. Теперь вы можете видеть сквозь стены!
vampire-drink-target-not-viable = У этого существа нет бьющегося сердца!
vampire-drink-target-rot = Сущность этого существа отвратительна!
vampire-sleep-shielded = Это существо невозможно усыпить из-за имплантата!
vampire-sleep-protected = Требуется лучший зрительный контакт...

vampire-role-greeting = Ты вампир!
    Ваша жажда крови заставляет вас питаться членами экипажа. Используйте свои способности, чтобы обратить вспять другую команду.
    Ваши клыки позволяют вам сосать кровь людей. Кровь восстановит здоровье и даст вам новые способности.
    Найдите, чем заняться в эту смену!

# Objectives
objective-issuer-vampire = [color=crimson]Вампир[/color]

objective-condition-drain-title = Слить {$count} единиц крови
objective-condition-drain-description = Выпейте {$count} единиц крови членов экипажа, используя свои клыки.

objective-vampire-thrall-obey-master-title = Повинуйся своему хозяину, {$targetName}.

# Class selection action
action-vampire-class-select = Выберите класс вампира
action-vampire-class-select-desc = Выберите свой подкласс вампиров

# Round end statistics
roundend-prepend-vampire-drained-low = Вампиры почти не кормили эту смену, истощая всего {$blood} единиц крови.
roundend-prepend-vampire-drained-medium = Вампиры прилично пообедали, истощив {$blood} единиц крови.
roundend-prepend-vampire-drained-high = Вампиры устроили кровавый пир, выпив {$blood} единиц крови!
roundend-prepend-vampire-drained-critical = Вампиры приступили к безумному питанию, высасывая ошеломляющее количество {$blood} единиц крови!

roundend-prepend-vampire-drained = Ни одному вампиру не удалось истощить сколько-нибудь значительное количество крови в этом раунде.
roundend-prepend-vampire-drained-named = {$name} был самым кровожадным вампиром, высасывавшим всего {$number} единиц крови.

# Vampire class selection tooltips
vampire-class-hemomancer-tooltip = Гемомант
    Фокусируется на магии крови и манипулировании кровью вокруг вас.

vampire-class-umbrae-tooltip = Умбра
    Фокусируется на темноте, скрытных засадах и мобильности.

vampire-class-gargantua-tooltip = Гаргантюа
    Фокусируется на упорстве и уроне в ближнем бою.

vampire-class-dantalion-tooltip = Данталион
    Сосредоточено на порабощении и иллюзиях.

# Hemomancer abilities
action-vampire-hemomancer-tendrils-wrong-place = Невозможно кастовать туда.

action-vampire-blood-barrier-wrong-place = Там нельзя размещать барьеры.

action-vampire-sanguine-pool-already-in = Вы уже в форме сангвиника!
action-vampire-sanguine-pool-invalid-tile = Здесь нельзя стать лужей крови.
action-vampire-sanguine-pool-enter = Ты превращаешься в лужу крови!
action-vampire-sanguine-pool-exit = Вы исправляетесь из лужи крови!
vampire-space-burn-warning = Резкий свет пустоты опаляет вашу плоть нежити!

action-vampire-blood-eruption-activated = Вы заставляете кровь извергаться шипами вокруг себя!

action-vampire-blood-bringers-rite-not-enough-power = Вам не хватает полной вампирической силы (нужно более 1000 общего количества крови и 8 уникальных жертв)
action-vampire-blood-brighters-rite-not-enough-blood = Недостаточно крови, чтобы активировать обряд принесения крови.
action-vampire-blood-bringers-rite-start = Обряд Несущих Кровь активирован!
action-vampire-blood-bringers-rite-stop = Обряд приносящих кровь деактивирован
action-vampire-blood-bringers-rite-stop-blood = Обряд Несущих Кровь деактивирован – крови недостаточно.

vampire-locate-result = Ваши чувства прослеживают путь от { $target } до { $location }.
vampire-locate-not-same-sector = Vampire-locate-not-same-sector = Этот человек не в вашем секторе.
vampire-locate-unknown = Неизвестный район
vampire-locate-no-targets = В этом секторе не чувствуется жертва.

predator-sense-title = Чувство хищника
vampire-locate-search-placeholder = Поиск...

vampiric-claws-remove-popup = Ты заставляешь когти исчезать.

# Umbrae abilities
action-vampire-cloak-of-darkness-start = Ты сливаешься с тенями!
action-vampire-cloak-of-darkness-stop = Ты выходишь из тени.

action-vampire-shadow-snare-placed = Вы устанавливаете теневую ловушку.
action-vampire-shadow-snare-wrong-place = Здесь нельзя поставить ловушку.
action-vampire-shadow-snare-scatter = Вы рассеяли теневую ловушку.
vampire-shadow-snare-oldest-removed = Ваша старая теневая ловушка рассеивается.
ent-shadow-snare-ensnare = теневая ловушка

action-vampire-shadow-anchor-returned = Вы вернулись к теневому якорю
action-vampire-shadow-anchor-installed = Вы обеспечили себе место в тени

action-vampire-shadow-boxing-start = Вы начинаете бой с тенью.
action-vampire-shadow-boxing-stop = Бой с тенью прекращен.
action-vampire-shadow-boxing-ends = Бой с тенью завершен.

action-vampire-dark-passage-wrong-place = Тьма здесь непроглядна...
action-vampire-dark-passage-activated = Ты проскользнул сквозь тьму...

action-vampire-extinguish-activated = Ты поглотил свет вокруг себя...({$count})

action-vampire-eternal-darkness-not-enough-blood = У вас закончилась кровь, чтобы поддерживать вечную тьму.
action-vampire-eternal-darkness-start = Ты вызвал вечную тьму...
action-vampire-eternal-darkness-stop = Вечная тьма рассеялась...

#Dantalion
vampire-enthrall-start = Вы проникаете в разум {CAPITALIZE(THE($target))}...
vampire-enthrall-success = {CAPITALIZE(THE($target))} преклоняет колени и становится вашим рабом.
vampire-enthrall-target = Ваш разум охвачен вампирическим господством!
vampire-enthrall-limit = Вы больше не можете контролировать рабов.
vampire-enthrall-invalid = Эту цель невозможно поработить.
vampire-thrall-released = Вампирская власть над вами ослабевает.

vampire-pacify-invalid = Эту цель невозможно усмирить.
vampire-pacify-success = {CAPITALIZE(THE($target))} поддается вашему подавляющему спокойствию.
vampire-pacify-target = Сокрушительное спокойствие заглушает вашу волю к борьбе!

vampire-subspace-swap-thrall = Вы не можете обмениваться подпространством со своими рабами.
vampire-subspace-swap-dead = Этот ум недосягаем для вас.
vampire-subspace-swap-failed = Подпространственный разлом бесполезно шипит.
vampire-subspace-swap-success = Пространство искривляется, когда вы меняетесь местами с {CAPITALIZE(THE($target))}!
vampire-subspace-swap-target = Реальность искажается и вас рвёт в новое положение!

vampire-rally-thralls-success = {$счет ->
    [one] Ваш призыв вернет на вашу сторону раба!
    *[other] Ваш призыв мобилизует {$count} обратно на вашу сторону!
}
vampire-rally-thralls-none = Никто из твоих рабов не сможет ответить на звонок.
vampire-thrall-holy-water-freed = Святая вода очищает ваш разум от вампиров!

vampire-blood-bond-start = Реки крови свяжут вас с вашими рабами.
vampire-blood-bond-stop = Ты позволяешь узам крови ослабнуть.
vampire-blood-bond-no-thralls = У вас нет порабощенных слуг, с которыми можно было бы сблизиться.
vampire-blood-bond-stop-blood = Связь разрывается сама собой; вам не хватает крови, чтобы поддерживать это.

action-vampire-not-enough-power = Вашей силы недостаточно (нужно >1000 общего количества крови и 8 уникальных жертв).

# Gargantua
vampire-blood-swell-start = Твои мышцы набухают от нечестивой силы.
vampire-blood-swell-end = Кровавая ярость утихает.

vampire-blood-rush-start = Кровь хлещет по твоим конечностям!
vampire-blood-rush-end = Ваша сверхъестественная скорость снижается.

vampire-seismic-stomp-activate = Земля содрогается от твоей ярости!

vampire-overwhelming-force-start = Ваше присутствие становится неподвижным.
vampire-overwhelming-force-stop = Вы ослабляете свою железную хватку.
vampire-overwhelming-force-too-heavy = Этот объект слишком тяжел, чтобы его можно было сдвинуть!
vampire-overwhelming-force-door-pried = Вы вырываете дверь с грубой силой.

vampire-demonic-grasp-hit = Демонический коготь схватит тебя!
vampire-demonic-grasp-pull = Коготь тянет вас к вампиру!

vampire-charge-start = Вы несетесь вперед с неудержимой силой!
vampire-charge-impact = Вы врезаетесь в {CAPITALIZE(THE($target))} с разрушительной силой!


vampire-blood-swell-cancel-shoot = Пальцы не помещаются в спусковую скобу!!

vampire-holy-place-burn = Священная земля обжигает твою нечестивую плоть!

alerts-vampire-blood-swell-name = Набухание крови
alerts-vampire-blood-swell-desc = Твои мышцы наполняются нечестивой силой.
alerts-vampire-blood-rush-name = Прилив крови
alerts-vampire-blood-rush-desc = Сверхъестественная скорость проходит через ваши конечности.

Vamp-converted-title = В восторге!
Vamp-converted-text =
    Вы были в восторге!
    Искренне подчиняйтесь своему хозяину, вы можете получить доступ к коллективному разуму, нажав «+p».
Vamp-converted-confirm = Понял
