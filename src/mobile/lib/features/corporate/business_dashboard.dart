import 'package:flutter/material.dart';

typedef BusinessRecord = Map<String, dynamic>;

/// Presentation only: authorization and mutations remain in CorporateScreen/API.
class BusinessDashboard extends StatefulWidget {
  final List<BusinessRecord> companies, employees, tasks;
  final BusinessRecord selected;
  final BusinessRecord? metrics;
  final bool isHr, isOwner, loading;
  final String? error;
  final Future<void> Function() onRefresh, onInvite, onNewTask;
  final ValueChanged<String> onCompany;
  final Future<void> Function(BusinessRecord) onEditMember;
  final Widget Function(BusinessRecord) taskActions;
  final List<Widget> invitations;
  const BusinessDashboard({
    super.key,
    required this.companies,
    required this.selected,
    required this.employees,
    required this.tasks,
    required this.metrics,
    required this.isHr,
    required this.isOwner,
    required this.loading,
    this.error,
    required this.onRefresh,
    required this.onCompany,
    required this.onInvite,
    required this.onNewTask,
    required this.onEditMember,
    required this.taskActions,
    this.invitations = const [],
  });
  @override
  State<BusinessDashboard> createState() => _BusinessDashboardState();
}

class _BusinessDashboardState extends State<BusinessDashboard> {
  static const navy = Color(0xff102a43), teal = Color(0xff126782);
  int page = 0;
  String employeeQuery = '',
      taskQuery = '',
      memberStatus = 'All',
      role = 'All',
      taskStatus = 'All',
      country = 'All',
      sort = 'Earliest first';
  bool dueOnly = false;
  final employeeSearch = TextEditingController();
  final taskSearch = TextEditingController();
  final scroll = ScrollController();

  @override
  void dispose() {
    employeeSearch.dispose();
    taskSearch.dispose();
    scroll.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(covariant BusinessDashboard oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.selected['id'] != widget.selected['id'] ||
        oldWidget.isHr != widget.isHr) {
      page = 0;
      employeeQuery = '';
      taskQuery = '';
      memberStatus = 'All';
      role = 'All';
      taskStatus = 'All';
      country = 'All';
      dueOnly = false;
      employeeSearch.clear();
      taskSearch.clear();
    }
  }

  void go(int value) {
    setState(() => page = value);
    if (scroll.hasClients) scroll.jumpTo(0);
  }

  String str(BusinessRecord r, String key, [String fallback = '']) =>
      r[key]?.toString() ?? fallback;
  String name(BusinessRecord r) => str(r, 'displayName').trim().isNotEmpty
      ? str(r, 'displayName')
      : str(r, 'email', 'Employee');
  String label(String value) => value == 'InProgress'
      ? 'In progress'
      : value == 'HR'
      ? 'HR Manager'
      : value;
  BusinessRecord? member(BusinessRecord task) {
    for (final e in widget.employees) {
      if (e['id'] == task['employeeUserId']) return e;
    }
    return null;
  }

  String assignee(BusinessRecord task) {
    final e = member(task);
    return e == null
        ? (widget.isHr
              ? 'Employee ${str(task, 'employeeUserId')}'
              : 'Assigned to you')
        : name(e);
  }

  DateTime? date(BusinessRecord task) =>
      DateTime.tryParse(str(task, 'dueAt'))?.toLocal();
  String dateLabel(BusinessRecord task) {
    final d = date(task);
    if (d == null) return 'No due date';
    return MaterialLocalizations.of(context).formatMediumDate(d);
  }

  bool dueSoon(BusinessRecord task) =>
      task['status'] != 'Closed' &&
      date(task) != null &&
      !date(task)!.isAfter(DateTime.now().add(const Duration(days: 30)));
  List<String> options(Iterable<String> values) => [
    'All',
    ...(values.where((v) => v.isNotEmpty).toSet().toList()..sort()),
  ];
  List<BusinessRecord> get matchingEmployees => widget.employees
      .where(
        (e) =>
            '${name(e)} ${str(e, 'email')}'.toLowerCase().contains(
              employeeQuery.trim().toLowerCase(),
            ) &&
            (memberStatus == 'All' || e['status'] == memberStatus) &&
            (role == 'All' || e['role'] == role),
      )
      .toList();
  List<BusinessRecord> get matchingTasks {
    final result = widget.tasks
        .where(
          (t) =>
              '${str(t, 'title')} ${assignee(t)} ${member(t)?['email'] ?? ''} ${str(t, 'purpose')}'
                  .toLowerCase()
                  .contains(taskQuery.trim().toLowerCase()) &&
              (taskStatus == 'All' || t['status'] == taskStatus) &&
              (country == 'All' || t['countryCode'] == country) &&
              (!dueOnly || dueSoon(t)),
        )
        .toList();
    result.sort((a, b) {
      final ad = date(a), bd = date(b);
      if (ad == null) return bd == null ? 0 : 1;
      if (bd == null) return -1;
      return sort == 'Latest first' ? bd.compareTo(ad) : ad.compareTo(bd);
    });
    return result;
  }

  Widget pill(String text, [Color color = teal]) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
    decoration: BoxDecoration(
      color: color.withValues(alpha: .09),
      borderRadius: BorderRadius.circular(20),
    ),
    child: Text(
      label(text),
      style: TextStyle(color: color, fontSize: 12, fontWeight: FontWeight.w600),
    ),
  );
  Widget card(Widget child) => Container(
    margin: const EdgeInsets.only(bottom: 16),
    padding: const EdgeInsets.all(18),
    decoration: BoxDecoration(
      color: Colors.white,
      borderRadius: BorderRadius.circular(16),
      border: Border.all(color: const Color(0xffdfe7ef)),
    ),
    child: child,
  );
  Widget heading(String title, [Widget? action]) => Padding(
    padding: const EdgeInsets.only(bottom: 16),
    child: Wrap(
      spacing: 16,
      runSpacing: 10,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(
          title,
          style: const TextStyle(
            fontSize: 21,
            fontWeight: FontWeight.w700,
            color: navy,
          ),
        ),
        if (action != null) action,
      ],
    ),
  );
  Widget empty(String title) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 28),
    child: Center(child: Text(title, textAlign: TextAlign.center)),
  );
  Widget search(
    TextEditingController controller,
    String hint,
    ValueChanged<String> onChanged,
  ) => TextField(
    controller: controller,
    onChanged: onChanged,
    decoration: InputDecoration(
      hintText: hint,
      prefixIcon: const Icon(Icons.search),
      suffixIcon: controller.text.isEmpty
          ? null
          : IconButton(
              tooltip: 'Clear search',
              icon: const Icon(Icons.close),
              onPressed: () {
                controller.clear();
                onChanged('');
              },
            ),
    ),
  );
  Widget filter(
    String title,
    String value,
    List<String> values,
    ValueChanged<String> change,
  ) => SizedBox(
    width: 180,
    child: DropdownButtonFormField<String>(
      key: ValueKey('$title:$value'),
      initialValue: values.contains(value) ? value : values.first,
      isExpanded: true,
      decoration: InputDecoration(labelText: title),
      items: values
          .map((v) => DropdownMenuItem(value: v, child: Text(label(v))))
          .toList(),
      onChanged: (v) {
        if (v != null) setState(() => change(v));
      },
    ),
  );
  Widget chips(
    List<String> values,
    String selected,
    ValueChanged<String> change,
  ) => Wrap(
    spacing: 8,
    runSpacing: 8,
    children: values
        .map(
          (v) => ChoiceChip(
            label: Text(label(v)),
            selected: selected == v,
            onSelected: (_) => setState(() => change(v)),
          ),
        )
        .toList(),
  );

  @override
  Widget build(BuildContext context) {
    final wide = MediaQuery.sizeOf(context).width >= 1000;
    final destinations = [
      const NavigationDestination(
        icon: Icon(Icons.dashboard_outlined),
        label: 'Overview',
      ),
      if (widget.isHr)
        const NavigationDestination(
          icon: Icon(Icons.people_outline),
          label: 'Employees',
        ),
      const NavigationDestination(
        icon: Icon(Icons.assignment_outlined),
        label: 'Tasks',
      ),
      if (widget.isHr)
        const NavigationDestination(
          icon: Icon(Icons.badge_outlined),
          label: 'Workforce & Visa',
        ),
    ];
    final index = widget.isHr ? page : (page == 2 ? 1 : 0);
    final theme = Theme.of(context).copyWith(
      colorScheme: ColorScheme.fromSeed(
        seedColor: teal,
        brightness: Brightness.light,
      ),
      scaffoldBackgroundColor: const Color(0xfff5f8fc),
      inputDecorationTheme: InputDecorationTheme(
        filled: true,
        fillColor: Colors.white,
        contentPadding: const EdgeInsets.all(14),
        border: OutlineInputBorder(
          borderRadius: BorderRadius.circular(12),
          borderSide: const BorderSide(color: Color(0xffdfe7ef)),
        ),
      ),
      filledButtonTheme: FilledButtonThemeData(
        style: FilledButton.styleFrom(
          backgroundColor: teal,
          foregroundColor: Colors.white,
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 16),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(12),
          ),
        ),
      ),
    );
    return Theme(
      data: theme,
      child: Scaffold(
        appBar: AppBar(
          backgroundColor: Colors.white,
          foregroundColor: navy,
          title: const Text(
            'ExpatOne Business',
            style: TextStyle(fontWeight: FontWeight.w700),
          ),
          actions: [
            IconButton(
              tooltip: 'Refresh',
              onPressed: widget.loading ? null : widget.onRefresh,
              icon: const Icon(Icons.refresh),
            ),
          ],
        ),
        bottomNavigationBar: wide
            ? null
            : NavigationBar(
                selectedIndex: index,
                onDestinationSelected: (i) =>
                    go(!widget.isHr && i == 1 ? 2 : i),
                destinations: destinations,
              ),
        body: Row(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (wide)
              Container(
                width: 224,
                color: navy,
                padding: const EdgeInsets.symmetric(
                  vertical: 28,
                  horizontal: 12,
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    const Padding(
                      padding: EdgeInsets.all(16),
                      child: Text(
                        'BUSINESS WORKSPACE',
                        style: TextStyle(
                          color: Colors.white70,
                          letterSpacing: 1.4,
                          fontSize: 11,
                        ),
                      ),
                    ),
                    for (final entry in <int, String>{
                      0: 'Overview',
                      if (widget.isHr) 1: 'Employees',
                      2: 'Applications & Tasks',
                      if (widget.isHr) 3: 'Workforce & Visa',
                    }.entries)
                      Padding(
                        padding: const EdgeInsets.only(bottom: 8),
                        child: Material(
                          color: Colors.transparent,
                          borderRadius: BorderRadius.circular(10),
                          child: ListTile(
                            shape: RoundedRectangleBorder(
                              borderRadius: BorderRadius.circular(10),
                            ),
                            tileColor: page == entry.key ? teal : null,
                            textColor: Colors.white,
                            iconColor: Colors.white,
                            leading: Icon(
                              [
                                Icons.dashboard_outlined,
                                Icons.people_outline,
                                Icons.assignment_outlined,
                                Icons.badge_outlined,
                              ][entry.key],
                            ),
                            title: Text(entry.value),
                            onTap: () => go(entry.key),
                          ),
                        ),
                      ),
                    const Spacer(),
                    TextButton.icon(
                      onPressed: () => Navigator.maybePop(context),
                      icon: const Icon(Icons.arrow_back, color: Colors.white70),
                      label: const Text(
                        'Back to ExpatOne',
                        style: TextStyle(color: Colors.white70),
                      ),
                    ),
                  ],
                ),
              ),
            Expanded(
              child: widget.loading
                  ? const Center(child: CircularProgressIndicator())
                  : widget.error != null
                  ? Center(
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          const Text('Unable to load your business workspace.'),
                          TextButton(
                            onPressed: widget.onRefresh,
                            child: const Text('Retry'),
                          ),
                        ],
                      ),
                    )
                  : RefreshIndicator(
                      onRefresh: widget.onRefresh,
                      child: ListView(
                        controller: scroll,
                        physics: const AlwaysScrollableScrollPhysics(),
                        padding: EdgeInsets.all(wide ? 28 : 16),
                        children: [
                          Row(
                            children: [
                              Expanded(
                                child: DropdownButtonFormField<String>(
                                  key: ValueKey(widget.selected['id']),
                                  initialValue: str(widget.selected, 'id'),
                                  isExpanded: true,
                                  decoration: const InputDecoration(
                                    labelText: 'Company',
                                    prefixIcon: Icon(Icons.business_outlined),
                                  ),
                                  items: widget.companies
                                      .map(
                                        (c) => DropdownMenuItem(
                                          value: str(c, 'id'),
                                          child: Text(
                                            str(c, 'name'),
                                            overflow: TextOverflow.ellipsis,
                                          ),
                                        ),
                                      )
                                      .toList(),
                                  onChanged: (v) {
                                    if (v != null) widget.onCompany(v);
                                  },
                                ),
                              ),
                              const SizedBox(width: 12),
                              pill(str(widget.selected, 'role')),
                            ],
                          ),
                          const SizedBox(height: 24),
                          ...widget.invitations,
                          if (page == 0) ...overview(wide),
                          if (page == 1 && widget.isHr) employeeSection(),
                          if (page == 2) taskSection(),
                          if (page == 3 && widget.isHr) workforceVisaSection(),
                          const Padding(
                            padding: EdgeInsets.symmetric(vertical: 12),
                            child: Text(
                              'Linked visa applications and supporting documents are read-only for HR. Personal documents remain private unless linked.',
                              style: TextStyle(
                                fontSize: 12,
                                color: Colors.blueGrey,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
            ),
          ],
        ),
      ),
    );
  }

  /// Counts linked application records, not issued visas or valid permits.
  /// Visa expiry requires a separately verified permit expiry date.
  Widget workforceVisaSection() {
    final active = widget.employees.where((e) =>
        e['status'] == 'Active' &&
        (e['role'] == 'Employee' || e['role'] == 'Viewer')).toList();
    final linked = widget.tasks.where((t) =>
        t['visaApplicationId'] != null &&
        t['visaApplicationId'].toString().isNotEmpty).toList();
    final peopleWithLinks = linked.map((t) => t['employeeUserId'].toString()).toSet();
    final unlinked = widget.tasks.where((t) =>
        t['visaApplicationId'] == null ||
        t['visaApplicationId'].toString().isEmpty).length;
    final local = active.where((e) => e['workforceClassification'] == 'Local').length;
    final foreign = active.where((e) => e['workforceClassification'] == 'Foreign').length;
    final unknown = active.length - local - foreign;
    final classified = local + foreign;
    final localPercent = classified == 0 ? null : (local * 100 / classified);
    final foreignPercent = classified == 0 ? null : (foreign * 100 / classified);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        heading('Workforce & Visa'),
        const Text(
          'Company workforce and linked visa applications. A linked application '
          'does not confirm that a visa or work permit has been issued.',
          style: TextStyle(color: Colors.blueGrey),
        ),
        const SizedBox(height: 16),
        Wrap(
          spacing: 12,
          runSpacing: 12,
          children: [
            card(SizedBox(width: 190, child: ListTile(
              title: Text('${active.length}'),
              subtitle: const Text('Active memberships'),
            ))),
            card(SizedBox(width: 190, child: ListTile(
              title: Text('${peopleWithLinks.length}'),
              subtitle: const Text('Employees with linked applications'),
            ))),
            card(SizedBox(width: 190, child: ListTile(
              title: Text('${linked.length}'),
              subtitle: const Text('Linked corporate tasks'),
            ))),
            card(SizedBox(width: 190, child: ListTile(
              title: Text('$unlinked'),
              subtitle: const Text('Tasks without linked applications'),
            ))),
          ],
        ),
        card(const ListTile(
          leading: Icon(Icons.info_outline),
          title: Text('Visa validity and expiry are not yet available'),
          subtitle: Text('Expiry alerts will require verified permit details. '
              'Task due dates are not visa expiry dates.'),
        )),
        heading('Workforce classification'),
        card(Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              classified == 0
                  ? 'No employees classified yet'
                  : 'Local ${localPercent!.toStringAsFixed(1)}% · Foreign ${foreignPercent!.toStringAsFixed(1)}%',
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
            const SizedBox(height: 10),
            if (classified > 0)
              LinearProgressIndicator(
                value: local / classified,
                minHeight: 10,
                backgroundColor: Colors.orange.shade100,
                color: teal,
                borderRadius: BorderRadius.circular(8),
              ),
            const SizedBox(height: 10),
            Wrap(spacing: 16, runSpacing: 8, children: [
              pill('Local: $local'),
              pill('Foreign: $foreign', Colors.deepOrange),
              pill('Unknown: $unknown', Colors.blueGrey),
            ]),
            const SizedBox(height: 8),
            const Text(
              'Percentages exclude Unknown classifications. These are company-provided '
              'workforce categories, not immigration or nationality determinations.',
              style: TextStyle(fontSize: 12, color: Colors.blueGrey),
            ),
          ],
        )),
        heading('Linked application records'),
        if (linked.isEmpty)
          card(const Text('No corporate tasks have linked visa applications yet.')),
        for (final task in linked)
          card(ListTile(
            title: Text(str(task, 'title', 'Corporate application')),
            subtitle: Text(
              'Employee: ${assignee(task)} · Task status: ${str(task, 'status', 'Unknown')}',
            ),
            trailing: const Icon(Icons.link),
          )),
      ],
    );
  }

  List<Widget> overview(bool wide) => [
    heading(
      widget.isHr ? 'Your workforce, connected.' : 'My business overview',
    ),
    const Padding(
      padding: EdgeInsets.only(bottom: 20),
      child: Text(
        'Your people and applications, in one place.',
        style: TextStyle(color: Colors.blueGrey),
      ),
    ),
    if (widget.isHr)
      LayoutBuilder(
        builder: (context, constraints) {
          final items = [
            (
              'Active employees',
              widget.metrics?['activeEmployees'],
              Icons.people_outline,
            ),
            (
              'Open cases',
              widget.metrics?['openCases'],
              Icons.assignment_outlined,
            ),
            (
              'Due / overdue in 30 days',
              widget.metrics?['dueWithin30Days'],
              Icons.event_outlined,
            ),
          ];
          final width = constraints.maxWidth < 330
              ? constraints.maxWidth
              : (constraints.maxWidth - 20) / 3;
          return Wrap(
            spacing: 10,
            children: items
                .map(
                  (m) => SizedBox(
                    width: width,
                    child: card(
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Icon(m.$3, color: teal),
                          const SizedBox(height: 10),
                          Text(
                            '${m.$2 ?? 0}',
                            style: const TextStyle(
                              fontSize: 28,
                              fontWeight: FontWeight.bold,
                              color: navy,
                            ),
                          ),
                          Text(m.$1, style: const TextStyle(fontSize: 12)),
                        ],
                      ),
                    ),
                  ),
                )
                .toList(),
          );
        },
      ),
    if (widget.tasks.any(dueSoon))
      card(
        ListTile(
          contentPadding: EdgeInsets.zero,
          leading: const Icon(Icons.event_outlined, color: Colors.deepOrange),
          title: Text(
            '${widget.tasks.where(dueSoon).length} tasks due within 30 days or overdue',
          ),
          trailing: const Icon(Icons.chevron_right),
          onTap: () {
            setState(() {
              dueOnly = true;
              taskStatus = 'All';
              country = 'All';
              taskQuery = '';
              taskSearch.clear();
            });
            go(2);
          },
        ),
      ),
    if (wide && widget.isHr)
      Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Expanded(child: employeeSection(preview: true)),
          const SizedBox(width: 20),
          Expanded(child: taskSection(preview: true)),
        ],
      )
    else ...[
      if (widget.isHr) employeeSection(preview: true),
      taskSection(preview: true),
    ],
  ];

  Widget employeeSection({bool preview = false}) {
    final list = preview
        ? widget.employees.take(3).toList()
        : matchingEmployees;
    return card(
      Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          heading(
            'Employees',
            FilledButton.icon(
              onPressed: widget.onInvite,
              icon: const Icon(Icons.person_add_alt, size: 18),
              label: const Text('Invite'),
            ),
          ),
          if (!preview) ...[
            Text('${widget.employees.length} memberships'),
            const SizedBox(height: 16),
            search(
              employeeSearch,
              'Search name or email',
              (v) => setState(() => employeeQuery = v),
            ),
            const SizedBox(height: 12),
            chips(
              ['All', 'Active', 'Inactive', 'Pending', 'Declined'],
              memberStatus,
              (v) => memberStatus = v,
            ),
            const SizedBox(height: 12),
            filter(
              'Role',
              role,
              options(widget.employees.map((e) => str(e, 'role'))),
              (v) => role = v,
            ),
            const SizedBox(height: 12),
          ],
          if (list.isEmpty)
            empty(
              widget.employees.isEmpty
                  ? 'No employees yet. Invite your first team member.'
                  : 'No employees match your search or filters.',
            ),
          for (final e in list)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  CircleAvatar(
                    backgroundColor: const Color(0xffe3efff),
                    foregroundColor: teal,
                    child: Text(
                      name(e).isEmpty
                          ? '?'
                          : name(e).characters.first.toUpperCase(),
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: InkWell(
                      onTap: () => memberDetails(e),
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            name(e),
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                          Text(
                            str(e, 'email'),
                            style: const TextStyle(
                              fontSize: 12,
                              color: Colors.blueGrey,
                            ),
                          ),
                          const SizedBox(height: 6),
                          Wrap(
                            spacing: 6,
                            runSpacing: 6,
                            children: [
                              pill(str(e, 'role')),
                              pill(
                                str(e, 'status'),
                                e['status'] == 'Active'
                                    ? Colors.teal
                                    : Colors.blueGrey,
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ),
                  if (widget.isOwner)
                    IconButton(
                      tooltip: 'Edit role / status',
                      onPressed: () => widget.onEditMember(e),
                      icon: const Icon(Icons.edit_outlined, size: 20),
                    ),
                ],
              ),
            ),
          if (preview)
            TextButton.icon(
              onPressed: () => go(1),
              icon: const Icon(Icons.arrow_forward),
              label: const Text('View all employees'),
            )
          else
            Text(
              'Showing ${list.length} of ${widget.employees.length} memberships',
              style: const TextStyle(color: Colors.blueGrey, fontSize: 12),
            ),
        ],
      ),
    );
  }

  Widget taskSection({bool preview = false}) {
    final list = preview
        ? (widget.tasks.where((t) => t['status'] != 'Closed').toList()..sort(
                (a, b) => (date(a) ?? DateTime(9999)).compareTo(
                  date(b) ?? DateTime(9999),
                ),
              ))
              .take(2)
              .toList()
        : matchingTasks;
    return card(
      Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          heading(
            widget.isHr ? 'Applications & Tasks' : 'My tasks',
            widget.isHr
                ? FilledButton.icon(
                    onPressed: widget.onNewTask,
                    icon: const Icon(Icons.add, size: 18),
                    label: const Text('New task'),
                  )
                : null,
          ),
          if (!preview) ...[
            search(
              taskSearch,
              'Search tasks or employee',
              (v) => setState(() => taskQuery = v),
            ),
            const SizedBox(height: 12),
            chips(
              ['All', 'Open', 'InProgress', 'Blocked', 'Closed'],
              taskStatus,
              (v) => taskStatus = v,
            ),
            const SizedBox(height: 12),
            Wrap(
              spacing: 12,
              runSpacing: 12,
              children: [
                filter(
                  'Destination',
                  country,
                  options(widget.tasks.map((t) => str(t, 'countryCode'))),
                  (v) => country = v,
                ),
                filter('Due date', sort, [
                  'Earliest first',
                  'Latest first',
                ], (v) => sort = v),
                FilterChip(
                  label: const Text('Due soon / overdue'),
                  selected: dueOnly,
                  onSelected: (v) => setState(() => dueOnly = v),
                ),
              ],
            ),
            const SizedBox(height: 16),
          ],
          if (list.isEmpty)
            empty(
              preview
                  ? 'No open tasks.'
                  : 'No tasks match your search or filters.',
            ),
          for (final t in list)
            card(
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        child: Text(
                          str(t, 'title', 'Application'),
                          style: const TextStyle(
                            fontWeight: FontWeight.w700,
                            fontSize: 16,
                          ),
                        ),
                      ),
                      widget.taskActions(t),
                    ],
                  ),
                  Text(
                    '${str(t, 'countryCode', 'No destination')} · ${assignee(t)}',
                    style: const TextStyle(color: Colors.blueGrey),
                  ),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 12,
                    runSpacing: 8,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      pill(
                        str(t, 'status'),
                        t['status'] == 'Blocked' ? Colors.deepOrange : teal,
                      ),
                      Text(dateLabel(t), style: const TextStyle(fontSize: 12)),
                    ],
                  ),
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton.icon(
                      onPressed: () => details(t),
                      icon: const Icon(Icons.chevron_right),
                      label: const Text('View details'),
                    ),
                  ),
                ],
              ),
            ),
          if (preview)
            TextButton.icon(
              onPressed: () {
                setState(() => dueOnly = false);
                go(2);
              },
              icon: const Icon(Icons.arrow_forward),
              label: const Text('View all tasks'),
            )
          else
            Text(
              'Showing ${list.length} of ${widget.tasks.length} tasks',
              style: const TextStyle(color: Colors.blueGrey, fontSize: 12),
            ),
        ],
      ),
    );
  }

  Future<void> memberDetails(BusinessRecord e) => showDialog<void>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: Text(name(e)),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(str(e, 'email')),
          const SizedBox(height: 12),
          Text('Role: ${label(str(e, 'role'))}'),
          Text('Status: ${str(e, 'status')}'),
        ],
      ),
      actions: [
        if (widget.isOwner)
          TextButton(
            onPressed: () {
              Navigator.pop(ctx);
              widget.onEditMember(e);
            },
            child: const Text('Edit role / status'),
          ),
        TextButton(
          onPressed: () => Navigator.pop(ctx),
          child: const Text('Close'),
        ),
      ],
    ),
  );

  Future<void> details(BusinessRecord t) => showDialog<void>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: Text(str(t, 'title', 'Application')),
      content: SizedBox(
        width: 520,
        child: SingleChildScrollView(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Employee: ${assignee(t)}'),
              const SizedBox(height: 12),
              Text('Destination: ${str(t, 'countryCode', 'Not set')}'),
              Text('Purpose: ${str(t, 'purpose', 'Not set')}'),
              Text('Status: ${label(str(t, 'status'))}'),
              Text('Priority: ${str(t, 'priority', 'Normal')}'),
              Text('Due: ${dateLabel(t)}'),
              const Divider(),
              Text(str(t, 'notes').isEmpty ? 'No notes.' : str(t, 'notes')),
              const SizedBox(height: 12),
              Text(
                t['visaApplicationId'] == null
                    ? 'No visa application linked.'
                    : 'Linked visa: ${t['visaApplicationId']}',
              ),
              const SizedBox(height: 8),
              const Text(
                'Use task actions to edit, update status or open the linked visa, according to your role.',
              ),
            ],
          ),
        ),
      ),
      actions: [
        widget.taskActions(t),
        TextButton(
          onPressed: () => Navigator.pop(ctx),
          child: const Text('Close'),
        ),
      ],
    ),
  );
}
