using System.Globalization;
using System.Text.RegularExpressions;

namespace CrewChiefRUAssistant.Responses;

public sealed class RussianVoicePlanner
{
    private static readonly Regex FuelLevel = new(
        @"^Осталось (?<value>\d+(?:,\d+)?) литра топлива\.$",
        RegexOptions.Compiled);

    private static readonly Regex FuelCapacity = new(
        @"^В баке (?<fuel>\d+(?:,\d+)?) из (?<capacity>\d+(?:,\d+)?) литров, (?<percent>\d+) процентов\.$",
        RegexOptions.Compiled);

    private static readonly Regex FuelConsumption = new(
        @"^Средний расход — (?<value>\d+(?:,\d+)?) литра на круг\.$",
        RegexOptions.Compiled);

    private static readonly Regex FuelLaps = new(
        @"^Топлива хватит примерно на (?<value>\d+(?:,\d+)?) круга\.$",
        RegexOptions.Compiled);

    private static readonly Regex FuelMargin = new(
        @"^(?<yes>Да\. С запасом примерно|Нет\. До финиша не хватает примерно) (?<value>\d+(?:,\d+)?) литра\.$",
        RegexOptions.Compiled);

    private static readonly Regex FuelToAdd = new(
        @"^До финиша нужно добавить примерно (?<value>\d+(?:,\d+)?) литра\.$",
        RegexOptions.Compiled);

    private static readonly Regex PitNeed = new(
        @"^Пит-стоп по топливу (?<needed>нужен\. Не хватает примерно|пока не нужен\. Запас) (?<value>\d+(?:,\d+)?) литра\.$",
        RegexOptions.Compiled);

    private static readonly Regex Position = new(
        @"^Ты на (?<value>\d+)-й позиции(?<class> в классе)?\.$",
        RegexOptions.Compiled);

    private static readonly Regex CarClass = new(
        @"^Класс машины — (?<value>.+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex CarsCount = new(
        @"^В сессии (?<overall>\d+) машин(?:, в классе (?<class>\d+) машин)?\.$",
        RegexOptions.Compiled);

    private static readonly Regex CarsClassOnly = new(
        @"^В классе (?<class>\d+) машин\.$",
        RegexOptions.Compiled);

    private static readonly Regex LapsRemaining = new(
        @"^Осталось примерно (?<value>\d+) (?:круг|круга|кругов)\.$",
        RegexOptions.Compiled);

    private static readonly Regex CompletedLaps = new(
        @"^Пройдено (?<value>\d+) (?:круг|круга|кругов)\.$",
        RegexOptions.Compiled);

    private static readonly Regex Duration = new(
        @"^Осталось (?:(?<hours>\d+) ч )?(?:(?<minutes>\d+) мин(?: )?)?(?:(?<seconds>\d+) сек)?\.$",
        RegexOptions.Compiled);

    private static readonly Regex LapNumber = new(
        @"^Сейчас (?<value>\d+)-й круг\.$",
        RegexOptions.Compiled);

    private static readonly Regex CurrentSector = new(
        @"^Сейчас (?<value>\d+)-й сектор\.$",
        RegexOptions.Compiled);

    private static readonly Regex LapTime = new(
        @"^(?<kind>Текущий круг|Последний круг|Лучший круг|Среднее время круга) — (?:(?<minutes>\d+):)?(?<seconds>\d{1,2})\.(?<milliseconds>\d{3})\.$",
        RegexOptions.Compiled);

    private static readonly Regex SectorTimes = new(
        @"^Сектора прошлого круга: (?<values>.+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex Gap = new(
        @"^Отрыв (?<direction>впереди|сзади) — (?<value>\d+(?:,\d+)?) секунды\.$",
        RegexOptions.Compiled);

    private static readonly Regex Flag = new(
        @"^Флаг — (?<value>.+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex Incidents = new(
        @"^Инцидентов: (?<current>\d+)(?: из (?<maximum>\d+)\. Осталось (?<remaining>\d+))?\.$",
        RegexOptions.Compiled);

    private static readonly Regex Track = new(
        @"^Трасса — (?<name>.+?)(?:, длина (?<length>\d+(?:,\d+)?) километра)?\.$",
        RegexOptions.Compiled);

    private static readonly Regex TrackLengthOnly = new(
        @"^Длина трассы — (?<length>\d+(?:,\d+)?) километра\.$",
        RegexOptions.Compiled);

    private static readonly Regex EnvironmentTemperature = new(
        @"^Температура (?<kind>трассы|воздуха) — (?<value>-?\d+(?:,\d+)?) (?:градус|градуса|градусов)\.$",
        RegexOptions.Compiled);

    private static readonly Regex Session = new(
        @"^Сессия — (?<type>.+?)(?:, фаза — (?<phase>.+))?\.$",
        RegexOptions.Compiled);

    private static readonly Regex SessionPhaseOnly = new(
        @"^Фаза сессии — (?<phase>.+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex CornerMeasurements = new(
        @"^(?<kind>Температуры шин|Температуры тормозов|Давление шин|Износ шин): (?<values>.+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex CornerValue = new(
        @"^(?<label>передняя левая шина|передняя правая шина|задняя левая шина|задняя правая шина|передний левый тормоз|передний правый тормоз|задний левый тормоз|задний правый тормоз)\s+—\s+(?<value>\d+(?:,\d+)?)(?<percent>%?)(?:\s+градус(?:а|ов)?)?$",
        RegexOptions.Compiled);

    private static readonly Regex TyreType = new(
        @"^Тип шин — (?<value>.+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex TyreSet = new(
        @"^Установлен комплект шин номер (?<value>\d+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex Battery = new(
        @"^Заряд батареи — (?<value>\d+) (?:процент|процента|процентов)\.$",
        RegexOptions.Compiled);

    private static readonly Regex DriverAid = new(
        @"^(?<kind>ABS|Трекшн-контроль) — уровень (?<value>\d+)\.$",
        RegexOptions.Compiled);

    private static readonly Regex LeaderLaps = new(
        @"^(?:Лидер проехал|Ты лидер\. Пройдено) (?<value>\d+) (?:круг|круга|кругов)\.$",
        RegexOptions.Compiled);

    private static readonly Regex IncidentDistance = new(
        @"^Авария (?<direction>впереди примерно через|позади примерно в) (?<value>\d+) (?:метр|метра|метров|метре|метрах)\.$",
        RegexOptions.Compiled);

    private static readonly Regex Damage = new(
        @"^Повреждения: (?<values>.+)\.$",
        RegexOptions.Compiled);

    private static readonly IReadOnlyDictionary<string, string> TrackPhrases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["MONZA"] = "phrases/track_monza",
            ["AUTODROMO NAZIONALE MONZA"] = "phrases/track_monza",
            ["AUTODROMO NAZIONALE DI MONZA"] = "phrases/track_monza",
            ["MONZA CIRCUIT"] = "phrases/track_monza",
            ["SPA FRANCORCHAMPS"] = "phrases/track_spa",
            ["CIRCUIT DE SPA FRANCORCHAMPS"] = "phrases/track_spa",
            ["SILVERSTONE"] = "phrases/track_silverstone",
            ["SUZUKA"] = "phrases/track_suzuka",
            ["IMOLA"] = "phrases/track_imola",
            ["AUTODROMO ENZO E DINO FERRARI"] = "phrases/track_imola",
            ["NURBURGRING"] = "phrases/track_nurburgring",
            ["NÜRBURGRING"] = "phrases/track_nurburgring",
            ["LE MANS"] = "phrases/track_le_mans",
            ["CIRCUIT DE LA SARTHE"] = "phrases/track_le_mans",
            ["BATHURST"] = "phrases/track_bathurst",
            ["MOUNT PANORAMA"] = "phrases/track_bathurst"
        };

    private static readonly IReadOnlyDictionary<string, string> ClassPhrases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["HYPER CAR RACE"] = "phrases/class_hypercar",
            ["HYPERCAR"] = "phrases/class_hypercar",
            ["LMH"] = "phrases/class_lmh",
            ["LMDH"] = "phrases/class_lmdh",
            ["LMP1"] = "phrases/class_lmp1",
            ["LMP2"] = "phrases/class_lmp2",
            ["LMP3"] = "phrases/class_lmp3",
            ["LMGT3"] = "phrases/class_lmgt3",
            ["GT3"] = "phrases/class_gt3",
            ["GT4"] = "phrases/class_gt4",
            ["GTE"] = "phrases/class_gte",
            ["FORMULA 1"] = "phrases/class_formula_one",
            ["F1"] = "phrases/class_formula_one",
            ["СОФТ"] = "phrases/tyre_soft",
            ["МЕДИУМ"] = "phrases/tyre_medium",
            ["ХАРД"] = "phrases/tyre_hard",
            ["ПРОМЕЖУТОЧНЫЕ"] = "phrases/tyre_intermediate",
            ["ДОЖДЕВЫЕ"] = "phrases/tyre_wet",
            ["ПРАКТИКА"] = "phrases/session_practice",
            ["КВАЛИФИКАЦИЯ"] = "phrases/session_qualifying",
            ["ГОНКА"] = "phrases/session_race",
            ["ЗЕЛЁНАЯ"] = "phrases/phase_green",
            ["ОБРАТНЫЙ ОТСЧЁТ"] = "phrases/phase_countdown",
            ["ФОРМИРОВОЧНЫЙ КРУГ"] = "phrases/phase_formation",
            ["ФИНИШ"] = "phrases/phase_finish",
            ["ЗАВЕРШЕНА"] = "phrases/phase_finished",
            ["ГАРАЖ"] = "phrases/phase_garage"
        };

    public IReadOnlyList<string> Plan(AssistantResponse response)
    {
        if (string.Equals(response.StaticWavKey, "unknown", StringComparison.OrdinalIgnoreCase))
            return ["phrases/unknown"];

        if (string.Equals(response.StaticWavKey, "unavailable", StringComparison.OrdinalIgnoreCase))
            return ["phrases/unavailable"];

        if (!string.IsNullOrWhiteSpace(response.StaticWavKey))
            return [$"phrases/{response.StaticWavKey}"];

        var staticPhrase = response.Text switch
        {
            "Ты лидер. Машины впереди нет." => "phrases/leader",
            "Да, слышу тебя хорошо." => "phrases/radio_check_ok",
            "Топлива хватает. Добавлять не нужно." => "phrases/fuel_enough_no_add",
            "Это последний круг." => "phrases/this_is_last_lap",
            "Сейчас не последний круг." => "phrases/not_last_lap",
            "Последний круг зачётный." => "phrases/last_lap_valid",
            "Последний круг недействительный." => "phrases/last_lap_invalid",
            "Блокировок, пробуксовки и оторванных колёс нет." => "phrases/wheels_ok",
            _ => null
        };

        if (staticPhrase is not null)
            return [staticPhrase];

        if (response.Text.StartsWith("Проблемы с колёсами:", StringComparison.Ordinal))
            return ["phrases/wheels_problem"];

        var result = new List<string>();

        var match = FuelLevel.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/remaining");
            AddDecimal(result, match.Groups["value"].Value, feminine: false);
            result.Add("phrases/litres_of_fuel");
            return result;
        }

        match = FuelCapacity.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/in_tank");
            AddDecimal(result, match.Groups["fuel"].Value, feminine: false);
            result.Add("phrases/out_of");
            AddDecimal(result, match.Groups["capacity"].Value, feminine: false);
            result.Add("units/litre_5");
            result.Add("phrases/that_is");
            AddInteger(result, ParseInt(match.Groups["percent"].Value));
            result.Add("units/percent_5");
            return result;
        }

        match = FuelConsumption.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/average_consumption");
            AddDecimal(result, match.Groups["value"].Value, feminine: false);
            result.Add("phrases/litres_per_lap");
            return result;
        }

        match = FuelLaps.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/fuel_for_about");
            AddDecimal(result, match.Groups["value"].Value, feminine: false);
            result.Add("units/lap_2");
            return result;
        }

        match = FuelMargin.Match(response.Text);
        if (match.Success)
        {
            result.Add(match.Groups["yes"].Value.StartsWith("Да", StringComparison.Ordinal)
                ? "phrases/yes_margin"
                : "phrases/no_shortage");
            AddDecimal(result, match.Groups["value"].Value, feminine: false);
            result.Add("units/litre_2");
            return result;
        }

        match = FuelToAdd.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/add_to_finish");
            AddDecimal(result, match.Groups["value"].Value, feminine: false);
            result.Add("units/litre_2");
            return result;
        }

        match = PitNeed.Match(response.Text);
        if (match.Success)
        {
            result.Add(match.Groups["needed"].Value.StartsWith("нужен", StringComparison.Ordinal)
                ? "phrases/pit_needed"
                : "phrases/pit_not_needed");

            AddDecimal(result, match.Groups["value"].Value, feminine: false);
            result.Add("units/litre_2");
            return result;
        }

        match = Position.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/you_are_on");
            AddInteger(result, ParseInt(match.Groups["value"].Value));
            result.Add("phrases/position");
            if (match.Groups["class"].Success)
                result.Add("phrases/in_class");

            return result;
        }

        match = CarClass.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/car_class");
            AddClass(result, match.Groups["value"].Value);
            return result;
        }

        match = CarsCount.Match(response.Text);
        if (match.Success)
        {
            var overall = ParseInt(match.Groups["overall"].Value);
            result.Add("phrases/in_session");
            AddInteger(result, overall);
            result.Add(Plural("car", overall));

            if (match.Groups["class"].Success)
            {
                var inClass = ParseInt(match.Groups["class"].Value);
                result.Add("phrases/in_class");
                AddInteger(result, inClass);
                result.Add(Plural("car", inClass));
            }

            return result;
        }

        match = CarsClassOnly.Match(response.Text);
        if (match.Success)
        {
            var inClass = ParseInt(match.Groups["class"].Value);
            result.Add("phrases/in_class");
            AddInteger(result, inClass);
            result.Add(Plural("car", inClass));
            return result;
        }

        match = LapsRemaining.Match(response.Text);
        if (match.Success)
        {
            var laps = ParseInt(match.Groups["value"].Value);
            result.Add("phrases/remaining_about");
            AddInteger(result, laps);
            result.Add(Plural("lap", laps));
            return result;
        }

        match = CompletedLaps.Match(response.Text);
        if (match.Success)
        {
            var laps = ParseInt(match.Groups["value"].Value);
            result.Add("phrases/completed");
            AddInteger(result, laps);
            result.Add(Plural("lap", laps));
            return result;
        }

        match = Duration.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/remaining");

            if (match.Groups["hours"].Success)
            {
                var hours = ParseInt(match.Groups["hours"].Value);
                AddInteger(result, hours);
                result.Add(Plural("hour", hours));
            }

            if (match.Groups["minutes"].Success)
            {
                var minutes = ParseInt(match.Groups["minutes"].Value);
                AddFeminineInteger(result, minutes);
                result.Add(Plural("minute", minutes));
            }

            if (match.Groups["seconds"].Success)
            {
                var seconds = ParseInt(match.Groups["seconds"].Value);
                AddFeminineInteger(result, seconds);
                result.Add(Plural("second", seconds));
            }

            return result;
        }

        match = LapNumber.Match(response.Text);
        if (match.Success)
        {
            var lap = ParseInt(match.Groups["value"].Value);
            result.Add("phrases/current_lap_number");
            AddInteger(result, lap);
            result.Add(Plural("lap", lap));
            return result;
        }

        match = CurrentSector.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/current_sector");
            AddInteger(result, ParseInt(match.Groups["value"].Value));
            return result;
        }

        match = LapTime.Match(response.Text);
        if (match.Success)
        {
            result.Add(match.Groups["kind"].Value switch
            {
                "Текущий круг" => "phrases/current_lap",
                "Последний круг" => "phrases/last_lap",
                "Лучший круг" => "phrases/best_lap",
                _ => "phrases/average_lap"
            });

            AddLapTime(
                result,
                match.Groups["minutes"].Success
                    ? ParseInt(match.Groups["minutes"].Value)
                    : 0,
                ParseInt(match.Groups["seconds"].Value),
                ParseInt(match.Groups["milliseconds"].Value));

            return result;
        }

        match = SectorTimes.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/last_sectors");

            var values = match.Groups["values"].Value.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            for (var index = 0; index < values.Length; index++)
            {
                if (index > 0)
                    result.Add("phrases/pause");

                AddParsedLapTime(result, values[index]);
            }

            return result;
        }

        match = Gap.Match(response.Text);
        if (match.Success)
        {
            result.Add(match.Groups["direction"].Value == "впереди"
                ? "phrases/gap_ahead"
                : "phrases/gap_behind");
            AddDecimal(result, match.Groups["value"].Value, feminine: true);
            result.Add("units/second_2");
            return result;
        }

        match = Flag.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/flag");
            result.Add(match.Groups["value"].Value.ToLowerInvariant() switch
            {
                "зелёный" => "phrases/flag_green",
                "жёлтый" => "phrases/flag_yellow",
                "двойной жёлтый" => "phrases/flag_double_yellow",
                "синий" => "phrases/flag_blue",
                "красный" => "phrases/flag_red",
                "белый" => "phrases/flag_white",
                "чёрный" => "phrases/flag_black",
                "клетчатый" => "phrases/flag_chequered",
                _ => "phrases/unknown_flag"
            });
            return result;
        }

        match = Incidents.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/incidents");
            AddInteger(result, ParseInt(match.Groups["current"].Value));

            if (match.Groups["maximum"].Success)
            {
                result.Add("phrases/out_of");
                AddInteger(result, ParseInt(match.Groups["maximum"].Value));
                result.Add("phrases/remaining");
                AddInteger(result, ParseInt(match.Groups["remaining"].Value));
            }

            return result;
        }

        match = Track.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/track");
            AddTrackName(result, match.Groups["name"].Value);

            if (match.Groups["length"].Success)
            {
                result.Add("phrases/length");
                AddDecimal(result, match.Groups["length"].Value, feminine: true);
                result.Add("units/kilometre_2");
            }

            return result;
        }

        match = TrackLengthOnly.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/track_length");
            AddDecimal(result, match.Groups["length"].Value, feminine: true);
            result.Add("units/kilometre_2");
            return result;
        }

        match = EnvironmentTemperature.Match(response.Text);
        if (match.Success)
        {
            result.Add(match.Groups["kind"].Value == "трассы"
                ? "phrases/track_temperature"
                : "phrases/air_temperature");

            var value = match.Groups["value"].Value;
            if (value.StartsWith("-", StringComparison.Ordinal))
            {
                result.Add("phrases/minus");
                value = value[1..];
            }

            AddDecimal(result, value, feminine: false);
            result.Add(Plural(
                "degree",
                (int)Math.Round(ParseDecimal(value))));
            return result;
        }

        match = Session.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/session");
            AddClass(result, match.Groups["type"].Value);

            if (match.Groups["phase"].Success)
            {
                result.Add("phrases/phase");
                AddClass(result, match.Groups["phase"].Value);
            }

            return result;
        }

        match = SessionPhaseOnly.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/session_phase");
            AddClass(result, match.Groups["phase"].Value);
            return result;
        }

        match = CornerMeasurements.Match(response.Text);
        if (match.Success)
        {
            var kind = match.Groups["kind"].Value;

            result.Add(kind switch
            {
                "Температуры шин" => "phrases/tyre_temperatures",
                "Температуры тормозов" => "phrases/brake_temperatures",
                "Давление шин" => "phrases/tyre_pressures",
                _ => "phrases/tyre_wear"
            });

            var pieces = match.Groups["values"].Value.Split(
                ';',
                StringSplitOptions.TrimEntries |
                StringSplitOptions.RemoveEmptyEntries);

            var spokenCount = 0;

            foreach (var piece in pieces)
            {
                var corner = CornerValue.Match(piece);

                if (!corner.Success)
                    continue;

                if (spokenCount > 0)
                    result.Add("phrases/pause");

                result.Add(CornerPhrase(
                    corner.Groups["label"].Value));

                var value = corner.Groups["value"].Value;

                if (kind == "Давление шин")
                {
                    AddDecimal(
                        result,
                        value,
                        feminine: false);
                }
                else
                {
                    var integer = ParseInt(
                        value.Split(',')[0]);

                    AddInteger(
                        result,
                        integer);

                    result.Add(kind == "Износ шин"
                        ? Plural("percent", integer)
                        : Plural("degree", integer));
                }

                spokenCount++;
            }

            return result;
        }

        match = TyreType.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/tyre_type");
            AddClass(result, match.Groups["value"].Value);
            return result;
        }

        match = TyreSet.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/tyre_set");
            AddInteger(result, ParseInt(match.Groups["value"].Value));
            return result;
        }

        match = Battery.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/battery_charge");
            var value = ParseInt(match.Groups["value"].Value);
            AddInteger(result, value);
            result.Add(Plural("percent", value));
            return result;
        }

        match = DriverAid.Match(response.Text);
        if (match.Success)
        {
            result.Add(match.Groups["kind"].Value == "ABS"
                ? "phrases/abs_level"
                : "phrases/tc_level");
            AddInteger(result, ParseInt(match.Groups["value"].Value));
            return result;
        }

        match = LeaderLaps.Match(response.Text);
        if (match.Success)
        {
            result.Add(response.Text.StartsWith("Ты лидер", StringComparison.Ordinal)
                ? "phrases/leader_completed_self"
                : "phrases/leader_completed");
            var value = ParseInt(match.Groups["value"].Value);
            AddInteger(result, value);
            result.Add(Plural("lap", value));
            return result;
        }

        match = IncidentDistance.Match(response.Text);
        if (match.Success)
        {
            result.Add(match.Groups["direction"].Value.StartsWith("впереди", StringComparison.Ordinal)
                ? "phrases/incident_ahead"
                : "phrases/incident_behind");
            var value = ParseInt(match.Groups["value"].Value);
            AddInteger(result, value);
            result.Add(Plural("metre", value));
            return result;
        }

        match = Damage.Match(response.Text);
        if (match.Success)
        {
            result.Add("phrases/damage");

            var pieces = match.Groups["values"].Value.Split(
                ',',
                StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

            foreach (var piece in pieces)
            {
                if (piece.StartsWith("двигатель", StringComparison.OrdinalIgnoreCase))
                    result.Add("phrases/engine");
                else if (piece.StartsWith("аэродинамика", StringComparison.OrdinalIgnoreCase))
                    result.Add("phrases/aero");
                else if (piece.StartsWith("трансмиссия", StringComparison.OrdinalIgnoreCase))
                    result.Add("phrases/transmission");
                else if (piece.StartsWith("подвеска", StringComparison.OrdinalIgnoreCase))
                    result.Add("phrases/suspension");
                else
                    continue;

                var lower = piece.ToLowerInvariant();
                if (lower.Contains("без повреждений"))
                    result.Add("phrases/damage_none");
                else if (lower.Contains("незначительные"))
                    result.Add("phrases/damage_trivial");
                else if (lower.Contains("лёгкие") || lower.Contains("легкие"))
                    result.Add("phrases/damage_minor");
                else if (lower.Contains("серьёзные") || lower.Contains("серьезные"))
                    result.Add("phrases/damage_major");
                else if (lower.Contains("критические"))
                    result.Add("phrases/damage_destroyed");
                else
                {
                    var number = Regex.Match(piece, @"\d+");
                    if (number.Success)
                    {
                        var percent = ParseInt(number.Value);
                        AddInteger(result, percent);
                        result.Add(Plural("percent", percent));
                    }
                }
            }

            return result;
        }

        return ["phrases/unknown"];
    }

    private static string CornerPhrase(
        string label) =>
        label switch
        {
            "передняя левая шина" =>
                "phrases/front_left_tyre",

            "передняя правая шина" =>
                "phrases/front_right_tyre",

            "задняя левая шина" =>
                "phrases/rear_left_tyre",

            "задняя правая шина" =>
                "phrases/rear_right_tyre",

            "передний левый тормоз" =>
                "phrases/front_left_brake",

            "передний правый тормоз" =>
                "phrases/front_right_brake",

            "задний левый тормоз" =>
                "phrases/rear_left_brake",

            "задний правый тормоз" =>
                "phrases/rear_right_brake",

            _ => "phrases/pause"
        };

    private static void AddParsedLapTime(
        ICollection<string> result,
        string value)
    {
        var match = Regex.Match(
            value,
            @"^(?:(?<minutes>\d+):)?(?<seconds>\d{1,2})\.(?<milliseconds>\d{3})$");

        if (!match.Success)
            return;

        AddLapTime(
            result,
            match.Groups["minutes"].Success
                ? ParseInt(match.Groups["minutes"].Value)
                : 0,
            ParseInt(match.Groups["seconds"].Value),
            ParseInt(match.Groups["milliseconds"].Value));
    }

    private static void AddLapTime(
        ICollection<string> result,
        int minutes,
        int seconds,
        int milliseconds)
    {
        if (minutes > 0)
        {
            AddFeminineInteger(result, minutes);
            result.Add(Plural("minute", minutes));
        }

        AddFeminineInteger(result, seconds);
        result.Add("phrases/whole");
        AddInteger(result, milliseconds);
        result.Add("phrases/thousandths");
        result.Add("units/second_2");
    }

    private static void AddIntegerList(
        ICollection<string> result,
        string value)
    {
        var values = Regex.Matches(value, @"-?\d+")
            .Select(item => ParseInt(item.Value))
            .ToArray();

        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
                result.Add("phrases/pause");

            AddInteger(result, Math.Abs(values[index]));
        }
    }

    private static void AddDecimalList(
        ICollection<string> result,
        string value)
    {
        var values = value.Split(
            ';',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < values.Length; index++)
        {
            if (index > 0)
                result.Add("phrases/pause");

            AddDecimal(result, values[index], feminine: false);
        }
    }

    private static void AddDecimal(
        ICollection<string> result,
        string value,
        bool feminine)
    {
        var parts = value.Split(',');
        var integer = ParseInt(parts[0]);

        if (feminine)
            AddFeminineInteger(result, integer);
        else
            AddInteger(result, integer);

        if (parts.Length == 1)
            return;

        result.Add("phrases/whole");

        var fractionText = parts[1].TrimEnd('0');
        if (fractionText.Length == 0)
            return;

        AddInteger(result, ParseInt(fractionText));
        result.Add(fractionText.Length switch
        {
            1 => "phrases/tenths",
            2 => "phrases/hundredths",
            _ => "phrases/thousandths"
        });
    }

    private static void AddTrackName(
        ICollection<string> result,
        string value)
    {
        var normalized = NormalizeLookupValue(value);

        if (TrackPhrases.TryGetValue(normalized, out var phrase))
        {
            result.Add(phrase);
            return;
        }

        AddClass(result, value);
    }

    private static string NormalizeLookupValue(string value) =>
        Regex.Replace(
            value.Replace('_', ' ').Trim().ToUpperInvariant(),
            @"[^\p{L}\p{N}]+",
            " ").Trim();

    private static double ParseDecimal(string value) =>
        double.TryParse(
            value.Replace(',', '.'),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;

    private static void AddClass(
        ICollection<string> result,
        string value)
    {
        var normalized = NormalizeLookupValue(value);

        if (ClassPhrases.TryGetValue(normalized, out var phrase))
        {
            result.Add(phrase);
            return;
        }

        foreach (var character in normalized)
        {
            if (character is >= 'A' and <= 'Z')
            {
                result.Add($"letters/{char.ToLowerInvariant(character)}");
            }
            else if (char.IsDigit(character))
            {
                result.Add($"digits/{character}");
            }
        }
    }

    private static void AddInteger(
        ICollection<string> result,
        int value)
    {
        value = Math.Abs(value);

        if (value <= 999)
        {
            result.Add($"numbers/{value}");
            return;
        }

        foreach (var digit in value.ToString(CultureInfo.InvariantCulture))
        {
            result.Add($"digits/{digit}");
        }
    }

    private static void AddFeminineInteger(
        ICollection<string> result,
        int value)
    {
        value = Math.Abs(value);

        if (value <= 99)
        {
            result.Add($"numbers_f/{value}");
            return;
        }

        AddInteger(result, value);
    }

    private static string Plural(string unit, int value)
    {
        var absolute = Math.Abs(value);
        var lastTwo = absolute % 100;
        var last = absolute % 10;
        var suffix = lastTwo is >= 11 and <= 14
            ? "5"
            : last == 1
                ? "1"
                : last is >= 2 and <= 4
                    ? "2"
                    : "5";

        return $"units/{unit}_{suffix}";
    }

    private static int ParseInt(string value) =>
        int.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : 0;
}
