#!/usr/bin/env python3
"""Load and validate the packages WoWVoxPack publishes to CurseForge."""

from dataclasses import dataclass
import json
from pathlib import Path
from types import MappingProxyType
from typing import Mapping


DEFAULT_CATALOG = Path(__file__).resolve().parent.parent / "curseforge-packages.json"
RELATION_TYPES = frozenset({"requiredDependency", "optionalDependency"})


@dataclass(frozen=True)
class Relation:
    slug: str
    type: str

    def render(self) -> str:
        return f"{self.slug}:{self.type}"


@dataclass(frozen=True)
class Addon:
    name: str
    slug: str
    relations: tuple[Relation, ...]


@dataclass(frozen=True)
class Voice:
    name: str
    description: str
    projects: Mapping[str, int]


class Catalog:
    def __init__(self, voices: tuple[Voice, ...], addons: tuple[Addon, ...]):
        self.voices = voices
        self.addons = addons
        self._voices = {voice.name: voice for voice in voices}
        self._addons = {addon.name: addon for addon in addons}

    @property
    def voice_names(self) -> tuple[str, ...]:
        return tuple(voice.name for voice in self.voices)

    @property
    def addon_names(self) -> tuple[str, ...]:
        return tuple(addon.name for addon in self.addons)

    @property
    def projects(self) -> dict[str, dict[str, int]]:
        return {voice.name: dict(voice.projects) for voice in self.voices}

    def project_id(self, voice: str, addon: str) -> int:
        return self._voices[voice].projects[addon]

    def voice_description(self, voice: str) -> str:
        return self._voices[voice].description

    def addon_slug(self, addon: str) -> str:
        return self._addons[addon].slug

    def relations(self, addon: str) -> str:
        return ",".join(relation.render() for relation in self._addons[addon].relations)


def load(path: Path = DEFAULT_CATALOG) -> Catalog:
    source = json.loads(path.read_text(encoding="utf-8"))
    raw_voices = source.get("voices")
    raw_addons = source.get("addons")
    if not isinstance(raw_voices, list) or not isinstance(raw_addons, list):
        raise ValueError("catalog must contain voices and addons arrays")

    addons = tuple(_load_addons(raw_addons))
    addon_names = {addon.name for addon in addons}
    voices = tuple(_load_voices(raw_voices, addon_names))
    return Catalog(voices, addons)


def _load_addons(raw_addons: list[object]) -> list[Addon]:
    addons = []
    names = set()
    for raw in raw_addons:
        if not isinstance(raw, dict):
            raise ValueError("each addon must be an object")
        name = _string(raw, "name", "addon")
        if name in names:
            raise ValueError(f"duplicate addon '{name}'")
        names.add(name)
        slug = _string(raw, "slug", name)
        raw_relations = raw.get("relations", [])
        if not isinstance(raw_relations, list):
            raise ValueError(f"{name} relations must be an array")
        relations = []
        for raw_relation in raw_relations:
            if not isinstance(raw_relation, dict):
                raise ValueError(f"{name} relation must be an object")
            relation_slug = _string(raw_relation, "slug", f"{name} relation")
            relation_type = _string(raw_relation, "type", f"{name} relation")
            if relation_type not in RELATION_TYPES:
                raise ValueError(
                    f"{name} has unsupported relation type '{relation_type}'"
                )
            relations.append(Relation(relation_slug, relation_type))
        addons.append(Addon(name, slug, tuple(relations)))
    return addons


def _load_voices(raw_voices: list[object], addon_names: set[str]) -> list[Voice]:
    voices = []
    names = set()
    for raw in raw_voices:
        if not isinstance(raw, dict):
            raise ValueError("each voice must be an object")
        name = _string(raw, "name", "voice")
        if name in names:
            raise ValueError(f"duplicate voice '{name}'")
        names.add(name)
        description = _string(raw, "description", name)
        projects = raw.get("projects")
        if not isinstance(projects, dict):
            raise ValueError(f"{name} projects must be an object")

        project_names = set(projects)
        missing = addon_names - project_names
        if missing:
            raise ValueError(f"{name} is missing project for {sorted(missing)[0]}")
        unknown = project_names - addon_names
        if unknown:
            raise ValueError(f"{name} has unknown project addon {sorted(unknown)[0]}")

        validated = {}
        for addon, project_id in projects.items():
            if type(project_id) is not int or project_id <= 0:
                raise ValueError(f"{name}/{addon} project ID must be a positive integer")
            validated[addon] = project_id
        voices.append(Voice(name, description, MappingProxyType(validated)))
    return voices


def _string(source: dict, key: str, owner: str) -> str:
    value = source.get(key)
    if not isinstance(value, str) or not value.strip():
        raise ValueError(f"{owner} {key} must be a nonempty string")
    return value
