# Phase4 — حصہ34: Quarantine کی موجودہ اجازت اور decision/reload؛ PT-54

**v0.112.0 Shore · مستقل5/6/7/17/20/23/24/25 · source checkpoint،Windows منظوری نہیں۔**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [فنی trace/حدود](POS-PHASE4-QUARANTINE-CURRENT-REVIEW.md) · [اردو استعمال نامہ](USER-GUIDE-URDU.md)

## 1۔ روزمرہ مثال

Manager نے خراب واپس آیا ہوا strip Quarantine میں کھولا،پھر Owner نے اس account کو بند یا Returns اجازت واپس لے لی۔ پرانا User object یا Q-row اب اسے دوبارہ shelf میں ڈالنے کا اختیار نہیں دیتا۔ موجودہ Returns reader فہرست دیکھ سکتا ہے،مگر فیصلہ صرف موجودہ جائز Owner/Manager کرے گا۔ فہرست/count پڑھنے میں خرابی ہو تو اسے **zero pending / سب صاف** نہیں سمجھنا۔

اگر decision محفوظ ہوگیا مگر اگلی فہرست refresh نہیں ہوئی،پیغام **SAVED؛ان goods کا فیصلہ دوبارہ نہ کریں،Refresh کریں** رہے۔ یہ نئی stock/refund policy نہیں۔

## 2۔ source میں کیا مکمل ہوا؟

1. **5/6/17:** Pending/History/PendingCount میں actor لازمی؛payload سے پہلے موجودہ stored active/sameparsedrole account اور Returns grant۔ Count اب explicit `ServiceResult<int>` ہے؛readfailure کو successful zero نہیں بنایا۔ Actorless overload نہیں۔
2. **5/23:** Dispose transaction کے اندر target پڑھنے سے پہلے current Returns Owner/Manager resolve؛disposed_by،stock movement اورaudit میںstoredactor۔ Valid currentOwner کیordinarybypassبرقرار،لیکنinactive/rolechangedOwnerdeny۔
3. **7/25:** UI میںdecisioncapability=currentaccess+manager+successfulload؛عام جائزReturnsreader فہرست پڑھ سکتا ہے مگرnotes/actionsdisabled۔ Detached یا refreshed-list سے ہٹی ہوئیrow کیcallbackrefuse۔ موجودہ nominal3second Returns timer/pagecleanup برقرار؛allowedticktypednoteنہیںمٹاتا۔
4. **24/25:** acknowledgeddecision کےبعدrow/note reload سے پہلےconsume؛localbusy/finally؛بعدکیrefreshfailureمیںSAVEDبرقرار،ambiguouspreackexceptionمیںoutcomeunknown۔ Corealreadydecidedguardبرقرار؛globalexactlyonceنہیں۔
5. **2/24:**79نئےC#sourcecases=24currentfault/regrant+24invalidactor+20BEFORE/AFTERstatementfaults+3storedactor+3repeat+5facts۔ **کل2479متوقع؛C#compile/runtimeسبUNRUN۔**

**Restocked/Sold کا موجودہ AddBack،Expired میںstock نہ بڑھنا،destroy-note validation،تمامstockSQL/refund/pricing/Extra/ReturnB/SupplierB/main/Cost/Profit rules نہیں بدلے۔ Alert scanner کا الگ راستہ اس حصے میں نہیں بدلا۔**

## 3۔ PT-54 — صرف isolated Windows copy؛تمام عملی steps UNRUN

**پہلے verified encrypted backup؛پھر واقعی الگ DB/photos/config۔ الگ exe folder کافی نہیں۔ اصل shop پر synthetic sale/return/permissionfaults نہیں۔ کوئیsecretیااصلDBchatمیںنہیں۔**

### 5.1 / 6.1 — current account،read/count اور فیصلہ

1. Synthetic sale3units،nonrestock return3؛stock97/100اورQ-number baseline۔ Same Manager/service سےPending/History/Count کاجائزread۔ Accountinactive/rolechange/Returnsrevoke/readgrantfaultکےبعداگلاراستہdeny،کوئیstock/money/audit/sequencemutationنہیں۔ Same-rolegrantrestoreپراسیserviceسےfreshread/decision؛rolechangeپرقابلِاعتماد نئیsignin۔
2. Null/zero/inactivecaller/forgedOwnerاورstoredOwnerinactive/rolechangeddeny۔ ValidcurrentOwnerordinarygrantbypassبرقرار۔ یہsame-role-IDimpersonationکےخلافsessiontokenproofنہیں۔
3. حقیقیCashier/Returnsreaderسےlists/countجائز،ہرdecisiondeny؛Manager/OwnerبھیReturnsOFFمیںdeny۔ Main/Cost/ProfitOFF کوReturnsgrantکابدلنہبنائیں۔
4. Count/queryfault: `Ok=false`،error،listpayloadنہیں؛اسےzeroquarantineیامصدقہemptyhistoryنہکہیں۔ Missingtarget/invalidoutcome/expiredبغیرnoteکوrefuse۔

### 7.1 / 25.1 — کھلا UI،stale row،SAVED

1. Pendingrowاورtypednoteکھلےہوں۔ Returnsrevokeیاaccountinactive؛اگلےaction/nominaltickپرپراناform/listclear۔ RegrantپرتازہRefresh،پراناrow/noteخودبحالنہیں۔ Stableallowedticktypednoteبرقرار۔ TimerUnloadedپربند۔
2. جائزread-onlyaccountمیںnotes/decisionbuttonsdisabledمگرlistsreadable۔ DetachedپرانےcallbackیاrefreshسےہٹیrowپرCorecallنہہو۔ اگرکسیاورجائزcounterنےفیصلہکردیاتوCorealreadydecidedrefuse۔
3. Decisionکامیاب،اگلیreloadfault:row/noteconsume،**SAVED؛دوبارہفیصلہنہیں**۔ Unknownقبلackexceptionمیںنہ"کچھsaveنہیںہوا"کہیںنہblindretry؛Refreshسےاصلstateدیکھیں۔ ActualWPFbindings/reentry/timingیہاںUNRUN۔

### 20.1 / 23.1 / 24.1 — stock/fault/retry

1. `QuarantineCurrentAccessTests`کے79اورپوری2479suiteکےاصلWindowslogsرکھیں۔ Restocked/Soldپرstock97→100اورایکquarantinemovement؛Expiredپر97اورکوئیquarantinemovementنہیں۔ Money/refund/customerbalance/payment/numberingunchanged۔
2. Dispositionupdate،batchupdate،movementinsert،auditinsertپرBEFORE/AFTERsynthetictriggers؛ہرfailureکےبعدپہلاsnapshotبعینہٖ،triggerremoveکےبعدایکretryکامیاب،اگلاrepeatrefused۔ Expiredمیںصرفdisposition/auditstages؛کوئیجعلیbatchwriteنہیں۔
3. Storedaccountنامبدلیںمگرcallerobjectپراناہو؛auditنیاstoredname،disposed_by/movementصحیحID۔ تینوںoutcomesکےبعدdecisionکسیoutcomeسےoverwriteنہیں۔
4. Actualprovider/productionjournal/busy/concurrency/ambiguousCOMMITاورmissing/inactivebatch/fallbacklineageedgecasesالگباقیہیں؛synthetictriggerrollbackانکاثبوتنہیں۔

## 4۔ evidence اور اسی نمبر میں باقی کام

فنی trace میں اصل local results ہیں؛PythonSQL/lexicalchecks،C#،Dapper،Database.Tx،WPFیاsessionexecutionنہیں۔

- **5/6/7/15/17/21/23:** باقیdraftSave/Open/held/quickadd/fullPOS/session/preparedoutputs،same-role-ID/sessionbinding/CAS؛Alertscannerseparaterouteunchanged۔
- **20/24/25:** دوسرےstock/expense/recoveryPR/draft/filefaults،legacybatch/fallbackedges،actualCOMMIT-I/O/provider/busy/parallel/crossprocess/durableidentity۔
- **26–29:** encryptedbackup/restore/newPC/files/cloudintegration۔
- **2/4/30–35:** Windows2479+build/logs/fix/retest،اصلWPF/printer/scanner/camera/startup/lock/isolation۔
- **36:** gatesکےبعدمجازsingle-doubleclicktestinstaller؛**ابھینہیںبنا،FullPhase4OPEN۔**

ہرportiondocs+samebranchcommit/push+actualremoteverification۔36categories،36Nextنہیں؛3–6integratedroundsمشروطاندازہ،deadlineیاupperboundنہیں۔
