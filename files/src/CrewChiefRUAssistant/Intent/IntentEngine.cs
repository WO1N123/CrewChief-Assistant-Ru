using CrewChiefRUAssistant.Utilities;

namespace CrewChiefRUAssistant.Intent;

public sealed class IntentEngine
{
    public IntentResult Match(string text)
    {
        var utterance = RussianText.Normalize(text);
        var normalized = utterance.Text;

        bool Has(params string[] roots) => utterance.Has(roots);
        bool HasWord(params string[] words) =>
            utterance.Words.Any(word => words.Contains(word, StringComparer.Ordinal));

        var radioCheck = Has("слыш", "радио", "связ") &&
                         Has("меня", "проверк", "как слышно", "прием", "приём");

        var fuel = Has("топлив", "бензин", "горюч");
        var litres = Has("литр");
        var finish = Has("финиш", "конц", "гонк", "доед", "дотян");
        var laps = Has("круг", "лап");
        var consumption = Has("расход", "потреб", "сжига", "трат");
        var addFuel = Has("добав", "дол", "залит", "заправ");
        var tank = Has("бак", "емкост", "объем", "процент");
        var pit = Has("пит", "бокс", "останов");
        var need = Has("нуж", "надо", "треб", "заезж");

        var tyre = Has(
            "шин", "резин", "колес", "шон", "сшит", "сын", "сына", "шына", "сша");
        var temperature = Has(
            "температур", "темпер", "темп", "градус", "горяч", "холод", "тепло", "тетро", "тротуар");
        var pressure = Has("давлен", "пси", "psi");
        var wear = Has("износ", "остат", "стер", "состояни");
        var type = Has("тип", "состав", "компаунд");
        var set = Has("комплект", "сет", "набор");

        var brake = Has("тормоз", "диск");
        var locking = Has("блокир", "юз", "закусы");
        var spinning = Has("букс", "пробукс", "скольз");
        var attached = Has("оторв", "целы колеса", "колеса на месте");

        var carWord = Has("машин", "авто", "болид", "автомоб", "мария");
        var typeWord = Has("тип", "класс", "категори", "модел");
        var speed = Has("скорост", "спрос");

        var damage =
            Has("повреж", "разруш", "двигател", "аэро", "подвес") ||
            (carWord && (Has("состояни", "цел", "исправ", "нормал", "день") || HasWord("как", "что")));

        var position = Has("позици", "мест", "иду");
        var classWord = Has("класс");
        var classPosition = position && classWord;
        var carClass =
            (typeWord && (carWord || classWord) && !position) ||
            (carWord && Has("какой", "такой", "что за") && !damage && !position);

        var time = Has("врем", "минут", "секунд", "час");
        var remaining = Has("остал", "до конца", "сколько еще");
        var current = Has("текущ", "этот", "идущ", "сейчас");
        var last = Has("последн", "прошл");
        var best = Has("лучш", "быстр");
        var average = Has("средн");
        var completed = Has("пройден", "заверш", "проех") ||
                        (laps && Has("сколько") && Has("прошл"));
        var leader = Has("лидер", "виде");
        var lapLike = laps || (best && Has("друг", "груз"));

        var sector = Has("сектор");
        var flag = Has("флаг");
        var incident = Has("инцидент", "штраф", "икс", "нарушен");
        var accident = Has("авари", "столкнов", "инцидент на трассе");

        var track = Has("трасс", "трек", "асфальт", "длина трассы");
        var air = Has("воздух", "окруж", "на улице");
        var session = Has("сесси", "заезд", "практик", "квалификац");
        var validity = Has("валид", "зачет", "действител");

        var battery = Has("батаре", "аккумулятор", "заряд");
        var abs = HasWord("абс") || Has("антиблок");
        var traction = Has("трекш", "тракш", "traction", "трещину контрол", "контроль тяги");

        var gap = Has("отрыв", "отрив", "разрыв", "дельт", "интервал", "андрей", "отряд", "три", "остальн");
        var ahead = Has("вперед", "переди", "сперед", "перейд", "перейт", "следующ", "ближайш", "встреч", "лидер");
        var behind = Has("сзад", "позад", "за мной", "ради");
        var questionWord = Has("какой", "такой", "сколько", "далеко", "есть ли") || HasWord("если");
        var distanceToCar = Has("до") && (carWord || leader);

        var cars = carWord &&
            (Has("количество", "число") || Has("в сессии", "в классе") ||
             ((Has("сколько", "теперь") || HasWord("сколько")) &&
              !Has("до", "следующ", "ближайш", "вперед", "сзад", "лидер")));

        var isLastLapQuestion = last && lapLike && Has("это", "сейчас", "ли", "финальн");

        if (radioCheck) return Result(IntentKind.RadioCheck, normalized, 0.97);
        if (pit && need) return Result(IntentKind.PitNeed, normalized, 0.96);

        if (finish && (fuel || litres))
        {
            if (need || addFuel || litres)
                return Result(IntentKind.FuelToAdd, normalized, 0.97);
            return Result(IntentKind.FuelToFinish, normalized, 0.96);
        }

        if (addFuel && (fuel || questionWord || pit)) return Result(IntentKind.FuelToAdd, normalized, 0.96);
        if (tank && (fuel || questionWord)) return Result(IntentKind.FuelCapacity, normalized, 0.93);
        if (fuel && laps) return Result(IntentKind.FuelLapsRemaining, normalized, 0.95);
        if (fuel && consumption) return Result(IntentKind.FuelConsumption, normalized, 0.93);
        if (fuel) return Result(IntentKind.FuelLevel, normalized, 0.84);

        if (battery) return Result(IntentKind.BatteryLevel, normalized, 0.94);
        if (abs) return Result(IntentKind.AbsSetting, normalized, 0.94);
        if (traction) return Result(IntentKind.TractionControlSetting, normalized, 0.94);

        if (classPosition) return Result(IntentKind.ClassPosition, normalized, 0.92);

        if (accident && ahead) return Result(IntentKind.IncidentAhead, normalized, 0.94);
        if (accident && behind) return Result(IntentKind.IncidentBehind, normalized, 0.94);

        if (speed && ahead) return Result(IntentKind.CarAheadSpeed, normalized, 0.93);
        if (speed && behind) return Result(IntentKind.CarBehindSpeed, normalized, 0.93);

        if (ahead && (gap || questionWord || distanceToCar || time || carWord))
            return Result(IntentKind.GapAhead, normalized, 0.90);
        if (behind && (gap || questionWord || distanceToCar || time || carWord))
            return Result(IntentKind.GapBehind, normalized, 0.90);

        if (cars) return Result(IntentKind.CarsCount, normalized, 0.89);
        if (carClass) return Result(IntentKind.CarClass, normalized, 0.90);
        if (position) return Result(IntentKind.Position, normalized, 0.86);
        if (incident) return Result(IntentKind.IncidentStatus, normalized, 0.92);
        if (flag) return Result(IntentKind.FlagStatus, normalized, 0.94);

        if (temperature && track) return Result(IntentKind.TrackTemperature, normalized, 0.96);
        if (temperature && air) return Result(IntentKind.AirTemperature, normalized, 0.95);
        if (track) return Result(IntentKind.TrackInfo, normalized, 0.90);

        if (isLastLapQuestion) return Result(IntentKind.LastLapStatus, normalized, 0.94);
        if (validity && last && lapLike) return Result(IntentKind.LastLapValidity, normalized, 0.94);
        if (sector && time) return Result(IntentKind.SectorTimes, normalized, 0.92);
        if (sector) return Result(IntentKind.CurrentSector, normalized, 0.89);

        if (leader && completed && lapLike)
            return Result(IntentKind.LeaderCompletedLaps, normalized, 0.95);
        if (completed && lapLike) return Result(IntentKind.CompletedLaps, normalized, 0.91);
        if (remaining && lapLike) return Result(IntentKind.LapsRemaining, normalized, 0.92);
        if ((remaining && time) || (time && Has("сколько", "долго", "конца")))
            return Result(IntentKind.TimeRemaining, normalized, 0.90);
        if (average && (lapLike || time)) return Result(IntentKind.AverageLap, normalized, 0.90);
        if (best && lapLike) return Result(IntentKind.BestLap, normalized, 0.94);
        if (last && lapLike) return Result(IntentKind.LastLap, normalized, 0.91);
        if ((time && lapLike && !remaining) || (current && lapLike && time))
            return Result(IntentKind.CurrentLap, normalized, 0.90);
        if ((current && lapLike) || (lapLike && questionWord && !time && !last && !best && !average && !remaining))
            return Result(IntentKind.CurrentLapNumber, normalized, 0.88);

        // Session information must not steal questions such as
        // "лучший круг за всё время заезда".
        if (session && !lapLike && !best && !last && !time)
            return Result(IntentKind.SessionInfo, normalized, 0.86);

        if (tyre && pressure) return Result(IntentKind.TyrePressures, normalized, 0.94);
        if (tyre && type) return Result(IntentKind.TyreType, normalized, 0.91);
        if (tyre && set) return Result(IntentKind.TyreSet, normalized, 0.91);
        if (locking || spinning || attached) return Result(IntentKind.WheelStatus, normalized, 0.90);
        if (tyre && temperature) return Result(IntentKind.TyreTemperatures, normalized, 0.92);
        if (tyre && wear) return Result(IntentKind.TyreWear, normalized, 0.90);
        if (tyre && Has("что", "как")) return Result(IntentKind.TyreWear, normalized, 0.82);
        if (brake && temperature) return Result(IntentKind.BrakeTemperatures, normalized, 0.92);
        if (damage) return Result(IntentKind.Damage, normalized, 0.87);

        if (IntentPhraseCatalog.TryFuzzyMatch(normalized, out var fuzzyIntent, out _, out var similarity))
        {
            return Result(fuzzyIntent, normalized,
                Math.Clamp(0.56 + similarity * 0.25, 0.56, 0.79), isFuzzy: true);
        }

        return Result(IntentKind.Unknown, normalized, 0.0);
    }

    private static IntentResult Result(IntentKind kind, string text, double confidence, bool isFuzzy = false) =>
        new(kind, confidence, text, isFuzzy);
}
