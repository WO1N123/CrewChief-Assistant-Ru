from __future__ import annotations

import argparse
import json
import math
import os
import sys
import wave
from pathlib import Path

try:
    import numpy as np
except Exception as exc:
    print()
    print("ERROR: NumPy is not installed.")
    print("Install the voice generator dependencies:")
    print("  py -3 -m pip install silero-tts numpy")
    print()
    print(f"Details: {exc}")
    raise SystemExit(2)


TARGET_SAMPLE_RATE = 22050
RADIO_EFFECT_VERSION = 1

ONES_M = [
    "ноль", "один", "два", "три", "четыре",
    "пять", "шесть", "семь", "восемь", "девять",
    "десять", "одиннадцать", "двенадцать", "тринадцать",
    "четырнадцать", "пятнадцать", "шестнадцать",
    "семнадцать", "восемнадцать", "девятнадцать",
]

ONES_F = [
    "ноль", "одна", "две", "три", "четыре",
    "пять", "шесть", "семь", "восемь", "девять",
    "десять", "одиннадцать", "двенадцать", "тринадцать",
    "четырнадцать", "пятнадцать", "шестнадцать",
    "семнадцать", "восемнадцать", "девятнадцать",
]

TENS = [
    "", "", "двадцать", "тридцать", "сорок",
    "пятьдесят", "шестьдесят", "семьдесят",
    "восемьдесят", "девяносто",
]

HUNDREDS = [
    "", "сто", "двести", "триста", "четыреста",
    "пятьсот", "шестьсот", "семьсот", "восемьсот",
    "девятьсот",
]


def number_words(value: int, feminine: bool = False) -> str:
    if not 0 <= value <= 999:
        raise ValueError(value)

    parts: list[str] = []

    if value >= 100:
        parts.append(HUNDREDS[value // 100])
        value %= 100

    ones = ONES_F if feminine else ONES_M

    if value < 20:
        if value > 0 or not parts:
            parts.append(ones[value])
    else:
        parts.append(TENS[value // 10])
        if value % 10:
            parts.append(ones[value % 10])

    return " ".join(parts)


PHRASES = {
    "unknown": "Я не понял вопрос.",
    "unavailable": "Сейчас эти данные недоступны.",
    "leader": "Ты лидер. Машины впереди нет.",
    "radio_check_ok": "Да, слышу тебя хорошо.",
    "remaining": "Осталось",
    "remaining_about": "Осталось примерно",
    "litres_of_fuel": "литра топлива",
    "average_consumption": "Средний расход",
    "litres_per_lap": "литра на круг",
    "fuel_for_about": "Топлива хватит примерно на",
    "yes_margin": "Да, с запасом примерно",
    "no_shortage": "Нет. До финиша не хватает примерно",
    "you_are_on": "Ты на",
    "position": "позиции",
    "in_class": "в классе",
    "car_class": "Класс машины",
    "current_lap": "Текущий круг",
    "last_lap": "Последний круг",
    "best_lap": "Лучший круг",
    "average_lap": "Среднее время круга",
    "gap_ahead": "Отрыв впереди",
    "gap_behind": "Отрыв сзади",
    "tyre_temperatures": "Температуры шин",
    "brake_temperatures": "Температуры тормозов",
    "front_left_tyre": "передняя левая шина",
    "front_right_tyre": "передняя правая шина",
    "rear_left_tyre": "задняя левая шина",
    "rear_right_tyre": "задняя правая шина",
    "front_left_brake": "передний левый тормоз",
    "front_right_brake": "передний правый тормоз",
    "rear_left_brake": "задний левый тормоз",
    "rear_right_brake": "задний правый тормоз",
    "tyre_remaining": "Остаток шин",
    "damage": "Повреждения",
    "engine": "двигатель",
    "aero": "аэродинамика",
    "suspension": "подвеска",
    "transmission": "трансмиссия",
    "damage_none": "без повреждений",
    "damage_trivial": "незначительные повреждения",
    "damage_minor": "лёгкие повреждения",
    "damage_major": "серьёзные повреждения",
    "damage_destroyed": "критические повреждения",
    "whole": "целых",
    "tenths": "десятых",
    "hundredths": "сотых",
    "thousandths": "тысячных",
    "pause": "и",
    "class_hypercar": "гиперкар",
    "class_lmh": "эл эм эйч",
    "class_lmdh": "эл эм ди эйч",
    "class_lmp1": "эл эм пи один",
    "class_lmp2": "эл эм пи два",
    "class_lmp3": "эл эм пи три",
    "class_lmgt3": "эл эм джи ти три",
    "class_gt3": "джи ти три",
    "class_gt4": "джи ти четыре",
    "class_gte": "джи ти и",
    "class_formula_one": "формула один",

    "in_tank": "В баке",
    "out_of": "из",
    "that_is": "это",
    "add_to_finish": "До финиша нужно добавить примерно",
    "fuel_enough_no_add": "Топлива хватает. Добавлять не нужно.",
    "pit_needed": "Пит-стоп по топливу нужен. Не хватает примерно",
    "pit_not_needed": "Пит-стоп по топливу пока не нужен. Запас",
    "in_session": "В сессии",
    "completed": "Пройдено",
    "current_lap_number": "Сейчас круг номер",
    "current_sector": "Сейчас сектор номер",
    "last_sectors": "Сектора прошлого круга",
    "flag": "Флаг",
    "flag_green": "зелёный",
    "flag_yellow": "жёлтый",
    "flag_double_yellow": "двойной жёлтый",
    "flag_blue": "синий",
    "flag_red": "красный",
    "flag_white": "белый",
    "flag_black": "чёрный",
    "flag_chequered": "клетчатый",
    "unknown_flag": "не определён",
    "incidents": "Инцидентов",
    "track": "Трасса",
    "track_length": "Длина трассы",
    "track_temperature": "Температура трассы",
    "air_temperature": "Температура воздуха",
    "battery_charge": "Заряд батареи",
    "leader_completed": "Лидер проехал",
    "leader_completed_self": "Ты лидер. Пройдено",
    "incident_ahead": "Авария впереди примерно через",
    "incident_behind": "Авария позади примерно в",
    "incident_is_behind": "Ближайшая авария уже позади.",
    "incident_is_ahead": "Ближайшая авария находится впереди.",
    "abs_level": "АБС, уровень",
    "tc_level": "Трекшн-контроль, уровень",
    "minus": "минус",
    "track_monza": "Автодромо Национале Монца",
    "track_spa": "Спа Франкоршам",
    "track_silverstone": "Сильверстоун",
    "track_suzuka": "Судзука",
    "track_imola": "Имола",
    "track_nurburgring": "Нюрбургринг",
    "track_le_mans": "Ле Ман",
    "track_bathurst": "Батерст",
    "length": "длина",
    "session": "Сессия",
    "phase": "фаза",
    "session_phase": "Фаза сессии",
    "this_is_last_lap": "Это последний круг.",
    "not_last_lap": "Сейчас не последний круг.",
    "last_lap_valid": "Последний круг зачётный.",
    "last_lap_invalid": "Последний круг недействительный.",
    "tyre_pressures": "Давление шин",
    "tyre_wear": "Износ шин",
    "tyre_type": "Тип шин",
    "tyre_set": "Установлен комплект шин номер",
    "wheels_ok": "Блокировок, пробуксовки и оторванных колёс нет.",
    "wheels_problem": "Обнаружены проблемы с колёсами.",
    "tyre_soft": "софт",
    "tyre_medium": "медиум",
    "tyre_hard": "хард",
    "tyre_intermediate": "промежуточные",
    "tyre_wet": "дождевые",
    "session_practice": "практика",
    "session_qualifying": "квалификация",
    "session_race": "гонка",
    "phase_green": "зелёная",
    "phase_countdown": "обратный отсчёт",
    "phase_formation": "формировочный круг",
    "phase_finish": "финиш",
    "phase_finished": "завершена",
    "phase_garage": "гараж",
}

UNITS = {
    "lap_1": "круг",
    "lap_2": "круга",
    "lap_5": "кругов",
    "hour_1": "час",
    "hour_2": "часа",
    "hour_5": "часов",
    "minute_1": "минута",
    "minute_2": "минуты",
    "minute_5": "минут",
    "second_1": "секунда",
    "second_2": "секунды",
    "second_5": "секунд",
    "litre_1": "литр",
    "litre_2": "литра",
    "litre_5": "литров",
    "percent_1": "процент",
    "percent_2": "процента",
    "percent_5": "процентов",
    "degree_1": "градус",
    "degree_2": "градуса",
    "degree_5": "градусов",

    "car_1": "машина",
    "car_2": "машины",
    "car_5": "машин",
    "kilometre_1": "километр",
    "kilometre_2": "километра",
    "kilometre_5": "километров",
    "metre_1": "метр",
    "metre_2": "метра",
    "metre_5": "метров",
}

LETTERS = {
    "a": "эй", "b": "би", "c": "си", "d": "ди", "e": "и",
    "f": "эф", "g": "джи", "h": "эйч", "i": "ай", "j": "джей",
    "k": "кей", "l": "эл", "m": "эм", "n": "эн", "o": "оу",
    "p": "пи", "q": "кью", "r": "ар", "s": "эс", "t": "ти",
    "u": "ю", "v": "ви", "w": "дабл-ю", "x": "икс",
    "y": "уай", "z": "зэд",
}


def read_wave(path: Path) -> tuple[np.ndarray, int]:
    with wave.open(str(path), "rb") as reader:
        channels = reader.getnchannels()
        sample_width = reader.getsampwidth()
        sample_rate = reader.getframerate()
        frames = reader.readframes(reader.getnframes())

    if sample_width != 2:
        raise RuntimeError(f"Only PCM 16-bit WAV is supported: {path}")

    data = np.frombuffer(frames, dtype="<i2").astype(np.float32) / 32768.0

    if channels > 1:
        data = data.reshape(-1, channels).mean(axis=1)

    return data, sample_rate


def write_wave(path: Path, data: np.ndarray, sample_rate: int) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)

    data = np.asarray(data, dtype=np.float32)
    data = np.clip(data, -0.999, 0.999)
    pcm = (data * 32767.0).astype("<i2")

    with wave.open(str(path), "wb") as writer:
        writer.setnchannels(1)
        writer.setsampwidth(2)
        writer.setframerate(sample_rate)
        writer.writeframes(pcm.tobytes())


def resample_linear(
    data: np.ndarray,
    source_rate: int,
    target_rate: int,
) -> np.ndarray:
    if source_rate == target_rate or data.size == 0:
        return data.astype(np.float32, copy=True)

    target_size = max(1, int(round(data.size * target_rate / source_rate)))

    source_positions = np.linspace(
        0.0,
        1.0,
        num=data.size,
        endpoint=True,
        dtype=np.float64,
    )

    target_positions = np.linspace(
        0.0,
        1.0,
        num=target_size,
        endpoint=True,
        dtype=np.float64,
    )

    return np.interp(
        target_positions,
        source_positions,
        data,
    ).astype(np.float32)


def trim_voice(
    data: np.ndarray,
    sample_rate: int,
) -> np.ndarray:
    if data.size == 0:
        return data

    active = np.flatnonzero(np.abs(data) > 0.0035)
    if active.size == 0:
        return data

    padding = int(sample_rate * 0.025)
    start = max(0, int(active[0]) - padding)
    end = min(data.size, int(active[-1]) + padding)
    return data[start:end]


def smoothstep(
    values: np.ndarray,
    edge0: float,
    edge1: float,
) -> np.ndarray:
    if edge1 <= edge0:
        return (values >= edge1).astype(np.float64)

    x = np.clip((values - edge0) / (edge1 - edge0), 0.0, 1.0)
    return x * x * (3.0 - 2.0 * x)


def spectral_radio_filter(
    data: np.ndarray,
    sample_rate: int,
) -> np.ndarray:
    if data.size < 8:
        return data.astype(np.float32, copy=True)

    fft_size = 1 << (data.size - 1).bit_length()
    spectrum = np.fft.rfft(data, n=fft_size)
    frequencies = np.fft.rfftfreq(fft_size, 1.0 / sample_rate)

    # Bass removal, broad speech presence and a soft high-frequency rolloff.
    high_pass = smoothstep(frequencies, 230.0, 480.0)
    low_pass = 1.0 - smoothstep(frequencies, 6100.0, 7600.0)

    presence = (
        1.0
        + 0.32 * np.exp(-0.5 * ((frequencies - 2450.0) / 1050.0) ** 2)
        + 0.10 * np.exp(-0.5 * ((frequencies - 4300.0) / 1150.0) ** 2)
    )

    response = high_pass * low_pass * presence
    filtered = np.fft.irfft(spectrum * response, n=fft_size)[: data.size]

    return filtered.astype(np.float32)


def radio_noise(
    size: int,
    sample_rate: int,
    seed: int,
) -> np.ndarray:
    if size <= 0:
        return np.zeros(0, dtype=np.float32)

    rng = np.random.default_rng(seed)
    noise = rng.standard_normal(size).astype(np.float32)
    noise = spectral_radio_filter(noise, sample_rate)

    rms = float(np.sqrt(np.mean(noise * noise))) if noise.size else 0.0
    if rms > 1e-9:
        noise = noise / rms

    return noise.astype(np.float32)


def apply_radio_effect(
    clean: np.ndarray,
    source_rate: int,
    seed: int,
) -> np.ndarray:
    data = resample_linear(clean, source_rate, TARGET_SAMPLE_RATE)
    data = trim_voice(data, TARGET_SAMPLE_RATE)

    if data.size == 0:
        return data

    data = spectral_radio_filter(data, TARGET_SAMPLE_RATE)

    # Gentle automatic gain before compression.
    rms = float(np.sqrt(np.mean(data * data)))
    if rms > 1e-7:
        target_rms = 0.165
        data = data * min(6.0, target_rms / rms)

    # Soft compression / transmitter saturation.
    drive = 1.85
    data = np.tanh(data * drive) / np.tanh(drive)

    # Subtle radio hiss mixed only under the voice.
    noise = radio_noise(data.size, TARGET_SAMPLE_RATE, seed)
    envelope = np.minimum(
        1.0,
        np.abs(data) * 8.0 + 0.10,
    ).astype(np.float32)
    data = data + noise * envelope * 0.0065

    # Slight amplitude flutter, typical of a radio link.
    positions = np.arange(data.size, dtype=np.float32) / TARGET_SAMPLE_RATE
    flutter = (
        1.0
        + 0.008 * np.sin(2.0 * math.pi * 23.0 * positions)
        + 0.004 * np.sin(2.0 * math.pi * 41.0 * positions)
    )
    data = data * flutter

    peak = float(np.max(np.abs(data)))
    if peak > 1e-7:
        data = data * min(1.0, 0.965 / peak)

    fade = min(int(TARGET_SAMPLE_RATE * 0.004), data.size // 2)
    if fade > 1:
        ramp = np.linspace(0.0, 1.0, fade, dtype=np.float32)
        data[:fade] *= ramp
        data[-fade:] *= ramp[::-1]

    return data.astype(np.float32)


def make_squelch(
    opening: bool,
    seed: int,
) -> np.ndarray:
    duration = 0.090 if opening else 0.125
    size = int(TARGET_SAMPLE_RATE * duration)
    rng = np.random.default_rng(seed)

    noise = radio_noise(size, TARGET_SAMPLE_RATE, seed)

    t = np.arange(size, dtype=np.float32) / TARGET_SAMPLE_RATE
    click = np.sin(
        2.0 * math.pi * (1750.0 if opening else 1100.0) * t
    ).astype(np.float32)

    click *= np.exp(
        -t * (72.0 if opening else 46.0)
    ).astype(np.float32)

    if opening:
        envelope = np.exp(-t * 28.0).astype(np.float32)
        burst = noise * envelope * 0.20 + click * 0.16
    else:
        reverse_t = duration - t
        envelope = np.exp(-reverse_t * 35.0).astype(np.float32)
        burst = noise * envelope * 0.18
        burst += click * 0.10

        tail_start = int(size * 0.48)
        if tail_start < size:
            tail = np.linspace(
                1.0,
                0.0,
                size - tail_start,
                dtype=np.float32,
            )
            burst[tail_start:] *= tail

    burst = spectral_radio_filter(burst, TARGET_SAMPLE_RATE)
    peak = float(np.max(np.abs(burst)))
    if peak > 1e-7:
        burst = burst * (0.34 / peak)

    return burst.astype(np.float32)


def generate_clean_wave(
    tts,
    text: str,
    output: Path,
) -> None:
    output.parent.mkdir(parents=True, exist_ok=True)
    temporary = output.with_suffix(".temporary.wav")

    try:
        tts.tts(text, str(temporary))
        data, sample_rate = read_wave(temporary)
        data = trim_voice(data, sample_rate)

        peak = float(np.max(np.abs(data))) if data.size else 0.0
        if peak > 1e-7:
            data = data * min(1.0, 0.92 / peak)

        write_wave(output, data, sample_rate)
    finally:
        temporary.unlink(missing_ok=True)


def load_tts(speaker: str):
    try:
        from silero_tts.silero_tts import SileroTTS
    except Exception as exc:
        print()
        print("ERROR: Silero TTS is not available.")
        print("Install it with:")
        print("  py -3 -m pip install silero-tts")
        print()
        print(f"Details: {exc}")
        raise SystemExit(2)

    print(f"Loading Silero v5_ru / {speaker}...")
    print("The TTS model is used only for missing clean fragments.")
    print()

    return SileroTTS(
        model_id="v5_ru",
        language="ru",
        speaker=speaker,
        sample_rate=24000,
        device="cpu",
    )


def build_voice_bank(
    audio_root: Path,
    speaker: str,
) -> int:
    clean_bank = audio_root / f"voice_bank_{speaker}_v1"
    destination = audio_root / f"voice_bank_{speaker}_radio_v1"

    clean_bank.mkdir(parents=True, exist_ok=True)
    destination.mkdir(parents=True, exist_ok=True)

    entries: list[tuple[Path, str]] = []

    for value in range(1000):
        entries.append((Path("numbers") / f"{value}.wav", number_words(value)))

    for value in range(100):
        entries.append(
            (Path("numbers_f") / f"{value}.wav", number_words(value, feminine=True))
        )

    for value in range(10):
        entries.append((Path("digits") / f"{value}.wav", ONES_M[value]))

    speaker_phrases = dict(PHRASES)

    if speaker == "xenia":
        speaker_phrases["unknown"] = "Я не поняла вопрос."
        speaker_phrases["preview"] = (
            "Я готова помочь. "
            "Спроси меня о топливе, позиции или состоянии машины."
        )
    else:
        speaker_phrases["unknown"] = "Я не понял вопрос."
        speaker_phrases["preview"] = (
            "Я готов помочь. "
            "Спроси меня о топливе, позиции или состоянии машины."
        )

    for name, text in speaker_phrases.items():
        entries.append((Path("phrases") / f"{name}.wav", text))

    for name, text in UNITS.items():
        entries.append((Path("units") / f"{name}.wav", text))

    for name, text in LETTERS.items():
        entries.append((Path("letters") / f"{name}.wav", text))

    total = len(entries)
    converted = 0
    generated = 0
    skipped = 0
    tts = None

    print(f"Creating the Silero {speaker} radio voice bank.")
    print(f"Clean source: {clean_bank}")
    print(f"Radio output: {destination}")
    print()

    for index, (relative_path, text) in enumerate(entries, start=1):
        clean_path = clean_bank / relative_path
        radio_path = destination / relative_path
        radio_path.parent.mkdir(parents=True, exist_ok=True)

        if radio_path.exists() and radio_path.stat().st_size > 512:
            skipped += 1
        else:
            if not clean_path.exists() or clean_path.stat().st_size <= 512:
                if tts is None:
                    tts = load_tts(speaker)

                generate_clean_wave(tts, text, clean_path)
                generated += 1

            clean_data, source_rate = read_wave(clean_path)
            processed = apply_radio_effect(
                clean_data,
                source_rate,
                seed=0xCC0000 + index,
            )
            write_wave(radio_path, processed, TARGET_SAMPLE_RATE)
            converted += 1

        if index == 1 or index % 25 == 0 or index == total:
            print(
                f"[{index:4d}/{total}] "
                f"radio_created={converted}, "
                f"tts_created={generated}, "
                f"already_present={skipped}"
            )

    radio_dir = destination / "radio"
    write_wave(
        radio_dir / "open.wav",
        make_squelch(opening=True, seed=0xCC1001),
        TARGET_SAMPLE_RATE,
    )
    write_wave(
        radio_dir / "close.wav",
        make_squelch(opening=False, seed=0xCC1002),
        TARGET_SAMPLE_RATE,
    )

    ready = {
        "version": 3,
        "speaker": speaker,
        "model": "v5_ru",
        "sample_rate": TARGET_SAMPLE_RATE,
        "channels": 1,
        "bits_per_sample": 16,
        "clips": total,
        "radio_effect": {
            "version": RADIO_EFFECT_VERSION,
            "profile": "CrewChief-style narrow radio",
            "high_pass_transition_hz": [230, 480],
            "low_pass_transition_hz": [6100, 7600],
            "presence_hz": 2450,
            "squelch": True,
        },
    }

    (destination / "READY.json").write_text(
        json.dumps(ready, ensure_ascii=False, indent=2),
        encoding="utf-8",
    )

    print()
    print("Radio voice bank is ready:")
    print(destination)
    print()
    print("Format: mono, PCM 16-bit, 22050 Hz.")
    print("Restart CrewChief RU Assistant and keep voice output enabled.")
    return 0


def parse_arguments() -> argparse.Namespace:
    parser = argparse.ArgumentParser(
        description="Generate CrewChief RU Assistant radio voice banks."
    )

    parser.add_argument(
        "--speaker",
        choices=("eugene", "xenia", "all"),
        default="all",
        help="Voice to generate. Default: all.",
    )

    return parser.parse_args()


def main() -> int:
    local_app_data = os.environ.get("LOCALAPPDATA")
    if not local_app_data:
        print("ERROR: LOCALAPPDATA is not defined.")
        return 3

    audio_root = (
        Path(local_app_data)
        / "CrewChiefRUAssistant"
        / "audio"
    )

    arguments = parse_arguments()
    speakers = (
        ("eugene", "xenia")
        if arguments.speaker == "all"
        else (arguments.speaker,)
    )

    for speaker in speakers:
        result = build_voice_bank(audio_root, speaker)
        if result != 0:
            return result

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
