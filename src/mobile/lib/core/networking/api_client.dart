import 'dart:convert';
import 'package:http/http.dart' as http;
import '../constants/api_constants.dart';
import '../error/app_exception.dart';
import '../services/auth_service.dart';

class ApiClient {
  final http.Client _httpClient;
  IAuthService? _authService;

  ApiClient({http.Client? httpClient})
      : _httpClient = httpClient ?? http.Client();

  void setAuthService(IAuthService authService) {
    _authService = authService;
  }

  Future<Map<String, String>> _getHeaders() async {
    final headers = <String, String>{
      'Content-Type': 'application/json',
    };

    if (_authService != null) {
      final token = await _authService!.getIdToken();
      if (token != null) {
        headers['Authorization'] = 'Bearer $token';
      }
    }

    return headers;
  }

  Future<Map<String, dynamic>> get(String path,
      {Map<String, String>? queryParams}) async {
    final uri = _buildUri(path, queryParams);
    final headers = await _getHeaders();
    final response = await _httpClient
        .get(uri, headers: headers)
        .timeout(ApiConstants.connectTimeout);
    return _handleResponse(response);
  }

  Future<Map<String, dynamic>> post(String path,
      {Map<String, dynamic>? body, Duration? timeout}) async {
    final uri = _buildUri(path);
    final headers = await _getHeaders();
    final response = await _httpClient
        .post(uri, headers: headers, body: body != null ? jsonEncode(body) : null)
        .timeout(timeout ?? ApiConstants.connectTimeout);
    return _handleResponse(response);
  }

  // For POST endpoints that return a JSON array (not an object).
  // Validates status only; the caller discards or parses the body separately.
  Future<void> postVoid(String path, {Map<String, dynamic>? body}) async {
    final uri = _buildUri(path);
    final headers = await _getHeaders();
    final response = await _httpClient
        .post(uri, headers: headers, body: body != null ? jsonEncode(body) : null)
        .timeout(ApiConstants.connectTimeout);
    _checkStatus(response);
  }

  Future<Map<String, dynamic>> put(String path,
      {Map<String, dynamic>? body}) async {
    final uri = _buildUri(path);
    final headers = await _getHeaders();
    final response = await _httpClient
        .put(uri, headers: headers, body: body != null ? jsonEncode(body) : null)
        .timeout(ApiConstants.connectTimeout);
    return _handleResponse(response);
  }

  Future<List<Map<String, dynamic>>> getList(String path,
      {Map<String, String>? queryParams}) async {
    final uri = _buildUri(path, queryParams);
    final headers = await _getHeaders();
    final response = await _httpClient
        .get(uri, headers: headers)
        .timeout(ApiConstants.connectTimeout);
    _checkStatus(response);
    if (response.body.isEmpty) return [];
    final decoded = jsonDecode(response.body);
    if (decoded is List) {
      return decoded.cast<Map<String, dynamic>>();
    }
    return [];
  }

  Future<Map<String, dynamic>> delete(String path) async {
    final uri = _buildUri(path);
    final headers = await _getHeaders();
    final response = await _httpClient
        .delete(uri, headers: headers)
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

  void _checkStatus(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    if (response.statusCode == 401) throw const AuthException('Authentication required');
    if (response.statusCode == 403) throw const AuthException('Access denied');
    String message = 'Request failed';
    try {
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      message = body['message'] as String? ?? message;
    } catch (_) {}
    throw NetworkException(message, statusCode: response.statusCode);
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
