using CrewChiefRUAssistant.Utilities;

namespace CrewChiefRUAssistant.Intent;

public static class IntentPhraseCatalog
{
    private sealed record PhraseGroup(
        IntentKind Intent,
        string[] Phrases);

    private static readonly PhraseGroup[] Groups =
    [
        new(
            IntentKind.RadioCheck,
            [
                "меня слышно",
                "как меня слышно",
                "проверка связи",
                "радио проверка",
                "прием как слышно"
            ]),

        new(
            IntentKind.FuelLevel,
            [
                "сколько топлива осталось",
                "остаток топлива",
                "сколько бензина",
                "уровень топлива"
            ]),

        new(
            IntentKind.FuelCapacity,
            [
                "какой объем бака",
                "сколько в полном баке",
                "сколько процентов топлива",
                "сколько топлива в баке"
            ]),

        new(
            IntentKind.FuelConsumption,
            [
                "какой расход топлива",
                "сколько топлива на круг",
                "сколько сжигаем за круг"
            ]),

        new(
            IntentKind.FuelLapsRemaining,
            [
                "на сколько кругов хватит топлива",
                "сколько кругов на топливе",
                "запас топлива по кругам"
            ]),

        new(
            IntentKind.FuelToFinish,
            [
                "хватит ли топлива до финиша",
                "доедем ли по топливу",
                "дотянем до финиша"
            ]),

        new(
            IntentKind.FuelToAdd,
            [
                "сколько топлива добавить",
                "сколько залить топлива",
                "сколько дозаправить",
                "сколько долить",
                "сколько добавить",
                "сколько залить"
            ]),

        new(
            IntentKind.PitNeed,
            [
                "нужен ли пит стоп",
                "надо ли в боксы",
                "нужно ли заезжать на пит"
            ]),

        new(
            IntentKind.Position,
            [
                "какая позиция",
                "на каком я месте",
                "каким я иду"
            ]),

        new(
            IntentKind.ClassPosition,
            [
                "какая позиция в классе",
                "какое место в классе"
            ]),

        new(
            IntentKind.CarClass,
            [
                "какой класс машины",
                "тип машины",
                "тип класса",
                "какая категория машины"
            ]),

        new(
            IntentKind.CarsCount,
            [
                "сколько машин",
                "сколько машин в сессии",
                "сколько машин в классе",
                "количество машин",
                "теперь машин"
            ]),

        new(
            IntentKind.LapsRemaining,
            [
                "сколько кругов осталось",
                "оставшиеся круги"
            ]),

        new(
            IntentKind.CompletedLaps,
            [
                "сколько кругов пройдено",
                "сколько кругов завершено",
                "сколько кругов прошло",
                "сколько кругов я проехал"
            ]),

        new(
            IntentKind.TimeRemaining,
            [
                "сколько времени осталось",
                "время до конца",
                "сколько еще времени"
            ]),

        new(
            IntentKind.CurrentLapNumber,
            [
                "какой сейчас круг",
                "номер текущего круга",
                "какой круг"
            ]),

        new(
            IntentKind.CurrentLap,
            [
                "время круга",
                "время текущего круга",
                "какое время текущего круга"
            ]),

        new(
            IntentKind.LastLap,
            [
                "время последнего круга",
                "какой был последний круг",
                "какой бы последний круг",
                "так был после",
                "последний круг"
            ]),

        new(
            IntentKind.BestLap,
            [
                "какой лучший круг",
                "лучшее время круга",
                "самый быстрый круг",
                "лучший друг",
                "лучший круг за все время заезда",
                "лучше груза все время заезда"
            ]),

        new(
            IntentKind.AverageLap,
            [
                "какое среднее время круга",
                "средний круг",
                "среднее время"
            ]),

        new(
            IntentKind.CurrentSector,
            [
                "какой сейчас сектор",
                "номер сектора"
            ]),

        new(
            IntentKind.SectorTimes,
            [
                "какие времена секторов",
                "время секторов",
                "сектора прошлого круга"
            ]),

        new(
            IntentKind.LastLapValidity,
            [
                "последний круг зачетный",
                "прошлый круг валидный",
                "круг действительный"
            ]),

        new(
            IntentKind.LastLapStatus,
            [
                "это последний круг",
                "сейчас последний круг",
                "финальный круг"
            ]),

        new(
            IntentKind.GapAhead,
            [
                "какой отрыв впереди",
                "какой отрыв спереди",
                "сколько до машины впереди",
                "сколько до следующей машины",
                "сколько до следующего машины",
                "сколько до ближайшей машины",
                "есть ли машина спереди",
                "если мария спереди",
                "машина спереди",
                "время до следующей машины",
                "время до ближайшей машины",
                "время до впереди машины",
                "сколько до лидера",
                "сколько да лидеров",
                "интервал впереди",
                "интервал спереди",
                "отряд спереди",
                "три спереди",
                "а три спереди"
            ]),

        new(
            IntentKind.GapBehind,
            [
                "какой отрыв сзади",
                "сколько до машины сзади",
                "сколько до ближайшей машины сзади",
                "время до машины сзади",
                "интервал сзади",
                "отрыв позади",
                "остальные сзади"
            ]),

        new(
            IntentKind.FlagStatus,
            [
                "какой флаг",
                "что за флаг",
                "текущий флаг"
            ]),

        new(
            IntentKind.IncidentStatus,
            [
                "сколько инцидентов",
                "сколько штрафных баллов",
                "сколько иксов"
            ]),

        new(
            IntentKind.TrackInfo,
            [
                "какая трасса",
                "название трассы",
                "какая длина трассы"
            ]),

        new(
            IntentKind.TrackTemperature,
            [
                "какая температура трассы",
                "температура трассы",
                "сколько градусов трасса",
                "какая температура асфальта",
                "температура асфальта"
            ]),

        new(
            IntentKind.AirTemperature,
            [
                "какая температура воздуха",
                "температура воздуха",
                "сколько градусов воздух"
            ]),

        new(
            IntentKind.SessionInfo,
            [
                "какая сейчас сессия",
                "какой тип сессии",
                "какая фаза сессии"
            ]),

        new(
            IntentKind.TyreTemperatures,
            [
                "температура шин",
                "температура колес",
                "какие температуры шин",
                "температура сын",
                "температура сша",
                "тротуару сын",
                "тротуару шин"
            ]),

        new(
            IntentKind.TyrePressures,
            [
                "давление шин",
                "какое давление в колесах",
                "давление колес"
            ]),

        new(
            IntentKind.TyreWear,
            [
                "износ шин",
                "состояние шин",
                "остаток шин"
            ]),

        new(
            IntentKind.TyreType,
            [
                "какой тип шин",
                "какой состав шин",
                "какой компаунд"
            ]),

        new(
            IntentKind.TyreSet,
            [
                "какой комплект шин",
                "номер комплекта шин"
            ]),

        new(
            IntentKind.BrakeTemperatures,
            [
                "температура тормозов",
                "температура дисков",
                "температура тормозных дисков",
                "темпера тормозов",
                "тетро тормозов",
                "тепло тормозов"
            ]),

        new(
            IntentKind.WheelStatus,
            [
                "блокируются колеса",
                "есть пробуксовка",
                "колеса на месте",
                "оторвано колесо"
            ]),

        new(
            IntentKind.LeaderCompletedLaps,
            [
                "сколько кругов проехал лидер",
                "сколько кругов у лидера",
                "круги лидера"
            ]),

        new(
            IntentKind.BatteryLevel,
            [
                "заряд батареи",
                "насколько заряжена батарея",
                "сколько процентов батареи",
                "уровень заряда",
                "зрения батареи"
            ]),

        new(
            IntentKind.AbsSetting,
            [
                "значение абс",
                "уровень абс",
                "какой абс"
            ]),

        new(
            IntentKind.TractionControlSetting,
            [
                "значение трекшн контроля",
                "уровень контроля тяги",
                "какой трекшн контроль",
                "значение трещину контрол"
            ]),

        new(
            IntentKind.CarAheadSpeed,
            [
                "скорость машины спереди",
                "скорость машины впереди"
            ]),

        new(
            IntentKind.CarBehindSpeed,
            [
                "скорость машины сзади",
                "спрос машина сзади"
            ]),

        new(
            IntentKind.IncidentAhead,
            [
                "есть ли авария спереди",
                "есть ли авария впереди",
                "авария спереди"
            ]),

        new(
            IntentKind.IncidentBehind,
            [
                "есть ли авария сзади",
                "авария сзади"
            ]),

        new(
            IntentKind.Damage,
            [
                "состояние машины",
                "повреждения машины",
                "есть повреждения",
                "состояние автомобиля",
                "как машина",
                "как там машина",
                "что с машиной",
                "как с машиной",
                "как ты с машины",
                "каким же день у машины",
                "и разрушен"
            ])
    ];

    private static readonly string[] All =
        Groups
            .SelectMany(group => group.Phrases)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    public static IReadOnlyList<string> AllPhrases => All;

    public static bool TryFuzzyMatch(
        string text,
        out IntentKind intent,
        out string matchedPhrase,
        out double similarity)
    {
        var query = RussianText.Normalize(text);

        intent = IntentKind.Unknown;
        matchedPhrase = string.Empty;
        similarity = 0;

        if (query.Words.Length == 0)
            return false;

        foreach (var group in Groups)
        {
            foreach (var phrase in group.Phrases)
            {
                var normalizedPhrase = RussianText.Normalize(phrase);
                var score = Similarity(query, normalizedPhrase);

                if (score <= similarity)
                    continue;

                similarity = score;
                intent = group.Intent;
                matchedPhrase = phrase;
            }
        }

        var threshold = query.Words.Length switch
        {
            1 => 0.82,
            2 => 0.70,
            _ => 0.66
        };

        return similarity >= threshold;
    }

    private static double Similarity(
        NormalizedRussianText left,
        NormalizedRussianText right)
    {
        if (left.Text == right.Text)
            return 1;

        var characterSimilarity =
            NormalizedLevenshtein(left.Text, right.Text);

        var tokenSimilarity =
            FuzzyTokenDice(left.Words, right.Words);

        var anchor = HasAnchor(left.Words, right.Words);

        if (!anchor && left.Words.Length > 1)
            return characterSimilarity * 0.72;

        return characterSimilarity * 0.62 +
               tokenSimilarity * 0.38;
    }

    private static bool HasAnchor(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        foreach (var first in left)
        {
            if (first.Length < 4)
                continue;

            foreach (var second in right)
            {
                if (second.Length < 4)
                    continue;

                if (NormalizedLevenshtein(first, second) >= 0.72)
                    return true;
            }
        }

        return false;
    }

    private static double FuzzyTokenDice(
        IReadOnlyList<string> left,
        IReadOnlyList<string> right)
    {
        if (left.Count == 0 || right.Count == 0)
            return 0;

        var used = new bool[right.Count];
        var matches = 0;

        foreach (var first in left)
        {
            var bestIndex = -1;
            var bestScore = 0.0;

            for (var index = 0; index < right.Count; index++)
            {
                if (used[index])
                    continue;

                var score = NormalizedLevenshtein(
                    first,
                    right[index]);

                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = index;
                }
            }

            var required = Math.Min(
                first.Length,
                bestIndex >= 0 ? right[bestIndex].Length : 0) >= 5
                    ? 0.70
                    : 0.82;

            if (bestIndex >= 0 && bestScore >= required)
            {
                used[bestIndex] = true;
                matches++;
            }
        }

        return 2.0 * matches / (left.Count + right.Count);
    }

    private static double NormalizedLevenshtein(
        string left,
        string right)
    {
        if (left.Length == 0)
            return right.Length == 0 ? 1 : 0;

        if (right.Length == 0)
            return 0;

        var previous = new int[right.Length + 1];
        var current = new int[right.Length + 1];

        for (var index = 0; index <= right.Length; index++)
            previous[index] = index;

        for (var leftIndex = 1;
             leftIndex <= left.Length;
             leftIndex++)
        {
            current[0] = leftIndex;

            for (var rightIndex = 1;
                 rightIndex <= right.Length;
                 rightIndex++)
            {
                var cost =
                    left[leftIndex - 1] == right[rightIndex - 1]
                        ? 0
                        : 1;

                current[rightIndex] = Math.Min(
                    Math.Min(
                        current[rightIndex - 1] + 1,
                        previous[rightIndex] + 1),
                    previous[rightIndex - 1] + cost);
            }

            (previous, current) = (current, previous);
        }

        var distance = previous[right.Length];
        return 1.0 -
               (double)distance /
               Math.Max(left.Length, right.Length);
    }
}
