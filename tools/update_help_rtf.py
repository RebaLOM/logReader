#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Patch existing Help.rtf by inserting a short note (cp1251-escaped) before the final '}'.
"""

from __future__ import annotations

from pathlib import Path


def rtf_esc_cp1251(text: str) -> str:
    out: list[str] = []
    for ch in text:
        if ch in "\\{}":
            out.append("\\" + ch)
            continue
        if ch in "\r\n":
            continue
        try:
            b = ch.encode("cp1251")
        except UnicodeEncodeError:
            out.append("?")
            continue
        code = b[0]
        if code < 128:
            out.append(ch)
        else:
            out.append(f"\\'{code:02x}")
    return "".join(out)


def main() -> None:
    help_path = Path(r"c:\Users\tv167\source\repos\logReader\logReader.UI\Resources\Help.rtf")
    src = help_path.read_text(encoding="ascii", errors="strict")

    needle = "Фильтр форматов папки:"
    if needle in src:
        return

    insert = (
        r"\par" + "\n"
        + "{\\b " + rtf_esc_cp1251("Фильтр форматов папки:") + "}" + r"\par" + "\n"
        + rtf_esc_cp1251(
            "В «Параметры сохранения» доступен выбор форматов для обработки папки (галочки). "
            "Программа определяет, какие форматы есть в папке, и обрабатывает только выбранные."
        )
        + r"\par" + "\n"
    )

    pos = src.rfind("}")
    if pos < 0:
        raise SystemExit("Invalid RTF: missing closing brace")

    out = src[:pos] + insert + src[pos:]
    help_path.write_text(out, encoding="ascii", errors="strict")


if __name__ == "__main__":
    main()

