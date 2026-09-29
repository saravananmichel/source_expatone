# Malaysian Customs & Traveller Requirements Sources Catalog — Batch 8

**Purpose:** Document all official sources investigated, verified, and ingested for Malaysian customs and traveller requirement knowledge in ExpatOne's Government Assistant. Category: `customs`.

**Last verified:** 2026-09-17

## Confirmed Sources

| # | Title | Official URL | Department | Category | Description | Verified Date | Status |
|---|-------|-------------|------------|----------|-------------|---------------|--------|
| B8-1 | JKDM Traveller's Guide | https://www.customs.gov.my/en/individu/pengembara/travelers-guide | Royal Malaysian Customs Department (JKDM) | customs | Comprehensive traveller guide: declaration requirements, duty-free allowances (air vs non-air), duty/tax rates, cash declaration (USD 10,000), prohibited goods, drugs, green/red lane, temporary import, ATA carnet, DFS, STA, K7 form | 2026-09-17 | DIRECT_OFFICIAL |
| B8-2 | JKDM Prohibition of Import and Export | https://www.customs.gov.my/en/individu/pengembara/prohibition-of-import-and-export | Royal Malaysian Customs Department (JKDM) | customs | Import and export prohibition orders — Customs (Prohibition of Imports) Order 2023 and Customs (Prohibition of Export) Order 2023, with links to official gazette PDFs | 2026-09-17 | DIRECT_OFFICIAL |
| B8-3 | JKDM Declaration of Strategic Items by Travellers | https://www.customs.gov.my/en/individu/pengembara/declaration-of-sta-for-travellers | Royal Malaysian Customs Department (JKDM) | customs | Strategic Trade Act 2010 traveller obligations — permit requirements, Customs Form No. 22, penalties under STA Sections 9(1)-(6), contact info for Customs Call Centre and MITI Strategic Trade Secretariat | 2026-09-17 | DIRECT_OFFICIAL |

## Summary

| Metric | Value |
|--------|-------|
| Sources investigated | 25+ URLs across customs.gov.my, MAQIS, NPRA, pharmacy.gov.my, malaysia.gov.my |
| Sources verified & added | 3 (all DIRECT_OFFICIAL from customs.gov.my) |
| Sources skipped | See below |
| Synthetic sources | 0 |
| Category | customs |
| Country | MY |

## Skipped Sources (Not Viable)

### customs.gov.my pages returning 404

| URL | Reason |
|-----|--------|
| https://www.customs.gov.my/en/individu/pengembara/customs-form-no-7-k7 | 404 — K7 form page not accessible. K7 info already covered in B8-1 Traveller's Guide. |
| https://www.customs.gov.my/en/individu/pengembara/declaration-of-cash-and-bearer-negotiable-instrument-bni-for-travellers-entering-leaving-malaysia | 404 — Cash declaration page not accessible. Cash declaration rules fully covered in B8-1. |
| https://www.customs.gov.my/en/individu/pengembara/ata-carnet | 404 — ATA Carnet sub-page not accessible. ATA carnet info already covered in B8-1. |
| https://www.customs.gov.my/en/individu/pengembara/malaysian-tourism-tax-system-myttx | 404 — MyTTX page not accessible. Tourism tax is not directly relevant to customs declaration. |
| https://www.customs.gov.my/en/individu | 404 — Individual section index page not routable. |
| https://www.customs.gov.my/en/individu/pengembara | 404 — Travellers section index not routable. |

### customs.gov.my FAQ

| URL | Reason |
|-----|--------|
| https://www.customs.gov.my/en/faq | 200 — Content is internal financial/accounting FAQ in Malay, not traveller-relevant. Skipped. |

### MAQIS (Malaysian Quarantine and Inspection Services)

| URL | Reason |
|-----|--------|
| https://www.maqis.gov.my/index.php/makanan/ | 200 — Food import page. Content is Malay-only, focuses on commercial importers (company registration, FoSIM system, import permits). Not traveller-specific. Insufficient detail for individual traveller food-carry rules. |
| https://www.maqis.gov.my/index.php/tumbuhan/ | 200 — Plants import page. Malay-only, commercial import focus (phytosanitary certificates, import permits, quarantine requirements by species). No individual traveller guidance. |
| https://www.maqis.gov.my/index.php/haiwan-2/ | 200 — Animals import page. Malay-only, commercial import focus. Duplicates plant table content (apparent CMS error). |
| https://www.maqis.gov.my/index.php/haiwan-kesayangan/ | 200 — Pet import page. Malay-only, detailed dog/cat import requirements (quarantine, microchip, vaccination, restricted breeds). Relevant to expats bringing pets but not customs-specific — would be better suited to a future "pets/animal import" batch. |
| https://www.maqis.gov.my/index.php/peraturan_kastam/ | 200 — Lists customs prohibition orders by name only (no content). Same orders already referenced in B8-1 and B8-2. |
| https://www.maqis.gov.my/index.php/soalan-lazim/ | 200 — FAQ page. Malay-only, focuses on commercial import permits, approved permits, HS codes, SPEED system. Not traveller-specific. |
| https://www.maqis.gov.my/index.php/garis-panduan-perkhidmatan-kuarantin/ | 200 — Quarantine guidelines. Not fetched — commercial focus expected. |

### NPRA (National Pharmaceutical Regulatory Agency)

| URL | Reason |
|-----|--------|
| https://www.npra.gov.my/index.php/en/ | 200 — Main page accessible. No traveller-facing "bringing medicines into Malaysia" page found. Consumer section exists but contains only drug reporting and safety alerts. |
| https://www.npra.gov.my/index.php/en/consumers.html | 200 — Consumer section. No medicine import/traveller guidance found. |
| https://www.npra.gov.my/index.php/en/consumers/information-for-consumers/bringing-medicines-into-malaysia.html | 404 — Historical URL no longer accessible. |

### Pharmacy Board

| URL | Reason |
|-----|--------|
| https://www.pharmacy.gov.my/v2/en/content/bringing-medicines-malaysia.html | 404 — Historical URL no longer accessible. |

### malaysia.gov.my

| URL | Reason |
|-----|--------|
| https://www.malaysia.gov.my/portal/content/30120 | 403 — Forbidden. |
| https://www.malaysia.gov.my/portal/content/30012 | 403 — Forbidden. |

## Technical Notes

- **SSL/HTTP2 issue:** customs.gov.my has a server-side HTTP/2 header bug causing SSL certificate verification failures in standard HTTPS clients (including WebFetch). All pages are accessible via `curl --http1.1 -k`. This is a known infrastructure issue on the JKDM portal, not a content problem.
- **Navigation links vs actual pages:** The customs.gov.my navigation menu shows 7 sub-pages under Travellers, but only 3 are actually accessible (travelers-guide, prohibition-of-import-and-export, declaration-of-sta-for-travellers). The other 4 return 404.
- **MAQIS content language:** MAQIS pages are exclusively in Malay with no English toggle. Content is commercial-import focused (company permits, FoSIM system, phytosanitary certificates), not individual traveller guidance.

## Documented Gaps

### Gap 1: Cigarette/Tobacco Traveller Allowances
The JKDM Traveller's Guide explicitly EXCLUDES cigarettes, tobacco products, electronic cigarettes, and vaping devices/liquids from the RM500/RM1,000 general goods exemptions. However, it does not state a specific traveller allowance quantity for cigarettes or tobacco. No separate official page with cigarette/tobacco traveller limits was found accessible on customs.gov.my. This is a significant gap — many countries publish specific cigarette stick limits, but the JKDM site as of 2026-09-17 does not.

### Gap 2: MAQIS Individual Traveller Food/Plant/Animal Rules
MAQIS pages are Malay-only and commercial-import focused. No English-language page with specific rules for individual travellers carrying food, plants, or animals was found. The Traveller's Guide (B8-1) notes that MAQIS rules apply in addition to customs rules, but does not detail them.

### Gap 3: Medication Import for Individual Travellers
No accessible official page from NPRA, Pharmacy Board, or MOH with specific rules for travellers carrying personal medication into Malaysia was found. The Traveller's Guide (B8-1) states prescription drugs require MOH licences/permits but provides no detail on personal-use quantities or procedures.

### Gap 4: Household Goods/Personal Effects for Relocating Expats
No accessible official page with specific customs duty exemptions or procedures for foreigners relocating household goods to Malaysia was found on customs.gov.my.

### Gap 5: Specific Prohibited/Restricted Goods List
The JKDM pages reference the Customs (Prohibition of Imports) Order 2023 and Customs (Prohibition of Export) Order 2023 via gazette PDF links, but the actual list is in the gazette PDFs (legal documents), not rendered as web content. The gazette links are: https://lom.agc.gov.my/act-view.php?type=pua&no=P.U.%20(A)%20117/2023 and https://lom.agc.gov.my/ilims/upload/portal/akta/outputp/PUA%20122.pdf

## Ingestion Notes

- All 3 sources use category `customs` and CountryCode `MY`
- B8-1 is the primary comprehensive source covering most P0 topics
- B8-2 supplements with specific prohibition order references
- B8-3 supplements with detailed STA traveller obligations
- Content hash (SHA-256) idempotency ensures re-running seed does not create duplicates
- Agency boundaries explicitly documented in B8-1 to help the RAG system distinguish customs vs quarantine vs health vs immigration rules
