<div dir="rtl">

# DECISIONS

> מלאו את הקובץ הזה. הוא חלק מההערכה — קצר וברור עדיף על ארוך.

## 1. החלטות ארכיטקטוניות
-ה־Controller מטפל רק ב־HTTP. הלוגיקה והגישה ל־DB ב־`LeaveRequestService`.
- EF Core ישירות, בלי Repository או Unit of Work. ההיקף קטן ולא היה בהם צורך.

## 2. הבאג ביתרת החופשה
- **הבאג:** ב־`Create` הימים שכבר אושרו חושבו (`used`) אבל לא היה שימוש בהם.
הבדיקה השוותה רק את הבקשה החדשה למכסה השנתית.
- **התיקון:** דוחים כש־`used + days > AnnualQuota`, לחופשה בלבד.
- **טסט:** `Create_ExceedingRemainingVacationBalance_ReturnsBadRequest`.
עובד שניצל 18 מתוך 20 ימים ומבקש 3 מקבל 400, והבקשה לא נשמרת.

## 3. אישור בקשה (approve) ו‑concurrency
- 404 אם הבקשה לא קיימת, 409 אם היא לא Pending, 204 בהצלחה.
- האישור הוא `ExecuteUpdate` מותנה (`WHERE Status = Pending`), אז שני אישורים מקבילים לאותה בקשה לא יצליחו שניהם.
- ב־SQLite אין נעילת שורות. בפרודקשן על PostgreSQL הייתי משאירה את העדכון המותנה, ואם צריך לוגיקה נוספת בזמן האישור, משתמשת בטרנזקציה עם `SELECT ... FOR UPDATE`.

## 4. על מה ויתרתי בגלל הזמן
 לא מימשתי בדיקות concurrency אמיתיות מול PostgreSQL, מכיוון שעבדתי עם SQLite.
 עם עוד יום הייתי מוסיפה בדיקת אינטגרציה מול PostgreSQL ובודקת אישורים מקבילים.
- בדיקות אינטגרציה ל־API ובדיקות לפרונטנד.

## 5. שימוש ב‑AI
### איפה AI עזר (כולל prompts)
1. prompt: "Review the vacation quota calculation and identify the bug. Suggest a minimal fix and a regression test."→ זיהיתי שהימים שכבר נוצלו חושבו אך לא נכללו בבדיקת המכסה. תיקנתי את התנאי והוספתי בדיקת רגרסיה.
2. prompt: "Review the approval endpoint requirements, including invalid states and concurrent approval attempts."
→ השתמשתי בהצעה לעדכון אטומי באמצעות EF Core, והתאמתי את התשובות למצבים של בקשה חסרה או בקשה שכבר אינה ממתינה.

### איפה דחיתי/תיקנתי הצעה של AI
- לא הוספתי שכבות Repository או Unit of Work, כדי לשמור על פתרון פשוט שמתאים להיקף הפרויקט.

### אבטחה
`Search` שרשר את שם העובד ל־`FromSqlRaw`, וזו חולשת SQL Injection. עברתי ל־LINQ, כך שהקלט מטופל כפרמטר.

## 6. הוראות הרצה
ללא שינוי מה־README.

</div>
