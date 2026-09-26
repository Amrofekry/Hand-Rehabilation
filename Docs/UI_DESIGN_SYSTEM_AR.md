# هوية واجهة GripRehab — Phase 3

[افتح ملف Figma القابل للتعديل](https://www.figma.com/design/2xyEji05scuske6RwWG8g4/GripRehab-Phase-3-Menu-and-HUD). يحتوي أربع Frames بقياس 1440×810: Main Menu، Patient Profile، Module Selection، In-game HUD. هذه لوحات تصميم؛ وظائف الأزرار الحقيقية موجودة في Unity، وليس في Figma. المشهد المحفوظ `Assets/Scenes/GripRehab.unity` هو التنفيذ المعتمد عند التشغيل.

## لغة التصميم

| دور اللون | Unity | الاستخدام |
| --- | --- | --- |
| خلفية داكنة | `#071820` → `#12333B` | خلفية القائمة، مع شبكة خفيفة ولمعة جانبية |
| بطاقات اللعب | `#102A32` / `#14333B` | بيانات الغرفة، المصدر، الإرشاد، الاختصارات |
| سطح النماذج | `#F3F8F5` | النماذج وبطاقات التحكم ذات النص الداكن |
| فعل أساسي | `#167E6F` | بدء الجلسة، الحفظ، الاستئناف |
| تمييز حي | `#36C9A4` | الموديول النشط، المصدر، شريط التقدم |
| نص فاتح | `#F5FBFA` | النص على الخلفيات الداكنة |

أزرار البداية والمتابعة رمادية حين لا يوجد ملف/جلسة صالحة، والأزرار المقفولة لـGP2 غير فعالة. لا تجعل الرسوم توحي بأن Module 02 أو 03 أو النتائج السريرية مكتملة. استخدم أرقامًا ووحدات حقيقية من حالة اللعبة فقط. `% closure` تقدير بصري من الكاميرا؛ `newtons` قوة من Phase 2 عند وجود عينة صالحة أو قوة اصطناعية واضحة باسم **KEYBOARD DEMO**.

## خريطة الشاشة إلى Unity

| التصميم | مكانه في Hierarchy | المنطق |
| --- | --- | --- |
| Main Menu | `Canvas/Rehab main menu overlay/Home` | `RehabAppShell.cs` |
| Patient Profile | `Canvas/Rehab main menu overlay/Profile` | `RehabAppShell.cs` و`RehabProfileStore.cs` |
| Module Selection | `Canvas/Rehab main menu overlay/Modules` | `RehabAppShell.cs` |
| HUD | `Canvas/Premium session HUD` | `RehabAppShell.UpdateTelemetry()` |
| اختيار الغرفة والجسم | لوحة `ApartmentChoices` وسط الشاشة | `ApartmentChoices.cs` |

مواد الواجهة `Rounded96.png` و`MenuBackdrop.png` داخل `Assets/Rehab/3.4-game-modules/UI`. الأولى Sprite بتسع حواف قابلة للتمدد؛ الثانية خلفية بسيطة صغيرة. الـCanvas وكل العناصر محفوظة في المشهد وليست مولدة أثناء Play. يمكنك اختيار عنصر من Hierarchy وتغيير اللون والحجم والنص في Inspector ثم `Ctrl+S` خارج Play. لا تشغّل أمر `Grip Rehab → Apply Premium UI Design` بعد تخصيص الواجهة يدويًا إلا إذا قصدت إعادة تطبيق القيم الأساسية.

## قواعد الوضوح

- اترك مركز اللعب مكشوفًا للّيد والجسم، واجعل البيانات الطرفية قصيرة.
- أظهر حالة انتظار Phase 2 بدل قيمة قوة قديمة؛ لا تعرض `%` على أنها نيوتن.
- ميّز **Patient Dashboard** و**Doctor Preview** كواجهات محدودة الوظيفة؛ البيانات التاريخية والتشخيصية مؤجلة.
- أي قيمة جديدة على الشاشة يجب أن ترتبط بمصدر حقيقي في `ApartmentExperience` أو `RehabProfileStore` وأن تدخل اختبارات القبول قبل عرضها للمستخدم.

صور التنفيذ الفعلي: [القائمة](images/ui-main-menu.png)، [الملف](images/ui-new-profile.png)، [الموديولات](images/ui-module-selection.png)، [اللعب](images/apartment-overview.png)، [الخيارات](images/apartment-options.png).
