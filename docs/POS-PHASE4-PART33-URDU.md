# Phase4 — حصہ33: Receipt / Bills کی موجودہ اجازت؛ PT-53

**v0.111.0 Inlet · مستقل5/6/7/15/17/21/23/24 · source checkpoint، Windows منظوری نہیں۔**

[مستقل1–36حیثیت](NUMBERED-WORK-STATUS-URDU.md) · [فنی trace/حدود](POS-PHASE4-RECEIPT-CURRENT-REVIEW.md) · [اردو استعمال نامہ](USER-GUIDE-URDU.md)

## 1۔ روزمرہ مثال

Manager نے Bills میں bill کھولا اور چند bills tick کیے۔ Owner نے Sales کی اجازت واپس لے لی۔ صرف پرانا billID یا User object اب نئی receipt، detail، bulk job یا reprint audit لینے کے لیے کافی نہیں۔ اگلے action یا nominal3second loaded check پر history rows/detail/preview/totals/ticked set صاف ہوں۔ Grant بحال کرنے سے پرانا selection خود واپس نہیں آتا؛ Refresh کریں۔ Unchanged allowed tick انتخاب نہیں مٹاتا۔

**صفحے کی آزاد اجازت برقرار:** Counter sale receipt=POS؛ Returns receipt=Returns؛ Bills history سے دونوں receipts=Sales۔ Sales-only شخص کو بلاوجہ POS/Returns کی اضافی شرط نہیں؛ POS-only شخص history نہیں پڑھ سکتا۔ Cost/Profit/main ان عام page grants کا متبادل نہیں۔

## 2۔ source میں کیا مکمل ہوا؟

1. **5/6/15/17:** دونوں receipt builders میں actor لازمی؛ actorless overload ختم۔ Payload SELECT سے پہلے stored active account،same parsed role اور workflow-specific module gate۔ نامعلوم purpose fail-closed۔ Pure receipt Compose/figures نہیں بدلے۔
2. **5/6/21:** History CanLook/current read،Search/Open،receipt/return wrappers،bulk documents اور summary کے gates۔ Missing selected bill پر پورا bulk job refuse،partial job نہیں۔
3. **23/24:** دونوں reprint audits میں transaction کے اندر current Sales actor؛audit نام stored account سے۔ Return code واقعی موجود ہو۔ Audit insert fail ہو تو service failure واپس دے؛پہلے helper یہ fault دبا سکتا تھا۔ Print پہلے ہوچکا ہو تو UI printed-but-unrecorded بتاتا ہے،دوبارہ print کی خودکار کوشش نہیں۔ یہ spooler اور DB کو ایک atomic transaction نہیں بناتا۔
4. **7:** Bills UI میں denial/read failure پر rows/detail/payments/returns/preview/custom text/totals/selection/bulk selection صاف۔ Next-action اور loaded nominal3second timer؛Unloaded stop۔ Regrant پر fresh Refresh،stableallowed tick selection برقرار۔
5. **2/3/24:**95 نئے C# source cases:44 current-change/regrant،44 invalid-actor routes،7 facts۔ کل2400 متوقع cases۔ **C# compilation/runtime سب UNRUN۔**

**Sale/Return/Purchase/Supplier financial posting،Pricing/Extra/ReturnB560then540/SupplierB،main/Cost/Profit کے طےشدہ قواعد نہیں بدلے۔**

## 3۔ PT-53 — صرف isolated Windows copy پر؛ تمام عملی steps UNRUN

**پہلے verified encrypted backup؛ پھر واقعی الگ DB/photos/config۔ الگ exe folder کافی نہیں۔ اصل shop پر synthetic bills/refunds/permissionfaults نہیں۔ کوئی secret یا اصل DB chat میں نہیں۔**

### 5.1 / 6.1 — stored account،workflow grants

1. Manager سے موجود sale/return receipts،Search/Open/bulk کا allowed baseline۔ پھر accountinactive یا rolechange؛اسی service/window کے اگلے reads/audits deny،کوئی مالی/stock/number/audit mutation نہیں۔ Rolechange کے بعد نئی signin؛same-role grantrestore پرfresh read جائز۔
2. صرف Sales ON،POS/Returns OFF: history sale/return receipts اور bulk چلیں،counter builders deny۔ پھر Sales OFF،POS/Returns ON: counter builders جائز،history deny۔ Settled financialvisibility کے toggles کو page grant کا بدل نہ بنائیں۔
3. Invalid/null/zero/inactivecaller/forgedOwner،storedOwnerinactive/rolechanged deny۔ Permission table read fault میں nonOwner کے لیے roledefaultallow نہیں؛validcurrentOwner کی ordinary bypass برقرار۔ یہ same-role-ID impersonation کے خلاف authenticatedsessiontoken کا ثبوت نہیں۔
4. Wrong purpose/missing bill/missing return/partial bulk selection میں کوئی receipt/job/audit ایجاد نہیں۔

### 7.1 / 21.1 — stale history اور output

1. Bills rows،detail،payments،returns،preview،customfields اور multi-selection کھلے ہوں۔ Owner سے Sales revoke یا accountinactive؛اگلے action/nominaltick پر payload clear۔ Grant بحال:Refresh کے بغیر پرانا bill/tickedset واپس نہیں۔ Stableallowed tick selection برقرار۔
2. Read failure کو certifiedemptyhistory نہ کہیں۔ Search/Open/build fail پر stale payload صاف؛pageUnloaded پر timer بند۔ Actual WPF bindings/timing/focus/reentrancy یہاں UNRUN۔
3. Preview/WhatsApp/PDF/bulk action تازہ builder سے ہو۔ **پہلے سے کھلا preview،exportedfile،بھیجاmessage یاprintedpaper واپس نہیں لیا گیا؛instantrecall کی ضمانت نہیں۔** Build سے actual printer/send کے درمیان revocation race بھی باقی ہے۔

### 23.1 / 24.1 — audit failure اور اصل suite

1. `ReceiptCurrentAccessTests` کے95 اور پوری2400suite کے Windows logs رکھیں۔ Synthetic trigger سے audit insert fail:service failure،noauditmutation۔ Stored accountname بدلیں؛دونوں reprint audits میں نیا storedname۔ Missing return پر invented audit نہیں۔
2. Print کامیاب،audit fail: **Printed،لیکن blackbox نے record نہیں کیا**؛پرانا receipt خود دوبارہ نہ چھپے۔ Actual spooler-after-acceptance ambiguity الگ test کرنا ہے۔
3. Receipt widths58/80،cash/split/credit،Extra/ReturnB/corrected-time/plaintext/figures کے existing tests بھی چلیں؛source میں layout tails برقرار ہیں مگر یہ runtimePASS نہیں۔

## 4۔ اصل evidence اور حدود

فنی trace میں final executed results الگ درج ہیں۔ Python actualSQL/trigger probes اور lexical negatives C#،Dapper،provider،session،WPF یا printer execution نہیں۔ Current account validation authenticatedsessionbinding،CAS یا durableexactlyonce نہیں۔

## 5۔ اسی نمبر میں باقی کام اور وجہ

- **5/6/7/15/17/21/23:** باقیquarantine/draft-save-open/held/quickadd/sessioncallers،fullPOS cache/masking policy،otherdialogs/preparedoutputs،same-role-ID/sessionbinding،check/use/CAS۔ Unsaved POS cart کو خاموشی سے نہیں مٹایا۔
- **24/25:** دوسرےstock/expense/recoveryPR/draft/files،actualCOMMIT-I/O/provider/busy/productionjournal/crossprocess/durableidentity۔ Spooler+audit atomic نہیں۔
- **26–29:** encryptedbackup/restore/newPC/files/cloud integration اور faults۔
- **2/4/30–35:** Windows2400+build/logs/fixes/retests،اصلWPF/printer/scanner/camera/startup/lock/isolation۔
- **36:** gates کے بعد مجاز single-doubleclick testinstaller؛**ابھی نہیں بنا،FullPhase4OPEN۔**

ہر portion docs+samebranchcommit/push+actualremoteverification۔36categories،36Next نہیں؛3–6integratedrounds مشروطاندازہ،deadlineیاupperbound نہیں۔
