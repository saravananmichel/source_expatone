# Malaysian Government Healthcare Sources Catalog — Batch 6

**Purpose:** Developer reference for Batch 6 healthcare knowledge sources in the ExpatOne government knowledge base.

**Date:** 2026-09-15

**Scope:** Malaysian government healthcare information relevant to foreigners and expats, specifically medical examination requirements.

---

## Active Sources (DIRECT_OFFICIAL)

| # | Title | Official URL | Agency | Category | Priority | Verified | Status |
|---|-------|-------------|--------|----------|----------|----------|--------|
| B6-1 | IMI Foreign Worker Medical Requirements and FOMEMA | https://www.imi.gov.my/index.php/en/main-services/foreign-worker/ | Immigration Department of Malaysia | healthcare | P0 | 2026-09-15 200 OK | DIRECT_OFFICIAL |
| B6-2 | IMI VP(TE) Pass Medical Requirements and Extension | https://www.imi.gov.my/index.php/en/main-services/pass/visitor-pass/visitors-pass-temporary-employment/ | Immigration Department of Malaysia | healthcare | P0 | 2026-09-15 200 OK | DIRECT_OFFICIAL |
| B6-3 | IMI Foreign Domestic Helper Medical Requirements | https://www.imi.gov.my/index.php/en/main-services/foreign-domestic-helper-fdh/ | Immigration Department of Malaysia | healthcare | P0 | 2026-09-15 200 OK | DIRECT_OFFICIAL |

### Source Detail

**B6-1: IMI Foreign Worker Medical Requirements and FOMEMA**
- URL: https://www.imi.gov.my/index.php/en/main-services/foreign-worker/
- Agency: Immigration Department of Malaysia (Jabatan Imigresen Malaysia)
- Category: `healthcare`
- Supported user questions: What is FOMEMA? How does a foreign worker get a medical exam in Malaysia? What medical requirements exist for VP(TE) workers? What are the sector levy fees? What are visa fees by nationality?
- Key content: FOMEMA Phase 1 (pre-arrival) and Phase 2 (within 30 days of arrival) requirements, VP(TE) extension FOMEMA conditions, repatriation on failure, levy fees by sector (Peninsular and Sabah/Sarawak), visa fees and security bonds by nationality, approved source countries.
- Notes: This is an IMI-hosted page, not MOH-hosted. MOH (moh.gov.my) returns 403 on all URLs — see gap section.

**B6-2: IMI VP(TE) Pass Medical Requirements and Extension**
- URL: https://www.imi.gov.my/index.php/en/main-services/pass/visitor-pass/visitors-pass-temporary-employment/
- Agency: Immigration Department of Malaysia
- Category: `healthcare`
- Supported user questions: What documents are needed for VP(TE)? When does FOMEMA apply? What happens at VP(TE) renewal? What is the i-KAD?
- Key content: FOMEMA mandatory within 1 month of entry, VP(TE) sticker only after passing FOMEMA, 2nd/3rd year extension requires FOMEMA clearance, extension must be submitted 3 months before expiry, application document checklist, i-KAD color coding by sector.

**B6-3: IMI Foreign Domestic Helper Medical Requirements**
- URL: https://www.imi.gov.my/index.php/en/main-services/foreign-domestic-helper-fdh/
- Agency: Immigration Department of Malaysia
- Category: `healthcare`
- Supported user questions: What medical check does a foreign domestic helper need? What happens if a maid fails FOMEMA? What are employer obligations for FDH health screening?
- Key content: FOMEMA required within 1 month of arrival, FOMEMA mandatory for each renewal, immediate repatriation if failed, pre-departure medical from MOH-approved clinic, financial requirements (Indonesian FDH enrollment max RM15,000, min wage RM1,500), personal bond amounts by nationality.

---

## MOH (Ministry of Health) — Complete Access Block

**All MOH pages return HTTP 403 Forbidden.** The entire moh.gov.my domain blocks automated access. No MOH-hosted content is included in Batch 6.

### URLs Tested — All 403 or Unreachable

| URL | Status | Notes |
|-----|--------|-------|
| https://www.moh.gov.my/en | 403 | Main MOH English site |
| https://www.moh.gov.my/en/private/caj-rawatan-hospital-kerajaan-untuk-orang-asing | 403 | Known foreign patient charges page — could not verify content |
| https://www.moh.gov.my/index.php/pages/view/caj-rawatan-hospital-kerajaan-untuk-orang-asing | 403 | Alternate URL format — same block |
| https://www.moh.gov.my/index.php/pages/view/185 | 403 | — |
| https://www.moh.gov.my/index.php/pages/view/189 | 403 | — |
| https://www.moh.gov.my/moh/resources/Penerbitan/... | 403 | PDF attempts blocked |
| https://pharmacy.moh.gov.my/en | timeout | Pharmacy division portal |
| https://myhealth.gov.my/en/ | ECONNREFUSED | Public health portal |
| https://data.moh.gov.my/ (KKMNOW) | 200 OK | Statistics only, no fee/service data |

### Other Government Healthcare Portals Tested — All Failed

| URL | Status |
|-----|--------|
| https://hfrims.moh.gov.my/ | DNS not found |
| https://ppim.moh.gov.my/en/ | DNS not found |
| https://medicalprac.moh.gov.my/ | DNS not found |
| https://www.kkm.gov.my/en | DNS not found |
| https://www.imr.gov.my/en/ | SSL cert mismatch |
| https://www.myhealth.gov.my/en/ | ECONNREFUSED |
| https://www.malaysia.gov.my/portal/content/30118 | 404 |
| https://www.malaysia.gov.my/portal/content/30767 | 404 |

### FOMEMA Website Status

| URL | Status | Notes |
|-----|--------|-------|
| https://fomema.com.my/ | DNS not found | Domain not resolving |
| https://www.fomema.com.my/ | SSL cert error | cert mismatch (vanilla2.sfdns.net) |

FOMEMA Sdn. Bhd. is also a **private company** (though government-authorized), not a government body. Even if accessible, it would not qualify as a DIRECT_OFFICIAL source under the project's source-integrity policy.

### AGC Legislation Database

| URL | Status | Notes |
|-----|--------|-------|
| https://lom.agc.gov.my/ | 200 OK | Navigation accessible |
| https://lom.agc.gov.my/principal.php?type=revised | 200 OK | Table loads empty (JavaScript-rendered data) |
| https://lom.agc.gov.my/result-legislation.php?q=Fees+Medical | 404 | Search API not accessible |

The Fees (Medical) Act 1951 (Act 189) governs government hospital charges, but its text and the fee schedules are not accessible via the AGC portal's web interface in an automated-fetch context.

---

## Healthcare Knowledge Gaps (documented)

Topics for which no verified official source was accessible:

| Gap | Impact | Priority | Potential Future Source |
|-----|--------|----------|------------------------|
| Government hospital outpatient charges for foreigners | HIGH — most common expat healthcare question | P0 | MOH website when accessible (caj-rawatan-hospital-kerajaan-untuk-orang-asing) |
| Government hospital inpatient/specialist charges for foreigners | HIGH | P0 | MOH website when accessible |
| Government clinic (klinik kesihatan) charges for foreigners | HIGH | P0 | MOH website when accessible |
| Emergency treatment charges for foreigners | HIGH | P0 | MOH website |
| Current Fees (Medical) Act schedule | MEDIUM | P0 | AGC legislation database when JS-rendered tables are accessible |
| Pharmacy/medication charges at government facilities | MEDIUM | P1 | MOH Pharmacy Division when accessible |
| Diagnostic/laboratory charges | MEDIUM | P1 | MOH |
| Ambulance service charges | LOW | P1 | MOH |
| FOMEMA examination fee amounts | MEDIUM | P0 | FOMEMA website when domain is restored |
| TB/HIV immigration-related medical requirements from MOH | MEDIUM | P1 | MOH when accessible |
| Vaccination schedules/requirements for foreigners | LOW | P2 | MOH |

**Critical gap:** The MOH charges page for foreigners (`caj-rawatan-hospital-kerajaan-untuk-orang-asing`) exists and was referenced in the Batch 6 specification, but returns 403 in automated fetches. This is the single highest-priority gap for future resolution.

---

## Content Coverage (Batch 6)

The 3 active sources together cover:

- FOMEMA medical examination requirement for VP(TE) foreign workers (Phase 1 pre-arrival + Phase 2 within 30 days)
- FOMEMA medical examination for foreign domestic helpers
- Consequences of failing FOMEMA (immediate repatriation, Check Out Memo)
- VP(TE) extension FOMEMA requirements (2nd and 3rd year)
- Application document requirements including MOH-approved medical report
- i-KAD identity card system
- Sector levy fees (Peninsular and Sabah/Sarawak)
- Visa fees and security bonds by nationality
- Employment conditions for VP(TE) holders
- Approved source countries for foreign workers
- FDH financial requirements (enrollment fees, minimum wage, personal bonds)

**What it does NOT cover** (and the assistant will correctly say so):
- Government hospital charges/fees for foreigners
- Outpatient, inpatient, specialist, emergency rates
- Government clinic charges
- Pharmacy/medication costs
- FOMEMA examination fee amounts

---

## Ingestion Notes

- All 3 sources use country code `MY` and category `healthcare`.
- Content chunked at 2000 characters with 200-character overlap (10 chunks from 3 sources).
- Embeddings via Gemini `gemini-embedding-2` (768 dimensions).
- SHA-256 deduplication: re-running seed skips unchanged content.
- Batch 6 is purely additive — no existing sources deactivated.
- No cross-category contamination (immigration, tax, driving, employment chunks unchanged).
