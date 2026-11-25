# ⏳ حصہ5.1 — موجودہ CustomField Update کا نتیجہ اور اصل baseline — v0.135.0 Foxglove

**صرف existing definition Update اوراس کے Desktop Save branch کا source checkpoint۔** Custom Add/OnOff/Move،باقی imperative Settings،startup registry،StockTake اورضروری Windows gates ابھی باقی؛حصے1تا5 بند ہونے سے پہلے6 نہیں۔ نئی مالی/business policy یاdurable identity نہیں۔

## 1.1 — اصل باقی build شہادت

مالک کا تازہ v0.134.0 Windows build **FAIL:1 CS0411،3 warnings،36.79s**؛tests/app نہیں چلے۔ v0.134.1 Borage میں Product.Update callback کی3 محدود source اصلاحات شائع/remote verified تھیں؛ان کا اصل Windows rerun ابھی نہیں ملا۔ موجودہ Next پر اسی5.1 کا محدود CustomField source کام جاری کیا،اسے build PASS نہیں کہا۔ [اصل لاگ/حدود](WINDOWS-V1340-BUILD-FAILURE-URDU.md)۔ آخری حقیقی test run پرانا v0.120.1:2963total/2941PASS/22FAIL؛نئے code کی green سند نہیں۔

## 1.2 — source میں کیا بدلا؟

- `CustomFieldService.Update` موجودہ Owner/session اور **پوری entity definition baseline** عین write transaction میں ملاتا ہے؛UPDATE/audit/duplicate/definition rules برقرار۔ Validations کی in-Tx refusal اب rollback کے لیے throw کرتی ہے؛preflight یا واقعی pre-COMMIT rollback پر ہی `NoCommitConfirmed`۔ COMMIT/rollback نامعلوم ہو تو unconfirmed اورکوئیform/token نہیں۔
- `UpdateChecked` null token کو sessions-disabled host میں بھی رد کرتا ہے۔ پرانے `Update` signature اورlegacy null-token host compatibility برقرار؛یہ wrapper کوئی نئی permission grant نہیں۔ Callback explicitly `Tx<ServiceResult>` ہے،void generic والا پچھلا CS0411 pattern نہیں۔
- اسی transaction میں پوری entity کی **active+hidden،صحیح ترتیب،stable keys اورہرrow کا entity token** لیا جاتا ہے۔ `CustomDefinitionEditForm` اندر defensive copies رکھتا اورباہر الگ copies دیتا ہے؛بعد کا writer یاcaller کی row mutation اصل receipt نہیں بدلتی۔ Result handoff JSON سے باہر ہے؛یہ durable identity/authorization نہیں۔
- Desktop existing Save **RefreshAccess سے token تازہ کرکےwrite نہیں کرتا**؛اصل selected row/ID/entity/token checked service کو دیتا ہے۔ Timer کو دوسری definition/order بدلے تو کھلا editor/token بدلنے کے بجائے explicit Refresh/review کا پیغام ملتا ہے۔ پہلے سے موجود selected-row drift پر discard/current access cleanup برقرار۔
- Existing Update کا persistent `PostingAttemptGuard`؛acknowledge پہلے،پھر backing selected ID،label/options/help/required/type کے inputs consume،پھر exact entity rows کی handoff اورbinding/status callbacks۔ آخری retained rows/tokens/خالیeditor verdict کے بعد ہی normal نیاکام کھلتا ہے۔ Normal success پرپرانی selection نہیں رکھی جاتی؛دوبارہترمیم کے لیے field منتخب کریں۔
- Unknown یاSAVED-callback failure میںguard اسیVM میں Refresh،tab،New/Cancel،regrant اوردوسرےcommands سےreset نہیں ہوتا؛backingdata/status پہلےinvalidate،callbacks بعد۔ اصل verification کے بعد page/window reopen الگ ہے؛نیاinstance/restart خود verified identity یاcross-PCdedup نہیں۔
- `Refresh fields` واضح read/review button؛known conflict پرموجودہ definitions دکھاتا اورunsaved edits discard کرتا ہے۔ Unknown/SAVED-failed-cleanup کا blockedguard یہbutton بھی نہیں کھولتا۔ Add/OnOff/Move اپنی پرانی Core writes پرہیں؛مشترکہ access/timer/blocked-state boundary ان پر بھی لاگو،مگران کے outcomes اسcheckpoint میں مکمل نہیں ہوئے۔

## 1.3 — محفوظ قواعد

Old answers rewrite/delete نہیں؛kind بدلے اورanswers موجود ہوں توپرانی warning آتی ہے۔ Stablekey،duplicate label،help/type/options،document-photo restriction،current stored Owner audit name،entity/order compare برقرار۔ Update کی اصل SQL،audit block اورAdd/OnOff/Move/answer service methods نہیں بدلے؛Database/session/EditConcurrency engine/37migrations/financial services اورپرانےC#tests محفوظ۔ UI کا normal-success editor reset زیادہ واضح ہے،نیاbusiness rule نہیں۔

## ✅ محدود مقامی ثبوت

-127 repository static checks؛34 negative scripts،ان میں46 نئی lexical mutations reject اورexact bytes restore۔ یہ compiler نہیں۔
-37 migrations کےساتھ30 نئے Python-owned extracted-SQL reference plans،WAL+DELETE؛no-op/exactreceipt،oldanswers،پڑوسیlabel/sort/active/required/help،duplicate،UPDATE/audit rollback،laterwriter،unrelatedentity،revokedowner،deferredCOMMIT۔ Python خود actor/CAS/transaction/outcome assumptions چلاتا ہے؛C# implementation کا ثبوت نہیں۔
-پرانےcustom-value،edit-concurrency،parent-saveاورCS0411 source/SQL regressions بھی برقرار/دوبارہجانچے؛کوئیاصلshoppost نہیں۔ GuideMD/HTMLparity اورdiffwhitespace/invariant checks الگ۔

## ⏳ نئی C# جانچ:26 cases،کل3373suite UNRUN

Exactfullentity/hiddenrows/rowtokens،defensivecopy/noJSON،neighbor changes،laterwriter/no-op،invalid/duplicate،null/wrongtarget/wrongDB tokens،legacyAPI،oldanswers/typewarning/stablekey،UPDATE/audit rollback+retry،deferredCOMMITunknown،currentOwner/session/storedname،deletedtarget،documentphoto اور2 **synthetic** guard cases۔ Syntheticguard tests اصلVM/timer/typing نہیںچلاتے۔ یہاں.NETSDKنہیں؛actualReleasebuildکی کوشش exit127۔ اصلWindows/.NET/provider/WPFrunباقی۔

## ⏳ PT-77 — اصل الگ Windows پر

Encryptedbackupکی تصدیق اورواقعیالگDB/photos/config پہلے؛اصلshopپرexperimentalwrites/faultsنہیں۔

1. v0.135.0 label/clean source؛Core/Tests+DesktopReleasebuild؛پہلےCS0411fixکیactualتصدیق،تینwarningsکاالگجائزہ؛3373expected/0fail/0skipاوراصلlogs۔ Countبدلےتوصرفحقیقیوجہکےساتھ۔
2. موجودہfield کھول کرlabel/type/options/help/requiredلکھیں؛دوسرےconnectionسےاسیentityکیselectedیاپڑوسیrow/order/activeبدلیں۔ Timer/action پراصلbaselineخاموشیتازہنہہو؛checkedSavewriteسےپہلےconflict؛unrelatedentityکیجائزwriteسےغلطconflictنہیں۔
3. Refreshfieldsموجودہlistloadکرے،unsavedinputdiscardواضح؛normalSaveمیںexactTxreceiptاورblank/no-selectededitor،warningsvisible؛دوبارہترمیم نئیselectionسے۔ بعدکاwriterپرانےhand-offمیںadoptنہہو،اگلیwriteconflictپکڑے۔
4. Doubleclick/Enter、tab/selection/type/typedbinding/timerreentry؛ackکےبعدcollection/selection/statuscallbacks میںfault۔ SAVEDبرقرار،backinginputconsume،guardblocked؛Refresh/New/Cancel/regrantسےreleaseنہیں۔ Unknown بھیblindretryنہکھولے۔ اصلpage/VMlifetimeجانچیں؛reopenخودverificationنہیں۔
5. UPDATE/auditfaultپرknownrollbackکیجائزcorrectedretry؛providerCOMMIT/rollback/dispose/lostresponseپرunknown،کوئیform/tokenنہیں۔ DeferredSQLiteconstraintکوproviderresponseinjectionنہکہیں۔
6. CurrentOwner/sessiondenial،missing/wrongtokens،legacycompatibility、duplicate/invalid،oldanswers/hidden/stablekeys/typewarning/documentphoto؛پرانےtestsضعیفنہکریں۔ باقیAdd/Move/Toggleکیfailure/retryابھیالگکھلیہیں۔

## ⏳ اگلا باقی5.1

Custom Add،OnOff،Moveکیپوریentityordering/outcomes،remainingimperativeSettingsinventory،Windowsregistry+SQLitepartialrestore،StockTake/update/toggle/bulk/snapshotاورPT-63–77کیاصلجانچ۔ [جامعحالت](COMPLETE-STATUS-AND-DECISIONS-URDU.md)؛[منصوبہ](REMAINING-14-PORTIONS-URDU.md)۔ نہپوراCustomFieldmanagerمکملنہ5.1؛❌6/installerشروعنہیں۔ Source/docspublicationکیactualremoteحالتآخریگفتگومیں۔

## ⏳ بعد کا اسی5.1 کا On/Off checkpoint

v0.136.0میںOn/Offاسیsharedguardاورexactentityhelperسےشامل؛[PT-78/موجودہحدود](CUSTOM-DEFINITION-ACTIVE-OUTCOME-URDU.md)۔ اوپرOnOffباقیوالےجملےv0.135.0کیsourceحیثیتتھے؛ابOnOffsourceشامل،مگرactualWindows/runtime،Add/Moveاورremaining5.1ابھیباقی۔ UpdateاورOnOffکےدوnormalverdictsitesہیguardresetکرتےہیں،Refresh/regrantنہیں۔
