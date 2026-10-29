import 'package:flutter/material.dart';

/// Reusable, accessible avatar with a stable fallback when no photo exists.
/// Pass only an authorized image URL (never an S3 object key).
class ProfileAvatar extends StatelessWidget {
  final String? imageUrl;
  final String? displayName;
  final String? email;
  final double radius;

  const ProfileAvatar({
    super.key,
    this.imageUrl,
    this.displayName,
    this.email,
    this.radius = 22,
  });

  String get _initials {
    final name = (displayName?.trim().isNotEmpty == true
            ? displayName!
            : (email ?? ''))
        .trim();
    if (name.isEmpty) return '';
    final words = name.split(RegExp(r'\\s+')).where((w) => w.isNotEmpty).toList();
    if (words.length >= 2) {
      return '${words.first[0]}${words.last[0]}'.toUpperCase();
    }
    return name.substring(0, name.length >= 2 ? 2 : 1).toUpperCase();
  }

  @override
  Widget build(BuildContext context) {
    final url = imageUrl?.trim();
    final hasImage = url != null &&
        url.isNotEmpty &&
        (Uri.tryParse(url)?.isScheme('https') ?? false);
    final colors = Theme.of(context).colorScheme;
    final fallback = _initials.isNotEmpty
        ? Text(_initials, style: TextStyle(
            color: colors.onPrimaryContainer,
            fontWeight: FontWeight.w700,
            fontSize: radius * 0.6,
          ))
        : Icon(Icons.person_outline, size: radius, color: colors.onPrimaryContainer);
    return Semantics(
      label: 'Profile picture of ${displayName ?? email ?? 'user'}',
      image: true,
      child: CircleAvatar(
        radius: radius,
        backgroundColor: colors.primaryContainer,
        child: hasImage
            ? ClipOval(
                child: Image.network(
                  url,
                  width: radius * 2,
                  height: radius * 2,
                  fit: BoxFit.cover,
                  errorBuilder: (_, __, ___) => fallback,
                ),
              )
            : fallback,
      ),
    );
  }
}
