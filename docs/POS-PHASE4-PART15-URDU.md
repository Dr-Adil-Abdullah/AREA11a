# حصہ15 — الگ password والا مکمل backup

**v0.93.0 Shearwater · 2026-11-10 · PT-35**

## اس حصے میں کیا شامل ہوا؟

- نیا **`.a11bak` encrypted package**: پورا SQLite database، اور Include the photos کے مطابق تصاویر۔ عام ZIP کی طرح کھول کر Excel میں بدلنے والی فائل نہیں۔ AES-GCM authentication اور اس کے اندر SHA256 completion check؛صرف باہر رکھی ہوئی hash پر اعتماد نہیں۔
- Owner کے لیے **Set / change backup password** اور دوبارہ confirmation۔ Password عام Settings field، export، audit یا backup manifest میں محفوظ نہیں ہوتا۔ Automatic کے لیے اسی Windows account کی DPAPI-protected local storage؛setup نہ ہو/کھل نہ سکے تو **backup FAIL،plain fallback نہیں**،sign-in پر تنبیہ۔
- Manual،automatic،restore سے پہلے full safety copy اور ناکامی پر rollback ایک ہی encrypted راستے پر۔ صرف نیا writer بنا کر پرانا ZIP-only rollback نہیں چھوڑا۔
- Restore پہلے package کی مکمل authentication/checksum،پھر ZIP کی size/count/path/decompression اور SQLite validation،پھر full encrypted safety،اس کے بعد ہی live replacement کی کوشش۔ غلط password یا بدلی/کٹ گئی/اضافی bytes والی فائل پہلے رد ہوگی۔
- **Restore file / new PC / legacy** سے باہر کی فائل منتخب ہو سکتی ہے۔ نئے PC پر پہلے local safety password setup،پھر اصل backup کا **اصل password** الگ درج کریں۔ password بدلنے سے پرانی فائل کا password نہیں بدلتا۔
- پرانی ZIP/DB کے لیے الگ واضح **Owner-only UNVERIFIED legacy recovery**۔ وہ encrypted/authentic بن نہیں جاتیں؛پرانے backups خودکار طور پر delete نہیں ہوتے۔ نئی retention صرف پروگرام کی encrypted automatic copies کی؛دستی/safety/پرانے legacy محفوظ۔

**Password بھولنے سے backup ناقابلِ بحالی ہو سکتا ہے۔ App کا login reset اسے recover نہیں کرتا۔ اصل password کبھی chat میں نہ دیں۔** مضبوط الگ password محفوظ جگہ سنبھالیں؛صرف فائل USB پر رکھنا کافی نہیں اگر password بھی اسی کھلی جگہ لکھا ہو۔

## حقیقتاً کون سی جانچ ہوئی؟

| جانچ | نتیجہ |
|---|---|
| Repository static grammar/member/XAML/source checks | **82 PASS** — compiler/runtime نہیں |
| Independent Python encrypted-format model | **6 round trips +24 negative cases PASS**؛C# execution نہیں |
| دس سابق synthetic SQLite probes | **PASS**؛اصل دکان/Windows نہیں |
| نئے C# cases | **51 لکھے**؛کل **1692،یہاں UNRUN** |
| Windows build،DPAPI/ACL،حقیقی نئی مشین،restore/fault matrix | **UNRUN** |
| Double-click installer | **نہیں بنا**؛اس ماحول میں .NET/Windows/Inno موجود نہیں |

Source مکمل طور پر نئے راستے سے جوڑا ہے،مگر اسے **production-safe یا cryptographically certified** قرار نہیں دیا۔ تفصیلی format،حدود،طریقے اور باقی gates: [review](POS-PHASE4-ENCRYPTED-BACKUP-REVIEW.md)۔

## صرف حقیقی الگ test ماحول میں PT-35

**Live shop میں فرضی bill،restore،cloud bring یا fault test نہیں۔** پہلے موجودہ verified backup اور واقعی الگ DB/photos والی VM/PC بنائیں؛دوسری exe folder کافی نہیں۔ پرانے backups نہ مٹائیں۔

1. تازہ source سے `tools\RUN-TESTS.cmd` چلائیں؛متوقع **1692 cases**۔ مکمل اصل log محفوظ کریں۔ C# tests گزرنے کے بعد WPF build/launch الگ آزمائیں؛یہ ایک دوسرے کا متبادل نہیں۔
2. بغیر password setup Back up now،automatic due اور safety/restore آزمائیں:صاف FAIL،کسی folder میں نئی plain ZIP/DB backup نہ بنے،current data برقرار۔ عام billing sign-in warning کے بعد کھل سکے۔ Automatic OFF / not-due / due الگ دیکھیں؛پروگرام بند ہو تو backup service چلنے کا دعویٰ نہیں۔
3. Cashier/Manager سے setup/change رد؛صرف موجودہ active Owner قبول۔ mismatch/کم لمبا password/Cancel رد؛owner account deactivate/role change کے بعد cached Owner سے بھی رد۔ Password screen/Settings/export/audit/log میں صاف نظر نہ آئے۔
4. Owner سے **مصنوعی test password صرف ایپ میں** set کریں۔ Manual،Save as USB،automatic اور photos unticked/ticked بنائیں؛ایک نام دوبارہ دینے سے پہلی فائل محفوظ،نئی الگ فائل۔ غلط extension اور managed `-auto` نام Save as میں رد۔
5. Same Windows account/app restart پر automatic secret دستیاب؛دوسرے Windows account یا DB path پر خودبخود دستیاب نہ ہو۔ اصل credential path کی ACL،unreadable/deleted/tampered credential،antivirus lock اور disk-full آزمائیں۔ **یہاں actual DPAPI/ACL نتیجہ ابھی نہیں۔**
6. چھوٹا اور1MiB سے بڑا synthetic package restore کریں؛database rows،bills،balances،photos اور before-restore safety ملائیں۔ source کا پرانا password اور safety کا نیا local password مختلف رکھیں؛پرانی فائل پر نیا password ناکام،اصل password کامیاب۔
7. واقعی الگ نئی Windows VM/PC پر صرف package+اصل password لے جائیں؛پرانی Windows credential file کی ضرورت نہ ہو۔ نئی مشین کا local safety password پہلے set کریں؛نئی safety copy اپنے نئے password سے کھلے۔ دونوں passwords کے بغیر recovery کا جھوٹا وعدہ نہیں۔
8. header/salt/nonce/length/cipher/tag/final digest،قطع شدہ/append/reordered/unsupported package؛wrong password؛valid encrypted مگر malformed ZIP/DB؛duplicate/traversal/Windows device/over-limit manifest/count/expansion۔ ہر بار **live DB/photos unchanged**،نئی safety صرف validation کامیاب ہونے کے بعد،private staging cleanup چیک۔
9. Legacy ZIP/DB معمول کے Restore سے رد؛Owner کی واضح unverified قبولیت کے بعد ہی staged validation/safety/restore۔ staff کی legacy کوشش اور trusted-looking مگر خراب legacy فائل رد۔ اصل legacy فائل نہ delete ہو۔
10. A backup → B live → restore A → C نیا کام/backup → restore C → B safety واپسی؛DB اورphotos چیک۔ Include photos بند ہونے پر existing photos حذف نہ ہوں؛included تصاویر overwrite/add،یہ پورے directory کا deletion-sync نہیں۔
11. Windows open-handle lock سے swap failure پر live WAL/journal محفوظ؛photo-copy failure پر encrypted safety سے DB/previous photos واپسی؛rollback failure پر صاف recovery-required،جھوٹی کامیابی نہیں۔ disk-full،access denied،crash/بجلی failure اور concurrent activity الگ جانچیں؛**restore کے دوران دکان کا کام روکنا لازم**۔
12. Retention limit کم رکھ کر نئی automatic copies،دستی،before-restore،پرانے auto ZIP/DB اور foreign file سب ملائیں۔ صرف نئی managed automatic encrypted فائلیں trim ہوں۔ Sync bring سے پہلے backup بھی encrypted ہو؛setup نہ ہو تو bring رد،cloud خود اس حصے میں encrypted نہیں کیا گیا۔

## چند ضروری حدود

Password رکھنے والا یا local administrator package پڑھ/دوبارہ بنا سکتا ہے؛یہ DRM نہیں۔ بغیر bytes بدلے فائل صرف کھولنے کا معتبر سراغ نہیں لگتا۔ Live DB/photos encrypted-at-rest نہیں کیے۔ بڑے restore کی جانچ کے لیے current-user profile میں عارضی plaintext staging بنتی ہے؛عام اختتام پر best-effort cleanup،مگر crash/lock پر باقی رہ سکتی ہے،secure erase کی ضمانت نہیں۔ DB snapshot اور الگ photo copy ایک atomic filesystem snapshot نہیں۔ Timestamp/filename پہلے صرف **UNVERIFIED hint** ہیں۔

## اگلا کام

اس source checkpoint کے بعد **Windows PT-35 اور حقیقی isolated restore** سب سے اہم gate ہیں۔ اسی بڑے Phase4 میں باقی financial outputs/StockMinus،Supplier paid/payable/Pay/SR کا غیرطےشدہ فیصلہ،session/record/stale access،fault/concurrency/files/cloud اور مکمل Windows review کھلے ہیں۔ اندازہ تقریباً **3–7 مزید مربوط review/result چکر**،نتائج سے بڑھ سکتا ہے؛وعدہ یا installer تاریخ نہیں۔ ہر حصے کی docs اور اسی branch پر commit/push لازم؛اشاعت کی اصل تصدیق الگ Git نتیجے سے ہوگی۔
