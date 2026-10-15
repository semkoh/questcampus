import unicodedata


def normalize_answer(answer: str | None) -> str:
    """
    Comparaison tolérante : casse, accents, espaces superflus et ponctuation ignorés.
    "  Hôtel-de-Ville ! " -> "hotel de ville"
    """
    if not answer or not answer.strip():
        return ""
    decomposed = unicodedata.normalize("NFD", answer.strip().lower())
    words, current = [], []
    for ch in decomposed:
        if unicodedata.category(ch) == "Mn":  # accent détaché de sa lettre (é = e + ´)
            continue
        if ch.isalnum():
            current.append(ch)
        elif current:  # espace, tiret, apostrophe, ponctuation => séparateur
            words.append("".join(current))
            current = []
    if current:
        words.append("".join(current))
    return " ".join(words)


def answers_match(expected: str, submitted: str | None) -> bool:
    normalized = normalize_answer(expected)
    return bool(normalized) and normalized == normalize_answer(submitted)
