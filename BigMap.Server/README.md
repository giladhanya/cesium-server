# BigMap.Server

ASP.NET Core Web API עבור אריחי PNG של Cesium. השרת בודק קודם את ה-cache המקומי, וב-cache miss פונה לשירות `png render` ב-`http://127.0.0.1:8081`.

## הרצה

```powershell
dotnet run --project BigMap.Server
```

Endpoints:

- `GET /health`
- `GET /api/base-tiles/{z}/{x}/{y}.png`
- `GET /api/tiles/{layer}/{z}/{x}/{y}.png`
- `GET /api/tiles/layers/{z}/{x}/{y}`

כל בקשת tile חייבת לכלול את שם שכבת ה־PBF כפי שהוא מופיע בשדה `name`.
רשימת השכבות הזמינות עבור אריח מסוים מתקבלת מ־`GET /api/tiles/layers/{z}/{x}/{y}`.
השרת קורא את שמות השכבות ישירות מה־PBF ואינו משתמש ברשימת `SourceLayers` בקונפיגורציה.
קבצי ה-cache נשמרים בנפרד לפי שכבה: `cache/{StyleVersion}/{layer}/{z}/{x}_{y}.png`.
קבצי PBF נשמרים תחת `cache/{StyleVersion}/pbf/{z}/{x}_{y}.pbf`.
עבור שכבות שאינן `base`, השרת משתמש ב־PBF מ־`TileRenderer:VectorPath` ומייצר PNG,
ומחזיר את ה-PNG דרך ה-API.

## Cesium

```ts
const provider = new UrlTemplateImageryProvider({
  url: 'http://localhost:5000/api/base-tiles/{z}/{x}/{y}.png',
  minimumLevel: 0,
  maximumLevel: 14,
});

viewer.imageryLayers.addImageryProvider(provider);

const transportationProvider = new UrlTemplateImageryProvider({
  url: 'http://localhost:5000/api/tiles/transportation/{z}/{x}/{y}.png',
  minimumLevel: 0,
  maximumLevel: 14,
});
viewer.imageryLayers.addImageryProvider(transportationProvider);

const waterProvider = new UrlTemplateImageryProvider({
  url: 'http://localhost:5000/api/tiles/water/{z}/{x}/{y}.png',
  minimumLevel: 0,
  maximumLevel: 14,
});
viewer.imageryLayers.addImageryProvider(waterProvider);
```

הגדרות נמצאות ב-`appsettings.json`. ניתן לשנות את `TileCache:RootPath` ואת כתובת ה-renderer לפי סביבת ההרצה.
