# Area11 — Real-life Physical Testing Log

یہ مالک کی عملی ٹیسٹنگ، تجاویز اور درخواست کردہ تبدیلیوں کی مستقل فائل ہے۔ ہر نتیجہ اور نئی بات اسی فائل میں شامل ہوگی تاکہ testing کے اختتام پر کوئی نکتہ نہ بھولے۔

## کام کا اصول

- Testing کے دوران پہلے واقعہ، توقع، حقیقی نتیجہ اور مطلوبہ تبدیلی **record** ہوگی۔
- اس مرحلے میں مسائل جمع کیے جا رہے ہیں؛ production changes صرف مالک کی ہدایت پر ہوں گے۔ کوئی نکتہ خاموشی سے fixed یا rejected نہیں لکھا جائے گا۔
- اصل دکان کے database میں صرف حقیقی لین دین؛ فرضی یا خطرناک مالیاتی cases کے لیے الگ test copy۔
- ہر item کی حالت: **Recorded → Planned → Fixed → Owner verified**۔
- پرانے owner notes نہیں مٹائے جائیں گے؛ نئی تصدیق کے ساتھ status اور تاریخ شامل ہوگی۔

## موجودہ build اور automated ثبوت

- Build: **v0.35.18 Obsidian `#27323A`**
- Owner xUnit: **860 passed / 0 failed / 0 skipped / 860 total — ALL GREEN**
- Automated مدت: **1 منٹ 17 سیکنڈ**
- Physical testing شروع: **27 اکتوبر 2026**
- Cloud sync: اس physical-testing مرحلے میں فی الحال شامل نہیں۔

## Testing summary

| Test | حالت | مختصر نتیجہ |
|---|---|---|
| **PT-01 — Startup, backup اور UI smoke** | **PASS — improvements logged** | ایپ کھلی، photos کے ساتھ backup بنا، اور تمام مقررہ pages کھلے |
| **PT-02 — حقیقی medicine اور product details** | **NEXT** | Product save، search، zero stock، Reorder اور Black box |
| **PT-03 — حقیقی purchase، batch، expiry اور payable** | Pending | Stock اور supplier حساب |
| **PT-04 — Sale، cash/khata، receipt اور drawer** | Pending | روزمرہ فروخت |
| **PT-05 — Returns، PR اور cancellation** | Pending | الگ test copy کے ساتھ محفوظ جانچ |
| **PT-06 — Shift، ledgers، permissions اور audit** | Pending | مالیاتی اور user workflows |
| **PT-07 — Excel، restore، printer اور scanner** | Pending | Hardware اور data workflows |

---

# PT-01 — Startup, backup اور UI smoke

## مالک کا نتیجہ — PASS

1. **Startup / sign-in:** ٹھیک رہا۔
2. **Backup with photos:** کامیاب؛ zip بن اور save ہوا۔
3. **Pages:** مقررہ تمام pages کامیابی سے کھلے۔
4. اس نتیجے کو PASS لکھا گیا ہے، مگر backup کے استعمال اور data-safety کے لیے درج ذیل بہترییں درکار ہیں۔

## PT01-BK-01 — Backup folder منتخب کرنا اور actual folder کھولنا

**حالت:** Recorded — fix pending
**اہمیت:** اہم؛ backup میسر ہے مگر اس تک رسائی اور جگہ کا انتخاب واضح ہونا چاہیے۔

### مالک کی بات

- مالک کو چاہیے کہ وہ خود منتخب کرے کہ backup کہاں بنے، مثلاً USB یا دوسرا folder۔
- اگر custom location کا option نہ ہو تو کم از کم **Open the folder** دبانے پر صرف folder کا نام نہیں بلکہ حقیقی Windows folder کھلنا چاہیے۔
- اگر custom location میں کوئی security خطرہ ہو تو موجودہ default قابل قبول ہے، مگر backup کی جگہ تک آسان رسائی ضروری ہے۔

### موجودہ code میں کیا ہے

- Backup default folder میں بنتا ہے؛ UI میں destination منتخب کرنے کا option نہیں۔
- صفحے پر پورا folder path لکھا ہوتا ہے۔
- **Open the folder** موجود ہے اور Windows shell کو folder کھولنے کو کہتا ہے؛ مالک کے تجربے میں مطلوبہ folder view واضح نہیں کھلا، اس لیے اسے reproduce کرنا ہے۔
- یہ owner-reported usability مسئلہ ہے؛ backup بننا ناکام نہیں ہوا۔

### قبولیت کا معیار

1. Owner ایک محفوظ folder picker سے backup destination منتخب کر سکے؛ default پہلے جیسا رہے۔
2. منتخب path مکمل اور پڑھنے کے قابل ہو؛ USB/removable drive ہو تو وہ واضح ہو۔
3. **Open the folder** حقیقی Windows Explorer میں folder کھولے۔ نیا backup بننے کے بعد **Show this backup** جیسا راستہ ہو تو نئی zip منتخب یا واضح ہو۔
4. Folder نہ ملنے، permission نہ ہونے، USB نکلنے یا disk full ہونے پر واضح error آئے؛ پرانا backup delete یا overwrite نہ ہو۔
5. Custom destination صرف backup zip کے لیے ہو؛ live `area11.db` کی جگہ نہیں بدلنی چاہیے۔
6. Physical test: default folder، دوسرا local folder اور USB پر backup بن کر کھولا جائے۔

## PT01-BK-02 — پرانا backup restore کرنے کے بعد نئے کام اور backups کی حفاظت

**حالت:** Recorded — fix and physical proof pending
**اہمیت:** **انتہائی اہم / data safety**
**نوٹ:** مالک نے خدشہ اور required safeguard بیان کیا ہے؛ ایسا کوئی actual data-loss incident report نہیں ہوا۔

### مالک کا خدشہ

اگر 26 اکتوبر کا پرانا backup restore کر کے کام جاری رکھا جائے اور بعد میں 27 اکتوبر یا اس کے بعد نیا کام ہو تو:

- پرانا backup restore کرتے وقت اس کے بعد کا نیا کام ضائع یا الجھا ہوا محسوس نہ ہو؛
- restore کے بعد کیا گیا نیا کام آگے بننے والے backups میں شامل ہو؛
- غلطی سے پرانی حالت پر آنے کے بعد بھی تازہ ترین کام واپس پانے کا واضح راستہ رہے؛
- کوئی پرانا یا نیا backup خاموشی سے overwrite نہ ہو۔

### موجودہ code میں پہلے سے موجود safeguards

- Backup نام میں milliseconds کا وقت ہے؛ collision پر `UniquePath` نیا نام بناتا ہے، اس لیے مقصد overwrite سے بچنا ہے۔
- Manual backups کو automatic trimming میں نہیں ہٹایا جاتا۔
- Database کی consistent copy SQLite کے `VACUUM INTO` سے بنتی ہے۔
- Restore سے پہلے current database کی **safety copy** بنتی ہے۔
- Restore سے پہلے database کا `quick_check` ہوتا ہے۔
- Restore کے بعد app restart ہوتی ہے تاکہ pages پرانی memory values نہ رکھیں۔
- اس کے بعد بنا کوئی نیا backup اس وقت کے live database سے بنتا ہے؛ پرانی zip میں نیا کام لکھا نہیں جاتا۔

یہ code-review facts ہیں؛ انھیں physical test کے بغیر مکمل حل نہیں لکھا جائے گا۔

### قبولیت کا معیار

1. Restore confirmation پر backup کی **بننے کی تاریخ، version اور عمر** نمایاں ہو۔
2. اگر backup current data سے پرانا ہو تو واضح warning ہو: **“اس کے بعد کا نیا کام موجود ہو سکتا ہے؛ پہلے تازہ safety backup بنے گا.”**
3. Safety copy بننا ناکام ہو تو restore رک جائے؛ کوئی exception ہو تو بھی current data کے نقصان کا خطرہ نہ ہو۔
4. Restore کے بعد recovery information restart کے باوجود نظر آئے: کونسا backup restore ہوا، کب، اور تازہ safety copy کہاں ہے۔
5. Restore کے فوراً بعد نیا baseline backup بنانے کا واضح option ہو؛ اس کے بعد کے automatic/manual backups اسی current live database اور نئے کام پر مشتمل ہوں۔
6. کسی بھی backup کو overwrite نہ کیا جائے؛ safety copies بھی الگ اور قابلِ شناخت رہیں۔
7. Physical scenario:
   - **A:** 26 اکتوبر کا backup بنائیں۔
   - **B:** اس کے بعد ایک نیا recognizable record بنائیں۔
   - **A restore:** پرانا backup restore کریں۔
   - **C:** restore کے بعد ایک مختلف recognizable record بنائیں۔
   - نیا backup بنائیں، اسے restore کر کے **C** اور اس وقت کا پورا live data verify کریں۔
   - **B** والا data pre-restore safety copy سے recover ہو سکے۔
8. یہ scenario صرف الگ test copy پر ہوگا، اصل دکان کے books پر نہیں۔

## PT01-BK-03 — متعلقہ code-review نکات: safety copy کی نمائش اور photos

**حالت:** Recorded — investigation pending
**ذریعہ:** Agent code review، owner-reported failure نہیں
**اہمیت:** BK-02 کے ساتھ حل کرنا ضروری ہے۔

### جو دیکھا گیا

- Restore سے پہلے بننے والی safety copy **صرف `.db`** ہے، جبکہ عام backup zip میں database اور photos دونوں ہوتے ہیں۔
- Safety copies عام backup list میں نہیں دکھتیں؛ recovery کے لیے owner کو file system پر انحصار کرنا پڑ سکتا ہے۔
- Photo restore existing same-name files کو overwrite کر سکتا ہے؛ اس سے پہلے photos کی safety zip نہیں بنتی۔
- Restore کے بعد status لکھا جاتا ہے مگر app restart ہوتی ہے، اس لیے recovery پیغام کو restart کے بعد بھی دکھانا بہتر ہوگا۔
- Live database کو staging کے بجائے براہِ راست overwrite کیا جاتا ہے؛ copy یا اس کے بعد کے photo step میں failure آئے تو partial restore اور غیر واضح حالت کا خطرہ ہے۔ Safety copy موجود ہے مگر recovery خودکار اور واضح ہونی چاہیے۔
- Hand-made zip کے photo paths کے لیے `..` اور sibling-prefix boundary cases کا explicit test درکار ہے تاکہ کوئی file photos folder سے باہر نہ لکھی جا سکے۔

### مطلوبہ حل کا دائرہ

1. Pre-restore safety کو پورے shop کے قابلِ بازیابی backup بنانا یا photos کے تحفظ کی حد واضح کرنا۔
2. Safety copies کو الگ **Recovery / safety copies** حصے میں دکھانا اور ان سے محفوظ restore ممکن بنانا۔
3. Current photos کو overwrite کرنے سے پہلے ان کا محفوظ راستہ فراہم کرنا۔
4. Recovery instructions کو restart کے بعد بھی دکھانا، صرف عارضی status پر اکتفا نہیں کرنا۔
5. Database کو staged و safe replacement کے ذریعے بدلنا؛ copy/photo failure پر واضح rollback یا recovery، اور partial success کو مکمل failure کہہ کر چھپانا نہیں۔
6. Locked file، disk full، damaged zip، `../` photo path اور restore کے دوران failure کے automated fault-injection tests۔

## Backup fixes کی تجویز کردہ ترتیب — testing کے بعد

1. **BK-02 + BK-03:** ڈیٹا، photos اور recovery کی حفاظت پہلے۔
2. **BK-01:** custom destination، Explorer اور backup discovery کا UI۔
3. ہر fix کے ساتھ automated regression، پھر اوپر والا physical A/B/C scenario۔

فی الحال کسی production file میں ان تبدیلیوں کا کام شروع نہیں کیا گیا۔

---

# PT-02 — حقیقی medicine اور product details

**مقصد:** حقیقی product بنانا، اس کی details بچنا، zero-stock behaviour درست رہنا، اور audit record بننا۔
**اہم:** ایسی حقیقی دوا منتخب کریں جسے واقعی دکان میں رکھنا ہے۔ Fake medicine اصل books میں نہ بنائیں۔
**Batch / expiry / stock:** اس test میں نہیں؛ Products صفحے پر stock batches سے آتا ہے، اس لیے وہ **PT-03 purchase** میں آئے گا۔

## مراحل

1. **Products → New** کھولیں۔
2. پہلے خالی name کے ساتھ **Save product** دبائیں: refusal آنی چاہیے اور کوئی product نہیں بننا چاہیے۔
3. دوبارہ **New** کر کے حقیقی medicine کی تفصیل بھریں:
   - Name اور salt / formula
   - Rack / shelf، ضرورت ہو تو Room / almari
   - Category اور Company / brand owner
   - Pack formula: strips / box اور tablets / strip
   - درست base unit
   - Cost، Retail اور Reorder level
   - حقیقی barcode موجود ہو تو Barcode؛ VIP/Doctor rates صرف اگر واقعی استعمال ہوں
4. **Save product** دبائیں۔
5. Product کو name، barcode یا rack سے search کریں اور دوبارہ کھولیں۔
6. تمام values دوبارہ درست نظر آنی چاہئیں؛ Cost/Retail کے حساب سے Margin معقول ہو۔
7. Purchase سے پہلے **Stock = 0** ہونا چاہیے۔ Products اور Reorder pad دونوں کھلنے چاہئیں؛ کوئی `Double -> Int64` error نہیں آنا چاہیے۔
8. **Black box** میں اس product کی create/save entry دیکھیں: درست medicine، وقت، user اور نئی values واضح ہوں۔
9. UI کا معائنہ کریں: کوئی field یا button کٹا ہوا نہیں، pack formula سمجھ آتا ہے، اور save/search آسان ہیں۔

## PT-02 PASS کا معیار

- Blank name refused۔
- حقیقی product صرف ایک بار بنا۔
- Search اور reopen پر تمام details درست۔
- Purchase سے پہلے stock صفر؛ Products/Reorder بغیر error کھلے۔
- Margin درست اور Black box entry موجود۔
- کوئی UI یا usability مسئلہ ہو تو یہیں record ہوگا۔

## مالک کا نتیجہ

```text
PT-02: PASS / PARTIAL / FAIL
Medicine کونسی بنائی:
Blank name refusal:
Save اور search:
Stock = 0 اور Reorder:
Margin:
Black box:
کوئی UI یا دیگر تبدیلی مطلوب ہو:
```

---

# اگلے notes کے لیے سانچہ

```text
تاریخ:
Test ID:
Build:
نتیجہ: PASS / PARTIAL / FAIL
کیا کیا:
کیا توقع تھی:
حقیقت میں کیا ہوا:
مطلوبہ تبدیلی:
اسکرین شاٹ / error:
Agent investigation:
Fix status:
Owner verification:
```
