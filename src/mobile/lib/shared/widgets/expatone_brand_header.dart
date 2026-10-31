import 'package:flutter/material.dart';

/// Header image is supplied separately as assets/destinations/expatone_header_logo.png.
/// Until the image is checked in, the app displays its existing text branding.
class ExpatOneBrandHeader extends StatelessWidget {
  const ExpatOneBrandHeader({super.key, this.height = 44});

  final double height;

  @override
  Widget build(BuildContext context) => SizedBox(
    height: height,
    child: Image.asset(
      'assets/destinations/expatone_header_logo.png',
      fit: BoxFit.contain,
      alignment: Alignment.centerLeft,
      errorBuilder: (context, error, stackTrace) => Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Icon(Icons.public, color: Color(0xFF0867DF)),
          const SizedBox(width: 8),
          Text('ExpatOne', style: Theme.of(context).textTheme.titleLarge?.copyWith(
            fontWeight: FontWeight.bold, color: const Color(0xFF07183C))),
        ],
      ),
    ),
  );
}
