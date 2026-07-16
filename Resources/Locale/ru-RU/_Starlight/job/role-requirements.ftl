job-no-requirements = Эта работа не имеет требований.
ghost-role-no-requirements = Эта роль не имеет никаких требований.

# Coloring rule of thumb: limegreen for met requirement, yellow for unmet requirement that can still be met, red for unmeetable

role-timer-department-sufficient = У вас есть [color=limegreen]{TOSTRING($current, "0")}[/color] игрового времени [color=lightblue]{TOSTRING($required, "0")}[/color], необходимое в отделе [color={$departmentColor}]{$department}[/color].
role-timer-department-insufficient = У вас есть [color=yellow]{TOSTRING($current, "0")}[/color] игрового времени [color=lightblue]{TOSTRING($required, "0")}[/color], необходимого в отделе [color={$departmentColor}]{$department}[/color].
role-timer-department-not-too-high = У вас есть [color=limegreen]{TOSTRING($current, "0")}[/color] не более [color=lightblue]{TOSTRING($required, "0")}[/color] игрового времени в отделе [color={$departmentColor}]{$department}[/color].
role-timer-department-too-high = У вас есть [color=red]{TOSTRING($current, "0")}[/color] не более [color=lightblue]{TOSTRING($required, "0")}[/color] игрового времени в отделе [color={$departmentColor}]{$department}[/color]. (Вы пытаетесь играть роль стажера?)

role-timer-overall-sufficient = У вас есть [color=limegreen]{TOSTRING($current, "0")}[/color] из [color=lightblue]{TOSTRING($required, "0")}[/color] общего времени игры.
role-timer-overall-insufficient = У вас есть [color=yellow]{TOSTRING($current, "0")}[/color] из [color=lightblue]{TOSTRING($required, "0")}[/color] общего времени игры.
role-timer-overall-not-too-high = У вас есть [color=limegreen]{TOSTRING($current, "0")}[/color] не более [color=lightblue]{TOSTRING($required, "0")}[/color] общего времени игры.
role-timer-overall-too-high = У вас есть [color=red]{TOSTRING($current, "0")}[/color] не более [color=lightblue]{TOSTRING($required, "0")}[/color] общего времени игры. (Вы пытаетесь играть роль стажера?)

role-timer-role-sufficient = У вас есть [color=limegreen]{TOSTRING($current, "0")}[/color] игрового времени [color=lightblue]{TOSTRING($required, "0")}[/color], необходимое как [color={$departmentColor}]{$job}[/color].
role-timer-role-insufficient = У вас есть [color=yellow]{TOSTRING($current, "0")}[/color] игрового времени [color=lightblue]{TOSTRING($required, "0")}[/color], необходимое как [color={$departmentColor}]{$job}[/color].
role-timer-role-not-too-high = У вас есть [color=limegreen]{TOSTRING($current, "0")}[/color] не более [color=lightblue]{TOSTRING($required, "0")}[/color] времени игры как [color={$departmentColor}]{$job}[/color].
role-timer-role-too-high = У вас есть [color=red]{TOSTRING($current, "0")}[/color] не более [color=lightblue]{TOSTRING($required, "0")}[/color] времени игры как [color={$departmentColor}]{$job}[/color]. (Вы пытаетесь играть роль стажера?)

role-whitelisted = Вы [color=limegreen][/color] внесены в белый список на эту роль.
role-not-whitelisted = Вы [color=yellow]не[/color] в белом списке для выполнения этой роли.

role-timer-age-old-enough = Возраст вашего персонажа должен быть не ниже [color=limegreen]{$age}[/color], чтобы играть эту роль.
role-timer-age-not-old-enough = Чтобы играть эту роль, возраст вашего персонажа должен быть не ниже [color=yellow]{$age}[/color].
role-timer-age-young-enough = Возраст вашего персонажа должен быть не более [color=limegreen]{$age}[/color], чтобы играть эту роль.
role-timer-age-not-young-enough = Возраст вашего персонажа должен быть не более [color=yellow]{$age}[/color], чтобы играть эту роль.

role-timer-whitelisted-species-pass = Чтобы играть эту роль, ваш персонаж [color=limegreen]должен[/color] принадлежать к одному из следующих видов: [color=limegreen]{$species}[/color]
role-timer-whitelisted-species-fail = Чтобы играть эту роль, ваш персонаж [color=yellow]должен[/color] принадлежать к одному из следующих видов: [color=yellow]{$species}[/color]
role-timer-blacklisted-species-pass = Чтобы играть эту роль, ваш персонаж [color=limegreen]не должен[/color] принадлежать к одному из следующих видов: [color=limegreen]{$species}[/color]
role-timer-blacklisted-species-fail = Чтобы играть эту роль, ваш персонаж [color=yellow]не должен[/color] принадлежать к одному из следующих видов: [color=yellow]{$species}[/color]

role-timer-whitelisted-traits-pass = Ваш персонаж [color=limegreen]должен[/color] иметь одну из следующих черт: [color=limegreen]{$traits}[/color]
role-timer-whitelisted-traits-fail = Ваш персонаж [color=yellow]должен[/color] иметь одну из следующих черт: [color=yellow]{$traits}[/color]
role-timer-blacklisted-traits-pass = Ваш персонаж [color=limegreen]не должен[/color] иметь одну из следующих черт: [color=limegreen]{$traits}[/color]
role-timer-blacklisted-traits-fail = Ваш персонаж [color=yellow]не должен[/color] иметь одну из следующих черт: [color=yellow]{$traits}[/color]

role-ban = Вам [color=red]забанили[/color] эту роль.
