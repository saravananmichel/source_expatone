# Malaysian Driving/JPJ Sources Catalog -- Batch 3

**Purpose:** Developer reference for Batch 3 knowledge expansion of the ExpatOne government knowledge base.
All sources below were verified via live fetches from the JPJ portal (jpj.gov.my) in September 2026. Each entry confirmed to return real, substantive content suitable for ingestion into the RAG pipeline.

**Last verified:** 2026-09-15

**Note:** Batch 1 (Immigration) and Batch 2 (Tax) sources remain unchanged. See `immigration-sources-catalog.md` and `tax-sources-catalog.md` for those catalogs.

---

## Confirmed Sources

| # | Title | Official URL | Department | Category | Description | Verified Date | Status |
|---|-------|-------------|------------|----------|-------------|---------------|--------|
| 1 | Foreign Licence Conversion FAQ | https://www.jpj.gov.my/faq-pertukaran-lesen-memandu-luar-negara-08092026/ | JPJ | driving | 22 Q&A: conversion definition, eligibility (only Malaysian citizens, diplomatic card holders, MM2H), processing time 30 working days, fees RM20 processing + RM120 for 2-year PDL, must attend KPP01, applications at JPJ Negeri only, no online applications, no representatives except diplomatic, conversion gives PDL not CDL, IDP not eligible for conversion, non-signatory countries require KPP01 | 2026-09-15 | Ready (PDF content extracted) |
| 2 | Foreign Licence Conversion Document Checklist | https://www.jpj.gov.my/conversion-checklist/ | JPJ | driving | Required documents for 4 categories: Malaysian (except Singapore), Malaysian (Singapore licence holder), Diplomatic, MM2H. Forms JPJL1, Appendix B2, MyKad/passport, valid foreign licence, embassy confirmation letter, proof of existence in issuing country. All documents valid 1 year unless stated. Effective 19 May 2025. | 2026-09-15 | Ready (PDF content extracted) |
| 3 | Learner's Driving Licence (LDL) Application | https://www.jpj.gov.my/permohonan-lesen-belajar-memandu-ldl/ | JPJ | driving | Age: 16+ for A/B2/B/C, 17+ for A1/D/DA, 21+ for E/F/G/H/I. Must pass Part 1 law test. Documents: MyKad or passport, photo. Fees: B&C RM20/40 (3/6 months), D+ RM30/60. Max 2 years total. Foreigners need valid passport. | 2026-09-15 | Ready |
| 4 | Probationary Driving Licence (PDL) Application | https://www.jpj.gov.my/permohonan-lesen-memandu-percubaan-pdl/ | JPJ | driving | Must pass practical test parts 2&3. 2-year probationary period. Documents: MyKad or passport. Foreigners need valid passport. Not blacklisted. | 2026-09-15 | Ready |
| 5 | Competent Driving Licence (CDL) Application | https://www.jpj.gov.my/permohonan-lesen-memandu-kompeten-cdl/ | JPJ | driving | Apply within 7 days before PDL expires, 1-year window. Documents: MyKad or passport. Citizens RM20 (B2/B/C), RM30 (D+). Non-citizens RM120 all classes. | 2026-09-15 | Ready |
| 6 | CDL Renewal | https://www.jpj.gov.my/pembaharuan-lesen-memandu-kompeten-cdl/ | JPJ | driving | Citizens/PR: 1-10 years. Non-citizens: 1-5 years. Citizens RM20/year (B2/B/C), RM30/year (D+). Non-citizens RM120 flat. At JPJ offices, UTC, 1JPJ, eKhidmat, Pos Malaysia. | 2026-09-15 | Ready |
| 7 | International Driving Permit (IDP) | https://www.jpj.gov.my/permohonan-permit-memandu-antarabangsa/ | JPJ | driving | Must hold CDL valid more than 1 year. Valid 1 year. At JPJ state/branch offices. Not blacklisted. PDL holders NOT eligible. | 2026-09-15 | Ready |
| 8 | Driving Licence Transaction Fee Rates | https://www.jpj.gov.my/pusat-media-3/informasi-perkhidmatan-jpj/kadar-bayaran-urusniaga-lesen-memandu-2/ | JPJ | driving | Complete fee schedule: LDL, PDL, CDL, vocational, IDP RM150, duplicate RM20, info extract RM10, class addition RM5. Citizen vs non-citizen rates. | 2026-09-15 | Ready |
| 9 | JPJ Driving Licence FAQ (General) | https://www.jpj.gov.my/soalan-lazim-lesen-memandu/ | JPJ | driving | P to CDL conversion (7 days before expiry), IDP eligibility (CDL only, 18+), OKU licence, GDL min age 21, LDL 2-year max, P licence 1 year, CDL 3 years, vocational licence restructuring, PSV requirements | 2026-09-15 | Ready |
| 10 | JPJ Service Information Overview | https://www.jpj.gov.my/pusat-media-3/informasi-perkhidmatan-jpj/ | JPJ | driving | Complete catalog of JPJ services: driver licensing (LDL, PDL, CDL, vocational, IDP, foreign conversion, test booking, fees), vehicle licensing, enforcement (KEJARA, summons, VEP), automotive engineering. Contact: 03-8000 8000. | 2026-09-15 | Ready |
| 11 | Driver Licensing Division Overview | https://www.jpj.gov.my/maklumat-korporat-3/bahagian/bahagian-pelesenan-pemandu/ | JPJ | driving | Division functions: licence issuance, training/curriculum, testing (Part I/II/III for LDL/PDL/CDL, vocational PSV/GDL/CON), foreign licence conversion and appeals, driving institute oversight. | 2026-09-15 | Ready |

---

## Summary

| Metric | Count |
|--------|-------|
| Total sources cataloged | 11 |
| Ready for ingestion | 11 |
| New sources for Batch 3 | 11 |

**Portal breakdown:**
- JPJ portal (jpj.gov.my): 11 sources (#1-11)

**Cross-batch totals:**
- Batch 1 (Immigration): 19 sources (17 new + 1 existing + 1 skipped status)
- Batch 2 (Tax): 13 sources (all new)
- Batch 3 (Driving/JPJ): 11 sources (all new)
- Combined total: 43 sources cataloged

---

## Skipped Sources (Not Viable)

The following URLs were investigated but excluded from Batch 3:

| Source | Reason |
|--------|--------|
| Counter charges page (/kadar-caj-bhg-pelesenan-memandu/) | PDF is image-only, no extractable text |
| Detailed LDL renewal page | URL not found separately; renewal info covered in general LDL page (#3) and FAQ (#9) |
| MyJPJ digital licence page | No dedicated information page found on jpj.gov.my |
| Licence classes detailed page | No standalone page; licence classes referenced across other pages (#3, #5, #8, #9) |

---

## Ingestion Notes

- All 11 sources are new and should be registered via `POST /api/knowledge/sources`, ingested via `POST /api/knowledge/sources/{id}/ingest`, and embedded via `POST /api/knowledge/embeddings/generate`.
- All sources use country code `MY` (Malaysia).
- All sources fall under the `driving` category.
- Content will be chunked at 2000 characters with 200-character overlap per the existing knowledge base architecture.
- Embeddings will be generated using Gemini `gemini-embedding-2` (768 dimensions).
- Sources #1 and #2 had content extracted from PDF documents linked on the JPJ pages. The extracted text has been verified as complete and suitable for chunking.
- Sources #1 (Foreign Licence Conversion FAQ) and #2 (Document Checklist) are the highest-priority sources for ExpatOne users, as they directly address the foreign-to-Malaysian licence conversion process that expats need.
- Source #8 (Fee Rates) provides the authoritative fee schedule referenced by multiple other sources; ingesting it ensures the RAG pipeline can answer fee-related questions with precision.
- Sources #9 (General FAQ) and #10 (Service Overview) have broad coverage that overlaps with individual process pages (#3-#7). Both are included because the FAQ contains practical Q&A not found on process pages, and the overview provides a navigational catalog useful for general queries. The chunking and embedding process will handle deduplication at the semantic search level.
