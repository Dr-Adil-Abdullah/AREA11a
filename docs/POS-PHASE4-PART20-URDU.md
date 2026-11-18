# PT-40 — parent tabs،مالک کی staff list اور گلہ کا نام-picker

**v0.98.0 Godwit · `#796044` · Source checkpoint؛Windows جانچ UNRUN**

مثال: Ali کو Customers کی اجازت ہے،Suppliers کی نہیں؛اسے Customers اور Recovery tabs ملیں،Supplier tab نہیں۔ اجازت ختم ہو تو پرانا tab/selection باقی نہ رہے۔ دوسری مثال: گلہ اگلے شخص کو دینے کے لیے نام چننا پورا Owner staff panel کھولنے کے برابر نہیں؛اس کے لیے الگ Cash-authorized active-person picker ہے۔

[تفصیلی trace/حدود](POS-PHASE4-PARENT-ROSTER-REVIEW.md) · [مکمل اردو استعمال نامہ](USER-GUIDE-URDU.md)

## حفاظت پہلے

- اصل دکان میں فرضی وصولی/فروخت/گلہ handover نہ کریں۔ پہلے verified backup،واقعی الگ test DB **اور photos/config paths**؛صرف الگ exe folder محفوظ isolation نہیں۔ یقین نہ ہو تو رکیں۔
- Backup password A/اصل password/PIN/cloud key/DB عام chat میں نہ دیں۔ اصل shop activity چلتے ہوئے restore نہ کریں۔
- `tools\RUN-TESTS.cmd` کا متوقع total **1817**؛28 نئے tests لکھے ہیں،اس Linux میں C# build/tests نہیں چلے۔ مکمل Windows log،version اور اصل screenshot چاہیے؛static/SQLite نتیجہ Windows PASS نہیں۔

## الگ test copy میں جانچ

| # | عمل | متوقع نتیجہ — ابھی خود آزمانا ہے |
|---|---|---|
| 1 | Customers-only user سے Customers & suppliers کھولیں | Customers/Recovery tabs،Suppliers نہیں؛Cost/Profit/main کی نئی شرط نہیں |
| 2 | Suppliers-only،Customers explicit OFF | صرف Supplier chapter،physical index2 پرصحیح content؛خالی index0 نہیں؛Supplier B accounting برقرار،direct cost اپنیالگgateسے |
| 3 | دونوں grants،پھر الگ الگ/دونوں OFF،اسی کھلے page پر | اگلے nominal3s active-view check پر منع branch detach،selected tab جائز fallback یاکوئی نہیں؛denied tab کا empty hint/پرانا content نہیں |
| 4 | دونوں میں کسی جائز tab کے form میں ادھورا متن،کئی access ticks | جب وہ branch جائز رہے تو child دوبارہ نہ بنے،متن برقرار؛branch revoke/unknown failure پر اس کا ادھورا form discard ہو سکتا ہے۔ Regrant پر fresh child |
| 5 | parent user inactive،role بدلنا،پھر sign-in/restore | current service reader انکار؛اگلے check پرtabs بند۔ Supplied valid ID کو authenticated session بنانے کا دعویٰ نہیں |
| 6 | Manager/Staff کو Settings اور Staff grants دیں | عام Settings کام اپنیاجازتسے؛Owner staff roster/permissions/pricing assignment list نہیں۔ grant یا rank اسےOwnerنہیںبناتا |
| 7 | Owner staff panel میں inactive account سمیت names/roles/permissions/pricing دیکھیں | جائز Owner کو مکمل roster؛password/PIN hashes directory میں نہیں؛Owner اپنے View Profit/View Purchase Cost checkboxes الگ بدل سکے |
| 8 | staff/person منتخب،pricing/permission values اورنیاPIN/reset/recovery متن بھرا؛Owner deactivate/demote کا controlled test | اگلے check/command پرnames،selection،permissions،pricing values،typed staff secrets اورپراناstatus صاف؛stale checkbox/delayed pointer action save نہ کرے |
| 9 | صرف معمولی Owner access ticks،دوسرے Settings form میںادھوریترمیم | جائزOwnerکےstaff/دیگرformsہر3secondresetنہیں؛nonownerکےordinarySettingsstatusکوہرtickownererrorسےنہبدلا جائے۔ عام Settings کی مکمل review ابھی باقی |
| 10 | developer-controlled isolated DB fault کے ساتھ staff refresh | پرانی staff list/selection/permission/pricing صاف؛safe failure،مصدقہempty-success نہیں۔ پراناdetachedUser/toggle دوبارہsaveنہہو |
| 11 | Cash grant والا staff گلہ کھولے،Staff OFF/CostProfit OFF/main OFF | handover picker میںصرفactiveدوسرےلوگ،خودنہیں؛fulladminrosterنہیںملتا۔ نامکیفہرستمیںآنامطلبrecipientکوCashpostingاجازتثابتہونانہیں |
| 12 | picker کھلاہو،Cash revoke/readerinactive یاrolechange؛پھرgrantrestore | اگلےRefreshPeople/nominal3stimerپرnames/selectionصاف؛بحالیپرfreshlist۔ **اسحصےسےگلہکےباقیپرانےرقم/history/read/postingراستےcertifiedنہیںہوئے** |
| 13 | ایکpossible recipientinactiveکرکےpickerکاتازہread | inactiveنامنکلے؛جائزمنتخبIDباقیہوتوبرقرار،ورنہتازہactivecandidate۔ Handoverpostingکیrecipientvalidationunchanged |
| 14 | Customers/Settings/Shift tabsباربارکھول/بند،headersectionsاورsettingscheckboxsingle/double/keyboardSpace | Loadedcheck،Unloadedtimerstop،کوئیduplicatehandlers/بےوجہformresetنہیں؛پراناremovedcheckboxactionنہیں۔ ActualWPFheaderbindingکیجانچلازم |
| 15 | Owneridentityبحال،Cost/Profit/mainمختلفcombinations | Ownerstaffreadاورownfinancialcheckboxeditبحال؛parentownmodules/operationalCashpickerانfinancialflagsسےبلاوجہبندنہیں |

## اس ماحول کا ثبوت

- **87 static checks PASS**؛C# grammar407files/0structuralissues،compilerنہیں۔
- **15 synthetic SQLite probes PASS**؛نئےprobeمیں36migrations،اصلroster/pickerSELECTs،ordering/active/selffilters،4nonsecretfields،boundID،read-only/FK۔ یہC#Owner/Cash/parentchecksنہیںچلاتا۔
- **17 source-negative mutations** رد/اصلbytesبحال؛executableC#mutationtestsنہیں۔
- **28 نئےfacts / کل1817C#casesUNRUN**؛Windows/WPF/DPAPI/installer/fullPhase4باقی۔
- 697baselinepathsبرقرار؛دوسرے114Core service/data files،Authlogin/credentialwrites،ShiftServiceاورBlackbox/BookCheckunchanged۔

نتیجہ: قدم نمبر،build/version،اصل screenshot یا مکمل log،متوقعاوراصلرویہ۔ یہ test checklist ہے،installer/runtimePASS نہیں۔
