# ⏳ 5.1 — Staff آن/آف اور password/PIN reset — v0.131.0 Celandine

تاریخ: **2026-11-10**۔ تازہ اجازت اسی 5.1 کے account toggle اور credential reset کے لیے؛حصہ6،نئی مالی پالیسی،recovery-word reset یا installer کے لیے نہیں۔ **حصوں1 تا5 کے باقی کام اور ضروری تصدیق سے پہلے6 نہیں۔**

## ⏳ مسئلہ اور source اصلاح

پہلے Desktop پرانے mutable `User.Active` کا الٹ اور `User.IsOwner` سے password/PIN کا کالم چنتا تھا۔ Service موجودہ Owner/target دیکھتی تھی،مگر loaded form کا مقابلہ نہیں تھا؛audit/COMMIT یا بعد کے UI callback کی خرابی میں نتیجہ غلط بتایا جا سکتا تھا۔

- `ListUsers(actor)` اب ایک مجاز **Snapshot** میں current Owner/session،فہرست اور ہر شخص کی baseline پڑھتا ہے۔ کسی read failure پر پوری editor read ناکام؛جعلی working/default فارم نہیں۔ Operational handover picker تبدیل نہیں کیا۔
- `StaffEditForm` immutable ہے: stored ID/name/parsed role/active اور local token۔ یہ mutable picker سے الگ ہے۔ `User.StaffForm`،result `StaffEdit` اور token JSON ignored ہیں۔ Password/PIN hashes کا کوئی public property نہیں؛raw hashes Core سے باہر نہیں بھیجے جاتے،audit میں بھی پہلے کی طرح صرف secret changed۔ Token authentication proof نہیں۔
- `staff` content baseline میں exact `id,name,role,active,password_hash,pin_hash` ہیں؛صرف Active نہیں۔ ایک نئی credential reset پرانے toggle کو بھی روکتی ہے اور اس کے برعکس۔ Token Database instance،scope اورtarget سے بندھا ہے۔ A→B→عینA کی درمیانی history نہیں پکڑتا؛یہ durable revision یا global retry identity نہیں۔
- نئے **explicit checked** `SetActive(...,expected)` / `SetSecret(...,expected)` میں null بھی reject،چاہے sessions-disabled isolated host ہو۔ Current Owner/target کے بعد اور پہلے UPDATE سے پہلے اسی write transaction میں baseline compare؛secret کے لیے loaded انتخاب stored target role سے بھی ملتا ہے۔ Owner کو Off کرنا ممنوع؛fresh inactive staff کی Owner administration پہلے کی طرح جائز۔
- **پرانے imperative signatures الگ برقرار**: ان میں loaded baseline لازم نہیں؛enabled اور disabled hosts میں پرانا intent/current authorization برقرار۔ Checked credential-type consistency loaded Desktop کا تحفظ ہے؛پرانے imperative caller کے column flag کا معاہدہ نہیں بدلا۔ اس سے تمام writers محفوظ ہونے کا دعویٰ نہیں۔
- UPDATE،nonsecret audit اور exact post-write form/token ایک transaction میں؛success صرف transaction واپس آنے کے بعد۔ `TransactionOutcome` کے مطابق definite pre-COMMIT rollback ہی `NoCommitConfirmed`؛COMMIT attempt/unknown cleanup پر unconfirmed،کوئی success handoff نہیں۔

## ⏳ Desktop کا نتیجہ اور پرانے فارم

- دونوں actions اب مشترکہ guarded handler سے **immutable form** کا ID/role/active لیتے ہیں؛mutable row کی role/active پر فیصلہ نہیں۔ Membership،ID اور local busy guards ہیں؛busy guard access/binding callbacks سے پہلے لگتا ہے۔ Selection،roster reload،permissions/Pricing writers اور access timer کے متعلقہ guards وسیع کیے ہیں۔
- کامیابی کی acknowledgement observable callbacks سے پہلے؛typed secret کا backing value بھی پہلے خالی۔ صرف اسی result کا نیا Staff row/form لگتا ہے،کوئی fresh `LoadUsers/LoadPermissions/LoadPricing` نہیں جو دوسرے writer کی تبدیلی کو اپنی baseline بنا دے۔ Related permission/pricing forms invalidate ہوتے ہیں؛انہیں استعمال کرنے سے پہلے دوبارہ load/review کریں۔
- Known no-commit پر loaded form برقرار ہے،**تازہ DB state ہونے کی ضمانت نہیں**؛conflict پر Refresh/review لازم۔ Refresh یا selection بدلنے سے typed reset secret صاف ہوتا ہے تاکہ دوسرے شخص کو پرانا راز نہ لگے۔
- Unknown یا save/refresh exception پر selection/token پہلے invalid،پھر observable collections صاف؛نئی action سے پہلے explicit reload/review۔ Ack کے بعد callback failure پر **SAVED**،نہ دوبارہ reset کرنے کی دعوت۔ Owner کے اپنے password reset کے بعد پہلے سے منظورشدہ next-action fresh-login rule برقرار؛یہ handoff session proof جاری یا carry نہیں کرتا۔
- یہ local invalidation ہے،cross-page/restart/cross-PC exactly-once نہیں۔ Reload کے بعد موجودہ حالت تصدیق کرنا لازم؛حصہ6 کی durable design یہاں شروع نہیں کی۔ Actual WPF queued pointer/key،binding exception اور modal/input teardown ابھی unverified ہیں۔

## ✅ صرف محدود مقامی جانچ

- **122 static checks**؛C# grammar/member heuristics compiler یا runtime نہیں۔
- **35 نئی lexical mutations** رد،اصل bytes بحال؛تمام **29 source-negative scripts** پاس۔ پرانا roster lexical guard دونوں actions کے مشترکہ current-owner handler پر منتقل ہوا؛permission guard اب Staff busy بھی روکتا ہے،کمزور نہیں کیا۔
- **37 migrations،33 Python-owned WAL/DELETE plans**: دونوں actions کے cross-conflicts،پانچ dependencies،actual source UPDATE/Audit SQL پر rollback+retry،deferred COMMIT failure،exact handoff،write reservation اور WAL snapshot۔ صرف عارضی الگ DB،financial posts صفر۔
- Python اپنے fingerprint/compare/transaction/outcome assumptions چلاتا ہے؛یہ **AuthService/EditConcurrency/Database/TransactionOutcome/session/provider/WPF/LAN proof نہیں**۔ Active dependency probe کی پہلی synthetic assignment پہلے والی value ہی تھی؛اسے واقعی مختلف value کیا،assertion برقرار رکھا،پھر پورا probe پاس۔
- پرانے C# tests،37 migrations،financial posting services،PasswordHasher اور session implementation نہیں بدلے۔ Permissions/Workbook/Hardware/Pricing/Settings/Icon/Draft/Edit regressions کی محدود local جانچ دوبارہ کی گئی؛اصل hardware/provider جانچ نہیں۔

## ⏳ 26 نئے C# cases — UNRUN

`StaffEditConcurrencyTests.cs`:9 Facts +17 theory rows۔ دو editors کے چار action combinations،name/role/active/password/PIN dependencies،deleted target،null،scope/context/target،stored credential type،Owner protection/fresh inactive administration،exact handoff،immutable form/mutable picker،JSON/no credential exposure،read failure،audit rollback/retry،deferred COMMIT unknown،current Owner/session اور legacy imperative compatibility۔

متوقع suite **3274 UNRUN**۔ Release build کی کوشش: `dotnet: command not found`،exit127۔ آخری اصل Windows evidence بدستور v0.120.1 کے **2963total/2941PASS/22FAIL** ہے؛نئے source/static/Python نتائج اس کا نیا green run نہیں۔

## ⏳ PT-73 — صرف الگ Windows ماحول

پہلے verified encrypted backup **اور واقعی الگ DB/photos/config**؛اصل shop میں experimental posts/faults نہیں۔

1. **3274/0fail/0skip** کے اصل TRX/log؛Core/Tests/Desktop Release build۔ پرانے credential/session/roster/permission tests بھی چلائیں۔
2. دو Staff editors سے پہلے toggle/credential reset،دوسرے سے دونوں پرانے actions؛چار combinations میں پہلا کام/audit برقرار،دوسرا atomic refuse۔ Name/role/active/password/PIN concurrent changes اور دوسرے connection بھی جانچیں۔
3. Owner Off ممنوع؛fresh inactive staff administration جائز؛stored-role/type mismatch،wrong ID/scope/context/null،expired proof/current Owner denial،imperative legacy APIs الگ جانچیں۔ Owner خود password بدلے تو اگلا input/action fresh login مانگے؛returned row authenticated user نہ بنے۔
4. Selection/reload/queued keyboard/mouse/double click/collection reentry میں پرانا secret دوسرے target پر نہ لگے۔ Mutable picker role/active بدلنے سے loaded intent نہ بدلے۔ Success کے بعد exact handoff؛درمیان کے دوسرے writer کو اگلی action پکڑے،خاموش fresh rebase نہ ہو۔ Related permissions/pricing پہلے reload/review ہوں۔
5. دونوں UPDATE+Audit کے faults،provider COMMIT/rollback/dispose اور post-ack binding/collection exceptions۔ Known rollback پر جائز retry؛unknown پر selection/token invalid اور verify؛callback failure پر **SAVED**۔ Read failure کوئی working editor نہ دے۔

## ⏳ باقی 5.1 اور واضح حد

Recovery word اب بھی الگ راستہ ہے: `AuthService.SetRecoveryWord` → `SettingsService.SaveRecoveryHash`؛اس checkpoint میں اس کی loaded-state/outcome حفاظت **نہیں کی**۔ CreateStaff،remaining native/custom/imperative Settings writers،startup registry+SQLite race/partial restore اور PT-67–73 کی اصل Windows/provider/UI/file/physical جانچ باقی۔ اس کے بعد اسی حصہ5 میں StockTake/toggle/bulk/snapshot کی باقی inventory۔ **5.1 جاری؛1تا5 کے باقی کام اور ضروری تصدیق سے پہلے6 نہیں؛❌installer نہیں بنایا۔**
