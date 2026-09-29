# Malaysian KWSP/PERKESO Sources Catalog — Batch 5

**Purpose:** Developer reference for Batch 5 knowledge sources in the ExpatOne government knowledge base.

**Date:** 2026-09-15

**Scope:** KWSP (EPF) and PERKESO (SOCSO) official sources relevant to foreigners/expats in Malaysia.

---

## Active Sources (DIRECT_OFFICIAL)

| # | Title | Official URL | Department | Verified | Status |
|---|-------|-------------|------------|----------|--------|
| B5-1 | PERKESO Employment Injury Scheme Benefits | https://www.perkeso.gov.my/en/our-services/protection/employment-injury-scheme.html | PERKESO | 2026-09-15 200 OK | DIRECT_OFFICIAL |
| B5-2 | PERKESO Invalidity Scheme Benefits | https://www.perkeso.gov.my/en/our-services/protection/invalidity-scheme.html | PERKESO | 2026-09-15 200 OK | DIRECT_OFFICIAL |
| B5-3 | PERKESO Domestic Worker Coverage | https://www.perkeso.gov.my/en/our-services/protection/domestic-workers.html | PERKESO | 2026-09-15 200 OK | DIRECT_OFFICIAL |
| B5-4 | PERKESO Employer Registration | https://www.perkeso.gov.my/en/our-services/employer-employee/employer-registration.html | PERKESO | 2026-09-15 200 OK | DIRECT_OFFICIAL |
| B5-5 | PERKESO Contributions Overview | https://www.perkeso.gov.my/en/our-services/employer-employee/contributions.html | PERKESO | 2026-09-15 200 OK | DIRECT_OFFICIAL |
| B5-6 | PERKESO Contribution Payment Procedures | https://www.perkeso.gov.my/en/our-services/employer-employee/pembayaran.html | PERKESO | 2026-09-15 200 OK | DIRECT_OFFICIAL |

**Note:** Three additional PERKESO sources already exist in Batch 4 (employment category):
- PERKESO/SOCSO Overview (perkeso.gov.my/en/)
- PERKESO Foreign Worker Coverage (perkeso.gov.my/en/our-services/protection/foreign-worker.html)
- Employment Insurance / LINDUNG KERJAYA (perkeso.gov.my/en/our-services/protection/employment-insurance.html)

**Total PERKESO sources across Batches 4+5:** 9

---

## KWSP (EPF) — Documented Gaps

**All KWSP pages return HTTP 403 Forbidden.** The entire kwsp.gov.my domain blocks automated access. No KWSP sources are included in Batch 5.

### URLs Tested (all 403):

| URL | Description | Status |
|-----|-------------|--------|
| https://www.kwsp.gov.my/ | Homepage | 403 Forbidden |
| https://www.kwsp.gov.my/en/employer | Employer section | 403 Forbidden |
| https://www.kwsp.gov.my/en/member | Member section | 403 Forbidden |
| https://www.kwsp.gov.my/en/employer/responsibilities | Employer responsibilities | 403 Forbidden |
| https://www.kwsp.gov.my/en/employer/responsibilities/non-malaysian-citizen-employees | Foreign worker EPF info | 403 Forbidden |
| https://www.kwsp.gov.my/en/employer/contribution-rate | Contribution rates | 403 Forbidden |
| https://www.kwsp.gov.my/en/member/withdrawal | Withdrawal information | 403 Forbidden |
| https://www.kwsp.gov.my/en/faq | FAQ | 403 Forbidden |

### Impact on Assistant

The assistant will correctly respond "I don't have verified official information about EPF/KWSP" for queries about:
- EPF contribution rates for foreign workers
- EPF withdrawal procedures for expatriates
- Whether EPF contributions are mandatory for foreign workers
- EPF employer obligations regarding foreign employees

This is the intended behavior — it is better for the assistant to acknowledge the gap than to provide ungrounded answers.

### Future Resolution

KWSP sources should be added when:
1. The KWSP website becomes accessible to automated retrieval, OR
2. Official KWSP information is available through an alternative verified channel (e.g., official gazette, MOHR circular referencing KWSP rules)

---

## Content Coverage Summary

### What Batch 5 Covers

| Topic | Source | Key Details |
|-------|--------|-------------|
| Employment injury benefits (detailed) | B5-1 | Temp/permanent disablement rates, medical benefit, rehabilitation, dependant's benefit, funeral benefit |
| Invalidity benefits (detailed) | B5-2 | Pension rates (50%-65%), qualifying periods, grant, survivors' pension, dialysis |
| Domestic worker coverage | B5-3 | Local vs foreign eligibility, scheme coverage matrix, contribution rates, EIS exclusion for foreign |
| Employer registration process | B5-4 | 3-step ASSIST Portal process, forms required (standard/foreign/domestic), record-keeping (7 years) |
| Contribution categories | B5-5 | First Category (<60) vs Second Category (60+), EIS rates, wage ceiling RM6,000 |
| Payment procedures | B5-6 | Deadline (15th of following month), 6% late penalty, ASSIST Portal, 19 participating banks |

### What Already Exists in Batch 4 (not duplicated)

| Topic | Source | Key Details |
|-------|--------|-------------|
| Foreign worker overview (3 schemes) | Batch 4 #13 | Employment Injury, Invalidity (from July 2024), LINDUNG 24 Jam. Rates: employer 1.75%, worker 1.25% |
| Employment insurance overview | Batch 4 #19 | JSA, RIA, ERA, Training Fee (RM4,000), Training Allowance (RM10-20/day) |
| PERKESO site navigation | Batch 4 #10 | General overview and scheme links |

### Combined Coverage (Batch 4 + Batch 5)

The 9 PERKESO sources together cover:
- All 3 foreign worker protection schemes (Employment Injury, Invalidity, LINDUNG 24 Jam)
- Employment injury detailed benefits and rates
- Invalidity detailed benefits, qualifying periods, and pension calculation
- Domestic worker scheme coverage and eligibility
- Employer registration process and forms
- Contribution categories (First/Second), rates, and wage ceiling
- Payment methods, deadlines, and late penalties
- Employment Insurance System (primarily for Malaysian workers)

---

## Ingestion Notes

- All sources use country code `MY` and category `employment` (following existing convention).
- Content chunked at 2000 characters with 200-character overlap.
- Embeddings via Gemini `gemini-embedding-2` (768 dimensions).
- SHA-256 deduplication: re-running seed skips unchanged content.
- No existing sources are deactivated by Batch 5 — it is purely additive.
- Batch 5 sources do NOT duplicate content already in Batch 4 PERKESO sources.
