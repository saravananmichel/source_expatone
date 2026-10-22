import 'package:flutter/material.dart';
import '../../core/services/corporate_service.dart';

class CorporateScreen extends StatefulWidget {
  final CorporateService service;
  const CorporateScreen({super.key,required this.service});
  @override State<CorporateScreen> createState()=>_CorporateScreenState();
}
class _CorporateScreenState extends State<CorporateScreen> {
  bool loading=true;
  String? error;
  List<Map<String,dynamic>> companies=[], invitations=[];
  Map<String,dynamic>? selected, metrics;
  List<Map<String,dynamic>> employees=[], cases=[];
  bool get isHr => selected?['role']=='Admin'||selected?['role']=='Owner';
  bool get isOwner => selected?['role']=='Owner';
  String get companyId => selected?['id']?.toString()??'';
  @override void initState(){super.initState();reload();}
  Future<void> reload() async {
    setState((){loading=true;error=null;});
    try {
      final cs=await widget.service.companies();
      final invites=await widget.service.invitations();
      Map<String,dynamic>? active;
      for(final c in cs){if(c['id']==selected?['id']){active=c;break;}}
      active ??= cs.isNotEmpty?cs.first:null;
      final id=active?['id']?.toString();
      final es=id!=null && (active?['role']=='Owner'||active?['role']=='Admin')?await widget.service.employees(id):<Map<String,dynamic>>[];
      final ks=id!=null?await widget.service.cases(id):<Map<String,dynamic>>[];
      final ms=id!=null && (active?['role']=='Owner'||active?['role']=='Admin')?await widget.service.dashboard(id):null;
      if(mounted)setState((){companies=cs;invitations=invites;selected=active;employees=es;cases=ks;metrics=ms;loading=false;});
    }catch(e){if(mounted)setState((){loading=false;error=e.toString();});}
  }
  Future<String?> ask(String title,String label) async {
    final c=TextEditingController();
    try {
      return await showDialog<String>(context:context,builder:(ctx)=>AlertDialog(
        title:Text(title),content:TextField(controller:c,autofocus:true,decoration:InputDecoration(labelText:label)),
        actions:[TextButton(onPressed:()=>Navigator.pop(ctx),child:const Text('Cancel')),
          FilledButton(onPressed:()=>Navigator.pop(ctx,c.text.trim()),child:const Text('Continue'))]));
    }finally{c.dispose();}
  }
  Future<void> action(Future<void> Function() fn) async {
    try {await fn();if(mounted)await reload();}
    catch(e){if(mounted)ScaffoldMessenger.of(context).showSnackBar(SnackBar(content:Text('Action failed: $e')));}
  }
  Widget panel(String title,Widget child)=>Card(child:Padding(padding:const EdgeInsets.all(14),child:Column(
    crossAxisAlignment:CrossAxisAlignment.start,children:[Text(title,style:Theme.of(context).textTheme.titleMedium),const SizedBox(height:10),child])));
  @override Widget build(BuildContext context)=>Scaffold(
    appBar:AppBar(title:const Text('ExpatOne Business'),actions:[IconButton(onPressed:reload,icon:const Icon(Icons.refresh))]),
    body:loading?const Center(child:CircularProgressIndicator()):error!=null?
      Center(child:Column(mainAxisSize:MainAxisSize.min,children:[Text(error!),TextButton(onPressed:reload,child:const Text('Retry'))])):
      ListView(padding:const EdgeInsets.all(12),children:[
        if(companies.isEmpty)panel('Individual account',Column(crossAxisAlignment:CrossAxisAlignment.start,children:[
          const Text('You have no active company membership. Your personal ExpatOne features remain private.'),
          const SizedBox(height:8),
          FilledButton.icon(onPressed:()=>action(() async {
            final name=await ask('Create company','Company name');
            if(name!=null && name.isNotEmpty)await widget.service.createCompany(name);
          }),icon:const Icon(Icons.add_business),label:const Text('Register a company'))
        ])),
        if(invitations.isNotEmpty)panel('Company invitations',Column(children:invitations.map((i)=>ListTile(
          title:Text(i['name']?.toString()??'Company'),
          subtitle:const Text('Invitation pending — no company access until accepted'),
          trailing:Wrap(spacing:4,children:[
            TextButton(onPressed:()=>action(() async {await widget.service.decline(i['id'].toString());}),child:const Text('Decline')),
            FilledButton(onPressed:()=>action(() async {await widget.service.accept(i['id'].toString());}),child:const Text('Accept'))
          ]) )).toList())),
        if(companies.isNotEmpty)...[
          panel('Company & role',Column(crossAxisAlignment:CrossAxisAlignment.start,children:[
            DropdownButtonFormField<String>(initialValue:companyId,decoration:const InputDecoration(labelText:'Company'),
              items:companies.map((c)=>DropdownMenuItem(value:c['id'].toString(),child:Text(c['name'].toString()))).toList(),
              onChanged:(id){setState((){selected=companies.firstWhere((c)=>c['id']==id);});reload();}),
            const SizedBox(height:8),Text('Your access: ${selected?['role']}'),
            if(isOwner)const Text('Company owner: corporate administration access',style:TextStyle(fontSize:12)),
          ])),
          if(isHr)...[
            panel('HR Overview',Wrap(spacing:18,runSpacing:10,children:[
              metric('Active employees',metrics?['activeEmployees']),
              metric('Open cases',metrics?['openCases']),
              metric('Due within 30 days',metrics?['dueWithin30Days']),
            ])),
            panel('Employee Management',Column(children:[
              Align(alignment:Alignment.centerRight,child:FilledButton.icon(
                onPressed:()=>action(() async {final email=await ask('Invite employee','Registered ExpatOne email');
                  if(email!=null&&email.isNotEmpty)await widget.service.invite(companyId,email);}),
                icon:const Icon(Icons.person_add_alt),label:const Text('Invite'))),
              ...employees.map((e)=>ListTile(title:Text(e['displayName']?.toString()??e['email']?.toString()??'Employee'),
                subtitle:Text('${e['email']} · ${e['role']} · ${e['status']}')))
            ])),
          ],
          panel(isHr?'Corporate Applications & Tasks':'My Company Tasks',Column(children:[
            if(isHr)Align(alignment:Alignment.centerRight,child:FilledButton.icon(
              onPressed:()=>createCase(),icon:const Icon(Icons.add),label:const Text('New task'))),
            if(cases.isEmpty)const ListTile(title:Text('No mobility cases yet')),
            ...cases.map((c)=>Card(child:ListTile(
              title:Text(c['title']?.toString()??'Case'),
              subtitle:Text('Status: ${c['status']}\nDue: ${c['dueAt']??'Not set'}\nVisa reference: ${c['visaApplicationId']??'Not linked'}'),
              isThreeLine:true,
              trailing:PopupMenuButton<String>(
                onSelected:(v)=>action(() async {
                  if(v=='Link visa'){final id=await ask('Link my SG-Visa application','Application ID');
                    if(id!=null&&id.isNotEmpty)await widget.service.linkVisa(companyId,c['id'].toString(),id);}
                  else if(v=='Unlink visa'){
                    await widget.service.unlinkVisa(companyId,c['id'].toString());
                  } else {
                    await widget.service.updateCase(companyId,c['id'].toString(),v);
                  }
                }),
                itemBuilder:(_)=>[
                  if(isHr)...['Open','InProgress','Blocked','Closed'].map((v)=>PopupMenuItem(value:v,child:Text('Set $v'))),
                  if(!isHr)...[const PopupMenuItem(value:'Link visa',child:Text('Link my SG-Visa')),
                    const PopupMenuItem(value:'Unlink visa',child:Text('Unlink my SG-Visa'))],
                ],
              ),
            )))
          ])),
          panel('Privacy',const Text('HR can see corporate task records, not your personal wallet, family documents, AI conversations or SG-Visa form content. Linking a visa shares only its application reference.')),
        ],
      ]),
  );
  Widget metric(String label,dynamic value)=>Column(crossAxisAlignment:CrossAxisAlignment.start,children:[
    Text('${value??0}',style:Theme.of(context).textTheme.headlineMedium),
    Text(label,style:Theme.of(context).textTheme.bodySmall)]);
  Future<void> createCase() async {
    final active=employees.where((e)=>e['role']=='Employee'&&e['status']=='Active').toList();
    if(active.isEmpty){ScaffoldMessenger.of(context).showSnackBar(const SnackBar(content:Text('Invite an employee and wait for acceptance first.')));return;}
    String? employeeId=active.first['id']?.toString();
    final title=TextEditingController();
    try {
      final result=await showDialog<bool>(context:context,builder:(ctx)=>StatefulBuilder(builder:(ctx,update)=>AlertDialog(
        title:const Text('Create mobility task'),
        content:Column(mainAxisSize:MainAxisSize.min,children:[
          DropdownButtonFormField<String>(initialValue:employeeId,items:active.map((e)=>DropdownMenuItem(
            value:e['id'].toString(),child:Text(e['displayName']?.toString()??e['email']?.toString()??'Employee'))).toList(),
            onChanged:(v)=>update(()=>employeeId=v)),
          TextField(controller:title,decoration:const InputDecoration(labelText:'Task title')),
        ]),
        actions:[TextButton(onPressed:()=>Navigator.pop(ctx,false),child:const Text('Cancel')),
          FilledButton(onPressed:()=>Navigator.pop(ctx,true),child:const Text('Create'))],
      )));
      if(result==true&&employeeId!=null&&title.text.trim().isNotEmpty){
        await action(() async {await widget.service.createCase(companyId,employeeId!,title.text.trim(),null);});
      }
    }finally{title.dispose();}
  }
}
