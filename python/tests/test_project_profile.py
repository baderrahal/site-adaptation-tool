from pathlib import Path

import pytest

from project_profile import ProfileError, load_profile

PROFILE = Path(__file__).resolve().parents[2] / "rules" / "roshn-makkah.json"


def test_r62_profile_file_loads_with_cluster_q01_of_four_units_and_front_max_two(tmp_path):
    profile = load_profile(PROFILE)
    assert len(profile.get("clusters.Q01")) == 4
    assert profile.get("slopes_percent.front_max") == 2.0
    with pytest.raises(ProfileError, match="slopes_percent.no_such_value"):
        profile.get("slopes_percent.no_such_value")
    # A null is an answer (not supplied yet) and a missing key is an error
    unset = tmp_path / "unset.json"
    unset.write_text('{"value": null}', encoding="utf-8")
    assert load_profile(unset).get("value") is None
