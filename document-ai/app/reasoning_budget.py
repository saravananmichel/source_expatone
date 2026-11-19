"""Bound the local reasoning stage without changing production defaults."""
import os


def reasoning_budget(profile, elapsed_seconds):
    legacy = profile.inference_seconds + 2
    if profile.name != "development" or os.getenv("DOCUMENT_AI_ADAPTIVE_BUDGET", "0") != "1":
        return legacy
    # Reserve five seconds for rendering and response overhead.
    remaining = 45.0 - max(0.0, elapsed_seconds)
    return max(0.0, min(float(legacy), remaining))
