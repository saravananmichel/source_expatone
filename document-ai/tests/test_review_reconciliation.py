from app.review_reconciliation import reconcile_review_fields
from app.schemas import Page

def test_review_conflicts_and_invalid_evidence():
    pages = [Page(page=1, text="Salary: RM 100\nSalary: RM 200\nNotice: 90 days",
                  width=600, height=800, extraction="native", blocks=[])]
    def field(name, value, quote):
        return dict(field=name, value=value, sourceText=quote, page=1)
    result, conflicts = reconcile_review_fields(pages,
        [field("salary", "RM 100", "Salary: RM 100")],
        [field("salary", "RM 200", "Salary: RM 200"),
         field("notice", "90 days", "Notice: 90 days"),
         field("start_date", "2030", "Invented: 2030")])
    assert conflicts == ["salary"]
    assert len(result) == 1
    assert result[0]["field"] == "notice"
    assert result[0]["requiresConfirmation"] is True
