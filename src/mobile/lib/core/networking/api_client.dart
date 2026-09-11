import 'dart:convert';
import 'package:http/http.dart' as http;
import '../constants/api_constants.dart';
import '../error/app_exception.dart';

class ApiClient {
  final http.Client _httpClient;
  String? _authToken;

  ApiClient({http.Client? httpClient})
      : _httpClient = httpClient ?? http.Client();

  void setAuthToken(String? token) {
    _authToken = token;
  }

  Map<String, String> get _headers => {
        'Content-Type': 'application/json',
        if (_authToken != null) 'Authorization': 'Bearer $_authToken',
      };

  Future<Map<String, dynamic>> get(String path,
      {Map<String, String>? queryParams}) async {
    final uri = _buildUri(path, queryParams);
    final response = await _httpClient
        .get(uri, headers: _headers)
        .timeout(ApiConstants.connectTimeout);
    return _handleResponse(response);
  }

  Future<Map<String, dynamic>> post(String path,
      {Map<String, dynamic>? body}) async {
    final uri = _buildUri(path);
    final response = await _httpClient
        .post(uri, headers: _headers, body: body != null ? jsonEncode(body) : null)
        .timeout(ApiConstants.connectTimeout);
    return _handleResponse(response);
  }

  Future<Map<String, dynamic>> put(String path,
      {Map<String, dynamic>? body}) async {
    final uri = _buildUri(path);
    final response = await _httpClient
        .put(uri, headers: _headers, body: body != null ? jsonEncode(body) : null)
        .timeout(ApiConstants.connectTimeout);
    return _handleResponse(response);
  }

  Future<Map<String, dynamic>> delete(String path) async {
    final uri = _buildUri(path);
    final response = await _httpClient
        .delete(uri, headers: _headers)
        .timeout(ApiConstants.connectTimeout);
    return _handleResponse(response);
  }

  Uri _buildUri(String path, [Map<String, String>? queryParams]) {
    final baseUri = Uri.parse(ApiConstants.baseUrl);
    return baseUri.replace(
      path: '${baseUri.path}$path',
      queryParameters: queryParams,
    );
  }

  Map<String, dynamic> _handleResponse(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) {
      if (response.body.isEmpty) return {};
      return jsonDecode(response.body) as Map<String, dynamic>;
    }

    if (response.statusCode == 401) {
      throw const AuthException('Authentication required');
    }

    if (response.statusCode == 403) {
      throw const AuthException('Access denied');
    }

    String message = 'Request failed';
    try {
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      message = body['message'] as String? ?? message;
    } catch (_) {}

    throw NetworkException(
      message,
      statusCode: response.statusCode,
    );
  }

  void dispose() {
    _httpClient.close();
  }
}
