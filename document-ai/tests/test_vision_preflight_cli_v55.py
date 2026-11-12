from app.vision_preflight_cli_v55 import readiness_decision


def test_listed_model_does_not_approve_activation():
    result = readiness_decision({"status": "model_listed", "modelAvailable": True})
    assert result["modelAvailable"] is True
    assert result["inferenceVerified"] is False
    assert result["devActivationApproved"] is False
    assert result["nextGate"] == "synthetic_image_inference_and_capacity_validation"


def test_missing_model_stays_blocked():
    result = readiness_decision({"status": "model_not_listed", "modelAvailable": False})
    assert result["devActivationApproved"] is False
    assert result["modelAvailable"] is False


def test_preflight_output_is_allowlisted():
    result = readiness_decision({
        "status": "model_listed", "modelAvailable": True,
        "secret": "do-not-echo", "document": "private-content",
    })
    assert "do-not-echo" not in str(result)
    assert "private-content" not in str(result)
