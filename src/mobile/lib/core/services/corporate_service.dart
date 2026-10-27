import '../networking/api_client.dart';

class CorporateService {
  final ApiClient api;
  CorporateService(this.api);
  // Phase 3.1: offer catalogs and company HR settings.
  Future<List<Map<String, dynamic>>> offerCountries() =>
      api.getList('/corporate/offer-catalog/countries');
  Future<List<Map<String, dynamic>>> offerCurrencies() =>
      api.getList('/corporate/offer-catalog/currencies');
  Future<List<Map<String, dynamic>>> companyBenefits(String companyId, {
    bool includeInactive = false,
  }) => api.getList('/corporate/companies/$companyId/settings/benefits',
      queryParams: {'includeInactive': '$includeInactive'});
  Future<Map<String, dynamic>> companyProfile(String companyId) =>
      api.get('/corporate/companies/$companyId/settings/profile');
  Future<Map<String, dynamic>> saveCompanyProfile(
    String companyId, Map<String, dynamic> values,
  ) => api.put('/corporate/companies/$companyId/settings/profile', body: values);
  Future<Map<String, dynamic>> createCompanyBenefit(
    String companyId, Map<String, dynamic> values,
  ) => api.post('/corporate/companies/$companyId/settings/benefits', body: values);
  Future<Map<String, dynamic>> updateCompanyBenefit(
    String companyId, String benefitId, Map<String, dynamic> values,
  ) => api.put('/corporate/companies/$companyId/settings/benefits/$benefitId',
      body: values);
  Future<void> deactivateCompanyBenefit(String companyId, String benefitId) async {
    await api.delete('/corporate/companies/$companyId/settings/benefits/$benefitId');
  }
  Future<List<Map<String, dynamic>>> offerSignatories(String companyId) =>
      api.getList('/corporate/companies/$companyId/settings/signatories');

  Future<List<Map<String, dynamic>>> companies() =>
      api.getList('/corporate/companies');
  Future<List<Map<String, dynamic>>> issuedPermits(String companyId) =>
      api.getList('/corporate/companies/$companyId/permits');

  Future<List<Map<String, dynamic>>> permitAlerts(String companyId) =>
      api.getList('/corporate/companies/$companyId/permits/alerts');

  Future<void> acknowledgePermitAlert(String companyId, String alertId) =>
      api.postVoid('/corporate/companies/$companyId/permits/alerts/$alertId/acknowledge');

  Future<Map<String, dynamic>> recordIssuedPermit(
    String companyId, Map<String, dynamic> payload,
  ) => api.post('/corporate/companies/$companyId/permits', body: payload);

  Future<Map<String, dynamic>> changePermitStatus(
    String companyId, String permitId, String status,
  ) => api.put('/corporate/companies/$companyId/permits/$permitId/status',
      body: {'status': status});

  Future<Map<String, dynamic>> issueOffer(
    String companyId, String employeeUserId, String renderedText, {
    String? replacesOfferId,
  }) => api.post(
    '/corporate/offers/companies/$companyId/issue',
    body: {'employeeUserId': employeeUserId, 'renderedText': renderedText,
      'replacesOfferId': ?replacesOfferId},
  );

  Future<List<Map<String, dynamic>>> companyOffers(String companyId) =>
      api.getList('/corporate/offers/companies/$companyId');

  Future<List<Map<String, dynamic>>> myOffers() =>
      api.getList('/corporate/offers/mine');

  Future<Map<String, dynamic>> offerDocumentUrl(String documentId) =>
      api.get('/documents/$documentId/access-url');

  Future<Map<String, dynamic>> respondToOffer(String offerId, String response) =>
      api.post('/corporate/offers/$offerId/respond', body: {'response': response});

  Future<List<Map<String, dynamic>>> invitations() =>
      api.getList('/corporate/invitations');
  Future<Map<String, dynamic>> createCompany(String name) =>
      api.post('/corporate/companies', body: {'name': name});
  /// HR-only, bounded search for registered users not already in the company.
  /// The API requires at least three characters and returns at most 10 matches.
  Future<List<Map<String, dynamic>>> searchRegisteredEmployees(
    String companyId,
    String query,
  ) {
    final term = query.trim();
    if (term.length < 3) return Future.value([]);
    return api.getList(
      '/corporate/companies/$companyId/employee-search',
      queryParams: {'q': term},
    );
  }

  Future<Map<String, dynamic>> invite(
    String companyId,
    String email,
    String role,
  ) => api.post(
    '/corporate/companies/$companyId/invite',
    body: {'email': email, 'role': role},
  );
  Future<Map<String, dynamic>> accept(String id) =>
      api.post('/corporate/invitations/$id/accept');
  Future<Map<String, dynamic>> decline(String id) =>
      api.post('/corporate/invitations/$id/decline');
  Future<Map<String, dynamic>> dashboard(String id) =>
      api.get('/corporate/companies/$id/dashboard');
  Future<Map<String, dynamic>> catalog() =>
      api.get('/visa-applications/catalog');
  // Existing authenticated, owner-scoped Application Assistant list.
  Future<List<Map<String, dynamic>>> myVisaApplications() =>
      api.getList('/visa-applications');
  Future<Map<String, dynamic>> memberRole(
    String companyId,
    String membershipId,
    String role,
  ) => api.put(
    '/corporate/companies/$companyId/members/$membershipId/role',
    body: {'role': role},
  );
  Future<Map<String, dynamic>> updateWorkforce(
    String companyId,
    String membershipId, {
    required String classification,
    String? nationality,
    String? employmentCountry,
    DateTime? employmentStartDate,
  }) => api.put(
    '/corporate/companies/$companyId/members/$membershipId/workforce',
    body: {
      'workforceClassification': classification,
      'nationality': nationality,
      'employmentCountry': employmentCountry,
      'employmentStartDate': employmentStartDate?.toIso8601String(),
    },
  );

  Future<Map<String, dynamic>> memberStatus(
    String companyId,
    String membershipId,
    String status,
  ) => api.put(
    '/corporate/companies/$companyId/members/$membershipId/status',
    body: {'status': status},
  );
  Future<Map<String, dynamic>> corporateDocumentUrl(
    String companyId,
    String caseId,
    String documentId,
  ) => api.get(
    '/corporate/companies/$companyId/cases/$caseId/visa/documents/$documentId/access-url',
  );
  Future<Map<String, dynamic>> corporateVisa(String companyId, String caseId) =>
      api.get('/corporate/companies/$companyId/cases/$caseId/visa');
  Future<List<Map<String, dynamic>>> employees(String id) =>
      api.getList('/corporate/companies/$id/employees');
  Future<List<Map<String, dynamic>>> cases(String id) async {
    final result = <Map<String, dynamic>>[];
    final seen = <String>{};
    while (true) {
      final page = await api.getList(
        '/corporate/companies/$id/cases',
        queryParams: {'skip': '${result.length}'},
      );
      // Older API versions ignore skip: never loop forever or claim completeness.
      for (final row in page) {
        if (!seen.add(row['id'].toString())) {
          throw StateError(
            'Task pagination is unavailable. Deploy the updated corporate API and retry.',
          );
        }
        result.add(row);
      }
      if (page.length < 250) return result;
    }
  }

  Future<Map<String, dynamic>> createCase(
    String id,
    Map<String, dynamic> data,
  ) => api.post('/corporate/companies/$id/cases', body: data);
  Future<Map<String, dynamic>> updateCase(
    String id,
    String caseId,
    Map<String, dynamic> data,
  ) => api.put('/corporate/companies/$id/cases/$caseId', body: data);
  Future<Map<String, dynamic>> linkVisa(
    String id,
    String caseId,
    String visaId,
  ) => api.post(
    '/corporate/companies/$id/cases/$caseId/link-my-visa',
    body: {'visaApplicationId': visaId},
  );
  Future<void> unlinkVisa(String id, String caseId) =>
      api.postVoid('/corporate/companies/$id/cases/$caseId/unlink-my-visa');
}
