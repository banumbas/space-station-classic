entity-condition-guidebook-unknown-reagent = неизвестный реагент

entity-condition-guidebook-blood-reagent-threshold =
    { $max ->
    [2147483648] в кровотоке есть как минимум {NATURALFIXED($min, 2)}u из {$reagent}
    *[other] { $min ->
    [0] в кровотоке не более {NATURALFIXED($max, 2)}u из {$reagent}
    *[other] кровоток находится между {NATURALFIXED($min, 2)}u и {NATURALFIXED($max, 2)}u из {$reagent}
    }
    }

entity-condition-guidebook-has-components =
    цель { $shouldhave ->
    [true] имеет
    *[false] не имеет
    } компонент {$name}
