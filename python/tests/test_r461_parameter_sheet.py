"""R461, R465. The parameter sheet of button 1, python/parameter_sheet.py, the twin of ParameterSheet of Common: the sheet of the repo and
made up rows (R10), its refusals word for word as the C# side gives them."""
import pytest
from openpyxl import Workbook

import parameter_sheet
import project_profile

COLUMNS = list(parameter_sheet.COLUMNS)


def rows(*items):
    """A sheet of the eight columns, one row per parameter of name, GUID, group, categories, Filled by and Keep."""
    return [COLUMNS] + [[name, guid, "Text", group, on, filled, "x", keep] for name, guid, group, on, filled, keep in items]


def test_r461_the_sheet_of_the_repo_gives_the_10_link_parameters_and_plot_with_their_buttons():
    settings = project_profile.load_settings()
    kept, left_out = parameter_sheet.from_file(parameter_sheet.path_of(project_profile.SETTINGS_PATH, {"addin": settings.get("addin")}))
    assert left_out == [] and len(kept) == 11 and kept[-1]["name"] == "Plot" and kept[-1]["filledBy"] == 5
    assert [item["name"] for item in kept if item["filledBy"] == 7] == ["RRE_ZZ_SP_Plot-Number", "RRE_ZZ_SP_Prototype", "RRE_ZZ_SP_Number-of-Steps",
                                                                         "RRE_ZZ_SP_Façade-Color", "RRE_ZZ_SP_Façade-Design-Option",
                                                                         "RRE_ZZ_SP_Prototype-Foundation-Type", "RRE_ZZ_SP_Prototype-HVAC-Schedule-Type",
                                                                         "RRE_ZZ_SP_RVT-Name", "CASE"]
    assert [item["name"] for item in kept if item["filledBy"] is None] == [parameter_sheet.FFL]
    assert {item["dataType"] for item in kept} == {"TEXT"} and [item["group"] for item in kept] == ["Text"] * 10 + ["Data"]
    assert next(item for item in kept if item["name"] == "CASE")["categories"] == ["Detail Items", "RVT Links"]


def test_r461_the_columns_read_in_any_order_and_case_and_keep_no_leaves_a_row_out(tmp_path):
    made = [["guid", "PARAMETER", "Category", "Data type", "group", "Filled By", "Sample value", "keep", "a note"],
            ["C0B2C4EE-E129-4A42-BCB6-225759B874EB", " CASE ", "Detail Items, RVT Links", "text", "Data", "7 Place villas", "A", "yes", "x"],
            ["6e8e827b-cc9a-4da2-9ddb-c8e3f09ffa36", "RRE_ZZ_SP_RVT-Name", "RVT Links", "Text", "Data", 7, None, "No", None],
            [None] * 9]
    kept, left_out = parameter_sheet.from_rows("job parameters.xlsx", made)
    assert kept == [{"name": "CASE", "guid": "c0b2c4ee-e129-4a42-bcb6-225759b874eb", "dataType": "TEXT", "group": "Data", "categories": ["Detail Items", "RVT Links"],
                     "filledBy": 7}]
    assert left_out == ["RRE_ZZ_SP_RVT-Name"]
    book = Workbook()
    book.active.title = parameter_sheet.SHEET
    for row in made:
        book.active.append(row)
    book.save(tmp_path / "p.xlsx")
    assert parameter_sheet.from_file(tmp_path / "p.xlsx") == (kept, left_out)


@pytest.mark.parametrize("made, message", [
    (rows(("A", None, "Data", "Floors", "7", "Yes"), ("A", None, "Data", "Floors", "none", "No")), "the parameter sheet a.xlsx names A twice, one parameter per name (Q155)"),
    ([["Parameter", "GUID", "Data type"]], "the parameter sheet a.xlsx has no column Group, Category, Filled by, Sample value, Keep in its first row (R461)"),
    (rows(("A", "not a guid", "Data", "Floors", "7", "Yes")), "the parameter sheet a.xlsx gives A the GUID not a guid, which is no GUID (Q155)"),
    (rows(("A", None, "Data", "Floors", "7", None)), "the parameter sheet a.xlsx gives A the Keep blank, Yes or No (R461)"),
    (rows(("A", None, "Data", "Floors", "Floors", "Yes")), "the parameter sheet a.xlsx gives A the Filled by Floors, a button from 1 to 9 or none (R465)"),
    (rows((parameter_sheet.FFL, None, "Data", "RVT Links", "7", "Yes")),
     "the parameter sheet a.xlsx gives RRE_ZZ_SP_Prototype-FFL to button 7, it is never written, Filled by none (R465)"),
    ([], "the parameter sheet a.xlsx holds no row (Q155)"),
])
def test_r461_a_sheet_that_does_not_fit_is_refused_with_the_words_of_the_csharp_side(made, message):
    with pytest.raises(parameter_sheet.SheetError) as error:
        parameter_sheet.from_rows("a.xlsx", made)
    assert str(error.value) == message


def test_r461_config_with_no_sheet_named_is_refused_and_a_relative_sheet_lies_under_the_repo_root(tmp_path):
    with pytest.raises(parameter_sheet.SheetError, match="config names no addin.parameterSheet"):
        parameter_sheet.path_of(tmp_path / "config" / "project.json", {"addin": {}})
    assert parameter_sheet.path_of(tmp_path / "config" / "project.json", {"addin": {"parameterSheet": "x.xlsx"}}) == tmp_path.resolve() / "x.xlsx"
