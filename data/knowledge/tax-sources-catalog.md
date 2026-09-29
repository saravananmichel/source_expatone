# Malaysian Tax Sources Catalog -- Batch 2

**Purpose:** Developer reference for Batch 2 knowledge expansion of the ExpatOne government knowledge base.
All sources below were verified via live fetches from the HASiL/LHDN portal (hasil.gov.my) in September 2026. Each entry confirmed to return real, substantive content suitable for ingestion into the RAG pipeline.

**Last verified:** 2026-09-15

**Note:** Batch 1 (Immigration) sources remain unchanged. See `immigration-sources-catalog.md` for that catalog.

---

## Confirmed Sources

| # | Title | Official URL | Department | Category | Description | Verified Date | Status |
|---|-------|-------------|------------|----------|-------------|---------------|--------|
| 1 | Individual Income Tax Overview | https://www.hasil.gov.my/individu/ | LHDN/HASiL | tax | Comprehensive: what is income tax, who must pay, resident vs non-resident (30% flat), foreign income exemption, employment income thresholds (RM37,333), business income, self-assessment, forms (BE/B/M), final tax option | 2026-09-15 | Ready |
| 2 | Individual Tax Rates | https://www.hasil.gov.my/individu/kadar-cukai/ | LHDN/HASiL | tax | Full tax brackets for YA 2015-2025: 0%-30% graduated rates for residents, 10 brackets for current years, historical rates | 2026-09-15 | Ready |
| 3 | Individual Tax Reliefs | https://www.hasil.gov.my/individu/pelepasan-cukai/ | LHDN/HASiL | tax | Complete relief tables for YA 2014-2025: 22 relief categories for YA2025 including individual RM9,000, medical RM10,000, education RM7,000, lifestyle, children, spouse, EPF/insurance, SOCSO, EV charging, housing loan interest | 2026-09-15 | Ready |
| 4 | Tax Registration / TIN | https://www.hasil.gov.my/individu/pendaftaran/ | LHDN/HASiL | tax | TIN registration via e-Daftar on MyTax portal, automatic TIN for citizens/PR aged 18+, foreign individual registration (file type IG), required documents for non-citizens | 2026-09-15 | Ready |
| 5 | Filing Individual Income Tax (e-Filing) | https://www.hasil.gov.my/individu/lapor-pendapatan/ | LHDN/HASiL | tax | Manual vs e-Filing, MyTax portal, how to activate MyTax account, filing deadlines: BE by 30 Apr (manual) / 15 May (e-Filing), B by 30 Jun / 15 Jul | 2026-09-15 | Ready |
| 6 | Tax Offences Fines and Penalties | https://www.hasil.gov.my/perundangan/kesalahan-denda-dan-penalti/ | LHDN/HASiL | tax | All offences under ACP 1967: failure to file (RM200-20,000 + 6 months), under-reporting (200% penalty), evasion (300% + 3 years), late payment (10% increase), obstruction, record-keeping failures | 2026-09-15 | Ready |
| 7 | Tax Overpayment Refund | https://www.hasil.gov.my/individu/bayaran/cukai-terlebih-bayar/ | LHDN/HASiL | tax | Refund processing: 30 working days for e-Filing, 90 working days for manual, conditions, what to do if delayed | 2026-09-15 | Ready |
| 8 | Tax Rebates | https://www.hasil.gov.my/individu/rebat/ | LHDN/HASiL | tax | Individual rebate RM400 (spouse RM400) when chargeable income under RM35,000, Zakat/Fitrah rebate, departure levy for umrah | 2026-09-15 | Ready |
| 9 | Cessation of Employment / Tax Clearance | https://www.hasil.gov.my/individu/penamatan-perkhidmatan/ | LHDN/HASiL | tax | SPC process: employer must notify HASiL 30 days before cessation, forms CP22A/CP22B/CP21, withholding requirements, when notification not required, permanent file closure conditions | 2026-09-15 | Ready |
| 10 | Travel Restrictions for Tax Defaulters | https://www.hasil.gov.my/individu/sekatan-perjalanan/ | LHDN/HASiL | tax | How to check restriction status, how to pay (ByrHASiL), temporary release (50% payment + 5 business days), full cancellation, payment code 084/095 | 2026-09-15 | Ready |
| 11 | HASiL e-Services Overview | https://www.hasil.gov.my/e-perkhidmatan/ | LHDN/HASiL | tax | Complete catalog: e-Daftar, e-KYC, e-Filing, e-PCB, ByrHASiL, e-SPC, e-Billing, e-Kemaskini, mandatory vs encouraged services, effective dates | 2026-09-15 | Ready |
| 12 | Introduction to Individual Income Tax | https://www.hasil.gov.my/individu/pengenalan-cukai-pendapatan-individu/ | LHDN/HASiL | tax | Who needs to pay, taxable income types, residence rules summary, when tax is imposed, self-assessment overview, forms, final tax option | 2026-09-15 | Ready |
| 13 | Employer Cessation Notification | https://www.hasil.gov.my/majikan/pemberitahuan-pemberhentian-kerja/ | LHDN/HASiL | tax | CP22A/CP22B submission 30 days before cessation, CP21 for leaving Malaysia 3+ months, withholding obligations 90 days, penalties RM200-20,000 + 6 months, employer liability for employee tax | 2026-09-15 | Ready |

---

## Summary

| Metric | Count |
|--------|-------|
| Total sources cataloged | 13 |
| Ready for ingestion | 13 |
| New sources for Batch 2 | 13 |

**Portal breakdown:**
- HASiL portal (hasil.gov.my): 13 sources (#1-13)

**Cross-batch totals:**
- Batch 1 (Immigration): 19 sources (17 new + 1 existing + 1 skipped status)
- Batch 2 (Tax): 13 sources (all new)
- Combined total: 32 sources cataloged

---

## Skipped Sources (Not Viable)

The following URLs were investigated but excluded from Batch 2:

| Source | URL | Reason |
|--------|-----|--------|
| Payment page | https://www.hasil.gov.my/individu/bayaran/ | Page body loads dynamically, no content extractable |
| FAQ page | https://www.hasil.gov.my/individu/soalan-lazim/ | Redirects to stamp duty FAQ, individual FAQs are in PDFs |
| Filing schedule | https://www.hasil.gov.my/borang/program-memfail-borang-nyata/ | Deadlines are in downloadable PDFs only |
| Non-resident specific page | N/A | No dedicated non-resident page found; non-resident coverage provided through individual overview (#1) and tax rates (#2) pages |

---

## Ingestion Notes

- All 13 sources are new and should be registered via `POST /api/knowledge/sources`, ingested via `POST /api/knowledge/sources/{id}/ingest`, and embedded via `POST /api/knowledge/embeddings/generate`.
- All sources use country code `MY` (Malaysia).
- All sources fall under the `tax` category.
- Content will be chunked at 2000 characters with 200-character overlap per the existing knowledge base architecture.
- Embeddings will be generated using Gemini `gemini-embedding-2` (768 dimensions).
- Sources #1 and #12 have overlapping content (both cover individual income tax introduction). Both are included because #1 is broader (includes foreign income, thresholds, business income) while #12 is a more focused introduction. The chunking and embedding process will handle deduplication at the semantic search level.
- Source #9 (Cessation/Tax Clearance) and #13 (Employer Cessation Notification) cover related but distinct perspectives: #9 is employee-facing (what happens to your tax file) while #13 is employer-facing (notification obligations and penalties).
