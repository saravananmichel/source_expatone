# Malaysian Education Sources Catalog — Batch 7

**Purpose:** Developer reference for Batch 7 education knowledge sources in the ExpatOne government knowledge base.

**Date:** 2026-09-15

**Scope:** Malaysian government education information relevant to foreigners, expats, and their families.

---

## Active Sources (DIRECT_OFFICIAL)

| # | Title | Official URL | Agency | Category | Priority | Verified | Status |
|---|-------|-------------|--------|----------|----------|----------|--------|
| B7-1 | IMI Student Pass (Pas Pelajar) — Requirements and Procedures | https://www.imi.gov.my/index.php/en/main-services/pass/pelajar/pas-pelajar/ | Immigration Department of Malaysia | education | P0 | 2026-09-15 200 OK | DIRECT_OFFICIAL |

### Source Detail

**B7-1: IMI Student Pass (Pas Pelajar) — Requirements and Procedures**
- URL: https://www.imi.gov.my/index.php/en/main-services/pass/pelajar/pas-pelajar/
- Agency: Immigration Department of Malaysia (Jabatan Imigresen Malaysia)
- Category: `education`
- Chunks: 6 (from 1 source, 2000-char chunking)
- Supported user questions:
  - What is the Student Pass (Pas Pelajar)?
  - What documents does a foreign student need to study in Malaysia?
  - Can my foreign child attend a Malaysian government school?
  - What is required for an international student to study in Malaysia?
  - How do I register a foreign student in Malaysia?
  - What visa does a foreign student need?
  - What are the escort/dependant pass rules for students?
  - Can a student work part-time in Malaysia?
  - What health insurance is required for students?
  - How long does a student need the passport to be valid?
- Key content: Student Pass fee (RM60), higher education eligibility and documents, EMGS/STARS system, health examination on arrival, school-level student pass requirements, KDN approval, escort pass, dependant pass (Masters/PhD), part-time work conditions, document certification rules, legal basis.
- Notes: This is the authoritative IMI page covering student immigration status for both school-level and higher-education foreign students. MOE pages return empty bodies. EMGS is a CLBG (private company), not eligible under source-integrity policy.

---

## Sources Investigated — Not Added

### MOE (Ministry of Education Malaysia) — moe.gov.my

| URL Tested | Status | Reason Not Added |
|------------|--------|-----------------|
| https://www.moe.gov.my/en | 200 OK (navigation only) | Page body is navigation links only; no content on foreign students |
| https://www.moe.gov.my/pendidikan/pendidikan-rendah | 200 OK (navigation only) | Lists topic names only; subpage content not rendered |
| https://www.moe.gov.my/pendidikan/pendidikan-rendah/pendaftaran | Empty body | No content rendered |
| https://www.moe.gov.my/pendidikan/pendidikan-rendah/pendaftaran-sekolah-rendah | Empty body | No content rendered |
| https://www.moe.gov.my/pendidikan/pendidikan-menengah | 200 OK (navigation only) | School types listed; no admission/foreign student content |
| https://www.moe.gov.my/pendidikan/pendidikan-menengah/sekolah-antarabangsa | Empty body | No content |
| https://www.moe.gov.my/pendidikan/prasekolah | 200 OK (navigation only) | No content on foreign children |
| https://www.moe.gov.my/institut-pendidikan-swasta | 200 OK (minimal) | 4 FAQ items about private institution setup only; no foreign student content |
| https://www.moe.gov.my/institut-pendidikan-swasta/permohonan | Empty body | No content |
| https://www.moe.gov.my/pendidikan/bantuan-persekolahan-dan-pembelajaran | 200 OK (list only) | Directory of aid program names; no eligibility criteria; programs appear to target Malaysian citizens |

**Conclusion:** MOE website pages either render empty bodies or contain navigation structure only. No substantive content about foreign student admission procedures, required documents, or eligibility criteria is accessible via automated fetching.

### EMGS (Education Malaysia Global Services) — educationmalaysia.gov.my

| URL Tested | Status | Reason Not Added |
|------------|--------|-----------------|
| https://educationmalaysia.gov.my/ | 200 OK | EMGS is a CLBG (Company Limited by Guarantee), not a government body |
| https://educationmalaysia.gov.my/faq/ | 404 | Not found |
| https://educationmalaysia.gov.my/application-guide/ | 404 | Not found |
| https://educationmalaysia.gov.my/5-easy-steps-to-study-in-malaysia/ | 404 | Not found |
| https://educationmalaysia.gov.my/bringing-your-family-to-malaysia/ | 404 | Not found |
| https://visa.educationmalaysia.gov.my/ | 200 OK | Gateway page only; detailed requirements on sub-pages not accessible |

**Organisation status:** EMGS is stated in its footer to be "a Company Limited by Guarantee ('CLBG') under the purview of the Ministry of Higher Education Malaysia." A CLBG is a private non-profit company registered under company law, not a statutory government body. EMGS does not have regulatory authority. Under this project's source-integrity policy (DIRECT_OFFICIAL only), EMGS does not qualify as a government source.

**Note:** EMGS administers the student visa application process on behalf of the government. The IMI Student Pass page (B7-1) references EMGS as the processing channel, which is accurate and does not require EMGS to be a direct source.

### Other Portals

| Portal | Status | Notes |
|--------|--------|-------|
| https://bpsk.moe.gov.my/ | 200 OK | This is the Psychology and Counselling Division of MOE — not relevant to education procedures for foreigners |
| https://smpk.moe.gov.my/ | (linked, not fetched) | Kindergarten search tool |
| https://direktori.moe.gov.my/ | (linked, not fetched) | School directory |

---

## Education Knowledge Gaps (documented)

| Gap | Impact | Priority | Potential Future Source |
|-----|--------|----------|------------------------|
| MOE government school admission procedures and documents for foreign children | HIGH — direct government school eligibility is a primary expat question | P0 | MOE when page bodies render content |
| School fees at government schools for foreign students | HIGH | P0 | MOE when accessible |
| School fees at international schools (government-regulated) | MEDIUM | P1 | MOE Bahagian Pendidikan Swasta when accessible |
| Primary school registration procedure for foreigners | HIGH | P0 | MOE primary registration page when content rendered |
| Secondary school enrollment procedure for foreigners | HIGH | P0 | MOE secondary page when content rendered |
| Preschool/kindergarten procedures for foreign children | MEDIUM | P1 | MOE preschool page when accessible |
| KDN approval procedure for international schools | MEDIUM | P1 | MOHA or MOE |
| EMGS application process details (step-by-step) | MEDIUM | P1 | EMGS when sub-pages accessible, or official MOHE publication |
| Graduate Pass post-study work permit | MEDIUM | P1 | IMI graduate pass page when accessible |
| Scholarship programs explicitly available to foreign students | LOW | P2 | MOE or MOHE when accessible |
| School academic calendar / examination information | LOW | P2 | MOE when accessible |
| Special education access for foreign children with disabilities | LOW | P2 | MOE when accessible |

**Critical gap:** The MOE admission procedures page for foreign children in government schools (`moe.gov.my/pendidikan/pendidikan-rendah/pendaftaran`) returns an empty body. This is the highest-priority gap for Batch 7's objectives. The assistant will correctly respond "I don't have verified official information" for direct questions about government school admission procedures and fees.

---

## What the Assistant Can and Cannot Answer

### CAN answer (from B7-1):
- What pass does a foreign student need to study in Malaysia?
- What documents are needed for a Student Pass at a private university?
- Can a foreign child attend a government school? (Yes, with JPN/PPD approval letter)
- What health insurance is required for students?
- What are the escort rules for school-level students?
- Can a university student work part-time?
- What passport validity is required?
- How do I apply for a Student Pass for a school-level student (where to apply)?
- What is the Student Pass fee?

### CANNOT answer (documented gap):
- What are the school fees at Malaysian government schools for foreign children?
- What is the exact admission procedure for a foreign child at a government primary school?
- What documents does a foreign child need to enroll in a government school (beyond the Student Pass)?
- What are the fees at international schools?
- What is the current academic year calendar?
- What scholarships are available to foreign students?

---

## Ingestion Notes

- Source uses country code `MY` and category `education`.
- 1 source, 6 chunks from combined page content (2000-char chunker).
- Embeddings via Gemini `gemini-embedding-2` (768 dimensions).
- SHA-256 deduplication: re-running seed skips unchanged content.
- Batch 7 is purely additive — no existing sources deactivated.
- No cross-category contamination confirmed by test.
