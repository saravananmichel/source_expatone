"""Private document-AI execution profiles.

This module has no external provider dependencies. The caller must enforce the
overall deadline; these settings alone do not guarantee inference completion.
"""
from dataclasses import dataclass
import os


@dataclass(frozen=True)
class InferenceProfile:
    name: str
    model: str
    context_tokens: int
    output_tokens: int
    inference_seconds: int
    reasoning_enabled: bool


def selected_profile() -> InferenceProfile:
    mode = os.getenv("DOCUMENT_AI_PROFILE", "production").strip().lower()
    if mode == "development":
        return InferenceProfile(
            name="development",
            model=os.getenv("DOCUMENT_AI_DEV_MODEL", "qwen3:0.6b"),
            context_tokens=2048,
            output_tokens=450,
            inference_seconds=int(os.getenv("DOCUMENT_AI_DEV_INFERENCE_SECONDS", "120")),
            reasoning_enabled=False,
        )
    if mode == "production":
        return InferenceProfile(
            name="production",
            model=os.getenv("OLLAMA_MODEL", "qwen3:4b"),
            context_tokens=16384,
            output_tokens=2500,
            inference_seconds=300,
            reasoning_enabled=True,
        )
    raise ValueError("DOCUMENT_AI_PROFILE must be development or production")
