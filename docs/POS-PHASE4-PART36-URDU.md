# فیز4 حصہ36 — Resume کا محفوظ recovery draft اور posting faults

**PT-56 · v0.114.0 Lichen · 2026-11-10 · مستقل نمبر15/23/24/25**  
[تمام1–36نمبر](NUMBERED-WORK-STATUS-URDU.md) · [source/حدود](POS-PHASE4-RECOVERY-FAULT-REVIEW.md)

## 1۔ آپ کے منتخب فیصلے اور عام مثال

H-0001 واپس لانے پر پہلے صرف parked حیثیت ختم ہوتی تھی؛ Counter کھلتے وقت خرابی آئے تو عام parked فہرست سے کام غائب دکھائی دیتا تھا۔ اب **اسی database transaction میں ایک linked Sale recovery draft بنتا ہے**، پھر cart resumed ہوتا ہے۔ Counter اسی draft کے ذریعے bill مکمل کرتا ہے۔ Restart پر Open drafts میں `held-…` نام سے محفوظ کام ملے گا۔ الگ غیر منسلک copy نہیں بنائی جاتی۔ یہ source implementation ہے؛ اصل Windows crash/restart کا کامیاب تجربہ ابھی نہیں ہوا۔

دوسرا انتخاب: posting کا نتیجہ نامعلوم ہو تو اسے ناکام سمجھ کر دوبارہ bill نہیں بنانا۔ **Verification marker برقرار**؛ پہلے اصل Bills/Purchases record دیکھیں۔ واضح validation یا COMMIT سے پہلے تصدیق شدہ rollback ہو تو latest payload دوبارہ open draft بنتا ہے۔ فیصلے error کے الفاظ سے نہیں بلکہ explicit outcome سے ہوتے ہیں۔

## 2۔ اس حصے میں کیا بدلا؟

- Held Resume میں draft insert،held link/consume اور دونوں audits ایک transaction میں؛ کسی statement/audit کی خرابی پوری recovery کو rollback کرتی ہے۔ پہلے سے resumed تاریخی carts کو backfill یا replay نہیں کیا۔
- Customer،quantity،unit،integer-paisa rate،line pricing/basis/percent،bill discount/basis،Extra اور held note کا موجودہ محفوظ snapshot recovery payload میں رہتا ہے۔ Hold stock reserve نہیں کرتا؛ post پر اصل قواعد دوبارہ لگتے ہیں۔
- Counter پہلے draft ID جوڑتا ہے، پھر UI بناتا ہے۔ Reconstruction یا missing-product skip پر partial cart کا Save/Park/Complete بند؛ Clear صرف Counter چھوڑنے کی تصدیق مانگتا ہے، saved draft باقی رہتا ہے۔ Unsaved edits محفوظ ہونے کا دعویٰ نہیں۔
- Linked recovery draft دوبارہ Park کرکے دوسری copy نہیں بن سکتی؛ Save اسی draft پر یا Complete استعمال کریں۔ کسی linked terminal/verification draft کو Hold کے ذریعے بھی نہیں گھمایا جا سکتا۔ عام open draft کو Hold کرنے کی پرانی duplicate-intent policy مکمل redesign نہیں ہوئی۔
- Unknown/throwing creator marker رکھتا ہے۔ Restoration ناکام/zero-row ہو تو جھوٹا “draft kept/restored” پیغام نہیں۔ Document محفوظ مگر finalization/audit ناکام ہو تو **SAVED،دوبارہ post نہیں** warning Core result،Counter،Purchase اور Draft drawer تک پہنچتی ہے۔
- COMMIT attempt کے بعد failure کو conservative unknown کہتے ہیں، چاہے بعد کا rollback call واپس آجائے۔ یہ durable commit receipt یا global exactly-once نہیں۔

## 3۔ محفوظ Windows مشق — تمام قدم UNRUN

**پہلے verified backup اور واقعی الگ test DB/photos/config۔ صرف exe folder الگ ہونا کافی نہیں۔ اصل دکان پر فرضی bill،fault یا SQL trigger مت چلائیں۔** Fault injection صرف developer کے disposable test database میں؛ مالک اپنی live database file نہ بدلیں۔

### 15۔ Held Resume/restart

1. الگ test shop میں H-0001 میں دو items،custom allowed rate،bill discount اور جائز Extra رکھیں؛ quantities/rates لکھ لیں۔ Bring back پر parked فہرست سے نکلے اور Counter میں linked draft دکھے۔ ابھی sale/payment/stock movement نہ بنے۔
2. Complete سے پہلے test app بند/کھولیں؛ Open drafts سے `held-…` کھولیں۔ وہی saved quantities/pricing ملیں؛ Save اسی draft ID پر ہو۔ دوبارہ Park انکار دے۔
3. ایک بار Complete کریں؛ ایک ہی sale/number/payment/stock اثر ہو۔ اسی draft کی دوبارہ posting نہ ہو؛ receipt reprint نئی sale نہیں۔
4. Developer disposable WPF test میں پہلی/درمیانی line construction،binding،discount/customer callback اور refresh میں خرابی دے۔ Partial cart سے Complete/Save/Park انکار؛ Clear confirmation کے بعد Drafts میں اصل محفوظ payload ملے۔ Missing catalog product بھی خاموش shortened draft نہ overwrite کرے۔
5. Clear کی No سے cart/link برقرار؛ Yes سے صرف Counter چھوڑے،disk draft باقی۔ غیر محفوظ typing ضائع ہونے کی warning واضح ہو۔ پرانے resumed cart خود نئی draft copy نہ بنائیں۔

### 23/24۔ Transaction اور draft-stage faults

1. `DraftRecoveryFaultTests` کے **29 cases** اور پوری **2644-case suite** کے Windows build/TRX logs رکھیں۔ یہاں compile اور test execution **UNRUN** ہیں۔
2. Recovery draft insert،held consume،draft audit اورresume audit میں الگ fault؛ پورا snapshot برابر،cart parked،کوئی orphan draft نہیں۔ Fault ہٹانے پر صرف ایک کامیاب recovery؛دوسری Resume سے کوئی mutation/audit نہیں۔
3. Draft premark fault: creator نہ چلے۔ Actual validation failure: latest payload open draft،stock/money نہیں؛ درست کرکے ایک کامیاب retry۔
4. Creator throw،unknown failure،یا document commit کے بعد reply fault: marker رہے؛ Open/update/post/Hold-copy بند۔ پہلے اصل document verify کریں۔ **Marker نہ document کے وجود کا ثبوت ہے نہ عدم وجود کا۔**
5. Restore UPDATE میں ABORT اور zero-row/IGNORE: “restoration not confirmed”؛خودکار retry نہیں۔ Finalize audit fault: document موجود،result success+warning،marker برقرار،retry سے دوسری entry نہیں۔
6. اصل provider COMMIT/I-O،process kill/power cut،busy/parallel،journal/WAL اور disk-full الگ ثبوت مانگتے ہیں؛Python یا reflection test ان کی جگہ نہیں۔

## 4۔ یہاں کی جانچ اور واضح باقی دروازے

- نیا synthetic SQLite probe اصل SQL کو تمام37 migrations پر چلاتا ہے؛4 recovery rollback/retry مراحل،restore ABORT/IGNORE،typed marker/finalize،unique/FK/legacyNULL اور no-money assertions۔ Python transaction owner ہے،C#/Dapper/Database.Tx نہیں۔
- Source-negative mutations guards کی lexical موجودگی چیک کرتی ہیں؛حقیقی execution یا security certification نہیں۔ مکمل تازہ counts trace میں درج ہوں گے۔
- Draft premark/Create/finalize اب بھی تین transactions ہیں۔ Durable reconciliation identity،unknown direct non-draft Sale/Purchase retry،عام Hold acknowledgement/UI crash،parallel re-auth/session/CAS،quick-add/full POS/prepared outputs،دیگرstock/expense/recoveryPR/file faults باقی۔
-26–29 backup/restore/new-PC/cloud،2/4/30–35 Windows/build/results/hardware/isolation باقی؛**36 مجاز single-double-click test installer NOT BUILT؛FullPhase4 OPEN**۔

ہر حصے کے docs،اسی active branch پر commit/push اور actual remote hash verification لازم ہیں۔36 categories کا مطلب36Next نہیں؛3–6 مربوط review/result rounds صرف مشروط اندازہ ہیں،deadline یا upper bound نہیں۔
