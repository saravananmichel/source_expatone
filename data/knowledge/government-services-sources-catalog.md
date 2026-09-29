# Government Services Sources Catalog — Batch 9

**Purpose:** Document all official sources investigated, verified, and ingested for Malaysian government services knowledge in ExpatOne's Government Assistant. Category: `government-services`.

**Last verified:** 2026-09-17

## Confirmed Sources

| # | Title | Official URL | Department | Category | Description | Verified Date | Status |
|---|-------|-------------|------------|----------|-------------|---------------|--------|
| B9-1 | JPN Marriage and Divorce FAQ | https://www.jpn.gov.my/en/faq/marriage-and-divorce/ | National Registration Department (JPN), Ministry of Home Affairs | government-services | Non-Muslim marriage registration under Act 164, fees (RM20/RM100), forms, re-registration of overseas marriages, divorce/annulment, foreigner-relevant marriage procedures | 2026-09-17 | DIRECT_OFFICIAL |
| B9-2 | JPN Identity Card FAQ | https://www.jpn.gov.my/en/faq/identity-card/ | National Registration Department (JPN), Ministry of Home Affairs | government-services | MyPR application for foreigners (RM40), MyKad/MyKid/MyKAS identity cards, New Generation MyKad (Sep 2026), processing times, fees, appointment system | 2026-09-17 | DIRECT_OFFICIAL |
| B9-3 | JPN Birth Registration FAQ | https://www.jpn.gov.my/en/faq/birth/ | National Registration Department (JPN), Ministry of Home Affairs | government-services | Birth registration procedures, who can notify, late registration, out-of-wedlock children, Birth Certificate Extract (RM5), foreigner-parent implications | 2026-09-17 | DIRECT_OFFICIAL |
| B9-4 | JPN Citizenship FAQ | https://www.jpn.gov.my/en/faq/citizenship/ | National Registration Department (JPN), Ministry of Home Affairs | government-services | Citizenship application for foreigners, Article 15(2) Federal Constitution, mixed-nationality family procedures, revoked/renounced citizenship, no guaranteed processing time | 2026-09-17 | DIRECT_OFFICIAL |

## Summary

| Metric | Value |
|--------|-------|
| Sources investigated | 25+ URLs across malaysia.gov.my, jpn.gov.my, rmp.gov.my, spab.gov.my, kpdnhep.gov.my, dbkl.gov.my, mbpj.gov.my, apad.gov.my, mers999.gov.my, bomba.gov.my |
| Sources verified & added | 4 (all DIRECT_OFFICIAL from jpn.gov.my) |
| Sources skipped | See below |
| Synthetic sources | 0 |
| Category | government-services |
| Country | MY |

## Skipped Sources (Not Viable)

### malaysia.gov.my (MyGovernment Portal)

| URL | Reason |
|-----|--------|
| https://www.malaysia.gov.my/portal/index | 403 Forbidden — main portal blocked |
| https://www.malaysia.gov.my/portal/content/27732 | 403 Forbidden — services page blocked |
| https://www.malaysia.gov.my/portal/content/30120 | 403 Forbidden |
| https://www.malaysia.gov.my/portal/content/30012 | 403 Forbidden |

### rmp.gov.my / PDRM (Royal Malaysia Police)

| URL | Reason |
|-----|--------|
| https://www.rmp.gov.my/ | 200 — Main page accessible but entirely in Malay. Contains only navigation and news. No English content. Contact: Bukit Aman HQ, 50560 KL, Tel: 03-2266 2222, Email: rmp@rmp.gov.my |
| https://www.rmp.gov.my/e-perkhidmatan/e-reporting | 500 — Internal server error |
| https://www.rmp.gov.my/soalan-lazim | 500 — Internal server error |
| https://www.rmp.gov.my/hubungi-kami | 200 — Contact info only in Malay, minimal content |
| https://www.rmp.gov.my/piagam-pelanggan | 500 — Internal server error |
| https://ereporting.rmp.gov.my/ | Connection failed — e-Reporting portal not downloadable |

### JPN pages (JS-rendered, no static content)

| URL | Reason |
|-----|--------|
| https://www.jpn.gov.my/en/services/marriage/ | 200 — Navigation menu only, actual content is JavaScript-rendered. Cannot extract via curl. |
| https://www.jpn.gov.my/en/services/birth/ | 200 — Same JS-rendering issue |
| https://www.jpn.gov.my/en/services/death/ | 200 — Same JS-rendering issue |
| https://www.jpn.gov.my/en/information/mypr/ | 200 — Same JS-rendering issue |
| https://www.jpn.gov.my/en/contact-us/ | 200 — Same JS-rendering issue, 232KB of HTML with no extractable text content |
| https://www.jpn.gov.my/en/contact-us/counter-operating-hours/ | 200 — Same JS-rendering issue |
| https://www.jpn.gov.my/en/contact-us/nrd-branches/ | 404 — Not found |

### JPN FAQ pages (minimal foreigner-relevant content)

| URL | Reason |
|-----|--------|
| https://www.jpn.gov.my/en/faq/death/ | 200 — Contains only Hajj-related death registration FAQ. Not relevant to typical foreigners/expats. |
| https://www.jpn.gov.my/en/faq/customer-service-office/ | 200 — Contains complaint channels (Tel: 03-88807077, visit NRD, letter). Minimal standalone content — contact info already included in other JPN FAQ pages. |

### Other government portals

| URL | Reason |
|-----|--------|
| https://www.spab.gov.my/ | 200 — 197 bytes, effectively empty page |
| https://www.kpdnhep.gov.my/ | Connection timeout — Consumer Protection portal unreachable |
| https://www.kpdnhep.gov.my/en/ | Connection timeout |
| https://www.mers999.gov.my/ | Connection timeout — Emergency services portal unreachable |
| https://www.mers999.my/ | Connection timeout |
| https://www.bomba.gov.my/ | Connection timeout — Fire Department portal unreachable |
| https://www.bomba.gov.my/index.php/en/ | Connection timeout |
| https://www.pcb.gov.my/en | 503 — Service unavailable (Public Complaints Bureau) |
| https://www.bpa.jpm.gov.my/ | Connection timeout (Bureau of Public Complaints) |
| https://www.mavcom.my/en/ | Connection timeout (Aviation Commission) |

### Local councils

| URL | Reason |
|-----|--------|
| https://www.dbkl.gov.my/en/ | 200 — KL City Hall. Page is mostly JS-rendered. Only extractable: contact info (Tel: 03-2617 9000, Fax: 03-2698 0460, Email: dbkl@dbkl.gov.my). Insufficient structured content for knowledge base. |
| https://www.mbpj.gov.my/ | 200 — Petaling Jaya City Council. Entirely in Malay. No English content. |
| https://www.mbsa.gov.my/ | Connection timeout — Shah Alam City Council |

### Other agencies

| URL | Reason |
|-----|--------|
| https://www.apad.gov.my/ | 200 — Land Public Transport Agency. Entirely in Malay. Commercial/operator focus. |
| https://www.jtksm.mohr.gov.my/en/ | Connection timeout — Labour Department (already covered in Batch 4) |
| https://www.imi.gov.my/portal2017/index.php/en/contact-us.html | 404 — Old IMI contact page no longer exists |

## Technical Notes

- **JPN website architecture:** jpn.gov.my uses WordPress with Astra theme. Service pages (marriage, birth, death, MyPR) are JavaScript-rendered and return only navigation menus when fetched via curl. FAQ pages use server-side rendered content and are fully extractable.
- **PDRM website:** rmp.gov.my is entirely in Malay with no English toggle. Multiple service pages return HTTP 500. The e-Reporting portal (ereporting.rmp.gov.my) was not accessible during verification.
- **MyGovernment portal:** malaysia.gov.my returns 403 Forbidden for all tested URLs, suggesting access restrictions or bot protection.
- **Emergency services:** Both mers999.gov.my and bomba.gov.my timed out on all connection attempts.

## Documented Gaps

### Gap 1: Police Report Procedures (PDRM)
No accessible English-language official page from PDRM with police report procedures was found. The PDRM website (rmp.gov.my) is entirely in Malay and multiple pages return HTTP 500. The e-Reporting portal was inaccessible. This is a significant gap — police reports are frequently needed by expats (theft, accidents, lost documents).

### Gap 2: Emergency Contact Numbers
No accessible official page listing Malaysian emergency numbers (999 police/ambulance/fire, 112 mobile emergency, etc.) was found. The MERS 999 portal was unreachable. Emergency contact information is critical for expats.

### Gap 3: MyGovernment Service Directory
malaysia.gov.my returns 403 for all tested URLs. The main government service directory portal is not accessible for content extraction.

### Gap 4: Consumer Complaints (KPDNHEP)
The Consumer Protection portal (kpdnhep.gov.my) timed out on all connection attempts. Consumer complaint procedures for foreigners are not covered.

### Gap 5: Public Complaints Bureau
Both pcb.gov.my (503) and bpa.jpm.gov.my (timeout) were inaccessible. Government complaint channels beyond JPN's SISPAA system are not covered.

### Gap 6: Local Council Services
Local council websites (DBKL, MBPJ, MBSA) are either JS-rendered, Malay-only, or inaccessible. No structured English-language local council information could be extracted. This affects property/assessment, waste management, and licensing queries.

### Gap 7: Death Registration Involving Foreigners
JPN Death FAQ contains only Hajj-related death registration. No specific guidance for registering deaths of foreigners or deaths involving foreign family members was found.

### Gap 8: Police Clearance Certificates
No accessible official page on PDRM police clearance certificate (PCCC) procedures for foreigners was found. This is commonly needed by expats for visa applications in other countries.

## Ingestion Notes

- All 4 sources use category `government-services` and CountryCode `MY`
- Agency boundaries explicitly documented in each source to prevent the RAG system from using JPN content to answer Immigration/PDRM/MOH questions
- Content faithfully reproduced from official JPN FAQ pages
- Numerical values (fees, penalties, periods) are exact as stated on official pages
- SISPAA NRD contact info included in all sources for enquiry channel
- Content hash (SHA-256) idempotency ensures re-running seed does not create duplicates
