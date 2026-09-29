using ExpatOne.Application.DTOs;
using ExpatOne.Application.Interfaces;

namespace ExpatOne.Api.Services;

public class KnowledgeSeedService
{
    private readonly IKnowledgeIngestionService _ingestionService;
    private readonly ILogger<KnowledgeSeedService> _logger;

    public KnowledgeSeedService(
        IKnowledgeIngestionService ingestionService,
        ILogger<KnowledgeSeedService> logger)
    {
        _ingestionService = ingestionService;
        _logger = logger;
    }

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedImmigrationBatch1Async()
        => SeedSourcesAsync(GetImmigrationSources());

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedTaxBatch2Async()
        => SeedSourcesAsync(GetTaxSources());

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedDrivingBatch3Async()
        => SeedSourcesAsync(GetDrivingSources());

    public async Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedEmploymentBatch4Async()
    {
        await DeactivateOldEmploymentSourcesAsync();
        return await SeedSourcesAsync(GetEmploymentSources());
    }

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedPerkesoBatch5Async()
        => SeedSourcesAsync(GetPerkesoSources());

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedHealthcareBatch6Async()
        => SeedSourcesAsync(GetHealthcareSources());

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedEducationBatch7Async()
        => SeedSourcesAsync(GetEducationSources());

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedCustomsBatch8Async()
        => SeedSourcesAsync(GetCustomsSources());

    public Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedGovernmentServicesBatch9Async()
        => SeedSourcesAsync(GetGovernmentServicesSources());

    private static readonly string[] DeactivatedEmploymentUrls =
    [
        "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/akta-kerja-1955",
        "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/akta-kerja-1955/fasal-penamatan",
        "http://jtksm.mohr.gov.my/ms/perkhidmatan/penggajian-pekerja-asing",
        "http://jtksm.mohr.gov.my/ms/perkhidmatan/penggajian-pekerja-asing/seksyen-60k",
        "https://www.mohr.gov.my/index.php/en/legislation/acts",
        "http://jtksm.mohr.gov.my/ms/akta-dan-peraturan/kontrak-perkhidmatan",
        "https://www.kwsp.gov.my/",
        "https://jpp.mohr.gov.my/index.php/en/",
        "https://perkeso.gov.my/en/lindung-kerjaya/",
    ];

    private async Task DeactivateOldEmploymentSourcesAsync()
    {
        var existing = await _ingestionService.GetSourcesAsync("MY");
        foreach (var source in existing.Where(s => s.IsActive && DeactivatedEmploymentUrls.Contains(s.Url)))
        {
            await _ingestionService.DeactivateSourceAsync(source.Id);
            _logger.LogInformation("Deactivated old employment source: {Name} ({Url})", source.Name, source.Url);
        }
    }

    private async Task<(int sourcesCreated, int chunksCreated, int duplicatesSkipped)> SeedSourcesAsync(
        List<(RegisterSourceDto registration, IngestContentDto ingestion)> sources)
    {
        var sourcesCreated = 0;
        var totalChunks = 0;
        var duplicatesSkipped = 0;

        foreach (var (registration, ingestion) in sources)
        {
            try
            {
                var existing = await _ingestionService.GetSourcesAsync("MY");
                var existingSource = existing.FirstOrDefault(s =>
                    s.Url == registration.Url && s.IsActive);

                GovernmentSourceDto source;
                if (existingSource is not null)
                {
                    source = existingSource;
                    _logger.LogInformation("Source already exists: {Name}", source.Name);
                }
                else
                {
                    source = await _ingestionService.RegisterSourceAsync(registration);
                    sourcesCreated++;
                    _logger.LogInformation("Registered source: {Name} ({Id})", source.Name, source.Id);
                }

                var result = await _ingestionService.IngestSourceAsync(source.Id, ingestion);

                if (result.ContentChanged)
                {
                    totalChunks += result.ChunksCreated;
                    _logger.LogInformation("Ingested {Chunks} chunks for: {Name}", result.ChunksCreated, source.Name);
                }
                else
                {
                    duplicatesSkipped++;
                    _logger.LogInformation("Content unchanged (deduplicated): {Name}", source.Name);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to seed source: {Name}", registration.Name);
            }
        }

        return (sourcesCreated, totalChunks, duplicatesSkipped);
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetImmigrationSources()
    {
        return
        [
            // Source 1: Employment Pass Overview (ESD)
            (
                new RegisterSourceDto
                {
                    Name = "Employment Pass Overview",
                    Url = "https://esd.imi.gov.my/portal/expatriates/myxpats/key-services/employment-pass/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Employment Pass Overview",
                    Category = "immigration",
                    Content = """
Employment Pass (EP) — Malaysia

The Employment Pass is a work permit that enables an expatriate to take up employment with an organisation in Malaysia. It is subject to the contract of employment, for a period of up to 60 months.

Approval Authority: The Expatriate Committee (EC) or other relevant authorities must approve the foreign talent for a position before the Immigration Department of Malaysia can issue the pass.

Pre-Application: Companies should check to see if the company is eligible to apply for an Expatriate Employment Pass before beginning the application.

Application Process: Companies apply through the ESD website by logging into the company's ESD account and submitting the expatriate application online.

Duration: Maximum of 60 months, dependent on the employment contract and upon discretion of the Expatriate Committee. The pass is only valid in Peninsular Malaysia.

Work Restrictions: Holders may only work for the company named in the Employment Pass. If they change employers, they need to resubmit their application.

Associated Passes: Only EP holders earning above RM5,000 are eligible to apply for the following:

1. Dependant Pass — Available for: Spouse, Children under 18, Legally adopted child under 18, Parents / Parents-in-law.

2. Long-Term Social Visit Pass — Available for: Children over 18, Legally adopted child over 18, Parents / Parents-in-law.

3. Social Visit (Temporary Employment) — For a foreign maid.

Employment Pass Categories and Salary Thresholds (Effective 1 June 2026):
- Category I: RM20,000 and above — Up to 10 years
- Category II: RM10,000 to RM19,999 — Up to 10 years (succession plan required)
- Category III: RM5,000 to RM9,999 — Up to 5 years (succession plan required)

All new and renewal Employment Pass applications submitted on or after 1 June 2026 shall comply with the revised requirements.

Contact: MYXpats Helpdesk at helpdesk@myxpats.com.my or +603-7839 7171.
""",
                }
            ),

            // Source 3: Dependant Pass
            (
                new RegisterSourceDto
                {
                    Name = "Dependant Pass",
                    Url = "https://esd.imi.gov.my/portal/expatriates/myxpats/key-services/employment-pass/dependant-pass/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Dependant Pass",
                    Category = "immigration",
                    Content = """
Dependant Pass — Malaysia

Eligibility: Holders of a valid Employment Pass may sponsor Dependant Pass applications for:
- Spouses
- Children under 18 years of age
- Legally adopted children under 18 years of age

The principal holder (sponsor) must possess a valid Employment Pass at the time of application. Only EP holders earning above RM5,000 are eligible to apply for a Dependant Pass.

How to Apply: Applications are submitted online via the ESD website. The sponsoring company logs into its ESD account and submits the application through the portal.

Duration / Validity: The pass duration aligns with the principal applicant's Employment Pass. However, if the dependant's passport expires before the principal's pass, the dependant pass duration will match the shorter passport validity instead.

Work Rights: Dependant Pass holders do not automatically receive work rights. Those wishing to work in Malaysia must separately apply for an Employment Pass.

Required Documents for Spouse and Children of Expatriate Workers:
- Support letter from the employer
- Power of attorney from the employer
- 2 sets of Form Imm.10
- 2 sets of Form Imm.12
- Form Imm.38 (if required)
- Marriage certificate
- Children's birth certificates
- Family members' passports
- Approval letter for the expatriate
- 2 recent passport-sized photographs of the applicant

Contact: MYXpats Helpdesk at helpdesk@myxpats.com.my or +603-7839 7171.
""",
                }
            ),

            // Source 4: Long-Term Social Visit Pass (ESD)
            (
                new RegisterSourceDto
                {
                    Name = "Long-Term Social Visit Pass (ESD)",
                    Url = "https://esd.imi.gov.my/portal/expatriates/myxpats/key-services/employment-pass/long-term-social-visit-pass/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Long-Term Social Visit Pass for Employment Pass Holders",
                    Category = "immigration",
                    Content = """
Long-Term Social Visit Pass (LTSVP) — Malaysia (for Employment Pass Holders)

Eligibility: Holders of an Employment Pass may sponsor this pass for specific family members: parents/parents-in-law, children over 18 years of age, or legally adopted children over 18 years of age.

Prerequisites: Principal holder must have a valid Employment Pass. Only EP holders earning above RM5,000 are eligible.

Application Process: Visit the ESD website, then log in through the company's ESD account and submit the application.

Pass Duration: The validity period follows that of the principal (Employment Pass) holder. However, if the applicant's own passport expires sooner than the principal's pass, the duration will instead match the applicant's passport validity.

Work Rights: Pass holders under this category do not automatically receive permission to work. Those wishing to be employed are required to apply for the Employment Pass separately.

Contact: MYXpats Helpdesk at helpdesk@myxpats.com.my or +603-7839 7171.
""",
                }
            ),

            // Source 5: Professional Visit Pass (ESD)
            (
                new RegisterSourceDto
                {
                    Name = "Professional Visit Pass (ESD)",
                    Url = "https://esd.imi.gov.my/portal/expatriates/myxpats/key-services/professional-visit-pass/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Professional Visit Pass",
                    Category = "immigration",
                    Content = """
Professional Visit Pass (PVP) — Malaysia

Purpose: Granted to foreign talents possessing recognized professional qualifications or skills. It allows them to enter Malaysia and deliver services or complete practical training with a Malaysian company, acting on behalf of an overseas employer, on a temporary basis.

Validity: Maximum duration of 12 months per issuance.

Application Process:
1. Eligibility check: Verify that the sponsoring company qualifies to apply for a PVP.
2. Registration: If eligible, the company must register on the ESD portal.
3. Submission: Log in via the company's ESD account on the ESD website and submit the application online.

Work Conditions: The expatriate is restricted to working solely for the company specified in the pass.

Dependant Pass Restriction: PVP holders are not entitled to apply for a Dependant Pass.

Contact: MYXpats Helpdesk at helpdesk@myxpats.com.my or +603-7839 7171.
""",
                }
            ),

            // Source 6: Succession Plan Requirement
            (
                new RegisterSourceDto
                {
                    Name = "Succession Plan Requirement for Employment Pass",
                    Url = "https://esd.imi.gov.my/portal/latest-news/announcement/announcement-278/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Succession Plan Requirement for Employment Pass",
                    Category = "immigration",
                    Content = """
Update on Revised Expatriate Salary Policy — Succession Plan Requirement

Date: 26 May 2026
Issuing body: MYXpats Centre, Expatriate Services Division (ESD), Immigration Department of Malaysia

The Revised Expatriate Salary Policy was announced on 15 January 2026 and will take effect from 1 June 2026.

A major new element is the succession plan requirement, designed to ensure the structured transfer of knowledge and expertise to local employees during an expatriate's time of employment.

The succession plan is required for Employment Pass Category II (RM10,000–RM19,999) and Category III (RM5,000–RM9,999).

Phased Implementation: The succession plan component will not begin immediately with the broader policy. As part of a gradual rollout, this requirement will only take effect from 1 January 2027 onwards, giving organizations additional time to prepare and comply.

Contact: Phone 03-7839 7171, Email helpdesk@myxpats.com.my
""",
                }
            ),

            // Source 7: 1:3 Internship Policy
            (
                new RegisterSourceDto
                {
                    Name = "1:3 Internship Policy for Employment Pass",
                    Url = "https://esd.imi.gov.my/portal/latest-news/announcement/announcement-274/",
                    Department = "Ministry of Human Resources (KESUMA) / TalentCorp",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "1:3 Internship Policy (Progressive Policy on Expatriate Contribution to Local Talent Development)",
                    Category = "immigration",
                    Content = """
1:3 Internship Policy — Malaysia

Official name: Progressive Policy on Expatriate Contribution to Local Talent Development
Issued by: Ministry of Human Resources (KESUMA) and implemented through TalentCorp
Implementation date: 1 June 2026

This policy links expatriate hiring to local talent development. Companies that receive Employment Pass (EP) approvals must offer internship placements to local students.

Ratios by Employment Pass Category:
- EP Category I (RM20,000+): 1:3 ratio — each EP I approval requires 3 internship placements
- EP Category II (RM10,000–RM19,999): 1:2 ratio — each EP II approval requires 2 internship placements
- EP Category III (RM5,000–RM9,999): 1:1 ratio — each EP III approval requires 1 internship placement

Internship Requirements:
- Minimum 10 weeks duration
- Must be structured, quality, and paid
- Must be endorsed under the MySIP program (National Structured Internship Programme)
- Minimum pay of RM500 monthly or more depending on student's level of study

Application Process: After EP approval, TalentCorp sends an email notification. The company then registers on MyNext.my, creates MySIP internship postings, recruits interns, and can claim a double tax deduction on internship-related expenses through MySIP.

Students: Register at www.mynext.my, browse internship opportunities via MyNext Talent, and apply for matching positions.

Appeals / Exemptions:
- Companies may appeal for a reduced placement cap of 2% of total workforce
- New companies operating in Malaysia for less than two years
- Companies with Representative Office / Regional Office (RE/RO) status
- Companies granted government tax exemptions for critical sectors (digital, energy, and other approved sectors)

The policy will not interfere with any current EP approval processes. The duration and approval requirements for the EP process itself remain unchanged.

Contact: mysip@talentcorp.com.my, www.talentcorp.com.my/1to3policy
""",
                }
            ),

            // Source 8: Appeal Submission Timeline
            (
                new RegisterSourceDto
                {
                    Name = "Appeal Submission Timeline for EP and PVP",
                    Url = "https://esd.imi.gov.my/portal/latest-news/announcement/announcement-275/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Revision to Online Appeal Submission Timeline for Employment Pass and Professional Visit Pass",
                    Category = "immigration",
                    Content = """
Revision to Online Appeal Submission Timeline

Effective Date: 15 May 2026
Issuing body: MYXpats Centre, Expatriate Services Division (ESD)

This revision applies to rejected Employment Pass (EP) and Professional Visit Pass (PVP) applications submitted through the ESD online system, aligning with the New Expatriate Employment Policy.

Key Change: The previous window for submitting appeals was six (6) months. The new policy shortens this to fourteen (14) days from the rejection date at the Expatriate Committee Meeting.

Appeal Submission Guidelines:
1. Appeals must be filed within fourteen (14) days from the date of rejection at the Expatriate Committee Meeting.
2. The appeal facility is only accessible during that 14-day window.
3. Once an appeal begins, the application fee cannot be refunded.
4. Appeals submitted after the 14-day period will not be accepted by the system.

This revision supersedes the previous appeal submission timeline of six (6) months. Companies should ensure all supporting documents and justifications are complete before submitting, since incomplete or late appeals will not be considered.

Contact: Email helpdesk@myxpats.com.my, Phone +603-7839 7171
""",
                }
            ),

            // Source 9: Photo Verification for MyVISA
            (
                new RegisterSourceDto
                {
                    Name = "Photo Verification for MyVISA Applications",
                    Url = "https://esd.imi.gov.my/portal/latest-news/announcement/photo-verification-for-myvisa/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Photo Verification Enhancement for MyVISA Applications via ESD Online",
                    Category = "immigration",
                    Content = """
Photo Verification Enhancement for MyVISA Applications via ESD Online

Effective Date: 21 August 2026

The ESD Online introduced an enhanced photo verification feature for Visa With Reference (VDR) and Electronic Visa Approval Letter (eVAL) applications.

Two Key Enhancements:

1. Public Photo Verification (for Applicants): A verification tool is now accessible on the ESD Online landing page without requiring login. Applicants can confirm their passport photo meets specifications before providing it to their employer for the application process.

2. Employer Photo Verification: Within the ESD Online application interface, the designated person listed in the Letter of Undertaking (LOU) on the employer's side can recheck and verify the applicant's passport photo before submitting the expatriate application.

Purpose: To facilitate early detection of non-compliant passport photos and minimise issues arising from non-compliant passport photos during the MyVISA application process.

Contact: Tel +603-7839 7171, Email helpdesk@myxpats.com.my
""",
                }
            ),

            // Source 10: Residence Pass
            (
                new RegisterSourceDto
                {
                    Name = "Residence Pass (Pas Residen)",
                    Url = "https://www.imi.gov.my/index.php/perkhidmatan-utama/pas/pas-residen/",
                    Department = "Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Residence Pass (Pas Residen)",
                    Category = "immigration",
                    Content = """
Residence Pass (Pas Residen) — Malaysia

The Residence Pass is issued to foreign nationals under categories specified in Regulation 16A of the Immigration Regulations 1963.

Eligible Categories:

A. Applicants with Family Ties to Malaysian Citizens:

1. Biological child / stepchild / adopted child of a citizen
Required: Form IMM 16A, birth/adoption certificate, parents' marriage certificate, parent ID documents, passport copy, sponsor's IC copy, 2 passport-sized photos.

2. Widow/widower of a citizen who has biological children with that citizen
Required: Form IMM 16A, marriage/divorce/death certificate, statutory declaration of not having remarried, passport copy, sponsor's IC copy, child's birth certificate and IC copy, 2 photos.

3. Parents / parents-in-law of a Malaysian citizen who have been residing in Malaysia on a valid pass
Required: Form IMM 16A, birth certificates, marriage certificate, 2 photos, sponsor's IC copy.

B. Applicants with Family Ties to Malaysian Permanent Residents:

4. Biological child of a permanent resident
Required: Form IMM 16A, child's birth certificate, parent's IM5 (Entry Permit), parent's IC and passport, applicant's passport, parents' marriage certificate, 2 photos, sponsor's IC copy.

5. Spouse of a permanent resident
Required: Form IMM 16A, marriage certificate, passport copy, spouse's IC, spouse's IM5, children's birth certificates, 2 photos, sponsor's IC copy.

Application Conditions:
- Family of Malaysian citizens: must have been in Malaysia on a valid long-term pass for at least 3 years.
- Family of permanent residents: must have been in Malaysia on a valid long-term pass for at least 5 years.
- Sponsor must be a Malaysian citizen aged 21 or above.

Application Procedure:
1. Submit to Immigration HQ in Putrajaya or any nearby immigration office.
2. Applicant must appear in person together with the sponsor.
3. Old and new passports plus copies must be presented.
4. Original supporting documents and copies required for verification.
5. Foreign documents must be authenticated by the respective country's embassy.

Fees: RM500.00 for a 5-year period. Multiple Entry Visa (MEV) fees vary by nationality.

Benefits: Holders are entitled to work, study, and conduct business, subject to conditions set by relevant authorities.
""",
                }
            ),

            // Source 11: Visa With Reference
            (
                new RegisterSourceDto
                {
                    Name = "Visa With Reference (VDR)",
                    Url = "https://www.imi.gov.my/index.php/visa-dengan-rujukan-vdr/",
                    Department = "Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Visa With Reference (VDR)",
                    Category = "immigration",
                    Content = """
Visa With Reference (VDR) — Malaysia

VDR is a visa issued by Malaysian Representative Offices abroad to non-citizens, allowing entry into Malaysia after approval from Immigration Headquarters in Putrajaya. It requires a referral/approval process through the head office before the overseas mission can issue the visa.

Sponsor Requirement: The application must be submitted by a Malaysian citizen sponsor. The sponsor must appear in person. The applicant (non-citizen) must be in their home country at the time of application.

Required Documents:
1. Form Imm.12 (2 copies) — applicant's photo must be affixed
2. Sponsor's identity card copy (sponsor must attend personally)
3. Full passport copy of applicant (all printed pages)
4. Personal Bond / Security Bond (stamped with RM10 revenue stamp from LHDN). Security Bond specifically required for Chinese and Nigerian nationals
5. Sponsor's Statutory Declaration Form (stamped by a Commissioner for Oaths from a Government Court)
6. Sponsor's proof of income: 3 months' recent pay slips — minimum RM2,000 or above. If self-employed: Business License / SSM / ROC copies AND 3 months' recent bank statements
7. Employer confirmation letter (for sponsor)
8. Sponsor's additional information form
9. Cover letter
10. Documentary proof of relationship between sponsor and applicant
11. Security clearance letter — required only for citizens of Afghanistan and Pakistan
12. Marriage Certificate (where applicable)

Processing Time: 7 working days for complete applications.

Relationship Categories and Supporting Documents:
- In-laws / Biological parents: Marriage certificate, birth certificate, passport of sponsor's spouse, Notarial Certificate (verified by Malaysian Embassy abroad and Wisma Putra)
- Sponsor's child under 7 years old: Birth certificate and sponsor's marriage certificate
- Spouse of a Malaysian citizen: Marriage certificate

Contact: Immigration Headquarters, Putrajaya. Phone: 03-8000 8000 (MyGCC).
""",
                }
            ),

            // Source 12: Professional Visit Pass (IMI detailed)
            (
                new RegisterSourceDto
                {
                    Name = "Professional Visit Pass (IMI Detailed Categories)",
                    Url = "https://www.imi.gov.my/index.php/perkhidmatan-utama/pas/pas-lawatan-ikhtisas/",
                    Department = "Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Professional Visit Pass — Detailed Categories and Procedures",
                    Category = "immigration",
                    Content = """
Professional Visit Pass (Pas Lawatan Ikhtisas / PLIK) — Malaysia (Detailed)

The PLIK is issued to foreign nationals who wish to serve and carry out specialized work in Malaysia for a short period.

General Application Conditions:
1. Local sponsor required: Applications must be submitted by a local sponsor only, and all dealings with Immigration are handled by the sponsor's authorized representative.
2. Applicant location: The applicant must be in their home country when the application is made and must enter Malaysia via an entry visa issued by a Malaysian Embassy.
3. Maximum duration: Up to 12 months, or subject to the period supported by the regulatory agency.
4. International students on exchange/mobility programs and industrial training: maximum 6 months.

Where to Submit: Director, Visa, Pass and Permit Division, Level 3 (Podium), Block 2G4, Persiaran Perdana, Precinct 2, Federal Government Administrative Centre, 62250 Putrajaya.

PLIK Categories:

1. Experts (Other Than Those Handled by Expatriate Services): Includes jockeys, volunteers, students under exchange/mobility/industrial training programs, and other categories deemed appropriate by the Director General of Immigration.

2. Foreign Film Production and Foreign Artist Performances (PUSPAL): Applications go through the one-stop center managed by the Ministry of Communications. PUSPAL approval must be obtained before submitting the PLIK application to Immigration.
Fees: Foreign Artist — RM500 (performance), RM90 (film shooting). Foreign Crew — RM90. Opera/Circus — RM90.

3. Islamic Religious Teachers: For foreign nationals teaching Islamic studies at JAKIM-recognized schools or madrasahs. Dependants not allowed.

4. Islamic Students: For foreign nationals studying Islamic studies. Under 18, 18 and above, and converts (mualaf). Dependants/companions not allowed.

5. Other Religious Practitioners: For Hindu priests (Gurukkal), Granthis, Dharma teachers, Padres. Minimum age requirements: priests 40+, musicians 35+, sculptors/carvers 30+. Dependants not allowed.

6. International Students at Malaysia Bible Seminary (MBS): Dependants allowed (spouse and biological children only). Working NOT permitted during study.

7. Government Cooperation Programs: Support letter from sponsoring government agency required. Dependants allowed (spouse and children under 18).

Common Procedural Requirements (All Categories):
1. Obtain a Visa With Reference (VDR) letter
2. Applicant must be in home country when sponsor submits application
3. Apply for visa at Malaysian Embassy in home country before entry
4. New applicants must attend Visa, Pass and Permit Division, Putrajaya HQ
5. New applications: at least 1 month before start of service
6. Renewal: via ePLSI online portal or at any State Immigration Office, no later than 2 weeks before current pass expiry
7. Approval subject to the Special Professional Visit Pass Committee decision
""",
                }
            ),

            // Source 13: Required Documents by Category
            (
                new RegisterSourceDto
                {
                    Name = "Required Documents by Pass Category",
                    Url = "https://www.imi.gov.my/index.php/perkhidmatan-utama/pas/dokumen-yang-diperlukan-mengikut-kategori/",
                    Department = "Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Required Documents by Immigration Pass Category",
                    Category = "immigration",
                    Content = """
Required Documents by Immigration Pass Category — Malaysia

1. Child/Stepchild of a Citizen / Child Whose Citizenship Status Is Undetermined:
Form Imm.12 / Imm.55, Form Imm.38, Parent's identity card, Applicant's birth certificate, Parents' marriage certificate, Court-issued child custody order (if under 18), Parent's death certificate (if applicable), Status verification from National Registration Department (JPN).

2. Elderly Parent (60+) With No Family in Home Country:
Form Imm.12 / Imm.55, Form Imm.38, Personal Bond form (stamped RM10), Sponsor's declaration by Malaysian citizen or Permanent Resident, Sponsor's identity card copy.

3. Student Pass / Dependant / Companion / Graduate:
Form Imm.12 / Imm.55, Form Imm.14, Form Imm.38 (if required).

4. Spouse of a Citizen:
Form Imm.12 / Imm.55, Form Imm.38, Marriage certificate, Children's birth certificates, Latest Statutory Declaration confirming marriage exists, Security Bond (stamped RM10), Sponsor/spouse's identity card, Proof of income, Passport copy, Wedding photographs.

5. Spouse and Children of an Expatriate (Pegawai Dagang):
Employer support letter, Power of attorney, 2 sets of Form Imm.10, 2 sets of Form Imm.12, Form Imm.38 (if required), Marriage certificate, Children's birth certificates, Family members' passports, Expatriate approval letter, 2 passport-sized photographs.

6. Medical Treatment at Malaysian Medical Institution (and one companion):
Treatment limited to government hospitals or specialist hospitals only. Form Imm.12 / Imm.55, Confirmation letter from hospital.

7. Wife of a Permanent Resident:
Marriage must be at least 6 months old. Husband's IC, Husband's Entry Permit, Wife's passport, Marriage certificate, 2 photos, Form Imm.47 (2 sets), Form Imm.55, Form Imm.38, Security Bond (stamped RM10).

Key forms: Imm.10, Imm.12, Imm.14, Imm.38, Imm.47, Imm.55. Security Bond with RM10 revenue stamp required for most spousal and family-related categories. Minimum sponsor income: RM2,000 where specified.
""",
                }
            ),

            // Source 14: Work Permission Endorsement
            (
                new RegisterSourceDto
                {
                    Name = "Work Permission Endorsement for Social Visit Pass Holders",
                    Url = "https://www.imi.gov.my/index.php/perkhidmatan-utama/pas/tatacara-memohon-endorsemen-kebenaran-kerja/",
                    Department = "Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Work Permission Endorsement for Foreign Spouses of Malaysian Citizens",
                    Category = "immigration",
                    Content = """
Work Permission Endorsement (Endorsemen Kebenaran Kerja) — Malaysia

This endorsement is for holders of a Social Visit Pass, specifically foreign spouses of Malaysian citizens who wish to work in Malaysia.

Where to Apply: Visa, Pass and Permit Division at Immigration Headquarters in Putrajaya, or at the nearest State Immigration Office.

When to Apply: The application can be submitted when obtaining approval for the Social Visit Pass, or at any time after the Social Visit Pass has been granted.

Required Documents:
- Marriage Certificate / Nikah Certificate
- Job Offer / Business Registration (whichever is applicable)

Additional Required Documents for Work Endorsement:
- Malaysian spouse's ID card copy
- Applicant's passport and current pass copy
- Statutory Declaration with photo (Commissioner for Oaths)
- Marriage/Nikah certificate
- Job offer letter from employer (on company letterhead)
- Employment contract (stamped RM10 revenue stamp) containing: position, employment period, monthly salary, signatures of both employer and employee
- Applicant/Spouse/Employer Information Form
- Latest company registration form printed from SSM MyData system

Application can be submitted at the Visa, Pass and Permit Division in the state where the applicant works.

Contact: Immigration HQ, No. 15, Levels 1-7 (Podium), Persiaran Perdana, Precinct 2, 62550 Putrajaya. Phone: 03-8000 8000 (MyGCC).
""",
                }
            ),

            // Source 15: Long-Term Social Visit Pass (IMI detailed)
            (
                new RegisterSourceDto
                {
                    Name = "Long-Term Social Visit Pass (IMI Comprehensive)",
                    Url = "https://www.imi.gov.my/index.php/perkhidmatan-utama/pas/pas-lawatan/pas-lawatan-sosial/pas-lawatan-sosial-jangka-panjang/",
                    Department = "Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Long-Term Social Visit Pass — Comprehensive Guide",
                    Category = "immigration",
                    Content = """
Long-Term Social Visit Pass (PLSJP / Pas Lawatan Sosial Jangka Panjang) — Malaysia

The PLSJP is issued by Malaysia's Immigration Department to foreign nationals for temporary residence in Malaysia, with a minimum period of six (6) months.

For foreign spouses of Malaysian citizens:
- The pass may be granted for up to five (5) years, subject to approval.
- Holders are permitted to work, conduct business, or engage in professional employment without needing to convert to an Employment Pass or Temporary Work Visit Pass.
- Inspections (Naziran) may be conducted to verify compliance.

Categories of Applicants:

1. Spouse of a Malaysian Citizen — New Application:
Required: Form Imm.12, Form Imm.38 (2 copies), Form Imm.55 (2 copies), Sponsor's Statement Form, Security Bond (stamped RM10), Statutory Declaration with photo, Malaysian spouse's IC and birth certificate, marriage certificate, wedding photo, child's birth certificate, employer/business documentation, income proof (3 months' pay slips), proof of residential address, Malaysian spouse's passport.

2. Spouse from China or Nigeria — New Application:
Additional requirements apply. Applications must be submitted to Immigration HQ Malaysia. Additional forms include Spousal Affairs Form, Additional Information Form. Sponsor must earn RM2,000 and above. False reports constitute an offense under Section 56(1)(f) of the Immigration Act 1959/63, punishable by fine up to RM10,000, imprisonment up to 5 years, or both.

3. Spouse — Extension/Renewal:
Required: Form Imm.38, Form Imm.55, Statutory Declaration with photo, child's birth certificate, Malaysian spouse's IC, marriage certificate, applicant's passport and current pass.

4. Child/Stepchild of Citizen (7 years old or below):
Application submitted by Malaysian citizen parent.

5. Elderly Parent (60+ years):
Sponsor must be biological child, in-law, or sibling. Sponsor must earn RM2,000 and above. Security Bond required for Chinese and Nigerian nationals.

6. Immediate Family of Foreign Student:
Student confirmation letter, birth certificate, embassy letter proving relationship.

7-9. Abused Wife / Divorcee / Widow of Malaysian Citizen:
Police/hospital report for abused wife. Statutory declarations required. Sponsor income minimum RM2,000.

10. Medical Treatment:
Limited to government/specialist hospitals. One companion allowed.

11. Spouse and Children of Expatriate Workers:
Employer support letter, power of attorney, expatriate approval letter required.

Work Permission for Citizen Spouses:
Employment or business must be lawful under Malaysian law. Work authorization is endorsed in the passport/pass. Required documents include job offer letter, stamped employment contract, SSM company registration.

Where to Apply: Visa, Pass and Permit Division, Immigration HQ Putrajaya or nearest State Immigration Office. Chinese and Nigerian nationals must apply at Immigration HQ.

Contact: No. 15, Levels 1-7, Persiaran Perdana, Precinct 2, 62550 Putrajaya. Phone: 03-8000 8000 (MyGCC).
""",
                }
            ),

            // Source 16: Work Permission Conditions for Citizen Spouses
            (
                new RegisterSourceDto
                {
                    Name = "Work Permission Conditions for Spouses of Citizens",
                    Url = "https://www.imi.gov.my/index.php/syarat-dan-peraturan-kebenaran-bekerja-pasangan-warganegara/",
                    Department = "Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Work Permission Conditions and Regulations for Spouses of Malaysian Citizens",
                    Category = "immigration",
                    Content = """
Work Permission Conditions for Spouses of Malaysian Citizens — Malaysia

Eligibility:
1. The applicant must be the wife or husband of a Malaysian citizen, whose marriage has been lawfully registered under Malaysian law currently in force.
2. The applicant must be eligible for or already hold a Social Visit Pass under the foreign spouse facility for Malaysian citizens.

Conditions:
3. The employment or business undertaken must be lawful under Malaysian laws and related regulations currently in force.
4. The work permission must be recorded or endorsed within the relevant Social Visit Pass by the Director General of Immigration Malaysia or any authorized Immigration Officer.
5. The Social Visit Pass holder must comply with all regulations set by the Director General of Immigration for the purposes of both issuing the Social Visit Pass and making the related work endorsement.

Key Points:
- Only legally married spouses of Malaysian citizens qualify — the marriage must be registered under Malaysian law.
- A Social Visit Pass (Pas Lawatan Sosial) is the prerequisite immigration document.
- Work or business activities must be legal under all applicable Malaysian laws.
- The work authorization is not automatic — it requires a specific endorsement on the Social Visit Pass by authorized immigration officials.
- The pass holder must follow all conditions imposed by the Director General of Immigration.
""",
                }
            ),

            // Source 17: 1:3 Internship Policy Pilot Extension
            (
                new RegisterSourceDto
                {
                    Name = "1:3 Internship Policy Pilot Phase Extension",
                    Url = "https://esd.imi.gov.my/portal/latest-news/announcement/announcement-267-1-3/",
                    Department = "Ministry of Human Resources (KESUMA) / TalentCorp",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "1:3 Internship Policy — Pilot Phase Extension Notice",
                    Category = "immigration",
                    Content = """
1:3 Internship Policy — Pilot Phase Extension

Announced: 21 January 2026

The 1:3 Internship Policy was originally announced by Malaysia's Ministry of Human Resources (KESUMA) on 15 January 2025. It ties expatriate hiring to local talent development, requiring employers to provide three structured, paid internship placements through the National Structured Internship Programme (MySIP) for each expatriate they hire.

Extension Details:
- Original pilot end date: 31 December 2025
- New pilot end date: 31 March 2026

The extension gives employers more time to understand the policy, organize internship placements, and register on the MyNext platform (www.mynext.my).

TalentCorp will maintain active engagement with employers throughout the extended period, providing guidance, clarification, and hands-on support.

Full implementation takes effect on 1 June 2026.

Contact: mysip@talentcorp.com.my, www.talentcorp.com.my/1to3policy
""",
                }
            ),

            // Source 18: ESD General Information
            (
                new RegisterSourceDto
                {
                    Name = "ESD General Information and Services Overview",
                    Url = "https://esd.imi.gov.my/portal/expatriates/general-information/",
                    Department = "Expatriate Services Division, Immigration Department of Malaysia",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Expatriate Services Division (ESD) — General Information and Services",
                    Category = "immigration",
                    Content = """
Expatriate Services Division (ESD) — Immigration Department of Malaysia

Overview: The ESD serves as the initial contact point for companies seeking to hire eligible expatriates. Company registration with ESD is the mandatory first step before any expatriate hiring can proceed.

The MYXpats Centre handles the processing of individual passes.

Pass Types Available Through MYXpats:
1. Employment Pass — the primary work authorization for expatriates
   - Dependant Pass — for family members of Employment Pass holders
   - Long-Term Social Visit Pass — linked to Employment Pass holders
   - Social Visit (Temporary Employment) – Foreign Maid — domestic worker authorization tied to expatriate employment
2. Professional Visit Pass — for professional engagements

Employer Registration: Companies must register through the ESD Online portal before submitting any expatriate-related applications. An online guidebook (V6 2025, dated 14 April 2025) is available for download.

Xpats Gateway: A separate platform (xpatsgateway.com.my) operates alongside ESD Online, handling Investor Pass applications.

Revised Expatriate Salary Policy: A revised salary policy for expatriates has been implemented effective 1 June 2026. FAQ documents and the full policy text are available for download through the portal.

Section 60K Requirement: Organizations must update their ESD Online profiles to support efficient processing of JTKSM Approval Letters under Section 60K of the Employment Act 1955.

Available Resources:
- ESD Online Guidebook V6 2025 (PDF)
- Xpats Gateway User Manual (PDF)
- Revised Expatriate Salary Policy (PDF)
- Revised Expatriate Salary Policy FAQ (PDF)

Portal Tools: Check Application Status, Verify Photo Requirement, Forgot Password recovery.

Contact: MYXpats Helpdesk at helpdesk@myxpats.com.my or +603-7839 7171.
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetTaxSources()
    {
        return
        [
            (
                new RegisterSourceDto
                {
                    Name = "Individual Income Tax Overview",
                    Url = "https://www.hasil.gov.my/individu/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Individual Income Tax Overview — Malaysia",
                    Category = "tax",
                    Content = """
Individual Income Tax — Malaysia (HASiL / LHDN)

Income tax is levied annually on individuals receiving revenue-type income from these sources: business profits/gains, employment profits/gains, dividends/interest/discounts, rent/royalties/premiums, pensions/annuities/other periodic payments, and other gains. The Income Tax Act 1967 (ACP) governs administration and collection. Lembaga Hasil Dalam Negeri Malaysia (HASiL), under the Ministry of Finance, is responsible for collecting direct taxes.

Who Is Liable: Tax applies to every individual on income accruing in, derived from, or received in Malaysia from abroad for each year of assessment. Individuals whose total income exceeds the prescribed threshold must register a tax file.

Resident individuals are taxed at graduated/scaled rates and may claim deductions under sections 45A through 49 of the ACP. Non-resident individuals are taxed at a flat rate of 30% with no entitlement to deductions. Foreign-sourced income remitted into Malaysia is exempt from tax.

Employment Income Thresholds: Individuals with annual employment income exceeding RM37,333 (or those subject to Monthly Tax Deduction/PCB) are liable. Reliefs considered: Individual and dependent relief RM9,000, spouse relief (for joint assessment), child relief (under 18) RM2,000 per child. Rebates of RM400 (individual) and RM400 (spouse) apply only when chargeable income does not exceed RM35,000.

Income Thresholds by Assessment Type:
- Single/widowed/divorced/spouse with no income: RM37,333 annual (RM3,111 monthly)
- Separate assessment — married + 0-1 child: RM37,333 (RM3,111)
- Separate assessment — married + 2 children: RM41,333 (RM3,444)
- Joint assessment — married + 0 children: RM48,000 (RM4,000)
- Joint assessment — married + 1 child: RM50,000 (RM4,167)
- Joint assessment — married + 2 children: RM52,000 (RM4,333)

Self-Assessment System: Taxpayers must determine chargeable income, compute tax payable, submit the return, and pay the tax. No notice of assessment is issued; the filed return constitutes the notice.

Forms: BE — resident individuals with employment and other non-business income. B — resident individuals with business, employment, and other income. M — non-resident individuals. Residence status determined under Section 7, ACP 1967.

Final Tax (Cukai Muktamad): From Year of Assessment 2014, employees with only employment income from one employer may elect not to file a return; their PCB deductions become the final tax.

Filing: Manual submission to HASiL Records Section, or online via e-Filing on MyTax at mytax.hasil.gov.my.

Contact: HASiL Contact Centre 603-8911 1000, 9:00 AM – 5:00 PM weekdays.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Individual Income Tax Rates",
                    Url = "https://www.hasil.gov.my/individu/kadar-cukai/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Malaysian Individual Income Tax Rates",
                    Category = "tax",
                    Content = """
Malaysian Individual Income Tax Rates — Resident Individuals

Year of Assessment 2023, 2024 and 2025:

Chargeable Income (RM) — Rate — Cumulative Tax (RM):
0 – 5,000: 0% — 0
5,001 – 20,000: 1% — 150
20,001 – 35,000: 3% — 600
35,001 – 50,000: 6% — 1,500
50,001 – 70,000: 11% — 3,700
70,001 – 100,000: 19% — 9,400
100,001 – 400,000: 25% — 84,400
400,001 – 600,000: 26% — 136,400
600,001 – 2,000,000: 28% — 528,400
Exceeding 2,000,000: 30% — 528,400 + 30% on excess

Non-Resident Individuals: Taxed at a flat rate of 30% on all Malaysian-sourced income with no entitlement to personal deductions or reliefs.

Key Differences from Prior Years:
- YA 2022: 35,001–50,000 bracket was 8% (now 6%); 50,001–70,000 was 13% (now 11%); 70,001–100,000 was 21% (now 19%); bracket G was 100,001–250,000 at 24%.
- YA 2020: 50,001–70,000 bracket was 14%.
- YA 2018-2019: Top bracket was RM1,000,000 at 28%.
- YA 2016-2017: 20,001–35,000 was 5%; 35,001–50,000 was 10%; 50,001–70,000 was 16%.

Source: Lembaga Hasil Dalam Negeri Malaysia (HASiL), official portal.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Individual Tax Reliefs",
                    Url = "https://www.hasil.gov.my/individu/pelepasan-cukai/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Malaysian Individual Tax Reliefs — Year of Assessment 2025",
                    Category = "tax",
                    Content = """
Malaysian Individual Tax Reliefs — Year of Assessment 2025

1. Individual and dependants: RM9,000
2. Parents and grandparents expenses (medical, dental, special needs, care; full medical check-up sub-limit RM1,000): RM8,000
3. Basic supporting equipment for disabled self/spouse/child/parents: RM6,000
4. Disabled individual: RM7,000
5. Education fees (self): non-Masters/PhD limited to specified fields; Masters/PhD any field; upskilling sub-limit RM2,000: RM7,000
6. Medical expenses (serious diseases, fertility, vaccination sub-limit RM1,000, dental sub-limit RM1,000): RM10,000
7. Health expenses (full medical check-up, COVID-19 detection, mental health, self-health check equipment, disease detection tests; sub-limit RM1,000): RM10,000
8. Child aged 18 and below (learning disability diagnosis, early intervention/rehabilitation; sub-limit RM6,000): RM10,000
9. Lifestyle — books/journals/publications, computer/smartphone/tablet, internet, upskilling courses: RM2,500
10. Sports lifestyle — equipment, facility rental/entry, competition fees, gym membership (self/spouse/child/parents): RM1,000
11. Breastfeeding equipment (child 2 and below, once every 2 years): RM1,000
12. Childcare/kindergarten fees (child 6 and below): RM3,000
13. SSPN (National Education Savings Scheme) net savings: RM8,000
14. Spouse / alimony to former wife: RM4,000
15. Disabled spouse: RM6,000
16a. Child under 18: RM2,000
16b. Unmarried child 18+ in full-time education (A-Level/certificate/matriculation): RM2,000
16b. Unmarried child 18+ in higher education (diploma+ Malaysia, degree+ overseas): RM8,000
16c. Disabled child: RM8,000 (additional RM8,000 if 18+ in higher education)
17. Life insurance and EPF: mandatory EPF/approved scheme sub-limit RM4,000; life insurance/takaful/voluntary EPF sub-limit RM3,000: RM7,000
18. Private Retirement Scheme (PRS) and Deferred Annuity: RM3,000
19. Education and medical insurance: RM4,000
20. SOCSO contributions: RM350
21. EV charging facility and domestic food waste composting machine (non-business): RM2,500
22. Housing loan interest for first home (SPA 1 Jan 2025 – 31 Dec 2027): house price up to RM500,000 → RM7,000; RM500,001–RM750,000 → RM5,000

Key 2025 changes: Disabled individual relief increased to RM7,000; disabled child to RM8,000; disabled spouse to RM6,000; education/medical insurance raised to RM4,000; grandparents now covered; housing loan interest relief is new.

Source: LHDN/HASiL official portal.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Tax Registration and TIN for Individuals",
                    Url = "https://www.hasil.gov.my/individu/pendaftaran/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Malaysian Tax Registration and Tax Identification Number (TIN)",
                    Category = "tax",
                    Content = """
Malaysian Tax Registration and Tax Identification Number (TIN)

From 1 January 2024, individual TIN registration must be done online via the e-Daftar application on the MyTax portal at mytax.hasil.gov.my.

Automatic TIN Registration: Malaysian citizens and permanent residents aged 18 and above receive automatic TIN registration using data from the National Registration Department (JPN).

Checking Your Auto-Registered TIN: Via MyTax portal (mytax.hasil.gov.my), HASiL Contact Centre (03-89111000 domestic, 603-89111100 overseas), or any nearby HASiL office.

Online Registration (e-Daftar) for Non-Citizens:

1. Non-Citizens and Non-Permanent Residents / Foreign Individuals — File type: IG
Required documents: Valid passport copy; or related documents such as visit pass, UNHCR card, IMM13 pass, home country ID number, or other identification. For those conducting business: SSM-issued sole proprietorship/partnership registration certificate, stamped agreement or joint venture agreement, partnership agreement, or registration certificate from a recognized professional body.

2. Temporary Residents (Pemastautin Sementara) — File type: IG
Required documents: Copy of Temporary Resident Identity Card (MyKAS). Same business documents as above if applicable.

e-KYC Registration: Foreign taxpayers can also register TIN via the MyTax mobile app, verifying identity by matching NFC-enabled passport photo with a facial selfie.

Page last updated: 15 June 2026.

Contact: HASiL Contact Centre 603-8911 1000, e-Janji Temu (e-Appointment) at ejanjitemu.hasil.gov.my.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Filing Individual Income Tax (e-Filing)",
                    Url = "https://www.hasil.gov.my/individu/lapor-pendapatan/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Filing Individual Income Tax — e-Filing and Manual Submission",
                    Category = "tax",
                    Content = """
Filing Individual Income Tax — Malaysia

Two Filing Methods:

1. Manual Filing: Submit completed Borang Nyata (tax return form) by mail to Lembaga Hasil Dalam Negeri Malaysia, Seksyen Pengurusan Rekod & Maklumat Percukaian, Jabatan Operasi Cukai, Karung Berkunci 00222, 43650 Bandar Baru Bangi, Selangor. Forms and guides available at hasil.gov.my under Borang > Muat Turun > Individu.

2. Online Filing (e-Filing): Submit via the MyTax system at mytax.hasil.gov.my.

Activating a MyTax Account (First-Time Login): Select identification number type, enter number, click submit. If no digital certificate exists, register via e-CP55D (web-based) or e-KYC (facial identity verification via mobile).

Filing Deadlines:

Manual Submission:
- Borang BE (employment income): on or before 30 April each year
- Borang B (business income): on or before 30 June each year

Online Submission (e-Filing):
- Borang e-BE (employment income): on or before 15 May each year
- Borang e-B (business income): on or before 15 July each year

Forms: BE — resident individuals with employment and other non-business income. B — resident individuals with business income. M — non-resident individuals.

Page last updated: 25 May 2026.

Contact: HASiL Contact Centre 603-8911 1000.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Tax Offences, Fines and Penalties",
                    Url = "https://www.hasil.gov.my/perundangan/kesalahan-denda-dan-penalti/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Malaysian Tax Offences, Fines and Penalties",
                    Category = "tax",
                    Content = """
Malaysian Tax Offences, Fines and Penalties — Under the Income Tax Act (ACP) 1967

General Tax Offences:
- Failure to submit Income Tax Return Form (S.112(1)): RM200–RM20,000 and/or imprisonment up to 6 months
- Not declaring tax liability eligibility (S.112(1)): RM200–RM20,000 and/or imprisonment up to 6 months
- Under-reporting actual income (S.113(1)(a)): RM1,000–RM10,000 plus 200% of tax under-reported
- Providing inaccurate information (S.113(1)(b)): RM1,000–RM10,000 plus 200% of tax under-reported
- Willful tax evasion or assisting evasion (S.114(1)): RM1,000–RM20,000 and/or imprisonment up to 3 years, plus 300% of tax under-reported
- Assisting/advising in returns causing tax shortfall (S.114(1A)): RM2,000–RM20,000 and/or imprisonment up to 3 years
- Leaving country without settling tax (S.115(1)): RM200–RM20,000 and/or imprisonment up to 6 months
- Obstructing LHDN officers (S.116): RM1,000–RM10,000 and/or imprisonment up to 1 year
- Failure to maintain records (S.119A): RM300–RM10,000 and/or imprisonment up to 1 year
- Not providing requested information (S.120(1)): RM200–RM20,000 and/or imprisonment up to 6 months
- Not reporting address change within 3 months (S.120(1)): RM200–RM20,000 and/or imprisonment up to 6 months

Late Payment Penalties:
- Non-business income paid after 30 April (S.103(3)): 10% increase on tax payable
- Business income paid after 30 June (S.103(3)): 10% increase on tax payable
- Instalment payment more than 30 days late (S.107B(3)): 10% on outstanding instalment
- Actual tax exceeds revised estimate by more than 30% (S.107B(4)): 10% on the difference

Source: LHDN official portal, last updated 25 June 2026.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Tax Overpayment Refund",
                    Url = "https://www.hasil.gov.my/individu/bayaran/cukai-terlebih-bayar/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Malaysian Tax Overpayment Refund Process",
                    Category = "tax",
                    Content = """
Malaysian Tax Overpayment Refund Process

Refund Timeline:
- e-Filing submissions: Refund within 30 working days after the return is submitted
- Postal or hand-delivered submissions: Refund within 90 working days after the return is submitted

Conditions: The refund procedure depends on the taxpayer having reported correct and complete information in their tax return. Supporting documents may be requested for verification.

If You Have Not Received Your Refund: Taxpayers who have not received any response after the stated timeframes should contact the HASiL office handling their tax file.

Contact: HASiL Contact Centre 603-8911 1000, operating hours 9:00 AM – 5:00 PM Monday to Friday (excluding public holidays). Feedback via Customer Feedback Form or visit a HASiL office.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Individual Tax Rebates",
                    Url = "https://www.hasil.gov.my/individu/rebat/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Malaysian Individual Tax Rebates",
                    Category = "tax",
                    Content = """
Malaysian Individual Tax Rebates

Income Tax Rebate (Assessment Year 2009 Onwards):
Individuals whose chargeable income does not exceed RM35,000 qualify for:
- Wife: RM400
- Husband: RM400
- Total (joint assessment or separate): RM800

For Assessment Years 2001-2008: RM350 per individual (total RM700).

Other Tax Rebates:

a) Zakat/Fitrah: Maximum rebate capped at the actual tax charged.

b) Foreign Worker Levy: Capped at tax charged. No longer applicable from assessment year 2011 onward.

c) Departure Levy for Umrah or Other Religious Travel: Limited to 2 trips.
Rates by flight class and destination:
- Economy class, ASEAN: RM8
- Economy class, Non-ASEAN: RM20
- Non-Economy class, ASEAN: RM50
- Non-Economy class, Non-ASEAN: RM150

Source: LHDN/HASiL official portal.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Cessation of Employment and Tax Clearance",
                    Url = "https://www.hasil.gov.my/individu/penamatan-perkhidmatan/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Cessation of Employment and Tax Clearance (SPC) — Malaysia",
                    Category = "tax",
                    Content = """
Cessation of Employment and Tax Clearance (SPC) — Malaysia

Employer Obligations When Workers Retire or Cease Employment:
Employers must notify HASiL within 30 days before the employee's cessation date when:
1. The employee is retiring
2. The employee is subject to PCB but the employer failed to make the deductions
3. The employee is leaving Malaysia permanently

Employers must also withhold all payments due to the employee until the Tax Clearance Letter (SPC) is received from HASiL.

Exceptions — Notification and Withholding NOT Required When:
1. PCB deductions have been properly made
2. Employee's remuneration falls below PCB threshold
3. Employer knows employee will work for another employer in Malaysia

Forms:
- CP22A: Private sector employees
- CP22B: Government employees
- CP21: Employee leaving Malaysia (must be submitted at least 30 days before departure)

Employee Leaving Malaysia: Employer must complete Form CP21. Exception: If departure is frequent and routine as part of employment duties.

Penalties: Failure to notify: fine RM200–RM2,000 or imprisonment up to 6 months, or both (Section 120(1), Income Tax Act 1967). Employer becomes liable for all outstanding taxes of the employee (Section 107(4)).

Permanent File Closure Conditions:
- Retired and no longer earning taxable income
- Leaving Malaysia permanently
- No taxable income and over 55 years old
- No outstanding balance (owed or refund pending)

Application for closure must be in writing to the HASiL office with supporting documents.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Tax-Related Travel Restrictions",
                    Url = "https://www.hasil.gov.my/individu/sekatan-perjalanan/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Tax-Related Travel Restrictions — Malaysia",
                    Category = "tax",
                    Content = """
Tax-Related Travel Restrictions — Malaysia

Checking Travel Restriction Status: Via MyTax portal (mytax.hasil.gov.my) or Immigration Department website.

Payment Method: Via ByrHASiL (byrhasil.hasil.gov.my) or other accepted channels. Required information: tax reference number/IC/passport number, payment code 084/095 for income tax or 090 for real property gains tax, assessment year, installment number 99.

Full Cancellation: Pay the entire amount on the certificate/notice, either in cash or online via ByrHASiL.

Temporary Release: For those unable to settle in full:
- Visit or contact the HASiL office handling the file
- Submit written application including destination, purpose, and duration abroad
- Pay 50% of total outstanding tax upfront
- A temporary release letter will be issued specifying the approved travel period
- Payment receipts and application documents must be submitted at least 5 business days before intended travel date

Contact: HASiL Contact Centre 603-8911 1000.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "HASiL e-Services Overview",
                    Url = "https://www.hasil.gov.my/e-perkhidmatan/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "HASiL e-Services — MyTax Portal Services Overview",
                    Category = "tax",
                    Content = """
HASiL e-Services — MyTax Portal

Mandatory use of e-Services phased from 1 September 2023, fully mandatory from 1 January 2024. Portal: mytax.hasil.gov.my.

Key Mandatory e-Services:

1. e-Daftar — TIN registration for individuals (IG), companies (C), employers (E), partnerships (D), LLPs (PT), cooperatives (CS), associations (F), trust bodies (TA/TC/TR/TN). Foreign individuals register with file type IG.

2. e-KYC — Digital certificate registration via MyTax mobile app using facial identity verification. Available for both citizens and foreign taxpayers.

3. e-CP55D — Digital certificate registration via web browser on MyTax Portal.

4. e-Filing (e-Borang) — Online submission of income tax returns. Mandatory for: individuals (BE, B, BT, M, MT forms), partnerships (P), companies (C), employers (E), and other entity types.

5. e-PCB — Monthly Tax Deduction calculation and submission for employers.

6. e-SPC — Tax clearance letter applications (CP22A, CP22B, CP21) since 1 January 2024.

7. e-CP22 — New employee notification to HASiL.

Encouraged e-Services (Not Yet Mandatory):

1. ByrHASiL — Online tax payment through FPX-member banks at byrhasil.hasil.gov.my.

2. e-Billing — Check or generate Bill Number for tax payments.

3. e-TT — Tax payments via Telegraphic Transfer using Virtual Account Number.

4. e-BNT — Electronic submission of amended tax returns.

5. e-Residen — Online application for Certificate of Residence (COR) for double taxation agreement purposes.

6. e-Permohonan Pindaan BE — Apply to amend over-reported income or under-claimed relief.

7. e-Rayuan Taksiran — Online tax assessment appeals (Form Q/N).

8. e-Kompaun — Check, apply for, and pay compound penalties.

Page last updated: 8 September 2026.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Employer Cessation of Employment Notification",
                    Url = "https://www.hasil.gov.my/majikan/pemberitahuan-pemberhentian-kerja/",
                    Department = "Lembaga Hasil Dalam Negeri Malaysia (LHDN/HASiL)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Employer Notification for Cessation of Employment — Malaysia",
                    Category = "tax",
                    Content = """
Employer Notification for Cessation of Employment — Malaysia

Cessation / Termination / Death of Employee:
Forms CP22A (private sector) / CP22B (government) must be submitted at least 30 days before cessation of employment, or within 30 days after being informed of an employee's death. Since 1 January 2024, submission is via MyTax portal using e-SPC application.

Exemption: Not required if employee's income is subject to PCB or monthly remuneration is below PCB threshold.

Withholding: For non-exempt cases, employers must retain all monies payable to the departing employee for up to 90 days after HASiL receives the CP22A/CP22B.

Employee Leaving Malaysia for More Than 3 Months:
Form CP21 must be submitted at least 30 days before departure. Since 1 January 2024, filed via MyTax e-SPC.

Exemption: Not required if employee regularly leaves Malaysia in connection with employment duties.

Withholding: Employer must hold all monies for up to 90 days after receipt of CP21.

CP21 required only when employee has taxable annual income AND is leaving Malaysia for more than 3 months.

Penalties for Non-Compliance:
- Fine: RM200–RM20,000
- Imprisonment: up to 6 months
- Or both
- Employer becomes liable to pay the full tax amount owed by the employee, recoverable as a government debt through civil action.

Relevant Legal Provisions: Subsections 83(3), (4), (5); Section 106; Subsection 107(4); Subsection 120(1) of the Income Tax Act 1967.
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetDrivingSources()
    {
        return
        [
            (
                new RegisterSourceDto
                {
                    Name = "Foreign Driving Licence Conversion FAQ",
                    Url = "https://www.jpj.gov.my/faq-pertukaran-lesen-memandu-luar-negara-08092026/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Foreign Driving Licence Conversion to Malaysian Driving Licence — FAQ",
                    Category = "driving",
                    Content = """
Foreign Driving Licence Conversion to Malaysian Driving Licence (LMM) — Official FAQ

Source: Jabatan Pengangkutan Jalan Malaysia (JPJ)

1. What is conversion? Conversion is the exchange of a Foreign Driving Licence to a Malaysian Driving Licence (LMM).

2. Who is eligible to apply for conversion? The applicant must belong to one of these three categories only:
   1) Malaysian Citizens
   2) Holders of Diplomatic Identification Cards
   3) Malaysia My Second Home (MM2H) participants
   Regular expatriates, Employment Pass holders, students, and other foreigners are NOT eligible for foreign licence conversion.

3. How long does the conversion process take? 30 working days from the date of application.

4. What are the fees? Processing fee is RM20 and the licence fee is RM120 for a two (2) year Probationary Driving Licence (PDL).

5. Can I skip attending KPP01 (Driver Education Curriculum)? No. If you are required to attend KPP01, there is no exemption.

6. Where can I apply for conversion? Applications can only be made at JPJ Negeri (State JPJ offices) only. Not at branch offices.

7. Can the conversion application be done online? No.

8. Can I send a representative? No, the applicant must attend in person, except for Diplomatic Identification Card holders who may send a representative.

9. Can I apply at JPJ branch offices? No. Applications can only be made at JPJ Negeri (State offices) only.

10. Will my foreign driving licence be cancelled after conversion? This depends on the regulations of the country that issued the foreign licence. JPJ will not cancel the applicant's foreign licence.

11. Will I get a CDL (Competent Driving Licence) after conversion? The applicant will be given a PDL (Probationary Driving Licence). CDL is only given in certain cases.

12. Why am I given only PDL even though I have held a foreign licence for 20 years? Conversion is only permitted for the three eligible categories. Regardless, approved applications will be given PDL only.

13-16. Why do I need to attend KPP01? Conversion applications are divided into two categories: licences from signatory countries (1949 Convention) and non-signatory countries. Foreign licences from non-signatory countries are required to attend KPP01 after the application is approved.

17. Can I convert an International Driving Permit (IDP)? No. Conversion is only for Foreign Driving Licences (Domestic Driving License), not IDPs.

18-22. Various scenarios confirm: eligibility is strictly limited to the three categories (Malaysian citizens, diplomatic card holders, MM2H). Students from New Zealand, holders of international licences from Bangladesh, Singapore licence holders — all must meet the eligibility criteria.

Contact: JPJ Headquarters, 03-8000 8000 (Malaysian Government Call Centre).
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Foreign Licence Conversion Document Checklist",
                    Url = "https://www.jpj.gov.my/conversion-checklist/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Foreign Licence Conversion — Required Documents Checklist",
                    Category = "driving",
                    Content = """
Checklist of Documents for Conversion of Foreign Driving License to Malaysia Driving License

Effective Date: 19 May 2025
Source: JPJ (Jabatan Pengangkutan Jalan Malaysia)
All documents are valid for a period of 1 year only, unless stated.

CATEGORY 1: MALAYSIAN (EXCEPT SINGAPORE DRIVING LICENSE HOLDER)
1. Application Form (JPJL1)
2. Application Form (Appendix B2) — Kaedah 5(3) Lampiran B2
3. Malaysia Identification Card (Original & Copy)
4. Proof of existence at Driving License Issuing Country during validity of the license
5. Foreign Driving License that is valid on the day of application (Original & Copy)
6. Confirmation Letter of Driving License from Embassy / Driving License Record or Extract of Driving License issued by Transport Authority (Original)

CATEGORY 2: MALAYSIAN (SINGAPORE DRIVING LICENSE HOLDER)
1. Application Form (JPJL1)
2. Application Form (Appendix B2)
3. Malaysia Identification Card (Original & Copy)
4. If Singapore Driving License is surrendered under revocation, letter from Singapore Police Force must be presented (Original) — if applicable
5. If conversion to Singapore licence not successful and want Malaysian licence back, must present Extract of Malaysia Driving License to uplift blacklist status — if applicable
6. Singapore Driving License (Original & Copy)
7. Extract Driving License from Singapore Police Force within 6 months from issued date (Original)

CATEGORY 3: DIPLOMATIC (Non-Diplomatic Staff / Consular / International Organization)
1. Application Form (JPJL1)
2. Application Form (Appendix B2)
3. Passport (Original & Copy)
4. Pass/Visa valid at least 90 days on the day of application
5. Confirmation Letter from Ministry of Foreign Affairs of Malaysia (Original & Copy)
6. Diplomatic Identification Card (Original Copy)
7. Foreign Driving License valid on the day of application (Original & Copy)
8. Confirmation Letter of Driving License from Embassy / Driving License Record or Extract issued by Transport Authority (Original)

CATEGORY 4: MALAYSIA MY SECOND HOME (MM2H)
1. Application Form (JPJL1)
2. Application Form (Appendix B2)
3. Passport (Original & Copy)
4. Pass/Visa valid at least 90 days on the day of application
5. MM2H Confirmation Letter from Immigration Department of Malaysia — for MM2H only
6. Proof of existence at Driving License Issuing Country during validity — if the license is not from country of origin
7. Foreign Driving License valid on the day of application (Original & Copy)
8. Confirmation Letter of Driving License from Embassy / Driving License Record or Extract issued by Transport Authority (Original)
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Learner's Driving Licence (LDL) Application",
                    Url = "https://www.jpj.gov.my/permohonan-lesen-belajar-memandu-ldl/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Learner's Driving Licence (LDL) — Application Requirements",
                    Category = "driving",
                    Content = """
Learner's Driving Licence (LDL / Lesen Belajar Memandu) — JPJ Malaysia

Where to Apply: Any JPJ office or eKhidmat kiosks.

Age Eligibility:
- Over 16 years old: Classes A, B2, B, and C
- Over 17 years old: Classes A1, D, and DA
- Over 21 years old: Classes E, F, G, H, I, and vocational licences (except CON which requires age 18)

Prerequisites: Must have passed the Part 1 law test (ujian undang-undang Bahagian 1), with results updated and still valid within a one-year period.

Required Documents:
1. Original MyKad or passport; if a representative handles the transaction, a copy of the applicant's MyKad/passport is needed
2. Original identification of the representative
3. One colour photo (white background), sized 25mm x 32mm
4. Foreign nationals must hold a valid passport

Fees:
- Classes B & C: RM20.00 (3 months) / RM40.00 (6 months)
- Classes A/A1/B2 (single): RM2.00 (3 months)
- Class D and above: RM30.00 (3 months) / RM60.00 (6 months)

Validity and Renewal: The LDL can be issued and renewed for periods of 3 or 6 months, up to a maximum total of 2 years from the original issue date.

Conditions: Licence class amendments are limited to one time only. The applicant must finalize their chosen licence class before the LDL is issued.

Malaysian Driving Licence Classes:
- Class A: Motorcycle without engine capacity limit
- Class A1: Motorcycle not exceeding 500cc
- Class B: Motor car not exceeding 3,500 kg
- Class B2: Motor car not exceeding 3,500 kg (manual transmission restriction removed after passing)
- Class C: Motor car between 3,500 kg and 7,500 kg
- Class D: Motor car exceeding 7,500 kg
- Class DA: Motor car exceeding 7,500 kg with trailer
- Class E: Omnibus
- Class F: Taxi/e-Hailing
- Class G: Self-propelled machinery
- Class H: Construction/farm equipment
- Class I: Special purpose vehicle
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Probationary Driving Licence (PDL) Application",
                    Url = "https://www.jpj.gov.my/permohonan-lesen-memandu-percubaan-pdl/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Probationary Driving Licence (PDL) — Application Requirements",
                    Category = "driving",
                    Content = """
Probationary Driving Licence (PDL / Lesen Memandu Percubaan) — JPJ Malaysia

Where to Apply: Any JPJ state or branch office.

Eligibility:
1. Must have passed the practical driving test Parts 2 and 3 (ujian amali bahagian 2 & 3), with results updated and still valid within a one-year period.
2. Must not be blacklisted.
3. The probationary period lasts 2 years for the earliest class obtained.

Required Documents:
1. Original MyKad or passport (or a copy if a representative handles the transaction)
2. Original identification of the representative (if applicable)
3. One colour photo with white background, 25mm x 32mm (optional)
4. Foreign nationals: a valid passport is required

Fees:
Citizens:
- Classes A/A1: RM2.00 (2 years)
- Classes B2 (single): RM2.00 (2 years)
- Classes B/C: RM40.00 (2 years)
- Classes D/DA/F/G/H/I: RM60.00 (2 years)
Non-Citizens:
- All applicable classes: RM120.00 (2 years)

P Licence Rules: PDL holders must display the P plate. The licence is valid for 2 years. PDL holders are NOT eligible to apply for an International Driving Permit (IDP). PDL holders are NOT eligible for Public Service Vehicle (PSV) or vocational licences.

Conversion to CDL: The P licence can be converted to CDL within 7 days before the probationary period expires. If processed in that 7-day window, the CDL issued is valid for one year only.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Competent Driving Licence (CDL) Application",
                    Url = "https://www.jpj.gov.my/permohonan-lesen-memandu-kompeten-cdl/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Competent Driving Licence (CDL) — Application Requirements",
                    Category = "driving",
                    Content = """
Competent Driving Licence (CDL / Lesen Memandu Kompeten) — JPJ Malaysia

Where to Apply: Any JPJ state or branch office.

Eligibility:
1. Must apply within 7 days before the probationary period expires.
2. Application window is one year from PDL expiry date.
3. Must not be blacklisted.

Required Documents:
1. Original MyKad or passport (or copy if representative)
2. Representative's original identification
3. One colour photo (white background), 25mm x 32mm (optional)
4. Foreign nationals must hold a valid passport
5. Payment

Fees:
Citizens:
- Classes B2, B & C: RM20.00 per year (Classes A/A1: RM2.00)
- Class D and above: RM30.00 per year
Non-Citizens:
- All classes (B2, B, C, D, DA, F, G, H, I): RM120.00

CDL Validity: 3 years for standard renewal. Can be renewed for 1-10 years (citizens/PR) or 1-5 years (non-citizens).
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "CDL Renewal",
                    Url = "https://www.jpj.gov.my/pembaharuan-lesen-memandu-kompeten-cdl/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Competent Driving Licence (CDL) Renewal",
                    Category = "driving",
                    Content = """
Competent Driving Licence (CDL) Renewal — JPJ Malaysia

Where to Renew: JPJ State/Branch offices, UTC centers, 1JPJ Counters, eKhidmat kiosks, and Pos Malaysia (PMB) outlets.

Renewal Period:
- Malaysian Citizens and Permanent Residents (MyPR): Can renew for 1 through 10 years.
- Non-Citizens (MyKAS holders and Passport holders): Can renew for 1 through 5 years only.
- Must not be blacklisted.

Required Documents:
1. Original MyKad or passport (or copy if representative)
2. Representative's original identification
3. Original or copy of the driving license
4. One colour photo (white background), 25mm x 32mm
5. Foreign nationals must hold a valid passport

Fees:
Citizens:
- Classes B2, B & C: RM20.00 per year (Classes A/A1: RM2.00 per year)
- Class D and above: RM30.00 per year
Non-Citizens:
- All classes: RM120.00 (flat rate)

Note: If a licence has been unrenewed, the P licence validity is one year and CDL validity is three years. Beyond that period, an appeal process is required.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "International Driving Permit (IDP) Application",
                    Url = "https://www.jpj.gov.my/permohonan-permit-memandu-antarabangsa/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "International Driving Permit (IDP) — Malaysia",
                    Category = "driving",
                    Content = """
International Driving Permit (IDP) — JPJ Malaysia

Where to Apply: Any JPJ state or branch office. In-person only.

Eligibility:
1. Must hold a Competent Driving License (CDL) that is still valid for more than 1 year.
2. Must not be blacklisted.
3. Must be aged 18 or above.
4. Probationary Licence (PDL) holders are NOT eligible for an IDP.

Validity: 1 year only.

Required Documents:
1. Original identification document and/or copy
2. Original ID of representative (if applicable)
3. Competent Driving License (CDL)
4. One colour passport-sized photograph

Fee: RM150.00 (same for citizens and non-citizens).

Important: An IDP cannot be used to apply for foreign licence conversion in Malaysia. Conversion is only for domestic foreign driving licences.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "Driving Licence Transaction Fee Rates",
                    Url = "https://www.jpj.gov.my/pusat-media-3/informasi-perkhidmatan-jpj/kadar-bayaran-urusniaga-lesen-memandu-2/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Malaysian Driving Licence Transaction Fee Rates — Complete Schedule",
                    Category = "driving",
                    Content = """
Malaysian Driving Licence Transaction Fee Rates — JPJ Official

LEARNER'S DRIVING LICENCE (LDL):
Class A: Citizen RM2.00 (3 months)
Class B2 (single): Citizen RM2.00/RM4.00 (3/6 months), Non-Citizen same
Class B/C: Citizen RM20.00/RM40.00 (3/6 months), Non-Citizen same
Class D and above: Citizen RM30.00/RM60.00 (3/6 months), Non-Citizen same

PROBATIONARY DRIVING LICENCE (PDL) — 2 years:
Class A/A1: Citizen RM2.00
Class B2 (single): Citizen RM2.00, Non-Citizen RM120.00
Class B/C: Citizen RM40.00, Non-Citizen RM120.00
Class D/DA/F/G/H/I: Citizen RM60.00, Non-Citizen RM120.00

COMPETENT DRIVING LICENCE (CDL) — per year:
Class A/A1: Citizen RM2.00
Class B2 (single)/B/C: Citizen RM20.00, Non-Citizen RM60.00
Class D/E/F/G/H/I: Citizen RM30.00, Non-Citizen RM60.00

OTHER TRANSACTIONS:
Vocational Licence (GDL/PSV/CON): Citizen RM20.00 per year
International Driving Permit (IDP): RM150.00 (citizen and non-citizen)
Duplicate licence copy: RM20.00
Licence information extract: RM10.00
Licence information verification: RM10.00
Add licence class: RM5.00

Key Notes:
- Non-citizen PDL fees are flat RM120.00 regardless of class.
- Non-citizen CDL fees are RM60.00 per year.
- IDP is the highest single fee at RM150.00.
- Class A/A1 citizen fees are RM2.00 across all licence stages.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "JPJ Driving Licence General FAQ",
                    Url = "https://www.jpj.gov.my/soalan-lazim-lesen-memandu/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "JPJ Driving Licence — Frequently Asked Questions",
                    Category = "driving",
                    Content = """
JPJ Driving Licence — Frequently Asked Questions

Can a Probationary Licence (P) convert to CDL early? Conversion is only permitted within 7 days before the probationary period expires. If processed in that 7-day window, the CDL issued is valid for one year only.

Can P licence holders apply for an International Driving Permit (IDP)? No. Only holders of a Competent Driving Licence (CDL) aged 18 and above qualify. PDL holders are not eligible.

What is the minimum age for a Goods Driving Licence (GDL)? Under the Road Transport Act (APJ) 1987, the age requirement is 21 years and above.

Can an expired LDL (over 2 years) be renewed for the practical test? A Learner Driving Licence (LDL) may be renewed for 3 or 6 months but cannot exceed a total aggregate of 2 years from the date of issuance. Beyond that, candidates must repeat all training and tests.

How long before an unrenewed licence expires? P Licence validity is one year; CDL validity is three years.

Can a P licence holder apply for a PSV licence? Under APJ 1987, PDL holders are ineligible. Applicants must hold a CDL, be at least 21 years old, and have satisfactory health.

Does JPJ set driving school prices? No. JPJ does not fix prices for learning to drive. Candidates should compare prices and quality across institutes.

How do persons with disabilities (OKU) obtain a licence? Requirements are the same as standard applicants. OKU candidates may use modified vehicles during the practical test.

Vocational Licence Restructuring (since 15 March 2013): PSV, GDL, and Conductor licences consolidated into a single vocational licence form. Fee is RM20 per year. Expiry date aligned with holder's birthday.

JPJ operates under: Road Transport Act 1987 (Act 333), Road Transport Rules (Act 334), Land Public Transport Act 2010 (Act 715).

Contact: JPJ Headquarters, Levels 3-5, No. 26, Jalan Tun Hussien, Precinct 4, 62100 Putrajaya. Phone: 03-8000 8000.
""",
                }
            ),

            (
                new RegisterSourceDto
                {
                    Name = "JPJ Services Overview",
                    Url = "https://www.jpj.gov.my/pusat-media-3/informasi-perkhidmatan-jpj/",
                    Department = "Jabatan Pengangkutan Jalan Malaysia (JPJ)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "JPJ Services Overview — Driver and Vehicle Licensing",
                    Category = "driving",
                    Content = """
JPJ (Jabatan Pengangkutan Jalan Malaysia) — Services Overview

JPJ services are organized into 5 categories:

1. DRIVER LICENSING (Pelesenan Pemandu):
- Learner's Driving Licence (LDL): application, renewal, adding classes
- Probationary Driving Licence (PDL): application, adding classes
- Competent Driving Licence (CDL): application, renewal, adding classes
- Vocational Licence (GDL, PSV, Conductor): application, renewal, adding classes
- International Driving Permit (IDP): application
- Foreign Licence Conversion: checklist and procedure
- OKU (Disabled Persons) Driving Licence: Class A and A1
- Driving Test Booking: Parts II and III
- Expired Licence Appeal
- Class B2 Licence Fee Schedule

2. VEHICLE LICENSING (Pelesenan Kenderaan):
- Motor Vehicle Licence (LKM) renewal
- Vehicle registration, ownership transfer
- Foreign vehicle ICP permit
- Number plate specifications and booking
- Commercial vehicle licensing

3. ENFORCEMENT (Penguatkuasa):
- Traffic offence compound rates
- JPJ summons payment
- Vehicle seizure/reclaim procedures
- KEJARA demerit points system
- Seatbelt requirements
- Tinted glass approval
- VEP and Road Charge (RC) system for foreign vehicles

4. AUTOMOTIVE ENGINEERING:
- Vehicle modification guidelines
- ICE to EV conversion guidelines
- Vehicle Type Approval (VTA)
- Child Restraint System (CRS) requirements

5. GENERAL:
- Counter transaction charges at JPJ state/branch/UTC offices

Contact: JPJ HQ, Levels 3-5, No. 26, Jalan Tun Hussien, Precinct 4, 62100 Putrajaya. Phone: 03-8000 8000. Email: binajpj@jpj.gov.my. Complaints: jpj.spab.gov.my.
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetEmploymentSources()
    {
        return
        [
            // Source 1: JTKSM Services Overview
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Services Overview",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "JTKSM — Department of Labour Peninsular Malaysia: Services Overview",
                    Category = "employment",
                    Content = """
Department of Labour Peninsular Malaysia (JTKSM) — Services Overview

JTKSM (Jabatan Tenaga Kerja Semenanjung Malaysia) is the official federal body responsible for labour matters in Peninsular Malaysia. It operates under the Ministry of Human Resources (Kementerian Sumber Manusia / MOHR). JTKSM covers Peninsular Malaysia only. Sabah and Sarawak have their own separate labour departments (JTK Sabah, JTK Sarawak).

Full list of JTKSM services as listed on the portal:

1. Pendaftaran Tempat Pekerjaan (Workplace Registration) — Section 63A, Employment Act 1955. Employers must register within 90 days of commencing, acquiring, or changing business details.

2. Permit Perburuhan (Labour Permits) — Exemptions from certain Employment Act provisions, where worker welfare is not compromised. Supports industry flexibility.

3. Perumahan, Penginapan dan Kemudahan Pekerja (Worker Housing and Amenities) — Act 446. Employer obligations for worker accommodation.

4. Penggajian Pekerja Asing (Foreign Worker Employment) — JTKSM's role in overseeing foreign worker employment and Section 60K approval.

5. Pemberhentian Pekerja (Retrenchment) — Section 63 Employment Act. Employers must file Borang PK at least 30 days before retrenchment action.

6. Agensi Pekerjaan Swasta (Private Employment Agencies) — Act 246. Licensing and oversight of agencies placing workers domestically and overseas.

7. Kes Buruh (Labour Cases) — Section 69 Employment Act 1955. The Labour Court handles monetary and other claims between workers and employers.

8. Aduan (Complaints) — Investigation of employer-employee disputes. Workers file complaints at the nearest Pejabat Tenaga Kerja (PTK, Labour Office) or online via SisPAA.

9. Instrumen Pembayaran Gaji (Wage Payment Instruments) — Wages must be paid through regulated financial institutions under the Employment Act.

10. Penguatkuasaan Akta Pekerja Gig 2025 (Gig Workers Act 2025 Enforcement) — Covering gig/platform workers.

Contact: JTKSM HQ, Aras 5, Blok Setia Perkasa 3, Kompleks Setia Perkasa, Putrajaya. Phone: 603-8000 8000 / 03-8888 9111. Website: jtksm.mohr.gov.my.
""",
                }
            ),

            // Source 2: Labour Court / Labour Cases
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Labour Court — Labour Cases (Kes Buruh)",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/kes-buruh",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Labour Court — How to File and Resolve Employment Disputes in Malaysia",
                    Category = "employment",
                    Content = """
Labour Court (Mahkamah Buruh) — Malaysia (JTKSM)

The Labour Court operates under Section 69 of the Employment Act 1955 [Act 265]. It handles monetary and other claims arising from employment disputes between workers and employers.

Workers can also file claims under Section 69F of Act 265 regarding employment discrimination.

Who can file (based on the First Schedule of Act 265):
Workers: Wage claims, and any other payments the worker is entitled to under their service contract or under any legislation.
Employers: Payment in lieu of notice (Gaji ganti Notis).

Important earnings limit:
Workers earning more than RM4,000 per month (excluding manual workers regardless of salary) CANNOT claim overtime pay, rest day pay, public holiday pay, shift allowance, or termination benefits through the Labour Court. Those workers must use the Industrial Relations mechanism instead.

Contact: Nearest Pejabat Tenaga Kerja (PTK) to your workplace. JTKSM HQ: 03-8888 9111.
""",
                }
            ),

            // Source 3: Labour Court FAQ
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Labour Cases FAQ",
                    Url = "http://jtksm.mohr.gov.my/ms/soalan-lazim/soalan-lazim-kes-buruh",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Labour Cases FAQ — JTKSM Malaysia",
                    Category = "employment",
                    Content = """
Labour Cases — Frequently Asked Questions (FAQ) — JTKSM Malaysia

Q: Who can file a labour case?
A: Either the employee or the employer.

Q: What types of claims can an employee make?
A: (1) Monetary claims under the service contract (wages, allowances); (2) monetary claims under the Employment Act 1955 (maternity allowance, notice pay, annual leave, overtime); (3) claims under the Wages Council Act 1947 for minimum wage disputes; (4) appeals against the employer's internal investigation/domestic inquiry decision.

Q: What claims can an employer make?
A: Salary in lieu of notice when an employee fails to give the required notice before leaving employment.

Q: Are there salary limits that affect eligibility?
A: Yes. Workers earning more than RM4,000 per month (excluding manual workers) CANNOT claim overtime pay, rest day pay, public holiday pay, shift allowance, or termination benefits in the Labour Court. These workers must use Industrial Relations mechanisms. Manual workers retain Labour Court access regardless of salary level.

Q: Is there a fee to file a claim?
A: No — filing a labour claim is completely free.

Q: Where do I file?
A: At the Pejabat Tenaga Kerja (PTK, Labour Office) nearest to the workplace — not to where the employee or employer lives.

Q: What documents do I need?
A: Identity card or passport; appointment letter or employment contract; recent payslips; termination letter if applicable. Employer representatives need a power of attorney letter.

Q: What happens after I file?
A: The complainant gives a statement at the PTK. A mention date is set. Both parties attend a discussion; if settlement is reached, a Consent Order is issued. If not, hearing dates are scheduled until a ruling is made.

Q: Can a case be withdrawn?
A: Yes, at any time by submitting a withdrawal letter to the handling officer.

Q: What are the possible outcomes?
A: (1) Order for employer to pay employee; (2) order for employee to pay notice compensation to employer; (3) case dismissed; (4) case cancelled (if withdrawn or complainant absent).

Q: Can I appeal?
A: Yes. Either party may appeal to the High Court within 14 days of the order date.
""",
                }
            ),

            // Source 4: Worker Complaints (Aduan)
            // DIRECT_OFFICIAL — verified accessible 2026-09-15 — cleaned to page content only
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Worker Complaints",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/aduan",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "How to File a Worker Complaint Against an Employer in Malaysia",
                    Category = "employment",
                    Content = """
Filing a Worker Complaint (Aduan Pekerja) — JTKSM Malaysia

JTKSM investigates labour disputes between employees and employers to ensure compliance with enforced legislation.

How to submit a complaint:
1. By letter (surat).
2. By email (e-mel) to JTKSM.
3. Online via SisPAA (Sistem Pengurusan Aduan Awam).
4. Via the Working for Workers (WFW) mobile application.
5. In person at the nearest Pejabat Tenaga Kerja (PTK, Labour Office).

Required information and documents:
1. Complete details of the complainant.
2. Complete details of the party complained against.
3. Complaint issues.
4. Supporting documents: appointment letter or service contract, and latest salary statements.

Contact:
JTKSM HQ: Aras 5, Blok Setia Perkasa 3, Kompleks Setia Perkasa, Putrajaya.
Phone: 603-8000 8000 / 03-8888 9111.
""",
                }
            ),

            // Source 5: Retrenchment
            // DIRECT_OFFICIAL — verified accessible 2026-09-15 — cleaned to page content only
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Employee Retrenchment Procedures",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/pemberhentian-pekerja",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Retrenchment, Redundancy and Layoff Procedures in Malaysia",
                    Category = "employment",
                    Content = """
Retrenchment and Layoff Procedures — Malaysia (Employment Act 1955, Section 63)

Under Section 63 of the Employment Act 1955 and the Pemberitahuan Pemberhentian Pekerja 2004 (Retrenchment Notification 2004), employers must notify JTKSM before conducting any of the following actions:

Actions requiring Borang PK notification:
1. Permanent retrenchment (Retrenchment).
2. Separation schemes (Separation Scheme / VSS).
3. Temporary layoff (Lay-Off).
4. Salary reduction (Pay-Cut).

Borang PK submission deadlines:
- Parts I–IV + Appendix 1: At least 30 days BEFORE the intended action.
- Part V + Appendix 1: Within 14 days AFTER the action takes place.
- Part VI: Within 30 days AFTER the action takes place.
- Parts V and VI are only required for permanent retrenchment and separation schemes.

The Borang PK must be submitted to the nearest Pejabat Tenaga Kerja (PTK, Labour Office).

Contact: JTKSM HQ, 03-8888 9111.
""",
                }
            ),

            // Source 6: Workplace Registration
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Workplace Registration (Section 63A)",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/pendaftaran-tempat-pekerjaan",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Employer Workplace Registration Obligation — Employment Act Section 63A",
                    Category = "employment",
                    Content = """
Workplace Registration — Employment Act 1955, Section 63A

All employers must report and submit information about their place of employment under Section 63A of the Employment Act 1955.

Employers must submit to the nearest Labour Office (PTK) within 90 days from the date of:
1. Commencement of operations/business.
2. Business takeover.
3. Change of business name or location.

Contact: Nearest Pejabat Tenaga Kerja (PTK). JTKSM HQ: Aras 5, Blok Setia Perkasa 3, Kompleks Setia Perkasa, Putrajaya. Phone: 03-8888 9111.
""",
                }
            ),

            // Source 7: Worker Housing and Amenities Act 446
            // DIRECT_OFFICIAL — verified accessible 2026-09-15 — cleaned to page content only
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Worker Housing and Amenities (Act 446)",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/perumahan-penginapan-dan-kemudahan-pekerja",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Employer Obligations for Worker Housing — Employees' Minimum Standards Act 1990",
                    Category = "employment",
                    Content = """
Worker Housing, Accommodation and Amenities — Malaysia (Act 446)

The Employees' Minimum Standards of Housing, Accommodations and Amenities Act 1990 (Act 446) sets minimum requirements for worker housing and facilities.

Geographic scope:
Act 446 applies to Peninsular Malaysia and the Federal Territory of Labuan only. Sabah and Sarawak have separate legislation.

Scope of coverage:
- Parts II & III (housing and amenities): Apply to workplaces outside city council / municipal council boundaries. This includes estates (agricultural land of 20 hectares or more) and any mines.
- Part IIIA (accommodation): Applies to all workers except estate workers.

Contact: JTKSM HQ, 03-8888 9111. jtksm.mohr.gov.my.
""",
                }
            ),

            // Source 8: Wage Payment Instruments
            // DIRECT_OFFICIAL — verified accessible 2026-09-15 — cleaned to page content only
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Wage Payment Instruments",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/instrumen-pembayaran-gaji",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Legal Wage Payment Methods in Malaysia — Employment Act Requirements",
                    Category = "employment",
                    Content = """
Wage Payment Instruments — Malaysia (Employment Act 1955)

Under the Employment Act 1955 [Act 265], the Minister of Human Resources can approve and gazette issuers of payment instruments (or Islamic payment instruments) recognised for the purpose of paying employee wages.

Approved wage payment channels must meet the definition of "financial institution":
1. A licensed bank and approved issuer of a designated payment instrument under the Financial Services Act 2013.
2. A licensed Islamic bank and approved issuer of a designated Islamic payment instrument under the Islamic Financial Services Act 2013.
3. A prescribed institution under the Development Financial Institutions Act 2002.

Companies wishing to apply as wage payment instrument issuers must submit a written application to the Ministry of Human Resources.

The list of recognised issuers is available on the JTKSM website.

Contact: JTKSM HQ, 03-8888 9111.
""",
                }
            ),

            // Source 8b: Wage Payment Instruments FAQ
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Wage Payment Instruments FAQ",
                    Url = "http://jtksm.mohr.gov.my/ms/soalan-lazim/soalan-lazim-instrumen-pembayaran-gaji",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Wage Payment Instruments FAQ — JTKSM Malaysia",
                    Category = "employment",
                    Content = """
Wage Payment Instruments — FAQ — JTKSM Malaysia

Q: Can employers pay wages through payment instrument issuers?
A: Yes, employers may pay through issuers recognised by the Minister of Human Resources under section 25 of the Employment Act 1955, effective 1 August 2024.

Q: Does this apply to all workers?
A: No. Payment via recognised instrument issuers is only permitted for foreign workers (including domestic workers and expatriates).

Q: What conditions apply to recognised issuers?
A: (i) Obtaining written consent from each foreign worker; (ii) informing workers of terms, conditions, and costs; (iii) maximum fees as set by Bank Negara Malaysia only; (iv) automatic revocation if BNM withdraws approval.

Q: Must employers get JTKSM approval first?
A: No JTKSM approval is needed, but the issuer must obtain written consent from the worker.

Q: Can foreign workers choose which recognised issuer to use?
A: The selection is at the employer's discretion.

Q: Can workers withdraw cash?
A: Yes, all recognised issuers provide ATM cash withdrawal facilities.

Q: Does this apply to Indonesian domestic workers?
A: Indonesian domestic workers' employers are bound to pay via bank per the pledge for employing foreign domestic workers.

Q: Can workers request cash payment?
A: Workers may apply in writing for cash payment under section 25A of the Act, but it is subject to employer discretion and written permission from the Director General.

Q: What if an employer uses an unrecognised issuer?
A: Using an unrecognised payment instrument issuer is an offence, punishable by a fine not exceeding RM50,000.

Contact: JTKSM HQ, 03-8888 9111.
""",
                }
            ),

            // Source 9: Section 60K — Foreign Worker Employment (REPLACED)
            // DIRECT_OFFICIAL — verified at corrected URL 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "Section 60K Employment Act — Foreign Worker Employment Approval",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/penggajian-pekerja-asing-0",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Section 60K Employment Act 1955 — JTKSM Approval for Foreign Worker Employment",
                    Category = "employment",
                    Content = """
Section 60K of the Employment Act 1955 — Foreign Worker Employment Approval (JTKSM)

Section 60K of the Employment Act 1955, effective 1 January 2023, requires employers to obtain prior approval from JTKSM before hiring any foreign worker. "Foreign worker" in this context refers to all non-citizen workers as defined under Section 2 of Act 265.

Key rules:
- Applies to new recruitment of non-citizen workers.
- Does NOT apply to renewal of Employment Passes or Temporary Work Visit Passes (PLKS).
- Mandatory for ALL employers with no exemptions.
- Approval is granted to the employer.

Six categories of foreign workers:

Category 1: Foreign workers under PLKS via new/regular quota. Applications through the FWeApproval module in FWCMS. Open to all permitted sectors.

Category 2: PLKS workers via employer transfer. Applications through the ePPAx system. Cross-sector transfers are permitted.

Category 3: Employment Pass holders (expatriates). Applications via Xpats Gateway. Also usable for Professional Visit Passes (PVP).

Category 4: Foreign domestic workers. Indonesian domestic workers must use licensed Private Employment Agencies (Licence B or C). Other nationalities can apply through agencies or directly.

Category 5: Covers foreign fishermen, foreign security guards, ship crew, Resident Pass holders, Talent Resident Pass holders, Student Pass holders, and Professional Passes. Filed via ePPAx.

Category 6: Thai nationals / Tom Yam cooks under PLKS. Filed via ePPAx.

Since December 2024, reporting of employment and termination of foreign workers must be done online via ePPAx, replacing the old PA 1/13 and PA 2/13 paper forms.

Workers from shelter homes require applications through the National Strategic Office (NSO) under MAPO (anti-trafficking council).

JTKSM currently only provides demand letter attestation for Nepali workers under an active MOU.

Contact: JTKSM HQ, 03-8888 9111. ePPAx Help Desk: bpajtksm@mohr.gov.my.
""",
                }
            ),

            // Source 10: PERKESO/SOCSO — Employee Social Security
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO SOCSO — Employee Social Security Malaysia",
                    Url = "https://perkeso.gov.my/en/",
                    Department = "Pertubuhan Keselamatan Sosial (PERKESO) / Social Security Organisation (SOCSO)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "SOCSO/PERKESO — Employee Social Security Coverage in Malaysia",
                    Category = "employment",
                    Content = """
PERKESO (Social Security Organisation / SOCSO) — Malaysia

PERKESO is the statutory body administering social security protection for workers in Malaysia.

Protection schemes (as listed on the PERKESO portal):

LINDUNG PEKERJA: For employed workers, foreign workers, domestic workers. Covers Employment Injury Scheme and Invalidity Scheme.

LINDUNG KERJAYA (Employment Insurance System — SIP): For workers who lose their jobs. Provides income replacement and re-employment assistance.

LINDUNG KENDIRI: For self-employed persons — voluntary scheme.

LINDUNG KASIH: For housewives — domestic accident protection.

Foreign worker coverage: Employers are responsible for registering their foreign workers and must ensure that mandatory contributions are paid in full based on specified contribution rates.

Online portals:
- ASSIST Portal (assist.perkeso.gov.my) — employer registration and contributions
- LINDUNG Kerjaya Portal (lindungkerjaya.perkeso.gov.my) — job loss insurance claims
- LINDUNG FAEDAH Portal (lindungfaedah.perkeso.gov.my) — invalidity/survivors' pensions
- Prihatin Portal — self-employment
- MYFutureJobs (myfuturejobs.gov.my) — job matching

Contribution rate ceiling: RM6,000/month (effective 1 October 2024).

Contact: 1-300-22-8000 / +603 4264 5000. Menara PERKESO, 281 Jalan Ampang, 50538 Kuala Lumpur.
""",
                }
            ),

            // Source 15: Private Employment Agencies
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Private Employment Agencies (Act 246)",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/agensi-pekerjaan-swasta",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Private Employment Agencies in Malaysia — Licensing and Regulations",
                    Category = "employment",
                    Content = """
Private Employment Agencies — Malaysia (Private Employment Agencies Act 1981, Act 246)

JTKSM licenses and regulates private employment agencies under the Private Employment Agencies Act 1981 (Act 246). All employment agencies operating in Peninsular Malaysia must hold a valid JTKSM licence.

Licence categories:

Licence A:
- Scope: Domestic placements only (placing workers within Malaysia).
- Minimum paid-up capital: RM50,000.
- Performance bond: RM5,000.

Licence B:
- Scope: Domestic placements, overseas placements, and placement of foreign domestic workers.
- Minimum paid-up capital: RM100,000.
- Performance bond: RM100,000.

Licence C:
- Scope: Domestic placements, overseas placements, and placement of non-citizen workers (broad category).
- Minimum paid-up capital: RM250,000.
- Performance bond: RM250,000.

Contact: JTKSM HQ, 03-8888 9111.
""",
                }
            ),

            // Source 17: Industrial Relations — Unfair Dismissal (REPLACED)
            // DIRECT_OFFICIAL — verified at corrected URL 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JPPM — Unfair Dismissal Representation (Section 20 IRA 1967)",
                    Url = "https://jpp.mohr.gov.my/ems-pembuangan-kerja-seksyen-20/",
                    Department = "Jabatan Perhubungan Perusahaan Malaysia (JPPM) / Department of Industrial Relations",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Unfair Dismissal — Section 20 Industrial Relations Act 1967 — JPPM Malaysia",
                    Category = "employment",
                    Content = """
Unfair Dismissal Representation — Section 20, Industrial Relations Act 1967 (JPPM)

Under Section 20 of the Industrial Relations Act (IRA) 1967, a worker who believes their dismissal was unjust and lacked reasonable cause may submit a written representation to the Director General of Industrial Relations.

Time limit: Per Section 20(1A) of the IRA 1967, the representation must be submitted within 60 days after the date of dismissal. It can also be filed during the notice period of dismissal.

Representations may be filed online through the IRIS system or at the nearest JPPM office.

Process:

1. Settlement through ADR (Conciliation): JPPM employs an Alternative Disputes Resolution approach on a win-win principle. A conciliation meeting date is set, and both employer and worker attend in person. Online conciliation may also be conducted. If both parties reach a settlement, JPPM prepares a Memorandum of Agreement (MOA) signed by both parties. A case may be deemed withdrawn if the worker fails to attend three scheduled conciliation sessions.

2. Referral by the Director General: If no settlement is possible, the representation is referred to the Industrial Court for an award.

3. Arbitration by the Industrial Court: Cases referred by the Director General are heard and decided by the Industrial Court of Malaysia.

4. Industrial Court Award: The Industrial Court's decision is an award that is binding on both disputing parties.

Representation rules during JPPM conciliation:
- Workers and employers may represent themselves, or employers may be represented by an authorised company officer.
- Parties may be represented by a trade union or employers' union officer.
- Any officer of a registered employers'/workers' organisation in Malaysia may represent a party.
- Any person appointed by the worker/employer other than an advocate and solicitor, in writing, subject to approval from the Director General.

Advocates/lawyers are NOT permitted to represent workers or employers in conciliation proceedings at JPPM.

Contact: JPPM — (603) 8886 5434. jppm@mohr.gov.my. Level 9, Setia Perkasa Block 4, Kompleks Setia Perkasa, 62530 Putrajaya.
""",
                }
            ),

            // Source 19: Employment Insurance System (SIP) (REPLACED)
            // DIRECT_OFFICIAL — verified at corrected URL 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO Employment Insurance System (SIP / LINDUNG KERJAYA)",
                    Url = "https://perkeso.gov.my/en/our-services/protection/employment-insurance.html",
                    Department = "Pertubuhan Keselamatan Sosial (PERKESO) / Social Security Organisation (SOCSO)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Employment Insurance System (SIP / LINDUNG KERJAYA) — PERKESO Malaysia",
                    Category = "employment",
                    Content = """
Employment Insurance System (SIP) / LINDUNG KERJAYA — PERKESO Malaysia

The Employment Insurance System (Sistem Insurans Pekerjaan, SIP), branded as LINDUNG KERJAYA, was launched 1 January 2018 under PERKESO. It provides income replacement for insured persons who have lost their jobs.

Eligibility:
1. Must apply within 60 days from the date of loss of employment.
2. Must meet the Contributions Qualifying Conditions (CQC) — minimum number of months of contributions.
3. Loss of employment must fall within the EIS Act's definition.

What counts as loss of employment:
- Normal retrenchment
- VSS/MSS (voluntary or mutual separation schemes)
- Workplace closure due to natural disasters (force majeure)
- Bankruptcy or business closure
- Constructive dismissal
- Resignation due to sexual harassment or workplace threats
- Resignation after being ordered to perform dangerous duties outside the job scope

When SIP benefits are NOT payable:
- Voluntary resignation
- Dismissal due to misconduct
- Retirement
- Mutual agreement termination (unless evidence of constructive dismissal)

Benefits:
1. Job Search Allowance (JSA): Income replacement for those who lost their sole source of income. Paid for 3–6 months depending on contribution history.
2. Reduced Income Allowance (RIA): For those with multiple income sources who lost one or more jobs. Paid as a lump sum.
3. Early Re-Employment Allowance (ERA): 25% of remaining unpaid JSA, paid as a lump sum if employment is secured while still receiving JSA.
4. Training Fee: Up to RM4,000 for approved vocational skills training.
5. Training Allowance: RM10–20/day based on previous assumed salary.

How to claim:
Apply online via LINDUNG Kerjaya Portal at lindungkerjaya.perkeso.gov.my or at a PERKESO office.

Contact: PERKESO — 1-300-22-8000. Menara PERKESO, 281 Jalan Ampang, 50538 Kuala Lumpur.
""",
                }
            ),

            // Source 13: PERKESO Foreign Worker Coverage (REPLACED)
            // DIRECT_OFFICIAL — verified at corrected URL 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO — Social Security Coverage for Foreign Workers",
                    Url = "https://perkeso.gov.my/en/our-services/protection/foreign-worker.html",
                    Department = "Pertubuhan Keselamatan Sosial (PERKESO) / Social Security Organisation (SOCSO)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "PERKESO Social Security Coverage for Foreign Workers in Malaysia",
                    Category = "employment",
                    Content = """
PERKESO Social Security Coverage for Foreign Workers — Malaysia

Foreign workers in Malaysia, including expatriates and foreign domestic workers, are protected under three schemes:

1. Employment Injury Scheme (effective 1 January 2019): Covers workplace accidents, commuting accidents, emergencies, and occupational diseases.

2. Invalidity Scheme (effective 1 July 2024): Covers foreign workers suffering permanent conditions rendering them unable to earn at least one-third of what a normal worker customarily earns.

3. Non-Employment Injury Scheme / LINDUNG 24 JAM (effective 1 June 2026): 24-hour protection against accidents occurring outside working hours and unrelated to employment.

Eligibility: Must possess a valid passport and hold a valid work pass (temporary employment visit pass, employment pass, special pass, or related work passes).

Contribution rates (mandatory, split between employer and worker):
- Employment Injury Scheme: Employer 1.25%, Worker 0%.
- Invalidity Scheme: Employer 0.5%, Worker 0.5%.
- Non-Employment Injury Scheme: Worker 0.75% (Phase 1, years 1–2), increasing to 1.00% (Phase 2, years 3–5), then 1.25% (Phase 3, years 6+).
- Total: Employer 1.75%, Worker 1.25%, Combined 3.0%.

Employer obligations: Employers must register foreign workers and pay mandatory contributions in full. Non-compliance carries penalties of up to RM10,000 fine, 2 years imprisonment, or both.

Contribution categories:
- First Category (Employment Injury + Invalidity + Non-Employment Injury): Workers who first enter PERKESO before age 55.
- Second Category (Employment Injury + Non-Employment Injury only): Workers who enter at age 55 or older, or who have reached age 60 and continue working.

Benefits under Employment Injury & Non-Employment Injury Schemes:
- Medical benefit: Free treatment at PERKESO panel clinics or government facilities.
- Temporary disablement benefit: Paid when medical leave is certified for at least 4 days.
- Permanent disablement benefit: Based on Medical Board assessment.
- Constant-attendance allowance: RM500/month for 100% total permanent disablement.
- Dependants' benefit: Paid to eligible dependants upon worker's death.
- Funeral benefit: Up to RM7,500 (death in Malaysia, burial in home country) or RM3,000 (burial in Malaysia).

Benefits under the Invalidity Scheme:
- Invalidity pension: 50%–65% of average assumed monthly wage, minimum RM550/month.
- Survivors' pension: Paid to dependants when qualifying conditions are met.

Contact: PERKESO — 1-300-22-8000 / +603 4264 5000. Menara PERKESO, 281 Jalan Ampang, 50538 Kuala Lumpur.
""",
                }
            ),

            // Source 20: Labour Permits
            // DIRECT_OFFICIAL — verified accessible 2026-09-15 — cleaned to page content only
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Labour Permits — Working Hours Exemptions",
                    Url = "http://jtksm.mohr.gov.my/ms/perkhidmatan/permit-perburuhan",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Labour Permits and Exemptions from Employment Act Provisions — Malaysia",
                    Category = "employment",
                    Content = """
Labour Permits (Permit Perburuhan) — Malaysia (JTKSM)

Under the Employment Act 1955, employers may apply to JTKSM for a labour permit granting exemption from certain statutory provisions, where worker welfare is not compromised. The objective is to help industries optimise human resources and automation to boost productivity while protecting workers' interests.

Contact: JTKSM HQ, 03-8888 9111. Nearest PTK.
""",
                }
            ),

            // Source 20b: Labour Permits FAQ
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "JTKSM Labour Permits FAQ",
                    Url = "http://jtksm.mohr.gov.my/ms/soalan-lazim/soalan-lazim-permit-perburuhan",
                    Department = "Jabatan Tenaga Kerja Semenanjung Malaysia (JTKSM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Labour Permits FAQ — JTKSM Malaysia",
                    Category = "employment",
                    Content = """
Labour Permits — FAQ — JTKSM Malaysia

Q: What is a Labour Permit under JTKSM's jurisdiction?
A: A formal authorisation from the Director General allowing employers exemptions from certain provisions of the Employment Act 1955 and the Children and Young Persons (Employment) Act 1966.

Q: What is the objective of issuing Labour Permits?
A: Two objectives: helping industries adjust to specific operational needs, and ensuring exemptions do not exploit workers.

Q: What types of matters require a Labour Permit?
A: Twelve items including: wage period extensions, wage advances, wage deductions, overtime limits, working hour flexibility, rest day accumulation, incentive payment schemes, register-keeping arrangements, cash/cheque payments, shift work flexibility, public entertainment licences for children/youth, and apprenticeship contracts.

Q: Who is responsible for applying?
A: The employer must apply before any exemption is implemented.

Q: Where can application forms be obtained?
A: Through JTKSM offices or the JTKSM website.

Q: How should applications be submitted?
A: Sent to the nearest branch Labour Office in two copies.

Q: How long is the processing time?
A: 14 days from receipt of complete documents, with an additional 7 days if headquarters approval is needed.

Contact: JTKSM HQ, 03-8888 9111.
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetPerkesoSources()
    {
        return
        [
            // Source B5-1: PERKESO Employment Injury Scheme (detailed benefits page)
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO Employment Injury Scheme Benefits",
                    Url = "https://www.perkeso.gov.my/en/our-services/protection/employment-injury-scheme.html",
                    Department = "PERKESO (Social Security Organisation)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "PERKESO Employment Injury Scheme — Benefits and Coverage — Malaysia",
                    Category = "employment",
                    Content = """
Employment Injury Scheme (LINDUNG PEKERJA) — PERKESO Malaysia

The Employment Injury Scheme protects employees against accidents or occupational diseases arising out of and in the course of employment.

COVERAGE CATEGORIES:

1. Work Accidents — injuries occurring while performing work duties.
2. Commuting Accidents — travel between residence and workplace, journeys directly connected to employment, and workplace to meal location during authorised breaks. Accidents during interruptions or deviations from the journey are not covered.
3. Emergency Accidents — injuries sustained while assisting, rescuing, or protecting people from disaster or danger at the employer's premises.
4. Occupational Diseases — diseases listed in the Fifth Schedule of the Employees' Social Security Act 1969, including hearing loss from continuous excessive noise and occupational asthma from dust or harmful chemicals.

BENEFITS:

Medical Benefit: Free treatment at PERKESO panel clinics or government clinics/hospitals until full recovery. Serious injuries treated at government hospital, second-class ward, with specialist treatment if needed. Reimbursement available for non-panel clinics subject to PERKESO conditions or the Fees Act 1951.

Temporary Disablement Benefit: Paid when medical leave is certified for not less than 4 days including day of accident. Rate: 80% of average assumed daily wage. Minimum RM30.00/day. Maximum RM158.67/day. Not paid for days the employee works and earns wages.

Permanent Disablement Benefit: Rate: 90% of average assumed daily wage. Minimum RM30.00/day. Maximum RM178.50/day. Claim deadline: within 12 months from last date of temporary disablement. Assessment by Medical Board: 20% or less disability = lump sum; above 20% = option to commute 1/5 as lump sum, balance paid monthly for life.

Constant-Attendance Allowance: For total permanent disablement requiring constant personal attendance. Certified by Medical Board. Fixed rate RM500/month.

Physical or Vocational Rehabilitation: Physiotherapy, occupational therapy, prosthetics, orthotics, implants, wheelchairs, crutches, hearing aids, spectacles, special shoes. Vocational training courses. All costs borne by PERKESO.

Dependant's Benefit: Paid when employee dies from employment injury. Rate: 90% of average assumed daily wage (min RM30.00/day, max RM178.50/day). Widow/widower receives 3/5 share for life (even if remarried on or after 1 May 2005). Children receive 2/5 share until age 21 or marriage; extended through first degree for students; indefinite if incapacitated. If no widow/widower or children: parents receive 4/10 for life; siblings receive 3/10 until age 21 or marriage; grandparents 4/10 for life.

Funeral Benefit: RM3,000 (effective 1 June 2024). Paid to eligible person or whoever incurs funeral expenses. Capped at actual expenditure or RM3,000, whichever is lower.

Education Benefit: Loans for dependant children of insured persons who died from employment injury or receive periodic Permanent Disablement Benefit. Service charge 2%.

Foreign workers are covered under the Employment Injury Scheme. PERKESO lists Foreign Worker (LINDUNG PEKERJA) as a separate protection category with a dedicated page.

Contact: 1-300-22-8000 (domestic), +603 4264 5000 (international). Address: Menara PERKESO, 281 Jalan Ampang, 50538 Kuala Lumpur.
""",
                }
            ),

            // Source B5-2: PERKESO Invalidity Scheme (detailed benefits page)
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO Invalidity Scheme Benefits",
                    Url = "https://www.perkeso.gov.my/en/our-services/protection/invalidity-scheme.html",
                    Department = "PERKESO (Social Security Organisation)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "PERKESO Invalidity Scheme — Benefits and Eligibility — Malaysia",
                    Category = "employment",
                    Content = """
Invalidity Scheme (LINDUNG PEKERJA) — PERKESO Malaysia

The Invalidity Scheme provides 24-hour coverage to employees who suffer from invalidity or death from any cause not related to employment. An insured person is considered invalid when they have a specific morbid condition of permanent nature, either incurable or not likely to be cured, and can no longer earn at least one-third of what a healthy insured person in similar work would earn.

ELIGIBILITY:

Age: Must not have reached age 60 when the Notice of Invalidity is received. Those over 60 (effective 1 January 2013) may qualify if they demonstrate a permanent morbid condition, inability to engage in substantially gainful activities, and that the condition began before age 60 with no gainful employment since.

Certification: Must be certified invalid by the Medical Board or Appellate Medical Board.

Full Qualifying Period (two paths): (1) At least 24 months contributions within 40 consecutive months before Notice is received; OR (2) Not less than 2/3 of complete months between first payable contribution and Notice (minimum 24 months total).

Reduced Qualifying Period: Not less than 1/3 of complete months between first contribution and Notice, with at least 24 months total.

BENEFITS:

Invalidity Pension — Full qualifying period: 50% to 65% of average assumed monthly wages. Starts at 50%, increases by 1% for every 12 additional monthly contributions beyond the first 24, capped at 65%. Minimum RM550/month. Reduced qualifying period: flat 50%, minimum RM550/month. Payable from date Notice received. Continues as long as employee remains invalid or until death. Converts to Survivors' Pension upon death.

Invalidity Grant — For those certified invalid but failing to meet qualifying conditions. One-time lump sum equal to all contributions (employer + employee) under the Invalidity Scheme plus interest.

Constant-Attendance Allowance — For severely incapacitated persons requiring constant attendance. Certified by Medical Board. Fixed RM500/month.

Survivors' Pension — Paid when insured person dies before age 60 having met qualifying conditions, or dies while receiving Invalidity Pension. Full qualifying rate: 50%–65% (same escalation), minimum RM475/month. Reduced rate: 50%, minimum RM475/month. If deceased was receiving Invalidity Pension, Survivors' Pension matches that rate.

Physical or Vocational Rehabilitation and Dialysis — Physiotherapy, occupational therapy, prosthetics, medical aids. Vocational training courses. Dialysis facilities for chronic renal failure including haemodialysis, CAPD, EPO injection subsidy, immunosuppressant subsidy, AV Fistula surgery. All costs borne by PERKESO.

Funeral Benefit — RM3,000 (effective 1 June 2024). Capped at actual expenditure or RM3,000, whichever is lower.

Education Benefit — Loans for dependant children of insured persons who died while on Invalidity Pension or who are current recipients. Service charge 2%.

Contact: 1-300-22-8000 (domestic), +603 4264 5000 (international). Address: Menara PERKESO, 281 Jalan Ampang, 50538 Kuala Lumpur.
""",
                }
            ),

            // Source B5-3: PERKESO Domestic Worker Coverage
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO Domestic Worker Coverage",
                    Url = "https://www.perkeso.gov.my/en/our-services/protection/domestic-workers.html",
                    Department = "PERKESO (Social Security Organisation)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "PERKESO Domestic Worker Social Security Coverage — Malaysia",
                    Category = "employment",
                    Content = """
PERKESO Domestic Worker Coverage — Malaysia

Effective 1 June 2021, domestic workers are covered under the Employees' Social Security Act 1969 (Act 4) and Employment Insurance System Act 2017 (Act 800). Registration and contribution payment are mandatory.

DEFINITION: A domestic worker is someone employed exclusively in work of a private dwelling house and not engaged in any trade or business the employer runs from that home. Examples: housemaid, personal driver, gardener, personal bodyguard, personal security guard, personal cook, caregiver, nanny.

ELIGIBILITY:
Local domestic workers: Must be Malaysian, permanent resident, or temporary resident. Can be employed by a Malaysian, permanent resident, temporary resident, or foreigner.
Foreign domestic workers: Must hold a valid passport and work pass from Malaysia's Immigration Department. Must be employed by a Malaysian, permanent resident, or temporary resident.

SCHEMES AND COVERAGE:

Employment Injury Scheme (Act 4): Covers both local and foreign domestic workers.
Invalidity Scheme (Act 4): Covers both local and foreign domestic workers. Foreign domestic workers covered effective 1 July 2024.
LINDUNG 24 Jam: Covers both local and foreign domestic workers.
Employment Insurance System (Act 800): Covers local domestic workers ONLY. Foreign domestic workers are EXCLUDED.

CONTRIBUTION RATES:
Employment Injury Scheme: Employer 1.25%, Employee 0%.
Invalidity Scheme: Employer 0.5%, Employee 0.5%.
LINDUNG 24 Jam: Employer 0%, Employee 0.75% (Phase 1).
Employment Insurance (local only): Employer 0.2%, Employee 0.2%.

LINDUNG 24 Jam phased rates (starting 1 June 2026): Phase 1 (Years 1-2) 0.75%. Phase 2 (Years 3-5) 1.00%. Phase 3 (Year 6 onward) 1.25%.

Local domestic workers follow contribution schedules of both Act 4 and Act 800. Foreign domestic workers follow only Act 4 schedule.

PAYMENT METHODS: Monthly payment through ASSIST portal. Advance payment (up to 24 months) via Prihatin app.

BENEFITS: Same benefits as formal workers. Local domestic workers receive Employment Injury, Invalidity, LINDUNG 24 Jam, and Employment Insurance benefits. Foreign domestic workers receive the same except Employment Insurance.

PENALTIES: Employers who fail to register and pay contributions face a fine of up to RM10,000, imprisonment for up to 2 years, or both.

Contact: 1-300-22-8000 (domestic), +603 4264 5000 (international).
""",
                }
            ),

            // Source B5-4: PERKESO Employer Registration
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO Employer Registration",
                    Url = "https://www.perkeso.gov.my/en/our-services/employer-employee/employer-registration.html",
                    Department = "PERKESO (Social Security Organisation)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "PERKESO Employer Registration Process and Requirements — Malaysia",
                    Category = "employment",
                    Content = """
PERKESO Employer Registration — Malaysia

All industries with one or more employees must register with PERKESO and contribute. Both principal employers and immediate employers are covered.

Principal Employer: Employs an employee directly under a contract of service or apprenticeship.
Immediate Employer: Employs employees to perform work under the supervision of a principal employer, including temporary lending or leasing of employee services.

Principal employers must ensure all workers hired through immediate employers are registered and contributions are paid.

REGISTRATION PROCESS (3 STEPS):

Step 1 — Register Portal ID: Access the ASSIST Portal and complete registration. A temporary password arrives via email.

Step 2 — Submit Details: Fill in employer and employee details. Upload scanned forms and supporting documents.

Required forms for standard employees: Borang 1 (Employer Registration), Borang 2 (Employee Registration), Borang SIP 1 (EIS Employer Registration), Borang SIP 2 (EIS Employee Registration), Borang SIP 1A (Employer Notification), Borang SIP 2A (Employee Notification).

For Domestic Workers: Replace Borang 2 with the Domestic Worker Registration Form (Lampiran C).

For Foreign Workers: Replace Borang 2 with the Foreign Worker Registration Form. Also submit the Foreign Worker Dependant Declaration Form. Borang SIP 1 and SIP 2 are NOT required for foreign workers (foreign workers are excluded from EIS).

Step 3 — Pay Contributions: Via ASSIST Portal. Contact 1-300-22-8000 for enquiries.

CESSATION: Employers who cease operations must submit Form 1A (Act 4) and Form SIP 3 (Act 800) within 30 days of cessation via ASSIST Portal.

RECORD-KEEPING: Monthly records per employee required: name, NRIC number, occupation, contribution details, monthly wages, allowances. Records must be kept for 7 years from last entry date.

WAGES FOR CONTRIBUTION PURPOSES:
Counted: salary, overtime, commission, service charges, leave payments, allowances (incentives, shift, food/meal, cost of living, housing), hourly/daily/weekly/piece/task rates.
Not counted: statutory fund payments by employer, mileage claims, gratuity, dismissal/retrenchment payments, annual bonus.

ACCIDENT REPORTING: Employers must report all work-related accidents within 48 hours of notification.

Contact: 1-300-22-8000 (domestic), +603 4264 5000 (international). Address: Menara PERKESO, 281 Jalan Ampang, 50538 Kuala Lumpur.
""",
                }
            ),

            // Source B5-5: PERKESO Contributions Overview
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO Contributions Overview",
                    Url = "https://www.perkeso.gov.my/en/our-services/employer-employee/contributions.html",
                    Department = "PERKESO (Social Security Organisation)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "PERKESO Contribution Categories and Requirements — Malaysia",
                    Category = "employment",
                    Content = """
PERKESO Contributions — Malaysia

Under the Employees' Social Security Act 1969, employers must pay monthly contributions for each eligible employee.

TWO CATEGORIES OF CONTRIBUTIONS (Act 4):

First Category — Employees under 60 years of age. Covers Employment Injury Scheme AND Invalidity Scheme. Employer's share: 1.75% of monthly wages. Employee's share: 0.5% of monthly wages. Total: 2.25%. Exception: workers who reached 55 with no prior contributions before that age are excluded.

Second Category — Employees aged 60 and above. Covers Employment Injury Scheme ONLY. Rate: 1.25% of monthly wages, paid entirely by the employer. New eligible employees aged 55+ with no prior contributions also covered here.

EMPLOYMENT INSURANCE SYSTEM (EIS — Act 800):

Effective 1 January 2018. All private-sector employers must contribute. Exempt: government employees, domestic workers, self-employed. Employees aged 18 to 60 must contribute. Those 57+ with no prior contributions before 57 are exempt.

Wage ceiling for EIS: RM6,000 per month (effective 1 October 2024).

EIS contribution rate: Total 0.4% of assumed monthly salary. Employer pays 0.2%. Employee pays 0.2% (deducted from salary).

EIS BENEFIT ELIGIBILITY: Insured persons who lose employment may claim, EXCEPT in these situations: voluntary resignation, expiry of fixed-term contract, mutual termination agreement, completion of a specified project, retirement, dismissal for misconduct. Applicants must demonstrate they are able to work, available to work, and actively seeking work.

FOREIGN WORKERS: Foreign workers are excluded from EIS (Act 800). Foreign worker contribution rates follow the Third Schedule of Act 4 only.

Contact: 1-300-22-8000 (domestic), +603 4264 5000 (international).
""",
                }
            ),

            // Source B5-6: PERKESO Contribution Payment Procedures
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "PERKESO Contribution Payment Procedures",
                    Url = "https://www.perkeso.gov.my/en/our-services/employer-employee/pembayaran.html",
                    Department = "PERKESO (Social Security Organisation)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "PERKESO Contribution Payment Methods and Deadlines — Malaysia",
                    Category = "employment",
                    Content = """
PERKESO Contribution Payment — Malaysia

PAYMENT DEADLINE: Contributions for any given month must be submitted by the 15th of the following month. Example: July contributions are due by 15 August.

LATE PAYMENT PENALTY: Interest of 6% per annum for each day contributions remain unpaid past the deadline.

PAYMENT METHODS:

1. ASSIST Portal (primary online system): Launched 1 January 2018. Employers register for an ASSIST Portal ID and manage registration, records, and payments online. Payment via FPX (Financial Process Exchange) requires internet banking with an FPX-participating bank. Direct Debit Authorisation (DDA) available via E-mandate through the ASSIST Portal.

2. Internet Banking: Available through 19 participating banks including Maybank, CIMB, RHB, Public Bank, Hong Leong, AmBank, Alliance Bank, Affin Bank, HSBC, Bank Islam, Agro Bank, UOB, Bank Muamalat, Standard Chartered, OCBC, Bank Rakyat, MBSB Bank, Citibank, and J.P. Morgan.

3. Bank Counters: Payments at counters using ACR reference with contribution data from ASSIST Portal. PERKESO collection agents: Maybank Berhad and Public Bank Berhad (since 1 March 2018).

4. PRIHATIN Mobile Application: Download from Google Play Store or App Store.

Contact: 1-300-22-8000 (domestic), +603 4264 5000 (international). Address: Menara PERKESO, 281 Jalan Ampang, 50538 Kuala Lumpur.
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetHealthcareSources()
    {
        return
        [
            // Source B6-1: IMI Foreign Worker Medical Requirements
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            // Source: Immigration Department of Malaysia (IMI) official portal
            // Note: MOH (moh.gov.my) returns 403 on all URLs — see gap documentation.
            // Healthcare content sourced from IMI official foreign worker page which authoritatively
            // describes FOMEMA medical examination requirements as part of the VP(TE) process.
            (
                new RegisterSourceDto
                {
                    Name = "IMI Foreign Worker Medical Requirements and FOMEMA",
                    Url = "https://www.imi.gov.my/index.php/en/main-services/foreign-worker/",
                    Department = "Immigration Department of Malaysia (Jabatan Imigresen Malaysia)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Foreign Worker Medical Requirements and FOMEMA — Immigration Department Malaysia",
                    Category = "healthcare",
                    Content = """
Foreign Worker Medical Requirements and FOMEMA — Malaysia (Immigration Department)

OVERVIEW:
Foreign workers employed in Malaysia must meet mandatory health examination requirements administered through FOMEMA (Foreign Workers Medical Examination Monitoring Agency). These requirements are set by the Malaysian government and enforced by the Immigration Department of Malaysia (Jabatan Imigresen Malaysia).

WHO MUST COMPLY:
All foreign workers holding a Visitor's Pass (Temporary Employment) — VP(TE) — in the following sectors: Manufacturing, Construction, Agriculture, Plantation, and Services.

PHASE 1 — PRE-ARRIVAL HEALTH REQUIREMENT:
Before arriving in Malaysia, the worker must be "certified healthy by a health examination centre in the source country." The approved medical centres list is maintained at: https://fwcms.com.my/affiliates/#medical-centres

PHASE 2 — MANDATORY FOMEMA EXAMINATION AFTER ARRIVAL:
- Within 30 days of arriving in Malaysia, the employer must ensure the foreign worker undergoes a medical examination at any clinic registered with FOMEMA.
- The VP(TE) pass sticker is only issued after the worker passes the FOMEMA examination and is certified fit.
- If the worker FAILS the FOMEMA medical examination: the employer must arrange immediate repatriation via a Check Out Memo (COM) from the Immigration Department. The worker cannot remain in Malaysia.

FOMEMA FOR VP(TE) EXTENSIONS (2nd and 3rd year renewals):
- A new FOMEMA medical examination and clearance is MANDATORY for the 2nd-year and 3rd-year VP(TE) extension.
- Extension applications must be submitted 3 months before pass expiry.
- FOMEMA clearance must be obtained before the extension application is submitted.
- Workers who fail must be repatriated; they cannot continue working.

WHAT FOMEMA EXAMINES:
FOMEMA is the government-authorised body for foreign worker medical screening in Malaysia. The examination checks fitness to work and screens for conditions that would render a worker unfit for employment in Malaysia. Workers certified unfit cannot remain in the country.

APPROVED SECTORS:
Manufacturing, Construction, Agriculture, Plantation, Services.

SOURCE COUNTRIES COVERED:
Indonesia, Thailand, Cambodia, Bangladesh, Myanmar, Laos, Vietnam, Pakistan, Sri Lanka, Turkmenistan, Uzbekistan, Kazakhstan, Nepal, Philippines, India.

LEVY FEES BY SECTOR (Peninsular Malaysia):
Manufacturing: RM1,850. Construction: RM1,850. Plantation: RM640. Agriculture: RM640. Services: RM1,850. Services (island resort): RM1,850.

LEVY FEES BY SECTOR (Sabah/Sarawak):
Manufacturing: RM1,010. Construction: RM1,010. Plantation: RM590. Agriculture: RM410. Services: RM1,490. Services (island resort): RM1,010.

VP(TE) PROCESSING FEE: RM60. Application processing fee: RM125 (all sectors, both regions).

VISA FEES AND SECURITY BONDS BY NATIONALITY:
Indonesia: visa RM15, bond RM250. Bangladesh: visa RM20, bond RM500. Pakistan: visa RM20, bond RM750. Myanmar: visa RM19.50, bond RM750. India: visa RM50, bond RM750. Philippines: visa RM36, bond RM1,000. Thailand: visa free, bond RM250. Cambodia: visa RM20, bond RM250. Nepal: visa RM20, bond RM750. Vietnam: visa RM13, bond RM1,500. Sri Lanka: visa RM15, bond RM750.

CONDITIONS OF VP(TE):
- Family members may not accompany the worker or reside in Malaysia.
- Workers may not change employers without Ministry of Home Affairs approval.
- Workers may not work as front liners.
- Marriage to local or foreign citizens is prohibited.
- Maximum stay: 10 years (6P Program registrants: 3 years maximum).

Contact: Immigration HQ, No. 15, Persiaran Perdana, Presint 2, 62550 Putrajaya. Phone: 03-8000 8000 (MyGCC).
""",
                }
            ),

            // Source B6-2: IMI VP(TE) Pass — Medical Requirements Detail
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "IMI VP(TE) Pass Medical Requirements and Extension",
                    Url = "https://www.imi.gov.my/index.php/en/main-services/pass/visitor-pass/visitors-pass-temporary-employment/",
                    Department = "Immigration Department of Malaysia (Jabatan Imigresen Malaysia)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "VP(TE) Visitor's Pass (Temporary Employment) — Medical Requirements — Malaysia",
                    Category = "healthcare",
                    Content = """
VP(TE) Visitor's Pass (Temporary Employment) — Medical Requirements — Malaysia (Immigration Department)

PURPOSE:
The Visitor's Pass (Temporary Employment), or VP(TE), is the official work pass for foreign workers in Malaysia. Medical clearance through FOMEMA is a mandatory condition for obtaining and renewing this pass.

FOMEMA MEDICAL EXAMINATION — MANDATORY CONDITIONS:

New VP(TE) Applications:
- A medical report from the worker's country of origin, certified by a Ministry of Health Malaysia-approved clinic, must be submitted with the application.
- Upon arrival in Malaysia, the worker must undergo a FOMEMA medical examination within one month of entry.
- The VP(TE) sticker is issued ONLY after the worker passes the FOMEMA examination and is certified fit.
- Workers who fail must be immediately repatriated; no exceptions.

VP(TE) Extension (2nd and 3rd year):
- FOMEMA clearance is MANDATORY before submitting an extension application.
- Extension applications must be submitted 3 months before the current pass expires.
- Without valid FOMEMA clearance, the extension cannot proceed.

GENERAL CONDITIONS:
- Worker age: 18–45 years at time of application.
- Maximum total stay: 5 years (separate regulations apply for some programs — up to 10 years for general VP(TE), 3 years for 6P Program registrants).
- Worker must remain outside Malaysia while the application is processed (new applications).
- Cannot bring family members to Malaysia.
- Cannot change employer without Ministry of Home Affairs approval.

APPLICATION DOCUMENTS (NEW VP(TE)):
- Employer's letter.
- Form IMM.12 and VDR application form.
- Ministry of Home Affairs approval letter.
- Original levy payment receipts.
- Worker's passport copy and 1 photograph.
- Stamped personal bond.
- Bank draft covering levy, processing, and visa fees.
- Security deposit or insurance/bank guarantee (minimum 18 months validity).
- Medical report from country of origin, certified by a Ministry of Health Malaysia-approved clinic.

EXTENSION APPLICATION DOCUMENTS:
- Passport copy with minimum 12 months remaining validity.
- Form 49 / Form B & D / company representative card.
- Payment form.
- Bank guarantee or insurance guarantee (minimum 18 months validity).
- FOMEMA clearance certificate (2nd and 3rd year extensions only).

i-KAD (IDENTITY CARD):
- Issued simultaneously with VP(TE), mailed directly to employer.
- Color-coded by sector: Agriculture (green), Plantation (orange), Construction (gray), Manufacturing (maroon), Services (yellow), Foreign Domestic Helper (chocolate).

Contact: State Immigration Office or Foreign Workers Division HQ. Phone: 03-8000 8000 (MyGCC).
""",
                }
            ),

            // Source B6-3: IMI Foreign Domestic Helper — Medical Requirements
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            (
                new RegisterSourceDto
                {
                    Name = "IMI Foreign Domestic Helper Medical Requirements",
                    Url = "https://www.imi.gov.my/index.php/en/main-services/foreign-domestic-helper-fdh/",
                    Department = "Immigration Department of Malaysia (Jabatan Imigresen Malaysia)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Foreign Domestic Helper — Medical Requirements and FOMEMA — Malaysia",
                    Category = "healthcare",
                    Content = """
Foreign Domestic Helper (FDH) Medical Requirements — Malaysia (Immigration Department)

WHO THIS APPLIES TO:
Foreign domestic helpers (household workers such as maids, personal cooks, caregivers, nannies) employed in Malaysia under a PL(KS) or equivalent pass. This is a separate category from general foreign workers with VP(TE).

MANDATORY FOMEMA MEDICAL EXAMINATION:

Pre-employment (before or on arrival):
- The FDH must be "confirmed fit by an appointed medical centre" before employment begins.
- The employer must ensure that the FDH undergoes a medical examination at any clinic appointed by FOMEMA within one month of arrival.

FOMEMA CLEARANCE FOR PASS EXTENSIONS (2nd and 3rd year):
- FOMEMA clearance is mandatory before submitting any extension application.
- Workers must be "certified healthy" by FOMEMA before the extension can proceed.
- Extension applications must be submitted before the pass expires.

IF THE FDH FAILS THE MEDICAL EXAMINATION:
- The worker is NOT permitted to work in Malaysia.
- The employer must obtain a Check Out Memo from the Immigration Department.
- Immediate repatriation must be arranged at the employer's expense.
- Employees who fail this examination must be sent back to their home countries immediately.

PRE-DEPARTURE MEDICAL REPORT:
- A medical report from the source country, certified by a clinic appointed by the Ministry of Health Malaysia, is required as part of the application documents.

FEES AND FINANCIAL REQUIREMENTS:

Indonesian FDH enrollment (maximum): RM15,000.
Indonesian FDH minimum monthly wage: RM1,500.

Personal Bond by Nationality:
- Indonesia, Thailand, Cambodia: RM250.
- Philippines, Sri Lanka, India: RM750.
- Laos, Vietnam: RM1,500.

Extension fees include: levy, visa fees (by nationality), processing fees, PL(KS) sticker costs.

CONTACT AND APPLICATION:
Applications and extensions are processed at State Immigration Offices. Contact Immigration HQ: 03-8000 8000 (MyGCC). Address: No. 15, Persiaran Perdana, Presint 2, 62550 Putrajaya.
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetEducationSources()
    {
        return
        [
            // Source B7-1: IMI Student Pass (Pas Pelajar) — Full page content as single source
            // DIRECT_OFFICIAL — verified accessible 2026-09-15
            // Source: Immigration Department of Malaysia (IMI) official portal
            // MOE (moe.gov.my) pages return empty bodies — documented as gap.
            // EMGS (educationmalaysia.gov.my) is a CLBG (private company under MOHE) —
            // does not meet DIRECT_OFFICIAL source-integrity policy.
            // Single source with combined content — chunker splits at 2000 chars.
            (
                new RegisterSourceDto
                {
                    Name = "IMI Student Pass (Pas Pelajar) — Requirements and Procedures",
                    Url = "https://www.imi.gov.my/index.php/en/main-services/pass/pelajar/pas-pelajar/",
                    Department = "Immigration Department of Malaysia (Jabatan Imigresen Malaysia)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Student Pass (Pas Pelajar) — Requirements and Procedures — Malaysia Immigration",
                    Category = "education",
                    Content = """
Student Pass (Pas Pelajar) — Malaysia (Immigration Department)

OVERVIEW:
Foreign nationals studying in Malaysia must obtain a Student Pass (Pas Pelajar) from the Immigration Department of Malaysia. Requirements differ for higher education institutions versus school-level institutions.

FEES:
Student Pass (Pas Pelajar): RM60.00
Long-Term Social Visit Pass (dependant/escort): RM90.00
Visa fee: per nationality rate (separate)

HIGHER EDUCATION (Public Universities / Private HEIs / Language / Training Centres)

ELIGIBILITY:
Must have an offer letter from a public university (UA) or registered private higher education institution (IPTS) with MQA accreditation. IPTS must hold KDN approval to accept international students. Language or training centres must be registered with KPM and KESUMA and hold KDN approval. Applicant must be outside Malaysia when applying. Applications for public universities and accredited private HEIs use the STARS system operated by EMGS at visa.educationmalaysia.gov.my. EMGS issues an "Approval to Study" letter. Students from countries requiring visas must obtain a Single Entry Visa (SEV) after e-VAL approval. eVISA accepted globally except from Malaysia, Israel, and North Korea. Upon arrival: health examination within 7 days; pass issued after medical clearance and EMGS support letter.

DOCUMENTS FOR NEW APPLICATION (IPT / Language / Training Centres):
1. Form IMM.14.
2. Valid KDN approval letter for international student intake.
3. EMGS support letter (Acceptance Declaration).
4. Offer letter from institution.
5. Passport-sized photo (3.5cm x 5.0cm, white background).
6. Passport copy — all pages including cover; minimum 18 months validity.
7. Academic results and transcripts.
Sarawak only: institution permission letter, accompanying letter, Pre-VAL Medical Report.

DOCUMENTS FOR FIRST-YEAR STICKER ISSUANCE:
1. Original passport.
2. Institution's sticker issuance request letter.
3. EMGS sticker issuance support letter.
4. Stamped Personal Bond form (new applications only).
5. Malaysian health insurance — minimum 12-month coverage.
6. Online payment receipt (if applicable).

DOCUMENTS FOR RENEWAL/EXTENSION (IPT):
1. Form IMM.14.
2. Passport copy (biodata + current pass pages).
3. Previous offer letter copy.
4. EMGS renewal support letter.
5. Institution support letter.
6. JPT support letter (if applicable).
7. Previous semester academic results.
8. Latest attendance record.
9. Health insurance — minimum 12 months (Malaysian or internationally recognised).

SCHOOL-LEVEL STUDENTS

ELIGIBLE SCHOOL TYPES:
Government and government-aided schools under KPM's Bahagian Pengurusan Sekolah Harian. International schools, expatriate schools, private academic schools, religious schools, and Chinese secondary schools registered under Bahagian Pendidikan Swasta (KPM). International schools must hold KDN approval to accept foreign students.

WHERE TO APPLY (SCHOOL STUDENTS):
At the Immigration Office with jurisdiction over the school's address. May be submitted by the school representative, parent, or legal guardian.

DOCUMENTS FOR SCHOOL-LEVEL STUDENT PASS (New and Renewal):
1. Forms IMM.14 and IMM.38.
2. Valid KDN international student approval letter (international schools only).
3. Sabah/Sarawak state government approval (if applicable).
4. Valid private education registration certificate from KPM (private schools).
5. Support letter from Bahagian Pendidikan Swasta (KPM) or JPN Sarawak.
6. JPN/PPD approval letter (government and government-aided schools).
7. School representative appointment letter (if applicable).
8. Passport copy — all pages for new applications; biodata + current pass for renewal (minimum 18 months validity).
9. Passport-sized photo (3.5cm x 5.0cm, white background).
10. Health insurance — minimum 12-month coverage.
11. Stamped Personal Bond (new applications only).
12. Supporting documents as applicable: birth certificate, court custody orders, marriage certificate, parents' ID/passport, divorce certificate, death certificate, adoption orders.

ESCORT PASS FOR SCHOOL-LEVEL STUDENTS:
School-level foreign students may be accompanied by an Escort Pass holder. Eligible escorts: biological parents, legal guardians, adoptive parents, siblings under 7 years old. Escorts cannot work, do business, or engage in political activities in Malaysia.

DEPENDANTS FOR HIGHER EDUCATION STUDENTS (Master's/PhD only):
Master's and PhD students are eligible for dependant passes. Eligible dependants: spouse, children under 18 (biological/step/adopted), disabled child of any age (certified by doctor), biological parents, parents-in-law. Language centre/training centre holders are NOT eligible for dependant passes. Dependants cannot work, do business, or engage in political activities.

PART-TIME WORK PERMISSION:
Available ONLY for public university (UA) and private HEI (IPTS) students. NOT for language centre or training centre students. Conditions: only during semester breaks or public holidays; maximum 20 hours per week; permitted premises: restaurants, petrol stations, mini markets, hotels, university/college campuses; no front desk duties; cannot act as student recruitment agents. Documents required: job offer letter, institution support letter confirming semester break period, academic performance report, attendance record, passport copy, employer's Section 60K Labour Department approval.

DOCUMENT CERTIFICATION REQUIREMENTS:
All supporting documents must be certified by the applicant's foreign embassy or representative office in Malaysia. If no representative office exists in Malaysia: certified by the nearest representative office or home-country representative. Non-Malay and non-English documents require certified translation by: a foreign embassy, Institut Terjemahan dan Buku Malaysia (ITBM), or a registered member of Persatuan Penterjemah Malaysia (PPM). PRC (China) citizens: relationship documents must be certified by Malaysia's representative office in China.

HEALTH INSURANCE REQUIREMENT:
All student pass holders (higher education and school-level) must maintain Malaysian health insurance with at least 12 months of coverage throughout their studies.

DEPENDANT PASS DOCUMENTS (NEW AND RENEWAL):
1. Forms IMM.12 and IMM.38.
2. Institution or school representative appointment letter (if applicable).
3. Official application letter from institution or school.
4. Principal's Student Pass status confirmation letter.
5. Applicant's passport — all pages for new applications (minimum 18 months validity); biodata + current pass for renewal.
6. Principal's passport copy (biodata + current Student Pass).
7. Passport photo (3.5cm x 5.0cm, white background).
8. Health insurance — minimum 12-month coverage.
9. Personal Bond stamped (new applications only).
10. Financial proof: 3-month bank statements or sponsorship letter.
11. Supporting relationship documents: birth certificate, marriage certificate, custody orders, as applicable.

KEY DISTINCTIONS:
Government school students apply at the Immigration Office near the school. Higher education students apply via the STARS/EMGS system online. School-level students require an Escort Pass (not a Dependant Pass) for accompanying family. Only Master's and PhD students qualify for Dependant Passes for their family. Language centre and training centre students do NOT qualify for dependant passes or part-time work.

LEGAL BASIS:
All applications are subject to the Immigration Act 1959/63 (Act 155) and Immigration Regulations 1963. The Immigration Department reserves the right to request additional supporting documents and may approve or reject any application. The Student Pass may be cancelled if conditions are violated.

Contact: Immigration HQ, No. 15, Persiaran Perdana, Presint 2, 62550 Putrajaya. Phone: 03-8000 8000 (MyGCC).
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetCustomsSources()
    {
        return
        [
            // Source B8-1: JKDM Traveller's Guide — Full official page content
            // DIRECT_OFFICIAL — verified accessible 2026-09-17 via curl --http1.1 (200 OK, 284KB)
            // Source: Royal Malaysian Customs Department (JKDM) official portal
            // customs.gov.my has a server-side HTTP/2 header bug that causes WebFetch SSL errors,
            // but the page is fully accessible via curl --http1.1.
            // CMS header: Last Updated: 05 January 2026. Content body: 25 May 2023.
            // All numerical values cross-verified against live page 2026-09-17.
            (
                new RegisterSourceDto
                {
                    Name = "JKDM Traveller's Guide — Royal Malaysian Customs Department",
                    Url = "https://www.customs.gov.my/en/individu/pengembara/travelers-guide",
                    Department = "Royal Malaysian Customs Department (Jabatan Kastam Diraja Malaysia / JKDM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Traveller's Guide — Royal Malaysian Customs Department Malaysia",
                    Category = "customs",
                    Content = """
Traveller's Guide — Royal Malaysian Customs Department (JKDM) Malaysia

Source: https://www.customs.gov.my/en/individu/pengembara/travelers-guide
Page last updated: 05 January 2026 (CMS header). Content body dated: 25 May 2023.
Authority: Royal Malaysian Customs Department (Jabatan Kastam Diraja Malaysia / JKDM)
Contact: No. 3, Persiaran Perdana, Presint 2, 62596 Putrajaya. Tel: 03-8882 2100 / 2300. Email: ccc@customs.gov.my

DECLARATION REQUIREMENT FOR ALL TRAVELLERS:

Under the Customs Act 1967 (Amendment) 2019 and the Customs Regulations 2019, tourists or travellers entering or leaving Malaysia are required to declare all taxable goods, prohibited items, cash amounts, and negotiable monetary instruments (NMIs) in their possession, whether carried or contained in any of their luggage or vehicles.

Failure to declare or making a false declaration is an offence and will be subject to legal action under the Customs Act 1967.

Customs Form No. 7 (K7) is used for declarations at all entry and exit points.

DUTIABLE GOODS AND DUTY/TAX RATES:

Duty/tax rates for tourists or travellers are subject to the Customs Duties Order 2022 and the Sales Tax (Rate of Tax) Order 2018. Rates apply to goods imported together with individuals entering Malaysia or carried in baggage for non-commercial purposes (excluding motor vehicles, alcoholic beverages, spirits, tobacco, cigarettes, tyres, and tubes).

Duty/Tax Rate Table:
- Dutiable goods with 10% import duty + 10% sales tax: Traveller pays none (subject to Paragraph 2(1)(b), Sales Tax (Rate of Tax) Order 2018)
- Non-dutiable goods with 10% import duty + no sales tax: Traveller pays none
- Dutiable goods with 5% import duty + 10% sales tax: Traveller pays 5% (subject to Schedule 1, Sales Tax (Rate of Tax) Order 2018)
- Non-dutiable goods with 5% import duty + no sales tax: Traveller pays 5%
- Dutiable goods with specific import duty + 10% sales tax: Traveller pays the specific rate
- Non-dutiable goods with specific import duty + no sales tax: Traveller pays the specific rate

GOODS EXEMPT FROM CUSTOMS DUTY/TAX PAYMENT:

For tourists or travellers entering Malaysia through ALL modes of transport EXCEPT air transport:

| Description of Goods | Quantity/Value Exempt |
| Wine, spirits, beer or malt liquor | Not exceeding 1 litre in total |
| New apparels | Not exceeding 3 pieces |
| New footwear | Not exceeding one pair |
| Food preparations | Total value not exceeding RM150.00 |
| New portable electrically or battery-operated appliances for personal care and hygiene | Not exceeding 1 unit each |
| All other goods (EXCLUDING tyres and tubes; cigarettes; tobacco products; smoking pipes including pipe bowls; electronic cigarettes and similar personal electric vaporising devices; preparation used for smoking through electronic cigarettes in forms of liquid or gel, whether or not containing nicotine) | Total value not exceeding RM500.00 |

For tourists or travellers entering Malaysia VIA AIR TRANSPORT:

| Description of Goods | Quantity/Value Exempt |
| Wine, spirits, beer or malt liquor | Not exceeding 1 litre in total |
| New apparels | Not exceeding 3 pieces |
| New footwear | Not exceeding one pair |
| Food preparations | Total value not exceeding RM150.00 |
| New portable electrically or battery-operated appliances for personal care and hygiene | Not exceeding 1 unit each |
| All other goods (EXCLUDING tyres and tubes; cigarettes; tobacco products; smoking pipes including pipe bowls; electronic cigarettes and similar personal electric vaporising devices; preparation of a kind used for smoking through electronic cigarette and electric vaporising device, in forms of liquid or gel, whether or not containing nicotine) | Total value not exceeding RM1,000.00 |

Important distinction: Cigarettes, tobacco products, electronic cigarettes and similar vaping devices, and their liquids/gels are explicitly EXCLUDED from both the RM500 and RM1,000 general goods exemptions. These items are not eligible for any traveller duty-free allowance.

Reference: Customs Duties (Exemption) Order 2017 (P.U. (A) 445/2017) and Sales Tax (Persons Exempted From Payment Of Tax) Order 2018.

PAYMENT OF PASSENGER DUTY (AIR TRANSPORT):

For passenger duty payment, payment can only be made through Debit Card or Credit Card at the Royal Malaysian Customs Department (RMCD) counters at Malaysian airports.

DECLARATION OF CASH AND BEARER NEGOTIABLE INSTRUMENTS (CBNI):

Under Section 28B (1), Anti-Money Laundering, Anti-Terrorism Financing and Proceeds of Unlawful Activities Act 2001 (AMLATFPUAA):

"Any person leaving or entering Malaysia with an amount in cash, bearer negotiable instruments or both exceeding the value as prescribed by the competent authority by order published in the Gazette, shall declare such amount to the competent authority."

CASH DECLARATION THRESHOLD: Tourists or travellers entering or leaving Malaysia with cash and Bearer Negotiable Instruments (BNI) amounting to or exceeding USD 10,000.00 (or equivalent in other currencies) must declare using Customs Form No. 7 at customs counters at all entry and exit points.

Penalty for non-compliance: Fine not exceeding RM3 million or imprisonment for a term not exceeding 5 years, or both.

Under Regulation 55, Customs Regulations 2019: Any person who possesses cash and negotiable instruments of bearer, whether in their possession, baggage, or vehicle, exceeding the amount required to be declared by Bank Negara Malaysia under the Anti-Money Laundering, Anti-Terrorism Financing and Proceeds of Unlawful Activities (Declaration of Cash and Negotiable Instruments of Bearer) Order, shall make a declaration using Customs Form No. 7. Penalty for violation: fine not exceeding RM3 million or imprisonment not exceeding 5 years, or both.

Malaysian Ringgit (RM) currency: For tourists or travellers who import and export Malaysian Ringgit, written permission must be obtained from Bank Negara Malaysia (BNM) by submitting an application at https://www.bnm.gov.my/submission-of-application by completing the CN-RM form (Ringgit Notes — Confiscation, Import and Export). Even after BNM approval, a declaration using Customs Form No. 7 must still be made at customs.

IMPORT PROHIBITIONS AND RESTRICTIONS:

The list of prohibited or restricted goods imported into Malaysia is subject to the Customs (Prohibition of Imports) Order 2023. The full list is published at: https://lom.agc.gov.my/act-view.php?type=pua&no=P.U.%20(A)%20117/2023 (subject to amendment). Travellers must comply with this order when entering Malaysia.

EXPORT PROHIBITIONS AND RESTRICTIONS:

The list of prohibited or restricted goods for export from Malaysia is subject to the Customs (Prohibition of Export) Order 2023. The full list is published at: https://lom.agc.gov.my/ilims/upload/portal/akta/outputp/PUA%20122.pdf (subject to amendment).

DRUGS:

Under the Customs Act 1967 and the Dangerous Drugs Act 1952, the penalty for drug trafficking is the mandatory death penalty. The import and export of drugs (such as morphine, heroin, opium, cannabis, etc.) are STRICTLY PROHIBITED. Prescription drugs can only be imported into or exported from Malaysia subject to licences and permits issued by the Ministry of Health Malaysia.

GREEN LANE / RED LANE FACILITIES:

Green Lane: For travellers carrying goods NOT subject to duty/tax or categorised as "NOTHING TO DECLARE." Red Lane: Travellers carrying dutiable or taxable goods must proceed to the Red Lane and declare their goods. Penalties or imprisonment may be imposed if travellers misuse the Green Lane facility under Malaysian law.

GUARANTEE/SECURITY FOR TEMPORARY IMPORTATION:

A guarantee/security facility is available for travellers who engage in temporary importation of dutiable or taxable goods. Claims must be made within 3 months from the date of importation and can be made at any exit point in Malaysia.

ATA CARNET FACILITY:

The ATA Carnet is an internationally recognised customs document issued by authorised organisations appointed by member countries of the international convention. When an ATA Carnet is obtained from the country of origin, there is no need to make a customs declaration by filling out a customs form. It serves as a valid customs document for temporary importation and exportation of goods, simplifying customs procedures and facilitating cross-border transactions.

DUTY FREE SHOPS (DFS):

DFS allows tourists or travellers to purchase duty-free goods at designated DFS outlets including international airport DFS, port DFS, border DFS, and state DFS.

STRATEGIC TRADE ACT 2010 (STA):

Travellers leaving Malaysia with strategic goods listed in Parts 1 and 2 of the Schedule of the Strategic Trade (Strategic Items) Order 2010 must obtain a permit and make a declaration at the exit point using Customs Form No. 7. Failure is an offence under Section 9(1), 9(2), and 9(3) of the Strategic Trade Act 2010, punishable under Section 9(4), 9(5), and 9(6) of the Act.

CUSTOMS FORM NO. 7 (K7):

Required for: declaring all taxable goods; declaring prohibited or restricted items; declaring cash and bearer negotiable instruments amounting to or exceeding USD 10,000; declaring Malaysian Ringgit currency imports/exports (after BNM approval); declaring strategic goods at exit points. Available at customs counters at all Malaysian entry and exit points.

AGENCY BOUNDARIES:

CUSTOMS RULE: The JKDM (Royal Malaysian Customs Department) is the authority for customs duty, tax, declaration, prohibited/restricted goods under the Customs Act 1967, and cash/BNI declaration under AMLATFPUAA 2001.

QUARANTINE RULE: For food, agricultural products, plants, and animals, additional quarantine rules from MAQIS (Malaysian Quarantine and Inspection Services) and the Department of Veterinary Services (DVS) may apply in addition to customs requirements. MAQIS operates under the Malaysian Quarantine and Inspection Services Act 2011 (Act 728).

HEALTH RULE: For medical/prescription drugs, Ministry of Health Malaysia authorisation is required in addition to customs declaration. Prescription drugs require licences/permits from MOH.

IMMIGRATION RULE: Entry/exit from Malaysia is governed by the Immigration Department of Malaysia under the Immigration Act 1959/63, separately from customs.

Contact: Royal Malaysian Customs Department, No. 3, Persiaran Perdana, Presint 2, 62596 Putrajaya. Tel: 03-8882 2100 / 2300. Email: ccc@customs.gov.my
""",
                }
            ),

            // Source B8-2: JKDM Prohibition of Import and Export — Official page
            // DIRECT_OFFICIAL — verified accessible 2026-09-17 via curl --http1.1 (200 OK)
            // CMS header: Last Updated: 02 January 2026.
            // Links to Customs (Prohibition of Imports) Order 2023 and (Prohibition of Export) Order 2023.
            (
                new RegisterSourceDto
                {
                    Name = "JKDM Prohibition of Import and Export — Royal Malaysian Customs Department",
                    Url = "https://www.customs.gov.my/en/individu/pengembara/prohibition-of-import-and-export",
                    Department = "Royal Malaysian Customs Department (Jabatan Kastam Diraja Malaysia / JKDM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Prohibition of Import and Export — Royal Malaysian Customs Department Malaysia",
                    Category = "customs",
                    Content = """
Prohibition of Import and Export — Royal Malaysian Customs Department (JKDM) Malaysia

Source: https://www.customs.gov.my/en/individu/pengembara/prohibition-of-import-and-export
Page last updated: 02 January 2026 (CMS header).
Authority: Royal Malaysian Customs Department (Jabatan Kastam Diraja Malaysia / JKDM)

CUSTOMS (PROHIBITION OF IMPORT) ORDER 2023:

Prior to any importation activity, individuals must ensure that the goods to be imported are not subject to any prohibition or restriction under the Customs (Prohibition of Import) Order 2023.

If the goods to be imported are subject to this Order, the individual must fulfil all the requirements under this Order.

Official reference for the full list of prohibited and restricted imports:
https://lom.agc.gov.my/act-view.php?type=pua&no=P.U.%20(A)%20117/2023
(subject to amendment)

CUSTOMS (PROHIBITION OF EXPORT) ORDER 2023:

Prior to any exporting activity, individuals must ensure that the goods to be exported are not subject to any prohibition or restriction under the Customs (Prohibition of Export) Order 2023.

If the goods to be exported are subject to this Order, the individual must fulfil all the requirements under this Order.

Official reference for the full list of prohibited and restricted exports:
https://lom.agc.gov.my/act-view.php?type=pua&no=P.U.%20(A)%20122/2023
(subject to amendment)

IMPORTANT FOR TRAVELLERS: Travellers entering or leaving Malaysia must check whether any goods in their possession are subject to import or export prohibitions or restrictions. The Customs (Prohibition of Imports) Order 2023 and Customs (Prohibition of Export) Order 2023 define which goods are absolutely prohibited and which require permits or licences from the relevant Malaysian government agency. Carrying prohibited goods is an offence under the Customs Act 1967.

AGENCY BOUNDARY: The Customs (Prohibition of Imports) Order 2023 and Customs (Prohibition of Export) Order 2023 are issued by the Royal Malaysian Customs Department under the Customs Act 1967. Some items may additionally be subject to regulations by other agencies (e.g., MAQIS for agricultural quarantine, MOH for pharmaceuticals, DVS for animals).

Contact: Royal Malaysian Customs Department, No. 3, Persiaran Perdana, Presint 2, 62596 Putrajaya. Tel: 03-8882 2100 / 2300. Email: ccc@customs.gov.my
""",
                }
            ),

            // Source B8-3: JKDM Declaration of Strategic Items by Travellers — Official page
            // DIRECT_OFFICIAL — verified accessible 2026-09-17 via curl --http1.1 (200 OK)
            // CMS header: Last Updated: 02 January 2026.
            // Covers STA 2010 traveller obligations, permit requirements, and penalties.
            (
                new RegisterSourceDto
                {
                    Name = "JKDM Declaration of Strategic Items by Travellers — Royal Malaysian Customs Department",
                    Url = "https://www.customs.gov.my/en/individu/pengembara/declaration-of-sta-for-travellers",
                    Department = "Royal Malaysian Customs Department (Jabatan Kastam Diraja Malaysia / JKDM)",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "Declaration of Strategic Items by Travellers — Royal Malaysian Customs Department Malaysia",
                    Category = "customs",
                    Content = """
Declaration of Strategic Items by Travellers — Royal Malaysian Customs Department (JKDM) Malaysia

Source: https://www.customs.gov.my/en/individu/pengembara/declaration-of-sta-for-travellers
Page last updated: 02 January 2026 (CMS header).
Authority: Royal Malaysian Customs Department (Jabatan Kastam Diraja Malaysia / JKDM)

STRATEGIC TRADE ACT 2010 (STA) — NOTICE TO TRAVELLERS:

The implementation of the Strategic Trade Act 2010 requires travellers to obtain a permit for goods that are listed in Parts 1 and 2 of the Schedule to the Strategic Trade (Strategic Items) Order 2010 and declare to customs using Form Customs No. 22 when leaving Malaysia.

A Special Permit is required for strategic items if destined to restricted countries and destinations as stipulated in Part 3 of the First Schedule to the Strategic Trade (Restricted End-Users and Prohibited End-Users) Order 2010.

Failure to produce a permit and/or special permit and declare to customs for the strategic items is an offence under Section 9(1), 9(2) and 9(3) of the Strategic Trade Act 2010 and on conviction will be punished under Section 9(4), 9(5) and 9(6) of the Act.

The list of strategic goods can be referred to on the official website of the Royal Malaysian Customs Department at www.customs.gov.my, and brief information about the Strategic Trade Act 2010 can be obtained from www.miti.gov.my (Ministry of International Trade and Industry).

IMPORTANT FOR TRAVELLERS: If you are leaving Malaysia carrying goods that could be classified as strategic items (including certain electronics, chemicals, materials, or equipment with dual-use potential), you must check the Strategic Trade (Strategic Items) Order 2010 schedule and obtain the required permit before departure.

DECLARATION FORM: Customs Form No. 22 is used for declaring strategic items at exit points, in addition to Customs Form No. 7 (K7) for general customs declarations.

Contact for enquiries:
Customs Call Centre, Royal Malaysian Customs Department. Tel: 03-7806 7200 (0830-1700 hrs). Fax: 03-7806 7599. Email: ccc@customs.gov.my
The Strategic Trade Secretariat, Ministry of International Trade and Industry. Tel: 03-6203 4683 / 03-6203 3365. Email: sta@miti.gov.my
""",
                }
            ),
        ];
    }

    private static List<(RegisterSourceDto registration, IngestContentDto ingestion)> GetGovernmentServicesSources()
    {
        return
        [
            // B9-1: JPN Marriage and Divorce FAQ
            (
                new RegisterSourceDto
                {
                    Name = "JPN Marriage and Divorce FAQ",
                    Url = "https://www.jpn.gov.my/en/faq/marriage-and-divorce/",
                    Department = "National Registration Department (Jabatan Pendaftaran Negara / JPN), Ministry of Home Affairs",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "JPN Marriage and Divorce FAQ — National Registration Department Malaysia",
                    Category = "government-services",
                    Content = """
JPN Marriage and Divorce FAQ — National Registration Department (Jabatan Pendaftaran Negara / JPN) Malaysia

Source: https://www.jpn.gov.my/en/faq/marriage-and-divorce/
Authority: National Registration Department (JPN), Ministry of Home Affairs
Page last verified: 17 September 2026

AGENCY SCOPE: JPN handles civil registration of marriages and divorces for non-Muslims under the Law Reform (Marriage and Divorce) Act 1976 (Act 164). Muslim marriages and divorces are handled separately by the State Islamic Religious Affairs Department. Immigration matters (e.g., spouse visa, dependent pass) are handled by the Immigration Department, not JPN.

MARRIAGE REGISTRATION (NON-MUSLIM):

All non-Muslim marriages in Malaysia must be registered under the Law Reform (Marriage and Divorce) Act 1976 (Act 164), which came into effect on 01 March 1982. Non-Muslim marriages under Act 164 are monogamous — a legally married person cannot remarry while the existing marriage is still in effect, whether in Malaysia or abroad.

WHO CAN SOLEMNIZE A MARRIAGE: Marriage can only be solemnized by a person appointed as a Registrar of Marriages under Act 164 at the National Registration Department (JPN) or Malaysian representative offices abroad, or by an Assistant Registrar of Marriages at a temple, church, or association. It is an offence for an unauthorized person to solemnize a marriage — penalty: imprisonment not more than 10 years and fine not more than RM 15,000.

FALSE PERSONAL DETAILS: If a party provides false personal details when registering a marriage, the penalty upon conviction is imprisonment not more than 3 years or fine not more than RM 3,000 or both.

NOTICE PERIOD: There is a mandatory 21-day display period for the Application Form, Marriage Notification and Written Declaration (Form NRD.KC02). This can be exempted by applying for a special licence under Section 21(1) of Act 164 from the Registrar of Marriages in the relevant state. Fee for special licence: RM 100.00.

MARRIAGE AT TEMPLE/CHURCH/ASSOCIATION: Registration of marriage under Act 164 can only be solemnized at a temple, church, or association where the NRD has appointed an Assistant Registrar of Marriages (PPP).

MARRIAGE REGISTRATION FEE AT TEMPLE/CHURCH/ASSOCIATION: RM 20.00. Payment must be made at the wedding counter, NRD office. Payment receipt is issued in the name of the male spouse.

DOCUMENTS FOR MARRIAGE REGISTRATION: Couples must present together at the NRD office with Form NRD.KC02 (which must include a Statutory Declaration in Section D.2 made before a Commissioner for Oaths / Justice of Peace / Magistrate) and MyKad (identity card) respectively.

MARRIAGE ABROAD — RE-REGISTRATION FOR MALAYSIAN CITIZENS: If one or both spouses are Malaysian citizens and got married abroad under the civil law of the foreign country, the marriage must be re-registered under Section 31 of Act 164 at the nearest NRD office or Malaysian representative office abroad, within 6 months of the date of marriage abroad or within 6 months of arriving in Malaysia. A penalty fee will be charged for late registration.

FOREIGNER-RELEVANT NOTE: Foreigners marrying Malaysian citizens must follow Act 164 procedures. The marriage registration at JPN is separate from any immigration application for a spouse visa or dependent pass — those are handled by the Immigration Department of Malaysia.

DIVORCE / ANNULMENT: A Decree of Absolute Nisi is a divorce document issued by the High Court confirming the dissolution of a valid marriage. If the decree is lost, a copy can be obtained from the appointed lawyer or a certified copy from the relevant High Court.

MARRIAGE TRIBUNAL: Disputes can be referred to the Marriage Tribunal at the NRD office. The petitioner must submit an application at the nearest NRD office.

MARITAL STATUS CONFIRMATION LETTER: The Certificate of Confirmation of Marital Status is issued only at the Headquarters, National Registration Department, Putrajaya. It is for Malaysian citizens who need to do official business abroad.

CONTACT AND ENQUIRIES: Enquiries can be submitted through the Public Complaint Management System (SISPAA NRD) at https://jpn.spab.gov.my. Customer Service Officer: Tel: 03-80008000, Fax: 03-88808288, Email: pro@jpn.gov.my
""",
                }
            ),

            // B9-2: JPN Identity Card FAQ
            (
                new RegisterSourceDto
                {
                    Name = "JPN Identity Card FAQ",
                    Url = "https://www.jpn.gov.my/en/faq/identity-card/",
                    Department = "National Registration Department (Jabatan Pendaftaran Negara / JPN), Ministry of Home Affairs",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "JPN Identity Card FAQ — National Registration Department Malaysia",
                    Category = "government-services",
                    Content = """
JPN Identity Card FAQ — National Registration Department (Jabatan Pendaftaran Negara / JPN) Malaysia

Source: https://www.jpn.gov.my/en/faq/identity-card/
Authority: National Registration Department (JPN), Ministry of Home Affairs
Page last verified: 17 September 2026

AGENCY SCOPE: JPN issues and manages identity cards for Malaysian citizens and permanent residents. Immigration passes and work permits are handled by the Immigration Department, not JPN. Foreigners interact with JPN primarily for MyPR (Permanent Resident card) applications or when accompanying Malaysian family members for registration matters.

IDENTITY CARDS IN MALAYSIA:

MyKad — the national identity card for Malaysian citizens aged 12 and above.
MyKid — identity card for Malaysian children below 12 years old.
MyPR — Permanent Resident identity card (red card) for foreigners who have been granted an Entry Permit by the Immigration Department.
MyKAS — Temporary Identity Certificate.

MyPR (PERMANENT RESIDENT CARD) — FOR FOREIGNERS:

A foreign national who has obtained an Entry Permit from the Malaysian Immigration Department is eligible to apply for a Permanent Resident identity card (MyPR, red card).

Documents needed for MyPR application:
- Entry Permit (issued by Immigration Department)
- Passport
- Copies of related documents

Application fee: RM 40.00.
Application can only be made at the Putrajaya NRD or the State NRD Headquarters.

IMPORTANT FOR FOREIGNERS: The Entry Permit must first be obtained from the Immigration Department before applying to JPN for MyPR. JPN does not process immigration applications — it only issues the physical identity card after the Immigration Department has approved permanent residency.

NEW GENERATION MyKad (Effective 17 September 2026):

The New Generation MyKad is a new version introduced by JPN with improvements to design, security features, and functions. Key facts:
- Issued in phases starting 17 September 2026.
- Existing MyKad holders do NOT need to replace their cards unless the card is damaged.
- Holders of Plastic Identity Card (KPP), High Quality Identity Card (KPT), and MyKad without a ghost image DO need to replace with the New Generation MyKad.
- The MyKad number does NOT change when receiving a New Generation MyKad.
- Touch 'n Go facility is NO LONGER provided in the New Generation MyKad. Users should use Touch 'n Go eWallet or a separate Touch 'n Go card.
- Same-day printing available at select JPN branches (list includes JPN Putrajaya, UTC locations in all states, and state JPN offices).
- Standard processing: 5 working days in Peninsular Malaysia, 7 working days in Sabah, Sarawak, and Labuan.

IDENTITY CARD FEES AND PROCEDURES:

First identity card: Issued at age 12, must be replaced at age 18 (no fine if replaced between ages 18-25).
MyKad chip damage: Free replacement within 1 year if not due to negligence. After 1 year, fee of RM 10.00.
Lost identity card: Fee charged based on the amount of loss (refer to official NRD payment schedule). Second loss requires a police report.
MyKad processing time: Within 30 minutes at NRD offices with distributed printing machines. Otherwise 5 working days (Peninsular) / 7 working days (Sabah/Sarawak/Labuan) for uncomplicated applications.
Address change: Cannot change address on identity card chip without issuing a replacement card.

PAYMENT METHODS: NRD accepts cash, credit card, debit card, and MEPs (electronic payments).

APPOINTMENTS: Online appointment booking available at https://mytemujanji.jpn.gov.my/

CONTACT AND ENQUIRIES: e-Enquiry (SISPAA NRD): https://jpn.spab.gov.my/eApps/system/index.do. Customer Service Officer: Tel: 03-80008000, Fax: 03-88808288, Email: pro@jpn.gov.my
""",
                }
            ),

            // B9-3: JPN Birth Registration FAQ
            (
                new RegisterSourceDto
                {
                    Name = "JPN Birth Registration FAQ",
                    Url = "https://www.jpn.gov.my/en/faq/birth/",
                    Department = "National Registration Department (Jabatan Pendaftaran Negara / JPN), Ministry of Home Affairs",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "JPN Birth Registration FAQ — National Registration Department Malaysia",
                    Category = "government-services",
                    Content = """
JPN Birth Registration FAQ — National Registration Department (Jabatan Pendaftaran Negara / JPN) Malaysia

Source: https://www.jpn.gov.my/en/faq/birth/
Authority: National Registration Department (JPN), Ministry of Home Affairs
Page last verified: 17 September 2026

AGENCY SCOPE: JPN handles birth registration in Malaysia. Birth registration must be done within 60 days of birth in Peninsular Malaysia. The birth registration at JPN determines the child's legal identity record. Citizenship determination (for children with foreign parents) involves separate legal provisions under the Federal Constitution and may require consultation with the Immigration Department.

BIRTH REGISTRATION PROCEDURES:

WHO CAN NOTIFY A BIRTH: The following persons may notify the birth of a child to the NRD:
- Father of the child
- Mother of the child
- Persons living in the household where the child was born
- Any person witnessing the birth
- Any person caring for the child

LATE BIRTH REGISTRATION: If a child does not have a Birth Certificate, is over 60 days old, and was born within Peninsular Malaysia, a Late Registration of Birth Application can be made at any nearby NRD counter within Peninsular Malaysia, provided the parent or legal guardian has proof that the birth actually took place within Peninsular Malaysia.

CHILDREN BORN OUT OF WEDLOCK (NON-MUSLIM): The father's information for children born out of wedlock will NOT be recorded in the birth register, except with a joint application by the mother and the person claiming to be the father. The person claiming to be the father must sign the birth register together with the child's mother.

NAME CHANGE ON BIRTH CERTIFICATE: The name of the child on the Birth Certificate may be changed through an Information Correction Request if the child has not attained 1 year of age.

LOST/DAMAGED BIRTH CERTIFICATE — EXTRACT: The owner of the Birth Certificate, parents, or heirs may apply for an Extract of the Birth Certificate at the nearest NRD office. Required: Identity Card or identity document (original and copy) and Form NRD.LM12. Fee: RM 5.00 per Birth Certificate Extract.

FOREIGNER-RELEVANT NOTES:
- Births of children in Malaysia where one or both parents are foreign nationals are registered at JPN.
- The child's citizenship status depends on constitutional provisions (e.g., Article 14, 15 of the Federal Constitution) and the nationality/citizenship of the parents.
- JPN registers the birth; citizenship determination is a separate legal process.
- If a Malaysian citizen gives birth abroad, registration procedures differ (see JPN Citizenship FAQ).

CONTACT AND ENQUIRIES: e-Enquiry (SISPAA NRD): https://jpn.spab.gov.my. Customer Service Officer: Tel: 03-80008000, Fax: 03-88808288, Email: pro@jpn.gov.my
""",
                }
            ),

            // B9-4: JPN Citizenship FAQ
            (
                new RegisterSourceDto
                {
                    Name = "JPN Citizenship FAQ",
                    Url = "https://www.jpn.gov.my/en/faq/citizenship/",
                    Department = "National Registration Department (Jabatan Pendaftaran Negara / JPN), Ministry of Home Affairs",
                    CountryCode = "MY",
                },
                new IngestContentDto
                {
                    Title = "JPN Citizenship FAQ — National Registration Department Malaysia",
                    Category = "government-services",
                    Content = """
JPN Citizenship FAQ — National Registration Department (Jabatan Pendaftaran Negara / JPN) Malaysia

Source: https://www.jpn.gov.my/en/faq/citizenship/
Authority: National Registration Department (JPN), Ministry of Home Affairs
Page last verified: 17 September 2026

AGENCY SCOPE: JPN processes citizenship applications and related registrations. The decision on citizenship status is made by the Minister of Home Affairs (the Honourable Minister) under the Federal Constitution. JPN serves as the administrative channel but does not make the final determination. Immigration matters (entry permits, passes) are handled by the Immigration Department, not JPN.

CITIZENSHIP APPLICATION — KEY INFORMATION FOR FOREIGNERS AND MIXED-NATIONALITY FAMILIES:

APPLICATION OUTCOME: The Government may decide an application is unsuccessful even when all requirements are fulfilled. The decision of the Honourable Minister is final. If the application is unsuccessful, the applicant may submit a new application.

EXPEDITING AN APPLICATION: There is no guaranteed processing time. The applicant may submit a letter stating reasons for expediting. Consideration and decision are subject to the Government of Malaysia.

CHILD BORN ABROAD TO MALAYSIAN CITIZEN: A child born abroad to a Malaysian citizen who is already one year old may apply for an extension of overseas birth registration through the Honourable Minister, by completing the required form and providing reasons for late registration. The application can be made at any Malaysian Representative Office abroad, or at the nearest NRD office if the family has returned to Malaysia.

CHILD BORN IN SINGAPORE TO MALAYSIAN CITIZEN: Registration with the Representative Office in Singapore is not required. If the child intends to reside in Malaysia, it is sufficient to apply for confirmation of citizenship status with NRD through the Representative Office in Singapore, and then apply for status certification with the Immigration Department.

CHILD BORN ABROAD TO MALAYSIAN CITIZEN MARRIED TO FOREIGN NATIONAL: The Malaysian citizen parent may apply for citizenship for the child under Article 15(2) of the Federal Constitution. The application can be submitted at the nearest NRD Headquarters.

REVOKED OR RENOUNCED CITIZENSHIP: Persons whose citizenship has been revoked or who have renounced Malaysian citizenship are advised to visit an NRD office for an eligibility check, bringing all relevant documents.

FOREIGNER-RELEVANT NOTES:
- Citizenship applications by foreigners (naturalisation) are governed by Article 19 of the Federal Constitution and require meeting residency, character, and language requirements.
- JPN processes the paperwork but the Minister of Home Affairs makes the final decision.
- There is NO guaranteed processing time or guaranteed approval for any citizenship application.
- Permanent Residency (MyPR) is separate from citizenship — PR is handled via Immigration Department entry permit, with JPN issuing the MyPR card afterward.

CONTACT AND ENQUIRIES: e-Enquiry (SISPAA NRD): https://jpn.spab.gov.my. Customer Service Officer: Tel: 03-80008000, Fax: 03-88808288, Email: pro@jpn.gov.my
""",
                }
            ),
        ];
    }
}
